using AdsbCarMap.Localization;
using AdsbCarMap.Settings;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// User-visible description of the filter fields: names, hints and the kind of operand each field needs.
    /// </summary>
    public static class FilterFieldInfo
    {
        /// <summary>
        /// All fields in the order shown in the filter editor.
        /// </summary>
        public static readonly IReadOnlyList<FilterField> All = new List<FilterField>
        {
            FilterField.Destination,
            FilterField.Origin,
            FilterField.CallsignPrefix,
            FilterField.Registration,
            FilterField.Manufacturer,
            FilterField.TypeCode,
            FilterField.Kind,
            FilterField.Military,
            FilterField.OnGround,
            FilterField.Altitude,
            FilterField.Squawk,
            FilterField.VerticalRate,
        };

        /// <summary>
        /// Returns the name of a field shown to the user.
        /// </summary>
        /// <param name="field">Field.</param>
        /// <param name="altitudeUnit">Altitude unit chosen by the user, shown in the name of the altitude field.</param>
        /// <param name="verticalSpeedUnit">Vertical rate unit chosen by the user, shown in the name of the vertical rate field.</param>
        /// <returns>Polish name of the field.</returns>
        public static string GetLabel(FilterField field, AltitudeUnit altitudeUnit = AltitudeUnit.Metric, VerticalSpeedUnit verticalSpeedUnit = VerticalSpeedUnit.FeetPerMinute)
        {
            return field switch
            {
                FilterField.OnGround => Loc.T("Na ziemi"),
                FilterField.Military => Loc.T("Wojskowy"),
                FilterField.Kind => Loc.T("Rodzaj samolotu"),
                FilterField.Manufacturer => Loc.T("Producent"),
                FilterField.TypeCode => Loc.T("Typ (kod ICAO lub IATA)"),
                FilterField.CallsignPrefix => Loc.T("Znak wywoławczy (linia)"),
                FilterField.Registration => Loc.T("Rejestracja"),
                FilterField.Altitude => Loc.T("Wysokość [") + AltitudeFormatter.Symbol(altitudeUnit) + "]",
                FilterField.Origin => Loc.T("Lotnisko wylotu"),
                FilterField.Destination => Loc.T("Lotnisko docelowe"),
                FilterField.Squawk => Loc.T("Squawk (kod transpondera)"),
                FilterField.VerticalRate => Loc.T("Prędkość pionowa [") + UnitFormatter.VerticalSpeedSymbol(verticalSpeedUnit) + "]",
                _ => field.ToString(),
            };
        }

        /// <summary>
        /// Returns the hint shown in the input of a field.
        /// </summary>
        /// <param name="field">Field.</param>
        /// <returns>Polish hint or an empty text when the field needs no text input.</returns>
        public static string GetHint(FilterField field)
        {
            return field switch
            {
                FilterField.TypeCode => Loc.T("np. B738 lub 738"),
                FilterField.CallsignPrefix => Loc.T("np. LOT"),
                FilterField.Registration => Loc.T("np. SP-YBC, YB lub BC"),
                FilterField.Origin => Loc.T("kod lub miasto, np. WAW"),
                FilterField.Destination => Loc.T("kod lub miasto, np. RZE"),
                FilterField.Squawk => Loc.T("np. 7700"),
                _ => string.Empty,
            };
        }

        /// <summary>
        /// Returns the kind of operand a field needs.
        /// </summary>
        /// <param name="field">Field.</param>
        /// <returns>Kind of operand.</returns>
        public static FilterOperandType GetOperandType(FilterField field)
        {
            return field switch
            {
                FilterField.Kind => FilterOperandType.Kind,
                FilterField.Manufacturer => FilterOperandType.Manufacturer,
                FilterField.Altitude => FilterOperandType.Range,
                FilterField.VerticalRate => FilterOperandType.Range,
                FilterField.TypeCode => FilterOperandType.Text,
                FilterField.CallsignPrefix => FilterOperandType.Text,
                FilterField.Registration => FilterOperandType.Text,
                FilterField.Origin => FilterOperandType.Text,
                FilterField.Destination => FilterOperandType.Text,
                FilterField.Squawk => FilterOperandType.Text,
                _ => FilterOperandType.None,
            };
        }

        /// <summary>
        /// Lists the match modes a field supports. Fields that compare the text in a fixed way return an empty list.
        /// </summary>
        /// <param name="field">Field.</param>
        /// <returns>Supported modes in the order shown to the user.</returns>
        public static IReadOnlyList<FilterMatchMode> GetMatchModes(FilterField field)
        {
            switch (field)
            {
                case FilterField.CallsignPrefix:
                case FilterField.Registration:
                    return new List<FilterMatchMode>
                    {
                        FilterMatchMode.StartsWith,
                        FilterMatchMode.Contains,
                        FilterMatchMode.EndsWith,
                        FilterMatchMode.Exact,
                    };
                case FilterField.Squawk:
                    return new List<FilterMatchMode>
                    {
                        FilterMatchMode.Exact,
                        FilterMatchMode.StartsWith,
                    };
                default:
                    return new List<FilterMatchMode>();
            }
        }

        /// <summary>
        /// Returns the match mode a field used before modes could be chosen.
        /// </summary>
        /// <param name="field">Field.</param>
        /// <returns>The default mode.</returns>
        public static FilterMatchMode GetDefaultMatchMode(FilterField field)
        {
            IReadOnlyList<FilterMatchMode> modes = GetMatchModes(field);

            return modes.Count > 0 ? modes[0] : FilterMatchMode.Exact;
        }

        /// <summary>
        /// Returns a mode supported by the field: the given one or the default when it is missing or not supported.
        /// </summary>
        /// <param name="field">Field.</param>
        /// <param name="mode">Requested mode or null.</param>
        /// <returns>The mode to use.</returns>
        public static FilterMatchMode NormalizeMatchMode(FilterField field, FilterMatchMode? mode)
        {
            if (mode.HasValue && GetMatchModes(field).Contains(mode.Value))
            {
                return mode.Value;
            }

            return GetDefaultMatchMode(field);
        }

        /// <summary>
        /// Returns the name of a match mode shown to the user.
        /// </summary>
        /// <param name="mode">Match mode.</param>
        /// <returns>Polish name of the mode.</returns>
        public static string GetMatchModeLabel(FilterMatchMode mode)
        {
            return mode switch
            {
                FilterMatchMode.StartsWith => Loc.T("Zaczyna się od"),
                FilterMatchMode.Contains => Loc.T("Zawiera"),
                FilterMatchMode.EndsWith => Loc.T("Kończy się na"),
                _ => Loc.T("Dokładnie"),
            };
        }

        /// <summary>
        /// Lists the aircraft kinds a user can choose in a condition, without the unknown kind.
        /// </summary>
        /// <returns>Kinds in the order of the enumeration.</returns>
        public static IReadOnlyList<AircraftKind> GetSelectableKinds()
        {
            List<AircraftKind> kinds = new List<AircraftKind>();

            foreach (AircraftKind kind in Enum.GetValues<AircraftKind>())
            {
                if (kind != AircraftKind.Unknown)
                {
                    kinds.Add(kind);
                }
            }

            return kinds;
        }
    }
}
