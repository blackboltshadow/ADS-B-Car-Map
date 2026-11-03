using AdsbCarMap.DataSources.Models;


namespace AdsbCarMap.Data
{
    /// <summary>
    /// Aircraft that triggered an alert.
    /// </summary>
    public sealed class AlertMatch
    {
        /// <summary>
        /// Creates a match.
        /// </summary>
        /// <param name="rule">Rule that fired.</param>
        /// <param name="aircraft">Aircraft that triggered the rule.</param>
        /// <param name="trigger">Event that caused the rule to fire.</param>
        /// <param name="distanceKilometers">Distance of the aircraft from the center of the area.</param>
        public AlertMatch(AlertRule rule, Aircraft aircraft, AlertTrigger trigger, double distanceKilometers)
        {
            Rule = rule;
            Aircraft = aircraft;
            Trigger = trigger;
            DistanceKilometers = distanceKilometers;
        }

        /// <summary>
        /// Rule that fired.
        /// </summary>
        public AlertRule Rule { get; }

        /// <summary>
        /// Aircraft that triggered the rule.
        /// </summary>
        public Aircraft Aircraft { get; }

        /// <summary>
        /// Event that caused the rule to fire.
        /// </summary>
        public AlertTrigger Trigger { get; }

        /// <summary>
        /// Distance of the aircraft from the center of the area.
        /// </summary>
        public double DistanceKilometers { get; }
    }
}
