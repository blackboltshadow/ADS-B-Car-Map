namespace AdsbCarMap.Data
{
    /// <summary>
    /// Decides when the background checking of the alerts should run.
    /// </summary>
    public static class AlertBackgroundPolicy
    {
        /// <summary>
        /// Tells whether the background checking should run now.
        /// </summary>
        /// <param name="backgroundEnabled">Whether the background mode is switched on.</param>
        /// <param name="alertsEnabled">Whether the alerts are switched on as a whole.</param>
        /// <param name="activeRuleCount">Number of rules that are switched on and can be evaluated.</param>
        /// <returns>True when the background checking should run.</returns>
        public static bool ShouldRun(bool backgroundEnabled, bool alertsEnabled, int activeRuleCount)
        {
            return backgroundEnabled && alertsEnabled && activeRuleCount > 0;
        }

        /// <summary>
        /// Tells whether the background checking should be started after the device boots or the application is updated.
        /// </summary>
        /// <param name="startOnBootEnabled">Whether the user allowed the start without opening the application.</param>
        /// <param name="backgroundEnabled">Whether the background mode is switched on.</param>
        /// <param name="alertsEnabled">Whether the alerts are switched on as a whole.</param>
        /// <param name="activeRuleCount">Number of rules that are switched on and can be evaluated.</param>
        /// <returns>True when the background checking should be started.</returns>
        public static bool ShouldStartOnBoot(bool startOnBootEnabled, bool backgroundEnabled, bool alertsEnabled, int activeRuleCount)
        {
            return startOnBootEnabled && ShouldRun(backgroundEnabled, alertsEnabled, activeRuleCount);
        }
    }
}
