using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.Weather
{
    /// <summary>
    /// Memory of the forecasts of airports. A forecast is downloaded again when it is older than the interval chosen by the user, but it stays in the memory (and in the file) for longer, so that after a failed download the last known forecast can still be shown. A forecast that is no longer valid is asked about more often, because a new one is expected, and an airport for which no forecast is issued is asked about rarely.
    /// </summary>
    public sealed class ForecastStore
    {
        /// <summary>
        /// How long a downloaded answer stays in the memory, whatever the interval.
        /// </summary>
        public static readonly TimeSpan RetentionAge = TimeSpan.FromHours(6);

        /// <summary>
        /// Shortest time after which an airport without a forecast is asked about again.
        /// </summary>
        public static readonly TimeSpan NoForecastRefreshInterval = TimeSpan.FromHours(3);

        /// <summary>
        /// Longest time after which a forecast that is no longer valid is asked about again.
        /// </summary>
        public static readonly TimeSpan ExpiredRefreshInterval = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Source of the forecasts.
        /// </summary>
        private readonly IWeatherSource source;

        /// <summary>
        /// Stored answers by the ICAO code.
        /// </summary>
        private readonly TimedCache<ForecastEntry> memory;

        /// <summary>
        /// Returns the time after which a forecast is downloaded again, read at every use so that a changed setting takes effect immediately.
        /// </summary>
        private readonly Func<TimeSpan> refreshInterval;

        /// <summary>
        /// Source of the current time in UTC, replaceable in tests.
        /// </summary>
        private readonly Func<DateTime> utcNow;

        /// <summary>
        /// Receives the failed downloads, or null.
        /// </summary>
        private readonly Action<SourceEventKind, string, Exception?>? report;

        /// <summary>
        /// Lets only one download run at a time, so that two callers asking about the same airport cause one request.
        /// </summary>
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Creates the store with the system clock.
        /// </summary>
        /// <param name="source">Source of the forecasts.</param>
        /// <param name="memory">Memory of the answers; its validity should be <see cref="RetentionAge"/>.</param>
        /// <param name="refreshInterval">Returns the time after which a forecast is downloaded again.</param>
        /// <param name="report">Receives the failed downloads, or null.</param>
        public ForecastStore(IWeatherSource source, TimedCache<ForecastEntry> memory, Func<TimeSpan> refreshInterval, Action<SourceEventKind, string, Exception?>? report = null)
            : this(source, memory, refreshInterval, () => DateTime.UtcNow, report)
        {
        }

        /// <summary>
        /// Creates the store.
        /// </summary>
        /// <param name="source">Source of the forecasts.</param>
        /// <param name="memory">Memory of the answers; its validity should be <see cref="RetentionAge"/>.</param>
        /// <param name="refreshInterval">Returns the time after which a forecast is downloaded again.</param>
        /// <param name="utcNow">Source of the current time in UTC.</param>
        /// <param name="report">Receives the failed downloads, or null.</param>
        public ForecastStore(IWeatherSource source, TimedCache<ForecastEntry> memory, Func<TimeSpan> refreshInterval, Func<DateTime> utcNow, Action<SourceEventKind, string, Exception?>? report = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(memory);
            ArgumentNullException.ThrowIfNull(refreshInterval);
            ArgumentNullException.ThrowIfNull(utcNow);
            this.source = source;
            this.memory = memory;
            this.refreshInterval = refreshInterval;
            this.utcNow = utcNow;
            this.report = report;
        }

        /// <summary>
        /// Number of airports whose answer is stored.
        /// </summary>
        public int Count => memory.Count;

        /// <summary>
        /// Reads the stored answer for an airport, also when it is old enough to be downloaded again.
        /// </summary>
        /// <param name="icaoCode">ICAO code of the airport.</param>
        /// <returns>The stored answer, or null when the airport was not asked about or the answer is too old to be kept.</returns>
        public ForecastEntry? Get(string icaoCode)
        {
            return memory.TryGet(Normalize(icaoCode), out ForecastEntry? entry) ? entry : null;
        }

        /// <summary>
        /// Tells whether a stored answer does not need to be downloaded again yet.
        /// </summary>
        /// <param name="entry">The stored answer.</param>
        /// <returns>True when the answer is younger than the interval; for an airport without a forecast the interval is not shorter than <see cref="NoForecastRefreshInterval"/>, and for a forecast that is no longer valid it is not longer than <see cref="ExpiredRefreshInterval"/>.</returns>
        public bool IsFresh(ForecastEntry entry)
        {
            DateTime now = utcNow();
            TimeSpan limit = refreshInterval();

            if (entry.Forecast == null && limit < NoForecastRefreshInterval)
            {
                limit = NoForecastRefreshInterval;
            }

            if (entry.Forecast != null && entry.Forecast.ValidTo.UtcDateTime <= now && limit > ExpiredRefreshInterval)
            {
                limit = ExpiredRefreshInterval;
            }

            return now - entry.FetchedUtc < limit;
        }

        /// <summary>
        /// Tells whether the forecast of an airport has to be downloaded.
        /// </summary>
        /// <param name="icaoCode">ICAO code of the airport.</param>
        /// <returns>True when nothing is stored or the stored answer is old.</returns>
        public bool NeedsRefresh(string icaoCode)
        {
            ForecastEntry? entry = Get(icaoCode);

            return entry == null || !IsFresh(entry);
        }

        /// <summary>
        /// Downloads the forecasts of the airports whose stored answer is missing or old, in one request.
        /// </summary>
        /// <param name="icaoCodes">ICAO codes of the airports.</param>
        /// <param name="force">Whether to download all the given airports, whatever the age of the stored answers.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when everything asked for is up to date afterwards; false when the download failed, in which case the old answers stay.</returns>
        public async Task<bool> RefreshAsync(IReadOnlyCollection<string> icaoCodes, bool force, CancellationToken cancellationToken)
        {
            List<string> codes = icaoCodes.Select(Normalize).Where((string code) => code.Length > 0).Distinct().ToList();

            await gate.WaitAsync(cancellationToken);

            try
            {
                List<string> due = codes.Where((string code) => force || Get(code) is not ForecastEntry entry || !IsFresh(entry)).ToList();

                if (due.Count == 0)
                {
                    return true;
                }

                IReadOnlyList<Taf> forecasts;

                try
                {
                    forecasts = await source.GetTafsAsync(due, cancellationToken);
                }
                catch (RateLimitedException exception)
                {
                    report?.Invoke(SourceEventKind.Failure, "Weather source " + source.Name + " is paused after a limit response (429)", exception);

                    return false;
                }
                catch (Exception exception) when (SourceFailures.IsFailure(exception, cancellationToken))
                {
                    report?.Invoke(SourceEventKind.Failure, "Weather source " + source.Name + " failed to give a forecast (" + exception.GetType().Name + ")", exception);

                    return false;
                }

                Dictionary<string, Taf> byCode = forecasts
                    .GroupBy((Taf taf) => Normalize(taf.IcaoCode))
                    .ToDictionary((IGrouping<string, Taf> group) => group.Key, (IGrouping<string, Taf> group) => group.OrderByDescending((Taf taf) => taf.IssuedAt).First());
                DateTime now = utcNow();

                foreach (string code in due)
                {
                    memory.Set(code, new ForecastEntry(byCode.GetValueOrDefault(code), now));
                }

                return true;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Forgets all stored forecasts, also in the file.
        /// </summary>
        public void Clear()
        {
            memory.Clear();
            memory.Flush();
        }

        /// <summary>
        /// Brings an ICAO code to the form used as a key.
        /// </summary>
        /// <param name="icaoCode">The code.</param>
        /// <returns>The trimmed code in upper case, or an empty text when there is none.</returns>
        private static string Normalize(string? icaoCode)
        {
            return (icaoCode ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
