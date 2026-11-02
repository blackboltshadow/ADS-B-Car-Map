namespace AdsbCarMap.Data
{
    /// <summary>
    /// Leaf of a filter expression: a single test of one aircraft property. When the data needed for the test is missing the result is unknown.
    /// </summary>
    public sealed class FilterCondition : FilterNode
    {
        /// <summary>
        /// Number of meters in one foot.
        /// </summary>
        private const double MetersPerFoot = 0.3048;

        /// <summary>
        /// Accepted type codes computed for the type table in use. Replaced as a whole, so it can be read from several threads.
        /// </summary>
        private volatile AcceptedTypes? acceptedTypes;

        /// <summary>
        /// Creates a condition. Use the factory methods instead.
        /// </summary>
        /// <param name="field">Tested property.</param>
        /// <param name="text">Text operand or null.</param>
        /// <param name="kind">Aircraft kind operand.</param>
        /// <param name="minimum">Lower bound of the altitude in meters or null.</param>
        /// <param name="maximum">Upper bound of the altitude in meters or null.</param>
        /// <param name="family">Whether a type condition covers the whole family of the type.</param>
        private FilterCondition(FilterField field, string? text, AircraftKind kind, double? minimum, double? maximum, bool family = false)
        {
            Family = family;
            Field = field;
            Text = text;
            Kind = kind;
            Minimum = minimum;
            Maximum = maximum;
        }

        /// <summary>
        /// Tested property.
        /// </summary>
        public FilterField Field { get; }

        /// <summary>
        /// Text operand, used by the manufacturer, type, callsign and airport conditions.
        /// </summary>
        public string? Text { get; }

        /// <summary>
        /// Aircraft kind operand, used by the kind condition.
        /// </summary>
        public AircraftKind Kind { get; }

        /// <summary>
        /// Whether the condition tests the route of the flight.
        /// </summary>
        public override bool NeedsRoute => Field == FilterField.Origin || Field == FilterField.Destination;

        /// <summary>
        /// Whether a type condition covers the whole family of the type instead of the exact type only.
        /// </summary>
        public bool Family { get; }

        /// <summary>
        /// Lower bound of the altitude in meters or null when there is none.
        /// </summary>
        public double? Minimum { get; }

        /// <summary>
        /// Upper bound of the altitude in meters or null when there is none.
        /// </summary>
        public double? Maximum { get; }

        /// <summary>
        /// Creates a condition from its stored parts. Operands the field does not use are ignored.
        /// </summary>
        /// <param name="field">Tested property.</param>
        /// <param name="text">Text operand or null.</param>
        /// <param name="kind">Aircraft kind operand.</param>
        /// <param name="minimum">Lower bound of the altitude in meters or null.</param>
        /// <param name="maximum">Upper bound of the altitude in meters or null.</param>
        /// <param name="family">Whether a type condition covers the whole family; ignored for other fields.</param>
        /// <returns>The condition.</returns>
        public static FilterCondition Create(FilterField field, string? text, AircraftKind kind, double? minimum, double? maximum, bool family = false)
        {
            switch (FilterFieldInfo.GetOperandType(field))
            {
                case FilterOperandType.Kind:
                    return new FilterCondition(field, null, kind, null, null);
                case FilterOperandType.Range:
                    return new FilterCondition(field, null, AircraftKind.Unknown, minimum, maximum);
                case FilterOperandType.Text:
                case FilterOperandType.Manufacturer:
                    return new FilterCondition(field, text, AircraftKind.Unknown, null, null, field == FilterField.TypeCode && family);
                default:
                    return new FilterCondition(field, null, AircraftKind.Unknown, null, null);
            }
        }

        /// <summary>
        /// Creates the condition that the aircraft is on the ground.
        /// </summary>
        /// <returns>The condition.</returns>
        public static FilterCondition OnGround()
        {
            return new FilterCondition(FilterField.OnGround, null, AircraftKind.Unknown, null, null);
        }

        /// <summary>
        /// Creates the condition that the aircraft is marked as military.
        /// </summary>
        /// <returns>The condition.</returns>
        public static FilterCondition Military()
        {
            return new FilterCondition(FilterField.Military, null, AircraftKind.Unknown, null, null);
        }

        /// <summary>
        /// Creates the condition that the aircraft is of a given kind.
        /// </summary>
        /// <param name="kind">Aircraft kind.</param>
        /// <returns>The condition.</returns>
        public static FilterCondition OfKind(AircraftKind kind)
        {
            return new FilterCondition(FilterField.Kind, null, kind, null, null);
        }

        /// <summary>
        /// Creates the condition that the aircraft type was made by a manufacturer.
        /// </summary>
        /// <param name="manufacturer">Manufacturer name, see <see cref="AircraftManufacturers"/>.</param>
        /// <returns>The condition.</returns>
        public static FilterCondition MadeBy(string manufacturer)
        {
            return new FilterCondition(FilterField.Manufacturer, manufacturer, AircraftKind.Unknown, null, null);
        }

        /// <summary>
        /// Creates the condition that the aircraft has a given ICAO type code.
        /// </summary>
        /// <param name="typeCode">Type code in the ICAO or IATA form, for example "B738" or "738".</param>
        /// <param name="family">True to cover the whole family of the type, for example every 737 including the MAX.</param>
        /// <returns>The condition.</returns>
        public static FilterCondition OfType(string typeCode, bool family = false)
        {
            return new FilterCondition(FilterField.TypeCode, typeCode, AircraftKind.Unknown, null, null, family);
        }

        /// <summary>
        /// Creates the condition that the callsign starts with a given text, which selects an airline, for example "LOT".
        /// </summary>
        /// <param name="prefix">Beginning of the callsign.</param>
        /// <returns>The condition.</returns>
        public static FilterCondition CallsignStartsWith(string prefix)
        {
            return new FilterCondition(FilterField.CallsignPrefix, prefix, AircraftKind.Unknown, null, null);
        }

        /// <summary>
        /// Creates the condition that the altitude lies within a range. An aircraft on the ground counts as being at 0 m.
        /// </summary>
        /// <param name="minimumMeters">Lower bound in meters or null when there is none.</param>
        /// <param name="maximumMeters">Upper bound in meters or null when there is none.</param>
        /// <returns>The condition.</returns>
        public static FilterCondition AltitudeBetween(double? minimumMeters, double? maximumMeters)
        {
            return new FilterCondition(FilterField.Altitude, null, AircraftKind.Unknown, minimumMeters, maximumMeters);
        }

        /// <summary>
        /// Creates the condition that the flight departed from an airport.
        /// </summary>
        /// <param name="airport">IATA or ICAO code or the city of the airport.</param>
        /// <returns>The condition.</returns>
        public static FilterCondition FromAirport(string airport)
        {
            return new FilterCondition(FilterField.Origin, airport, AircraftKind.Unknown, null, null);
        }

        /// <summary>
        /// Creates the condition that the flight is heading to an airport.
        /// </summary>
        /// <param name="airport">IATA or ICAO code or the city of the airport.</param>
        /// <returns>The condition.</returns>
        public static FilterCondition ToAirport(string airport)
        {
            return new FilterCondition(FilterField.Destination, airport, AircraftKind.Unknown, null, null);
        }

        /// <summary>
        /// Evaluates the condition.
        /// </summary>
        /// <param name="aircraft">Aircraft to test.</param>
        /// <param name="routeLookup">Returns the route of a callsign or null when it is not known yet.</param>
        /// <returns>Result in three-valued logic.</returns>
        public override FilterResult Evaluate(Aircraft aircraft, Func<string?, FlightRoute?> routeLookup)
        {
            switch (Field)
            {
                case FilterField.OnGround:
                    return ToResult(aircraft.OnGround);
                case FilterField.Military:
                    return ToResult(aircraft.IsMilitary);
                case FilterField.Kind:
                    return ToResult(aircraft.Kind == Kind);
                case FilterField.Manufacturer:
                    return EvaluateManufacturer(aircraft);
                case FilterField.TypeCode:
                    return string.IsNullOrWhiteSpace(aircraft.TypeCode)
                        ? FilterResult.Unknown
                        : ToResult(GetAcceptedTypes().Contains(aircraft.TypeCode.Trim()));
                case FilterField.CallsignPrefix:
                    return string.IsNullOrWhiteSpace(aircraft.Callsign) || string.IsNullOrEmpty(Text)
                        ? FilterResult.Unknown
                        : ToResult(aircraft.Callsign.Trim().StartsWith(Text, StringComparison.OrdinalIgnoreCase));
                case FilterField.Altitude:
                    return EvaluateAltitude(aircraft);
                case FilterField.Origin:
                    return EvaluateAirport(aircraft, routeLookup, false);
                case FilterField.Destination:
                    return EvaluateAirport(aircraft, routeLookup, true);
                default:
                    return FilterResult.Unknown;
            }
        }

        /// <summary>
        /// Returns the ICAO type codes accepted by a type condition. The set is computed once for the type table in use and again when the table is replaced.
        /// </summary>
        /// <returns>Accepted ICAO type codes.</returns>
        private HashSet<string> GetAcceptedTypes()
        {
            AircraftTypeTable table = AircraftTypeTable.Current;
            AcceptedTypes? cached = acceptedTypes;

            if (cached != null && ReferenceEquals(cached.Table, table))
            {
                return cached.Codes;
            }

            AcceptedTypes created = new AcceptedTypes(table, table.Resolve(Text, Family));

            acceptedTypes = created;

            return created.Codes;
        }

        /// <summary>
        /// Converts a boolean to a definite result.
        /// </summary>
        /// <param name="value">Boolean value.</param>
        /// <returns>True or false result.</returns>
        private static FilterResult ToResult(bool value)
        {
            return value ? FilterResult.True : FilterResult.False;
        }

        /// <summary>
        /// Tests the manufacturer. A type that is missing or absent from the manufacturer table gives an unknown result.
        /// </summary>
        /// <param name="aircraft">Aircraft to test.</param>
        /// <returns>Result in three-valued logic.</returns>
        private FilterResult EvaluateManufacturer(Aircraft aircraft)
        {
            string? manufacturer = AircraftManufacturers.Find(aircraft.TypeCode);

            if (manufacturer == null || string.IsNullOrEmpty(Text))
            {
                return FilterResult.Unknown;
            }

            return ToResult(string.Equals(manufacturer, Text, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Tests the altitude range.
        /// </summary>
        /// <param name="aircraft">Aircraft to test.</param>
        /// <returns>Result in three-valued logic.</returns>
        private FilterResult EvaluateAltitude(Aircraft aircraft)
        {
            double meters;

            if (aircraft.OnGround)
            {
                meters = 0.0;
            }
            else if (aircraft.AltitudeFeet.HasValue)
            {
                meters = aircraft.AltitudeFeet.Value * MetersPerFoot;
            }
            else
            {
                return FilterResult.Unknown;
            }

            return ToResult((!Minimum.HasValue || meters >= Minimum.Value) && (!Maximum.HasValue || meters <= Maximum.Value));
        }

        /// <summary>
        /// Tests the origin or destination of the flight. The text matches the airport code or the city, ignoring letter case and diacritics. A flight whose route is not known gives an unknown result.
        /// </summary>
        /// <param name="aircraft">Aircraft to test.</param>
        /// <param name="routeLookup">Returns the route of a callsign or null when it is not known yet.</param>
        /// <param name="destination">True to test the destination, false to test the origin.</param>
        /// <returns>Result in three-valued logic.</returns>
        private FilterResult EvaluateAirport(Aircraft aircraft, Func<string?, FlightRoute?> routeLookup, bool destination)
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                return FilterResult.Unknown;
            }

            FlightRoute? route = routeLookup(aircraft.Callsign);

            if (route == null)
            {
                return FilterResult.Unknown;
            }

            string code = destination ? route.DestinationCode : route.OriginCode;
            string? city = destination ? route.DestinationCity : route.OriginCity;
            string wanted = TextFolding.Fold(Text.Trim());

            return ToResult(TextFolding.Fold(code) == wanted || (city != null && TextFolding.Fold(city) == wanted));
        }

        /// <summary>
        /// ICAO type codes accepted by a type condition together with the table they were computed for.
        /// </summary>
        private sealed class AcceptedTypes
        {
            /// <summary>
            /// Creates the cached set.
            /// </summary>
            /// <param name="table">Table the codes were computed for.</param>
            /// <param name="codes">Accepted ICAO type codes.</param>
            public AcceptedTypes(AircraftTypeTable table, HashSet<string> codes)
            {
                Table = table;
                Codes = codes;
            }

            /// <summary>
            /// Table the codes were computed for.
            /// </summary>
            public AircraftTypeTable Table { get; }

            /// <summary>
            /// Accepted ICAO type codes.
            /// </summary>
            public HashSet<string> Codes { get; }
        }
    }
}
