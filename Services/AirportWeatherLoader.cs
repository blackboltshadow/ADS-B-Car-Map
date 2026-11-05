using System.Collections.Concurrent;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Management.Weather;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Downloads the weather report and the forecast of the selected airport for the card of the phone and the popup of Android Auto, which share it. A download starts only when the stored answer is missing or old and the weather or the forecast is switched on in the settings. It runs in the background, one download of each kind at a time, and is not repeated for a minute after a failure of the same airport, so that a failure is not repeated at every refresh of the screen.
    /// </summary>
    public static class AirportWeatherLoader
    {
        /// <summary>
        /// Weather and forecast of an airport as they are stored now, with the information whether they are being downloaded.
        /// </summary>
        /// <param name="Weather">Stored weather, or null when there is none or it is switched off.</param>
        /// <param name="WeatherLoading">Whether the weather is missing and is being downloaded.</param>
        /// <param name="Forecast">Stored forecast, or null when there is none or it is switched off.</param>
        /// <param name="ForecastLoading">Whether the forecast is missing and is being downloaded.</param>
        public sealed record Snapshot(WeatherEntry? Weather, bool WeatherLoading, ForecastEntry? Forecast, bool ForecastLoading);

        /// <summary>
        /// Time after a failed download of an airport during which the download is not repeated.
        /// </summary>
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Moment before which the weather of an airport is not downloaded again after a failure, in UTC, by ICAO code.
        /// </summary>
        private static readonly ConcurrentDictionary<string, DateTime> WeatherRetryAfterUtc = new ConcurrentDictionary<string, DateTime>();

        /// <summary>
        /// Moment before which the forecast of an airport is not downloaded again after a failure, in UTC, by ICAO code.
        /// </summary>
        private static readonly ConcurrentDictionary<string, DateTime> ForecastRetryAfterUtc = new ConcurrentDictionary<string, DateTime>();

        /// <summary>
        /// Set to 1 while a weather download is running.
        /// </summary>
        private static int weatherDownloading;

        /// <summary>
        /// Set to 1 while a forecast download is running.
        /// </summary>
        private static int forecastDownloading;

        /// <summary>
        /// Event raised on a background thread when a download has ended, so that a screen can show the result.
        /// </summary>
        public static event System.Action? Updated;

        /// <summary>
        /// Reads what is stored for an airport and starts the downloads that are needed.
        /// </summary>
        /// <param name="airport">The airport.</param>
        /// <returns>The stored answers and whether they are being downloaded; nothing for the part that is switched off.</returns>
        public static Snapshot Request(Airport airport)
        {
            string icao = airport.Icao;
            WeatherEntry? weather = null;
            bool weatherLoading = false;
            ForecastEntry? forecast = null;
            bool forecastLoading = false;

            if (AppSettings.ShowAirportWeather)
            {
                double radius = AppSettings.AirportWeatherNearestKm;
                weather = AppServices.Weather.Get(icao);
                bool due = AppServices.Weather.NeedsRefresh(icao, radius);

                if (due && CanStart(WeatherRetryAfterUtc, icao) && Interlocked.Exchange(ref weatherDownloading, 1) == 0)
                {
                    double latitude = airport.Latitude;
                    double longitude = airport.Longitude;

                    _ = Task.Run(() => DownloadWeatherAsync(icao, latitude, longitude, radius));
                }

                weatherLoading = due && weather == null && Volatile.Read(ref weatherDownloading) == 1;
            }

            if (AppSettings.ShowAirportForecast)
            {
                forecast = AppServices.Forecast.Get(icao);
                bool due = AppServices.Forecast.NeedsRefresh(icao);

                if (due && CanStart(ForecastRetryAfterUtc, icao) && Interlocked.Exchange(ref forecastDownloading, 1) == 0)
                {
                    _ = Task.Run(() => DownloadForecastAsync(icao));
                }

                forecastLoading = due && forecast == null && Volatile.Read(ref forecastDownloading) == 1;
            }

            return new Snapshot(weather, weatherLoading, forecast, forecastLoading);
        }

        /// <summary>
        /// Tells whether a download of an airport may start: it did not fail a moment ago.
        /// </summary>
        /// <param name="retryAfter">Moments before which downloads are not repeated, by ICAO code.</param>
        /// <param name="icao">ICAO code of the airport.</param>
        /// <returns>True when no pause is running for the airport.</returns>
        private static bool CanStart(ConcurrentDictionary<string, DateTime> retryAfter, string icao)
        {
            return !retryAfter.TryGetValue(icao, out DateTime until) || DateTime.UtcNow >= until;
        }

        /// <summary>
        /// Downloads the weather of an airport.
        /// </summary>
        /// <param name="icao">ICAO code of the airport.</param>
        /// <param name="latitude">Latitude of the airport in degrees.</param>
        /// <param name="longitude">Longitude of the airport in degrees.</param>
        /// <param name="radius">Largest distance to the nearest weather station in kilometers; zero when switched off.</param>
        /// <returns>The task of the download.</returns>
        private static async Task DownloadWeatherAsync(string icao, double latitude, double longitude, double radius)
        {
            bool succeeded = false;

            try
            {
                succeeded = await AppServices.Weather.RefreshAsync(icao, latitude, longitude, radius, false, CancellationToken.None);
            }
            catch (Exception exception)
            {
                DevLogger.Warn("AdsbCarMap", "Pobieranie pogody lotniska nie powiodło się", exception);
            }
            finally
            {
                Volatile.Write(ref weatherDownloading, 0);
            }

            Finish(WeatherRetryAfterUtc, icao, succeeded);
        }

        /// <summary>
        /// Downloads the forecast of an airport.
        /// </summary>
        /// <param name="icao">ICAO code of the airport.</param>
        /// <returns>The task of the download.</returns>
        private static async Task DownloadForecastAsync(string icao)
        {
            bool succeeded = false;

            try
            {
                succeeded = await AppServices.Forecast.RefreshAsync(new List<string> { icao }, false, CancellationToken.None);
            }
            catch (Exception exception)
            {
                DevLogger.Warn("AdsbCarMap", "Pobieranie prognozy lotniska nie powiodło się", exception);
            }
            finally
            {
                Volatile.Write(ref forecastDownloading, 0);
            }

            Finish(ForecastRetryAfterUtc, icao, succeeded);
        }

        /// <summary>
        /// Remembers the result of a download and tells the screens.
        /// </summary>
        /// <param name="retryAfter">Moments before which downloads are not repeated, by ICAO code.</param>
        /// <param name="icao">ICAO code of the airport.</param>
        /// <param name="succeeded">Whether the download succeeded.</param>
        private static void Finish(ConcurrentDictionary<string, DateTime> retryAfter, string icao, bool succeeded)
        {
            if (succeeded)
            {
                retryAfter.TryRemove(icao, out DateTime removed);
            }
            else
            {
                retryAfter[icao] = DateTime.UtcNow + RetryDelay;
            }

            Updated?.Invoke();
        }
    }
}
