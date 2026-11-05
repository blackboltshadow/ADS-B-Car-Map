using System.Text.RegularExpressions;
using AdsbCarMap.Data;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Downloads the weather reports of the airports that are visible on the map, so that their markers can show the flight category and the wind. The map asks about the airports it draws; the downloads run in the background, in groups, at most one at a time, and only for airports whose report is missing or old. Only the reports of the airports themselves are asked for (no nearest station), and only for the larger airports and airports with scheduled service, because small airfields have no weather station.
    /// </summary>
    public static partial class AirportMapWeather
    {
        /// <summary>
        /// Largest number of airports asked about in one request.
        /// </summary>
        private const int MaxCodesPerRequest = 40;

        /// <summary>
        /// Time between two checks of the airports on the map.
        /// </summary>
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Time after a failed download during which the next one does not start.
        /// </summary>
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Moment before which the airports are not checked again, in UTC.
        /// </summary>
        private static DateTime nextCheckUtc = DateTime.MinValue;

        /// <summary>
        /// Moment before which no download starts after a failure, in UTC.
        /// </summary>
        private static DateTime retryAfterUtc = DateTime.MinValue;

        /// <summary>
        /// Set to 1 while a download is running.
        /// </summary>
        private static int downloading;

        /// <summary>
        /// Asks for the weather of the airports on the map. It does nothing while the weather on the markers is switched off, the zoom is lower than the one chosen in the settings, a download is running or a failure was a moment ago. Call it from the drawing thread; it does not block.
        /// </summary>
        /// <param name="airports">Airports drawn in the current frame, the most important first.</param>
        /// <param name="zoom">Zoom level of the map.</param>
        public static void Request(IReadOnlyList<Airport> airports, double zoom)
        {
            DateTime now = DateTime.UtcNow;

            if (!AppSettings.ShowAirportWeather || !AppSettings.ShowAirportMapWeather || zoom < AppSettings.AirportMapWeatherFromZoom || now < nextCheckUtc)
            {
                return;
            }

            nextCheckUtc = now + CheckInterval;

            if (now < retryAfterUtc || Volatile.Read(ref downloading) == 1)
            {
                return;
            }

            List<string> codes = new List<string>();

            foreach (Airport airport in airports)
            {
                if (codes.Count >= MaxCodesPerRequest)
                {
                    break;
                }

                if (!NeedsReport(airport))
                {
                    continue;
                }

                string code = airport.Icao.ToUpperInvariant();

                if (!codes.Contains(code))
                {
                    codes.Add(code);
                }
            }

            if (codes.Count > 0 && Interlocked.Exchange(ref downloading, 1) == 0)
            {
                _ = Task.Run(() => DownloadAsync(codes));
            }
        }

        /// <summary>
        /// Tells whether the report of an airport is asked for: a larger airport or an airport with scheduled service, with a four-letter code, whose report is missing or old and which does not show the report of another station now.
        /// </summary>
        /// <param name="airport">The airport.</param>
        /// <returns>True when the report should be downloaded.</returns>
        private static bool NeedsReport(Airport airport)
        {
            if (airport.Type == AirportType.Heliport || (airport.Type > AirportType.Medium && !airport.HasScheduledService) || !IcaoCodePattern().IsMatch(airport.Icao))
            {
                return false;
            }

            DataSources.Management.Weather.WeatherEntry? entry = AppServices.Weather.Get(airport.Icao);

            return entry == null || (entry.DistanceKilometers == null && !AppServices.Weather.IsFresh(entry));
        }

        /// <summary>
        /// Downloads the reports of a group of airports.
        /// </summary>
        /// <param name="codes">ICAO codes of the airports.</param>
        /// <returns>The task of the download.</returns>
        private static async Task DownloadAsync(List<string> codes)
        {
            bool succeeded = false;

            try
            {
                succeeded = await AppServices.Weather.RefreshAsync(codes, false, CancellationToken.None);
            }
            catch (Exception exception)
            {
                DevLogger.Warn("AdsbCarMap", "Pobieranie pogody lotnisk na mapie nie powiodło się", exception);
            }
            finally
            {
                if (!succeeded)
                {
                    retryAfterUtc = DateTime.UtcNow + RetryDelay;
                }

                Volatile.Write(ref downloading, 0);
            }
        }

        /// <summary>
        /// Pattern of an ICAO airport code: four letters.
        /// </summary>
        /// <returns>The pattern.</returns>
        [GeneratedRegex("^[A-Za-z]{4}$")]
        private static partial Regex IcaoCodePattern();
    }
}
