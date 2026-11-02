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
        };

        /// <summary>
        /// Returns the name of a field shown to the user.
        /// </summary>
        /// <param name="field">Field.</param>
        /// <returns>Polish name of the field.</returns>
        public static string GetLabel(FilterField field)
        {
            return field switch
            {
                FilterField.OnGround => "Na ziemi",
                FilterField.Military => "Wojskowy",
                FilterField.Kind => "Rodzaj samolotu",
                FilterField.Manufacturer => "Producent",
                FilterField.TypeCode => "Typ (kod ICAO)",
                FilterField.CallsignPrefix => "Linia (początek znaku)",
                FilterField.Altitude => "Wysokość [m]",
                FilterField.Origin => "Lotnisko wylotu",
                FilterField.Destination => "Lotnisko docelowe",
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
                FilterField.TypeCode => "np. B738",
                FilterField.CallsignPrefix => "np. LOT",
                FilterField.Origin => "kod lub miasto, np. WAW",
                FilterField.Destination => "kod lub miasto, np. RZE",
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
                FilterField.TypeCode => FilterOperandType.Text,
                FilterField.CallsignPrefix => FilterOperandType.Text,
                FilterField.Origin => FilterOperandType.Text,
                FilterField.Destination => FilterOperandType.Text,
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
