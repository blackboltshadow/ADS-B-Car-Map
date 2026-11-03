using System.Globalization;
using System.Text;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// An airspace area together with its state at the moment it was selected for drawing.
    /// </summary>
    /// <param name="Zone">The area.</param>
    /// <param name="State">State of the area.</param>
    /// <param name="Reservation">Reservation that describes the area best; null when it has none.</param>
    public sealed record AirspaceZoneView(AirspaceZone Zone, AirspaceZoneState State, AirspaceReservation? Reservation);

    /// <summary>
    /// Band of heights the user flies in. An area is of interest when the space it reserves overlaps the band.
    /// </summary>
    /// <param name="MinFeet">Lower edge of the band, in feet.</param>
    /// <param name="MaxFeet">Upper edge of the band, in feet; not lower than <paramref name="MinFeet"/>.</param>
    public readonly record struct AirspaceAltitudeRange(int MinFeet, int MaxFeet);

    /// <summary>
    /// What the user wants to see in the airspace layer: which kinds of areas, areas in which states and, optionally, only those that reach into a band of heights. The kinds are not a fixed list, because the source may publish a new one at any time; a kind the user has not decided about follows <see cref="ShowNewTypes"/>. The object does not change; every change gives a new one.
    /// </summary>
    public sealed class AirspaceLayerConfig
    {
        /// <summary>
        /// Prefix of the stored text of the current format.
        /// </summary>
        private const string StoredPrefix = "v2";

        /// <summary>
        /// Prefix of the first format, which had no band of heights; it is still read.
        /// </summary>
        private const string FirstStoredPrefix = "v1";

        /// <summary>
        /// Decisions of the user by kind, compared without regard to case.
        /// </summary>
        private readonly Dictionary<string, bool> typeChoices;

        /// <summary>
        /// Creates a configuration.
        /// </summary>
        /// <param name="typeChoices">Decision of the user for every kind he decided about; true means shown.</param>
        /// <param name="states">States of the areas that are shown.</param>
        /// <param name="showNewTypes">Whether a kind without a decision is shown.</param>
        /// <param name="altitudeRange">Band of heights the areas have to overlap, or null to show areas of any height.</param>
        public AirspaceLayerConfig(IReadOnlyDictionary<string, bool> typeChoices, AirspaceZoneState states, bool showNewTypes, AirspaceAltitudeRange? altitudeRange = null)
        {
            this.typeChoices = new Dictionary<string, bool>(typeChoices, StringComparer.OrdinalIgnoreCase);
            States = states & AirspaceZoneState.All;
            ShowNewTypes = showNewTypes;
            AltitudeRange = altitudeRange;
        }

        /// <summary>
        /// The configuration of a new installation: the kinds from <see cref="AirspaceTypes.DefaultShown"/> and active areas of any height; the other kinds, also new ones, stay hidden.
        /// </summary>
        public static AirspaceLayerConfig Default
        {
            get
            {
                Dictionary<string, bool> choices = new Dictionary<string, bool>();

                foreach (string type in AirspaceTypes.DefaultShown)
                {
                    choices[type] = true;
                }

                return new AirspaceLayerConfig(choices, AirspaceZoneState.Active, false);
            }
        }

        /// <summary>
        /// States of the areas that are shown.
        /// </summary>
        public AirspaceZoneState States { get; }

        /// <summary>
        /// Whether a kind the user has not decided about is shown.
        /// </summary>
        public bool ShowNewTypes { get; }

        /// <summary>
        /// Band of heights the areas have to overlap, or null when areas of any height are shown.
        /// </summary>
        public AirspaceAltitudeRange? AltitudeRange { get; }

        /// <summary>
        /// The kinds the user has decided about, whether the data holds them at the moment or not.
        /// </summary>
        public IReadOnlyCollection<string> DecidedTypes => typeChoices.Keys;

        /// <summary>
        /// Tells whether areas of a kind are shown.
        /// </summary>
        /// <param name="type">Kind of the area as the source names it.</param>
        /// <returns>The decision of the user, or <see cref="ShowNewTypes"/> when there is none.</returns>
        public bool IsTypeShown(string type)
        {
            return typeChoices.TryGetValue(type, out bool shown) ? shown : ShowNewTypes;
        }

        /// <summary>
        /// Tells whether an area in a state is shown.
        /// </summary>
        /// <param name="type">Kind of the area.</param>
        /// <param name="state">State of the area.</param>
        /// <returns>True when both the kind and the state are switched on.</returns>
        public bool Allows(string type, AirspaceZoneState state)
        {
            return (States & state) != AirspaceZoneState.None && IsTypeShown(type);
        }

        /// <summary>
        /// Tells whether a reservation reaches into the band of heights. Flight levels are compared as heights in feet, without the correction for the pressure, which is exact enough for choosing what to look at.
        /// </summary>
        /// <param name="reservation">The reservation, or null when the area has none.</param>
        /// <returns>True when no band is set, when the area has no reservation to judge by, or when the reserved space overlaps the band.</returns>
        public bool PassesAltitude(AirspaceReservation? reservation)
        {
            if (AltitudeRange == null || reservation == null)
            {
                return true;
            }

            return reservation.Lower.Feet <= AltitudeRange.Value.MaxFeet && reservation.Upper.Feet >= AltitudeRange.Value.MinFeet;
        }

        /// <summary>
        /// Selects the areas to draw and describes each of them.
        /// </summary>
        /// <param name="zones">All the areas.</param>
        /// <param name="nowUtc">The moment at which the states are found, in UTC.</param>
        /// <returns>The areas that pass the configuration, in the order of the input.</returns>
        public IReadOnlyList<AirspaceZoneView> Select(IEnumerable<AirspaceZone> zones, DateTime nowUtc)
        {
            List<AirspaceZoneView> result = new List<AirspaceZoneView>();

            foreach (AirspaceZone zone in zones)
            {
                if (!IsTypeShown(zone.Type))
                {
                    continue;
                }

                AirspaceZoneState state = AirspaceClassifier.GetState(zone, nowUtc);

                if ((States & state) == AirspaceZoneState.None)
                {
                    continue;
                }

                AirspaceReservation? reservation = AirspaceClassifier.GetRelevantReservation(zone, nowUtc);

                if (PassesAltitude(reservation))
                {
                    result.Add(new AirspaceZoneView(zone, state, reservation));
                }
            }

            return result;
        }

        /// <summary>
        /// Gives a configuration with the decision about one kind changed.
        /// </summary>
        /// <param name="type">Kind of the area.</param>
        /// <param name="shown">Whether the kind is shown.</param>
        /// <returns>The new configuration.</returns>
        public AirspaceLayerConfig WithType(string type, bool shown)
        {
            Dictionary<string, bool> choices = new Dictionary<string, bool>(typeChoices, StringComparer.OrdinalIgnoreCase);
            choices[type] = shown;

            return new AirspaceLayerConfig(choices, States, ShowNewTypes, AltitudeRange);
        }

        /// <summary>
        /// Gives a configuration with the shown states changed.
        /// </summary>
        /// <param name="states">States of the areas that are shown.</param>
        /// <returns>The new configuration.</returns>
        public AirspaceLayerConfig WithStates(AirspaceZoneState states)
        {
            return new AirspaceLayerConfig(typeChoices, states, ShowNewTypes, AltitudeRange);
        }

        /// <summary>
        /// Gives a configuration with the treatment of the new kinds changed.
        /// </summary>
        /// <param name="showNewTypes">Whether a kind without a decision is shown.</param>
        /// <returns>The new configuration.</returns>
        public AirspaceLayerConfig WithShowNewTypes(bool showNewTypes)
        {
            return new AirspaceLayerConfig(typeChoices, States, showNewTypes, AltitudeRange);
        }

        /// <summary>
        /// Gives a configuration with the band of heights changed.
        /// </summary>
        /// <param name="altitudeRange">The band, or null to show areas of any height. Negative heights are raised to zero and a band with the edges the wrong way round is turned over.</param>
        /// <returns>The new configuration.</returns>
        public AirspaceLayerConfig WithAltitudeRange(AirspaceAltitudeRange? altitudeRange)
        {
            AirspaceAltitudeRange? fixedRange = null;

            if (altitudeRange != null)
            {
                int low = Math.Max(0, Math.Min(altitudeRange.Value.MinFeet, altitudeRange.Value.MaxFeet));
                int high = Math.Max(0, Math.Max(altitudeRange.Value.MinFeet, altitudeRange.Value.MaxFeet));
                fixedRange = new AirspaceAltitudeRange(low, high);
            }

            return new AirspaceLayerConfig(typeChoices, States, ShowNewTypes, fixedRange);
        }

        /// <summary>
        /// Writes the configuration as text for the storage, e.g. "v2;3;0;D+,R+,TRA-;0-5000" (the last part is empty when no band of heights is set).
        /// </summary>
        /// <returns>Text that <see cref="Parse"/> reads back.</returns>
        public string Serialize()
        {
            List<string> types = new List<string>(typeChoices.Keys);
            types.Sort(StringComparer.OrdinalIgnoreCase);
            StringBuilder builder = new StringBuilder();

            foreach (string type in types)
            {
                if (builder.Length > 0)
                {
                    builder.Append(',');
                }

                builder.Append(type).Append(typeChoices[type] ? '+' : '-');
            }

            string range = AltitudeRange == null
                ? string.Empty
                : AltitudeRange.Value.MinFeet.ToString(CultureInfo.InvariantCulture) + "-" + AltitudeRange.Value.MaxFeet.ToString(CultureInfo.InvariantCulture);

            return string.Join(";", StoredPrefix, ((int)States).ToString(CultureInfo.InvariantCulture), ShowNewTypes ? "1" : "0", builder.ToString(), range);
        }

        /// <summary>
        /// Reads a configuration from the stored text.
        /// </summary>
        /// <param name="text">Text written by <see cref="Serialize"/> or by the first version of the application, or null.</param>
        /// <returns>The configuration; <see cref="Default"/> for missing, damaged or unknown text. A damaged entry of a kind, or a damaged band of heights, is skipped.</returns>
        public static AirspaceLayerConfig Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Default;
            }

            string[] parts = text.Split(';');
            bool isCurrent = parts.Length == 5 && parts[0] == StoredPrefix;
            bool isFirst = parts.Length == 4 && parts[0] == FirstStoredPrefix;

            if ((!isCurrent && !isFirst)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int states)
                || (parts[2] != "0" && parts[2] != "1"))
            {
                return Default;
            }

            Dictionary<string, bool> choices = new Dictionary<string, bool>();

            foreach (string entry in parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = entry.Trim();

                if (trimmed.Length < 2 || (trimmed[trimmed.Length - 1] != '+' && trimmed[trimmed.Length - 1] != '-'))
                {
                    continue;
                }

                choices[trimmed.Substring(0, trimmed.Length - 1)] = trimmed[trimmed.Length - 1] == '+';
            }

            return new AirspaceLayerConfig(choices, (AirspaceZoneState)states, parts[2] == "1", isCurrent ? ParseRange(parts[4]) : null);
        }

        /// <summary>
        /// Reads the band of heights from its stored text.
        /// </summary>
        /// <param name="text">Text such as "0-5000", or empty.</param>
        /// <returns>The band, or null for empty or damaged text, or for edges the wrong way round.</returns>
        private static AirspaceAltitudeRange? ParseRange(string text)
        {
            string[] edges = text.Split('-');

            if (edges.Length != 2
                || !int.TryParse(edges[0], NumberStyles.None, CultureInfo.InvariantCulture, out int low)
                || !int.TryParse(edges[1], NumberStyles.None, CultureInfo.InvariantCulture, out int high)
                || low > high)
            {
                return null;
            }

            return new AirspaceAltitudeRange(low, high);
        }
    }
}
