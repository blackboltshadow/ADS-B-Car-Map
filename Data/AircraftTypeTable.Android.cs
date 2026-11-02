using System.Globalization;
using System.Text;
using Android.Util;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// The part of the aircraft type table that reads the bundled asset and logs to the Android log.
    /// </summary>
    public sealed partial class AircraftTypeTable
    {
        /// <summary>
        /// Tag used in the Android log.
        /// </summary>
        private const string LogTag = "AdsbCarMap";

        /// <summary>
        /// Asset with the aircraft types.
        /// </summary>
        private const string TypesAsset = "AircraftTypes/aircraft-types.tsv";

        /// <summary>
        /// Reads the bundled table and makes it the table used by the filters. Does nothing when the asset cannot be read, so the filters then accept only exact ICAO codes.
        /// </summary>
        public static void LoadBundled()
        {
            try
            {
                Android.Content.Res.AssetManager? assets = Android.App.Application.Context.Assets;

                if (assets == null)
                {
                    return;
                }

                using Stream stream = assets.Open(TypesAsset);
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                List<string> lines = new List<string>();
                string? line;

                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }

                AircraftTypeTable table = Parse(lines);

                Current = table;
                Log.Info(LogTag, string.Format(CultureInfo.InvariantCulture, "Wczytano {0} typów samolotów", table.TypeCount));
            }
            catch (Exception exception)
            {
                Log.Warn(LogTag, "Wczytanie typów samolotów nie powiodło się: " + exception.Message);
            }
        }
    }
}
