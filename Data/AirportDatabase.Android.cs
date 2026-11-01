using System.Globalization;
using System.Text;
using Android.Util;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// The part of the airport database that reads the bundled assets and logs to the Android log.
    /// </summary>
    public sealed partial class AirportDatabase
    {
        /// <summary>
        /// Tag used in the Android log.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Asset with the airports.
        /// </summary>
        private const string AirportsAsset = "Airports/airports.tsv";

        /// <summary>
        /// Asset with the runways.
        /// </summary>
        private const string RunwaysAsset = "Airports/runways.tsv";

        /// <summary>
        /// Non-zero when the loading has been started.
        /// </summary>
        private int loadStarted;

        /// <summary>
        /// Starts loading the data in the background, unless it was already started.
        /// </summary>
        public void StartLoading()
        {
            if (Interlocked.Exchange(ref loadStarted, 1) != 0)
            {
                return;
            }

            _ = Task.Run(Load);
        }

        /// <summary>
        /// Reads the bundled files and builds the indexes.
        /// </summary>
        private void Load()
        {
            try
            {
                long started = Environment.TickCount64;
                Android.Content.Res.AssetManager? assets = Android.App.Application.Context.Assets;

                if (assets == null)
                {
                    return;
                }

                LoadFrom(ReadLines(assets, AirportsAsset), ReadLines(assets, RunwaysAsset));
                Log.Info(LogTag, string.Format(CultureInfo.InvariantCulture, "Wczytano {0} lotnisk i {1} pasów w {2} ms", Count, RunwayCount, Environment.TickCount64 - started));
            }
            catch (Exception exception)
            {
                Log.Warn(LogTag, "Wczytanie lotnisk nie powiodło się: " + exception.Message);
            }
        }

        /// <summary>
        /// Reads the lines of an asset one by one.
        /// </summary>
        /// <param name="assets">Asset manager.</param>
        /// <param name="asset">Path of the asset.</param>
        /// <returns>Lines of the asset.</returns>
        private static IEnumerable<string> ReadLines(Android.Content.Res.AssetManager assets, string asset)
        {
            using Stream stream = assets.Open(asset);
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }
    }
}
