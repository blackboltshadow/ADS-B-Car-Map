namespace AdsbCarMap.Settings
{
    /// <summary>
    /// The units chosen by the user for every kind of measured value. Passed to the text builders so they stay independent of the application settings.
    /// </summary>
    public sealed class DisplayUnits
    {
        /// <summary>
        /// Metric units with feet per minute for the vertical rate, the defaults of the settings.
        /// </summary>
        public static readonly DisplayUnits Default = new DisplayUnits(AltitudeUnit.Metric, DistanceUnit.Kilometers, SpeedUnit.KilometersPerHour, VerticalSpeedUnit.FeetPerMinute);

        /// <summary>
        /// Creates a set of units.
        /// </summary>
        /// <param name="altitude">Unit of altitudes, airport elevations and runway lengths.</param>
        /// <param name="distance">Unit of distances.</param>
        /// <param name="speed">Unit of ground speeds.</param>
        /// <param name="verticalSpeed">Unit of vertical rates.</param>
        public DisplayUnits(AltitudeUnit altitude, DistanceUnit distance, SpeedUnit speed, VerticalSpeedUnit verticalSpeed)
        {
            Altitude = altitude;
            Distance = distance;
            Speed = speed;
            VerticalSpeed = verticalSpeed;
        }

        /// <summary>
        /// Unit of altitudes, airport elevations and runway lengths.
        /// </summary>
        public AltitudeUnit Altitude { get; }

        /// <summary>
        /// Unit of distances.
        /// </summary>
        public DistanceUnit Distance { get; }

        /// <summary>
        /// Unit of ground speeds.
        /// </summary>
        public SpeedUnit Speed { get; }

        /// <summary>
        /// Unit of vertical rates.
        /// </summary>
        public VerticalSpeedUnit VerticalSpeed { get; }
    }
}
