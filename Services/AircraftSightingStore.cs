using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Collects the positions of the aircraft that are in the history of the aircraft, from every download of the data, and keeps them in a file in the application folder. Aircraft that are not in the history are ignored, so the cost of a download is one lookup per aircraft.
    /// </summary>
    public static class AircraftSightingStore
    {
        /// <summary>
        /// Name of the file with the log.
        /// </summary>
        private const string FileName = "aircraft-sightings.json";

        /// <summary>
        /// Oldest age of a position, in seconds, that is still taken as a sighting.
        /// </summary>
        private const double MaximumPositionAgeSeconds = 120.0;

        /// <summary>
        /// Shortest time between two writes of the file, because the samples arrive with every download.
        /// </summary>
        private static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Lock protecting the log and the file.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// The log, read from the file the first time it is needed.
        /// </summary>
        private static AircraftSightingLog? log;

        /// <summary>
        /// Whether the log has changes that are not saved.
        /// </summary>
        private static bool dirty;

        /// <summary>
        /// Time of the last write of the file.
        /// </summary>
        private static DateTime lastSaveUtc = DateTime.MinValue;

        /// <summary>
        /// Notes the aircraft of a download that are in the history. A route that is known is stored with the flight, and for a tracked aircraft without a known route the download of the route is requested.
        /// </summary>
        /// <param name="aircraft">Downloaded aircraft.</param>
        public static void Observe(IReadOnlyList<Aircraft> aircraft)
        {
            DateTime now = DateTime.UtcNow;

            foreach (Aircraft item in aircraft)
            {
                if (item.SecondsSincePosition > MaximumPositionAgeSeconds || !AircraftHistoryStore.Contains(item.Hex))
                {
                    continue;
                }

                FlightRoute? route = AppServices.Routes.Get(item.Callsign);
                Sighting sample = new Sighting(now, item.Latitude, item.Longitude, item.AltitudeFeet, item.GroundSpeedKnots, item.OnGround);

                lock (SyncRoot)
                {
                    if (GetLog().Observe(item.Hex, item.Callsign, sample, route?.OriginCode, route?.OriginCity, route?.DestinationCode, route?.DestinationCity, route?.AirlineName))
                    {
                        dirty = true;
                    }
                }
            }

            SaveWhenDue(now);
        }

        /// <summary>
        /// Gives the flights of an aircraft.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <returns>The flights with what is new in them, the newest first.</returns>
        public static List<FlightInsight> GetFlights(string hex)
        {
            lock (SyncRoot)
            {
                return GetLog().GetFlights(hex);
            }
        }

        /// <summary>
        /// Removes the flights of an aircraft and saves the log.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        public static void Remove(string hex)
        {
            lock (SyncRoot)
            {
                GetLog().Remove(hex);
                Save();
            }
        }

        /// <summary>
        /// Removes the flights of the aircraft that are no longer in the history and saves the log.
        /// </summary>
        public static void PruneToHistory()
        {
            lock (SyncRoot)
            {
                GetLog().RemoveWhere(AircraftHistoryStore.Contains);
                Save();
            }
        }

        /// <summary>
        /// Writes the log to the file when it has unsaved changes. Call it when a screen that shows the log opens, so that the newest samples are visible.
        /// </summary>
        public static void Flush()
        {
            lock (SyncRoot)
            {
                if (dirty)
                {
                    Save();
                }
            }
        }

        /// <summary>
        /// Writes the log when it has changes and the last write was long enough ago.
        /// </summary>
        /// <param name="now">Current time in UTC.</param>
        private static void SaveWhenDue(DateTime now)
        {
            lock (SyncRoot)
            {
                if (dirty && now - lastSaveUtc >= SaveInterval)
                {
                    GetLog().RemoveWhere(AircraftHistoryStore.Contains);
                    Save();
                }
            }
        }

        /// <summary>
        /// Gives the path of the file with the log.
        /// </summary>
        /// <returns>Full path.</returns>
        private static string GetPath()
        {
            return System.IO.Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, FileName);
        }

        /// <summary>
        /// Gives the log, reading the file the first time. Call it only with <see cref="SyncRoot"/> held.
        /// </summary>
        /// <returns>The log.</returns>
        private static AircraftSightingLog GetLog()
        {
            if (log == null)
            {
                string? text = null;

                try
                {
                    string path = GetPath();
                    text = File.Exists(path) ? File.ReadAllText(path) : null;
                }
                catch (Exception exception)
                {
                    DevLogger.Warn("AircraftSightingStore", "Nie udało się wczytać obserwacji samolotów: " + exception.Message);
                }

                log = AircraftSightingLog.Parse(text);
            }

            return log;
        }

        /// <summary>
        /// Writes the log to the file. Call it only with <see cref="SyncRoot"/> held.
        /// </summary>
        private static void Save()
        {
            try
            {
                File.WriteAllText(GetPath(), GetLog().Serialize());
                dirty = false;
                lastSaveUtc = DateTime.UtcNow;
            }
            catch (Exception exception)
            {
                DevLogger.Warn("AircraftSightingStore", "Nie udało się zapisać obserwacji samolotów: " + exception.Message);
            }
        }
    }
}
