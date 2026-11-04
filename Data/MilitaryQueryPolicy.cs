namespace AdsbCarMap.Data
{
    /// <summary>
    /// Decides how the aircraft are downloaded while the mode that shows only military aircraft is on: with one request for all the military aircraft in the world, or with the usual request for the visible area.
    /// </summary>
    public static class MilitaryQueryPolicy
    {
        /// <summary>
        /// Tells whether the request for all the military aircraft should be tried.
        /// </summary>
        /// <param name="militaryOnly">Whether the mode that shows only military aircraft is on.</param>
        /// <param name="settingOn">Whether the setting that allows the request for all the military aircraft is on.</param>
        /// <returns>True when both are on.</returns>
        public static bool UseGlobalRequest(bool militaryOnly, bool settingOn)
        {
            return militaryOnly && settingOn;
        }

        /// <summary>
        /// Gives the pause before the next download. The answer with all the military aircraft is large and the same for every user, so it is asked for less often than the area is.
        /// </summary>
        /// <param name="globalUsed">Whether the last download was the request for all the military aircraft.</param>
        /// <param name="normal">The usual pause between two downloads of an area.</param>
        /// <param name="militaryMinimum">The shortest pause between two downloads of all the military aircraft.</param>
        /// <returns>The longer of the two pauses after a global request, otherwise the usual pause.</returns>
        public static TimeSpan GetInterval(bool globalUsed, TimeSpan normal, TimeSpan militaryMinimum)
        {
            return globalUsed && militaryMinimum > normal ? militaryMinimum : normal;
        }
    }
}
