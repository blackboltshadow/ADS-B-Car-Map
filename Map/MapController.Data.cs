using AdsbCarMap.DataSources.Models;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.Enrichment;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AdsbCarMap.Data;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.Graphics;
using Android.Util;
using Android.Views;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Aircraft download loop, trails and selected aircraft details of the map controller.
    /// </summary>
    public sealed partial class MapController
    {
        /// <summary>
        /// Starts asking the other sources for the type, the registration and the category of the selected aircraft when the main source did not give them. The answer is merged into the current list of aircraft.
        /// </summary>
        /// <param name="fetched">Aircraft just downloaded from the main source.</param>
        /// <param name="cancellationToken">Token that stops the request.</param>
        private void RequestSelectedAircraftDetails(IReadOnlyList<Aircraft> fetched, CancellationToken cancellationToken)
        {
            AircraftEnricher? source = enricher;
            string? hex;

            lock (stateLock)
            {
                hex = primaryHex;
            }

            if (source == null || hex == null)
            {
                return;
            }

            Aircraft? aircraft = fetched.FirstOrDefault(item => item.Hex == hex);

            if (aircraft == null || !source.NeedsLookup(aircraft))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    Aircraft? filled = await source.LookupAsync(aircraft, cancellationToken);

                    if (filled == null)
                    {
                        return;
                    }

                    lock (stateLock)
                    {
                        List<Aircraft> list = new List<Aircraft>(latestAircraft);
                        int index = list.FindIndex(item => item.Hex == filled.Hex);

                        if (index >= 0)
                        {
                            list[index] = AircraftMerger.FillMissing(list[index], new AircraftDetails(filled.Hex, filled.TypeCode, filled.Registration, filled.Category));
                            latestAircraft = list;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    DevLogger.Warn(LogTag, "Uzupełnianie danych samolotu nie powiodło się", exception);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Loop that downloads the aircraft within the range of the visible area.
        /// </summary>
        /// <param name="cancellationToken">Token that stops the loop.</param>
        /// <returns>Task representing the loop.</returns>
        private async Task DataLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan rateLimitRemaining = LiveTraffic.GetRateLimitRemaining();

                if (rateLimitRemaining > TimeSpan.Zero)
                {
                    try
                    {
                        await fetchWake.WaitAsync(rateLimitRemaining, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    continue;
                }

                double latitude;
                double longitude;
                double radiusNauticalMiles;
                TimeSpan delay = AppSettings.FetchInterval;

                lock (stateLock)
                {
                    latitude = centerLatitude;
                    longitude = centerLongitude;
                    radiusNauticalMiles = MapMath.CalculateRadiusNauticalMiles(centerLatitude, zoom, surfaceWidth, surfaceHeight);
                }

                Volatile.Write(ref aircraftLoading, 1);

                try
                {
                    IReadOnlyList<Aircraft> fetched = await adsbClient.GetAircraftAsync(latitude, longitude, radiusNauticalMiles, cancellationToken);

                    if (enricher != null)
                    {
                        fetched = enricher.Apply(fetched);
                    }

                    keeper?.Observe(fetched);

                    DateTime fetchedAtUtc = DateTime.UtcNow;
                    IReadOnlyDictionary<string, TrailPoint[]> previousTrails;

                    lock (stateLock)
                    {
                        previousTrails = latestTrails;
                    }

                    IReadOnlyDictionary<string, TrailPoint[]> trails = TrailTracker.Update(previousTrails, fetched, fetchedAtUtc, AppSettings.TrailDuration, AppSettings.MaxTrailPoints);

                    lock (stateLock)
                    {
                        latestAircraft = fetched;
                        latestAircraftTimestampUtc = fetchedAtUtc;
                        latestTrails = trails;
                    }

                    LiveTraffic.Update(fetched, fetchedAtUtc, latitude, longitude);
                    RequestSelectedAircraftDetails(fetched, cancellationToken);
                    Volatile.Write(ref aircraftFailed, 0);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (RateLimitedException exception)
                {
                    DevLogger.Warn(LogTag, "Pobieranie samolotów wstrzymane przez źródło (limit zapytań lub odmowa dostępu)");
                    delay = exception.RetryAfter > delay ? exception.RetryAfter : delay;
                    LiveTraffic.ReportRateLimited(delay);
                }
                catch (Exception exception)
                {
                    DevLogger.Warn(LogTag, "Pobieranie samolotów nie powiodło się", exception);
                    Volatile.Write(ref aircraftFailed, 1);
                }

                Volatile.Write(ref aircraftLoadEndedTicks, DateTime.UtcNow.Ticks);
                Volatile.Write(ref aircraftLoading, 0);

                try
                {
                    await fetchWake.WaitAsync(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
