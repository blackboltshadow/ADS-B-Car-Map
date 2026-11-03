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
                FilterField.CallsignPrefix => Loc.T("Linia (początek znaku)"),
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
                FilterField.Origin => FilterOperandType.Text,
                FilterField.Destination => FilterOperandType.Text,
                FilterField.Squawk => FilterOperandType.Text,
                _ => FilterOperandType.None,
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
