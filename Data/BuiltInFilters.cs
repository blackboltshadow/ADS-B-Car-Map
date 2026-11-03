namespace AdsbCarMap.Data
{
    /// <summary>
    /// Filters predefined in the application.
    /// </summary>
    public static class BuiltInFilters
    {
        /// <summary>
        /// Shows only the military aircraft.
        /// </summary>
        public static readonly AircraftFilter Military = new AircraftFilter("military", "Wojskowe", FilterMode.Show, FilterCondition.Military());

        /// <summary>
        /// Hides the aircraft on the ground.
        /// </summary>
        public static readonly AircraftFilter HideOnGround = new AircraftFilter("hide_ground", "Ukryj na ziemi", FilterMode.Hide, FilterCondition.OnGround());

        /// <summary>
        /// Shows only the flights heading to Rzeszów. The route of each aircraft is downloaded in the background, so an aircraft appears once its route is known.
        /// </summary>
        public static readonly AircraftFilter ToRzeszow = new AircraftFilter(
            "to_rzeszow",
            "Do Rzeszowa",
            FilterMode.Show,
            FilterGroup.Any(FilterCondition.ToAirport("RZE"), FilterCondition.ToAirport("EPRZ"), FilterCondition.ToAirport("Rzeszów")));

        /// <summary>
        /// Shows only the aircraft squawking an emergency code: 7500 (hijacking), 7600 (radio failure) or 7700 (general emergency).
        /// </summary>
        public static readonly AircraftFilter Emergency = new AircraftFilter(
            "emergency",
            "Squawk awaryjny",
            FilterMode.Show,
            FilterGroup.Any(FilterCondition.SquawkIs("7500"), FilterCondition.SquawkIs("7600"), FilterCondition.SquawkIs("7700")));

        /// <summary>
        /// Shows only the aircraft descending faster than 3000 feet per minute.
        /// </summary>
        public static readonly AircraftFilter FastDescent = new AircraftFilter("fast_descent", "Szybko opadające", FilterMode.Show, FilterCondition.VerticalRateBetween(null, -3000.0));

        /// <summary>
        /// All predefined filters in display order.
        /// </summary>
        public static readonly IReadOnlyList<AircraftFilter> All = new List<AircraftFilter> { Military, HideOnGround, ToRzeszow, Emergency, FastDescent };
    }
}
