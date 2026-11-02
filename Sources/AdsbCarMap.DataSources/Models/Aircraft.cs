namespace AdsbCarMap.DataSources.Models
{
    /// <summary>
    /// A single aircraft with its last known position and flight parameters.
    /// </summary>
    public sealed class Aircraft
    {
        /// <summary>
        /// Creates an aircraft description.
        /// </summary>
        /// <param name="hex">ICAO address in hexadecimal notation.</param>
        /// <param name="callsign">Callsign or null when it is not transmitted.</param>
        /// <param name="latitude">Latitude.</param>
        /// <param name="longitude">Longitude.</param>
        /// <param name="altitudeFeet">Barometric altitude in feet or null when unknown.</param>
        /// <param name="onGround">Whether the aircraft is on the ground.</param>
        /// <param name="groundSpeedKnots">Ground speed in knots or null when unknown.</param>
        /// <param name="trackDegrees">Ground track in degrees or null when unknown.</param>
        /// <param name="secondsSincePosition">Age of the position in seconds at the moment of the server response.</param>
        /// <param name="typeCode">ICAO aircraft type code, e.g. "B738", or null when unknown.</param>
        /// <param name="registration">Registration or null when unknown.</param>
        /// <param name="category">ADS-B emitter category, e.g. "A3", or null when it is not transmitted.</param>
        /// <param name="isMilitary">Whether the database marks the aircraft as military.</param>
        /// <param name="verticalRateFeetPerMinute">Climb rate (positive) or descent rate (negative) in feet per minute or null when unknown.</param>
        /// <param name="squawk">Transponder code or null when unknown.</param>
        /// <param name="typeName">Full name of the aircraft type, e.g. "King Air B350", or null when unknown.</param>
        /// <param name="manufacturer">Manufacturer of the aircraft or null when unknown.</param>
        /// <param name="owner">Registered owner or operator of the aircraft or null when unknown.</param>
        public Aircraft(
            string hex,
            string? callsign,
            double latitude,
            double longitude,
            int? altitudeFeet,
            bool onGround,
            double? groundSpeedKnots,
            double? trackDegrees,
            double secondsSincePosition,
            string? typeCode,
            string? registration,
            string? category = null,
            bool isMilitary = false,
            int? verticalRateFeetPerMinute = null,
            string? squawk = null,
            string? typeName = null,
            string? manufacturer = null,
            string? owner = null)
        {
            TypeName = typeName;
            Manufacturer = manufacturer;
            Owner = owner;
            Category = category;
            IsMilitary = isMilitary;
            VerticalRateFeetPerMinute = verticalRateFeetPerMinute;
            Squawk = squawk;
            Hex = hex;
            Callsign = callsign;
            Latitude = latitude;
            Longitude = longitude;
            AltitudeFeet = altitudeFeet;
            OnGround = onGround;
            GroundSpeedKnots = groundSpeedKnots;
            TrackDegrees = trackDegrees;
            SecondsSincePosition = secondsSincePosition;
            TypeCode = typeCode;
            Registration = registration;
        }

        /// <summary>
        /// ADS-B emitter category, e.g. "A3", or null when it is not transmitted.
        /// </summary>
        public string? Category { get; }

        /// <summary>
        /// Whether the database marks the aircraft as military.
        /// </summary>
        public bool IsMilitary { get; }

        /// <summary>
        /// Climb rate (positive) or descent rate (negative) in feet per minute or null when unknown.
        /// </summary>
        public int? VerticalRateFeetPerMinute { get; }

        /// <summary>
        /// Transponder code or null when unknown.
        /// </summary>
        public string? Squawk { get; }

        /// <summary>
        /// ICAO address in hexadecimal notation.
        /// </summary>
        public string Hex { get; }

        /// <summary>
        /// Callsign or null when it is not transmitted.
        /// </summary>
        public string? Callsign { get; }

        /// <summary>
        /// Latitude.
        /// </summary>
        public double Latitude { get; }

        /// <summary>
        /// Longitude.
        /// </summary>
        public double Longitude { get; }

        /// <summary>
        /// Barometric altitude in feet or null when unknown.
        /// </summary>
        public int? AltitudeFeet { get; }

        /// <summary>
        /// Whether the aircraft is on the ground.
        /// </summary>
        public bool OnGround { get; }

        /// <summary>
        /// Ground speed in knots or null when unknown.
        /// </summary>
        public double? GroundSpeedKnots { get; }

        /// <summary>
        /// Ground track in degrees or null when unknown.
        /// </summary>
        public double? TrackDegrees { get; }

        /// <summary>
        /// Age of the position in seconds at the moment of the server response.
        /// </summary>
        public double SecondsSincePosition { get; }

        /// <summary>
        /// ICAO aircraft type code, e.g. "B738", or null when unknown.
        /// </summary>
        public string? TypeCode { get; }

        /// <summary>
        /// Registration or null when unknown.
        /// </summary>
        public string? Registration { get; }

        /// <summary>
        /// Full name of the aircraft type, e.g. "King Air B350", or null when unknown. The main source gives only the type code; the name is filled from the other sources.
        /// </summary>
        public string? TypeName { get; }

        /// <summary>
        /// Manufacturer of the aircraft or null when unknown.
        /// </summary>
        public string? Manufacturer { get; }

        /// <summary>
        /// Registered owner or operator of the aircraft or null when unknown.
        /// </summary>
        public string? Owner { get; }
    }
}
