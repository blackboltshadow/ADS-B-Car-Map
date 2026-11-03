namespace AdsbCarMap.Data
{
    /// <summary>
    /// Kind of aircraft or object that determines the shape of the icon on the map.
    /// </summary>
    public enum AircraftKind
    {
        /// <summary>
        /// The kind cannot be determined, a generic arrow is drawn.
        /// </summary>
        Unknown,

        /// <summary>
        /// Medium-sized passenger jet.
        /// </summary>
        Airliner,

        /// <summary>
        /// Large wide-body or four-engine aircraft.
        /// </summary>
        Heavy,

        /// <summary>
        /// Small jet, e.g. business or regional.
        /// </summary>
        SmallJet,

        /// <summary>
        /// Turboprop aircraft with a straight wing, e.g. regional or transport.
        /// </summary>
        Turboprop,

        /// <summary>
        /// Light piston aircraft, e.g. a small private plane.
        /// </summary>
        LightPlane,

        /// <summary>
        /// Helicopter.
        /// </summary>
        Helicopter,

        /// <summary>
        /// Military fighter or trainer aircraft.
        /// </summary>
        Fighter,

        /// <summary>
        /// Military transport aircraft or tanker.
        /// </summary>
        MilitaryTransport,

        /// <summary>
        /// Glider.
        /// </summary>
        Glider,

        /// <summary>
        /// Balloon.
        /// </summary>
        Balloon,

        /// <summary>
        /// Unmanned aircraft.
        /// </summary>
        Drone,

        /// <summary>
        /// Ground vehicle or object, e.g. a tower or an airport vehicle.
        /// </summary>
        Ground,
    }
}
