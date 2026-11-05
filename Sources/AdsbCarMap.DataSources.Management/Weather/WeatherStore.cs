using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Management.Weather
{
    /// <summary>
    /// Memory of the weather reports of airports. A report is downloaded again when it is older than the interval chosen by the user, but it stays in the memory (and in the file) for longer, so that after a failed download the last known report can still be shown together with its age. Airports for which the source has no report are asked about less often than the others. An airport without a report can take the report of the nearest station within a distance chosen by the user; such an entry says how far the station is.
    /// </summary>
    public sealed class WeatherStore
    {
        /// <summary>
        /// How long a downloaded answer stays in the memory, whatever the interval: after that a report is too old to be of use.
        /// </summary>
        public static readonly TimeSpan RetentionAge = TimeSpan.FromMinutes(90);

        /// <summary>
        /// Shortest time after which an airport without a report is asked about again.
        /// </summary>
        public static readonly TimeSpan NoReportRefreshInterval = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Mean radius of the Earth in kilometers.
        /// </summary>
        private const double EarthRadiusKilometers = 6371.0;

        /// <summary>
        /// Source of the reports.
        /// </summary>
        private readonly IWeatherSource source;

        /// <summary>
        /// Stored answers by the ICAO code.
        /// </summary>
        private readonly TimedCache<WeatherEntry> memory;

        /// <summary>
        /// Returns the time after which a report is downloaded again, read at every use so that a changed setting takes effect immediately.
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
        /// <param name="source">Source of the reports.</param>
        /// <param name="memory">Memory of the answers; its validity should be <see cref="RetentionAge"/>.</param>
        /// <param name="refreshInterval">Returns the time after which a report is downloaded again.</param>
        /// <param name="report">Receives the failed downloads, or null.</param>
        public WeatherStore(IWeatherSource source, TimedCache<WeatherEntry> memory, Func<TimeSpan> refreshInterval, Action<SourceEventKind, string, Exception?>? report = null)
            : this(source, memory, refreshInterval, () => DateTime.UtcNow, report)
        {
        }

        /// <summary>
        /// Creates the store.
        /// </summary>
        /// <param name="source">Source of the reports.</param>
        /// <param name="memory">Memory of the answers; its validity should be <see cref="RetentionAge"/>.</param>
        /// <param name="refreshInterval">Returns the time after which a report is downloaded again.</param>
        /// <param name="utcNow">Source of the current time in UTC.</param>
        /// <param name="report">Receives the failed downloads, or null.</param>
        public WeatherStore(IWeatherSource source, TimedCache<WeatherEntry> memory, Func<TimeSpan> refreshInterval, Func<DateTime> utcNow, Action<SourceEventKind, string, Exception?>? report = null)
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
        /// Display name of the source of the reports.
        /// </summary>
        public string SourceName => source.Name;

        /// <summary>
        /// Number of airports whose answer is stored.
        /// </summary>
        public int Count => memory.Count;

        /// <summary>
        /// Reads the stored answer for an airport, also when it is old enough to be downloaded again.
        /// </summary>
        /// <param name="icaoCode">ICAO code of the airport.</param>
        /// <returns>The stored answer, or null when the airport was not asked about or the answer is too old to be kept.</returns>
        public WeatherEntry? Get(string icaoCode)
        {
            return memory.TryGet(Normalize(icaoCode), out WeatherEntry? entry) ? entry : null;
        }

        /// <summary>
        /// Tells whether a stored answer does not need to be downloaded again yet.
        /// </summary>
        /// <param name="entry">The stored answer.</param>
        /// <returns>True when the answer is younger than the interval; for an airport without a report the interval is not shorter than <see cref="NoReportRefreshInterval"/>.</returns>
        public bool IsFresh(WeatherEntry entry)
        {
            TimeSpan limit = refreshInterval();

            if (entry.Report == null && limit < NoReportRefreshInterval)
            {
                limit = NoReportRefreshInterval;
            }

            return utcNow() - entry.FetchedUtc < limit;
        }

        /// <summary>
        /// Downloads the reports of the airports whose stored answer is missing or old, in one request.
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
                List<string> due = codes.Where((string code) => force || Get(code) is not WeatherEntry entry || !IsFresh(entry)).ToList();

                if (due.Count == 0)
                {
                    return true;
                }

                IReadOnlyList<Metar>? reports = await DownloadAsync(() => source.GetMetarsAsync(due, cancellationToken), cancellationToken);

                if (reports == null)
                {
                    return false;
                }

                Dictionary<string, Metar> byCode = reports
                    .GroupBy((Metar metar) => Normalize(metar.IcaoCode))
                    .ToDictionary((IGrouping<string, Metar> group) => group.Key, (IGrouping<string, Metar> group) => group.OrderByDescending((Metar metar) => metar.ObservedAt).First());
                DateTime now = utcNow();

                foreach (string code in due)
                {
                    memory.Set(code, new WeatherEntry(byCode.GetValueOrDefault(code), now));
                }

                return true;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Tells whether the weather of an airport has to be downloaded: when nothing is stored, the stored answer is old, the station of the stored report is farther than the distance now allowed, or the nearest station was not looked for within the distance now allowed.
        /// </summary>
        /// <param name="icaoCode">ICAO code of the airport.</param>
        /// <param name="nearestRadiusKilometers">Largest distance to the nearest station in kilometers; zero or less means that only the report of the airport itself is used.</param>
        /// <returns>True when <see cref="RefreshAsync(string, double, double, double, bool, CancellationToken)"/> would download something.</returns>
        public bool NeedsRefresh(string icaoCode, double nearestRadiusKilometers)
        {
            WeatherEntry? entry = Get(icaoCode);

            return entry == null || !IsFresh(entry) || IsTooFar(entry, nearestRadiusKilometers) || NeedsNearestSearch(entry, nearestRadiusKilometers);
        }

        /// <summary>
        /// Brings the weather of one airport up to date. The airport is asked about first; when it has no report and a distance is given, the report of the nearest station within that distance is stored for it, together with the distance. A report from a station that has since become too far is downloaded again.
        /// </summary>
        /// <param name="icaoCode">ICAO code of the airport.</param>
        /// <param name="latitude">Latitude of the airport in degrees.</param>
        /// <param name="longitude">Longitude of the airport in degrees.</param>
        /// <param name="nearestRadiusKilometers">Largest distance to the nearest station in kilometers; zero or less means that only the report of the airport itself is used.</param>
        /// <param name="force">Whether to download even when the stored answer is fresh.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when the weather of the airport is up to date afterwards; false when a download failed, in which case the old answer stays.</returns>
        public async Task<bool> RefreshAsync(string icaoCode, double latitude, double longitude, double nearestRadiusKilometers, bool force, CancellationToken cancellationToken)
        {
            string code = Normalize(icaoCode);

            if (code.Length == 0)
            {
                return true;
            }

            WeatherEntry? entry = Get(code);
            bool substituted = entry?.DistanceKilometers != null;
            bool tooFar = entry != null && IsTooFar(entry, nearestRadiusKilometers);
            bool needsNearest = entry != null && NeedsNearestSearch(entry, nearestRadiusKilometers);

            if (!force && !NeedsRefresh(code, nearestRadiusKilometers))
            {
                return true;
            }

            if (force || tooFar || (!substituted && !needsNearest))
            {
                if (!await RefreshAsync(new List<string> { code }, force || tooFar, cancellationToken))
                {
                    return false;
                }

                entry = Get(code);

                if (entry?.Report != null || nearestRadiusKilometers <= 0.0)
                {
                    return true;
                }
            }

            return await RefreshNearestAsync(code, latitude, longitude, nearestRadiusKilometers, cancellationToken);
        }

        /// <summary>
        /// Forgets all stored reports, also in the file.
        /// </summary>
        public void Clear()
        {
            memory.Clear();
            memory.Flush();
        }

        /// <summary>
        /// Tells whether a report of a nearest station is farther than the distance now allowed.
        /// </summary>
        /// <param name="entry">The stored answer.</param>
        /// <param name="radiusKilometers">Largest distance allowed in kilometers; zero or less allows none.</param>
        /// <returns>True when the answer comes from a station that is no longer allowed.</returns>
        private static bool IsTooFar(WeatherEntry entry, double radiusKilometers)
        {
            return entry.DistanceKilometers.HasValue && (radiusKilometers <= 0.0 || entry.DistanceKilometers.Value > radiusKilometers);
        }

        /// <summary>
        /// Tells whether the nearest station has to be looked for: the airport has no report and the search was not made within the distance now allowed.
        /// </summary>
        /// <param name="entry">The stored answer.</param>
        /// <param name="radiusKilometers">Largest distance allowed in kilometers; zero or less allows none.</param>
        /// <returns>True when a search within the distance is missing.</returns>
        private static bool NeedsNearestSearch(WeatherEntry entry, double radiusKilometers)
        {
            return entry.Report == null && radiusKilometers > 0.0 && (entry.SearchedRadiusKilometers ?? 0.0) < radiusKilometers;
        }

        /// <summary>
        /// Looks for the nearest station around an airport and stores its report for the airport.
        /// </summary>
        /// <param name="code">ICAO code of the airport in the form used as a key.</param>
        /// <param name="latitude">Latitude of the airport in degrees.</param>
        /// <param name="longitude">Longitude of the airport in degrees.</param>
        /// <param name="radiusKilometers">Largest distance to the station in kilometers.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True when the download succeeded, whether or not a station was found.</returns>
        private async Task<bool> RefreshNearestAsync(string code, double latitude, double longitude, double radiusKilometers, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);

            try
            {
                IReadOnlyList<Metar>? reports = await DownloadAsync(() => source.GetMetarsNearAsync(latitude, longitude, radiusKilometers, cancellationToken), cancellationToken);

                if (reports == null)
                {
                    return false;
                }

                Metar? nearest = null;
                double nearestDistance = double.MaxValue;

                foreach (Metar candidate in reports)
                {
                    if (!candidate.Latitude.HasValue || !candidate.Longitude.HasValue || string.Equals(Normalize(candidate.IcaoCode), code, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    double distance = DistanceKilometers(latitude, longitude, candidate.Latitude.Value, candidate.Longitude.Value);

                    if (distance <= radiusKilometers && distance < nearestDistance)
                    {
                        nearest = candidate;
                        nearestDistance = distance;
                    }
                }

                memory.Set(code, new WeatherEntry(nearest, utcNow(), nearest == null ? null : nearestDistance, radiusKilometers));

                return true;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Runs a request to the source and reports its failure.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">Cancellation token of the caller.</param>
        /// <returns>The reports, or null when the request failed.</returns>
        private async Task<IReadOnlyList<Metar>?> DownloadAsync(Func<Task<IReadOnlyList<Metar>>> request, CancellationToken cancellationToken)
        {
            try
            {
                return await request();
            }
            catch (RateLimitedException exception)
            {
                report?.Invoke(SourceEventKind.Failure, "Weather source " + source.Name + " is paused after a limit response (429)", exception);

                return null;
            }
            catch (Exception exception) when (SourceFailures.IsFailure(exception, cancellationToken))
            {
                report?.Invoke(SourceEventKind.Failure, "Weather source " + source.Name + " failed (" + exception.GetType().Name + ")", exception);

                return null;
            }
        }

        /// <summary>
        /// Computes the distance between two points along the surface of the Earth.
        /// </summary>
        /// <param name="latitude1">Latitude of the first point in degrees.</param>
        /// <param name="longitude1">Longitude of the first point in degrees.</param>
        /// <param name="latitude2">Latitude of the second point in degrees.</param>
        /// <param name="longitude2">Longitude of the second point in degrees.</param>
        /// <returns>Distance in kilometers.</returns>
        private static double DistanceKilometers(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            double radians = Math.PI / 180.0;
            double deltaLatitude = (latitude2 - latitude1) * radians;
            double deltaLongitude = (longitude2 - longitude1) * radians;
            double a = Math.Sin(deltaLatitude / 2.0) * Math.Sin(deltaLatitude / 2.0) + Math.Cos(latitude1 * radians) * Math.Cos(latitude2 * radians) * Math.Sin(deltaLongitude / 2.0) * Math.Sin(deltaLongitude / 2.0);

            return EarthRadiusKilometers * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
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
