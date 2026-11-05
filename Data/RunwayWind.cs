namespace AdsbCarMap.Data
{
    /// <summary>
    /// The runway end that the wind favors and the components of the wind along and across it.
    /// </summary>
    /// <param name="Runway">The runway.</param>
    /// <param name="IsHighEnd">Whether the favored end is the high end of the runway, which is the end that lies farther along the runway from the low end.</param>
    /// <param name="EndName">Designator of the favored end, for example "27".</param>
    /// <param name="HeadingDegrees">Direction of the runway when used from the favored end, in degrees.</param>
    /// <param name="HeadwindKnots">Wind component along the runway against the direction of use in knots; negative for a tailwind.</param>
    /// <param name="CrosswindKnots">Wind component across the runway in knots; positive when the wind blows from the right of the direction of use, negative from the left.</param>
    public sealed record RunwayWindResult(Runway Runway, bool IsHighEnd, string EndName, double HeadingDegrees, double HeadwindKnots, double CrosswindKnots);

    /// <summary>
    /// Chooses the runway end that is most likely in use from the wind: aircraft take off and land against the wind, so the end with the largest headwind is chosen. This is only a guess from one weather report, because the controller chooses the runway and may also consider noise rules, approach procedures and works.
    /// </summary>
    public static class RunwayWind
    {
        /// <summary>
        /// Wind speed in knots below which the wind does not favor any runway.
        /// </summary>
        public const int MinimumWindKnots = 3;

        /// <summary>
        /// Shortest runway that is considered, as a fraction of the longest runway of the airport, so that a short grass strip does not win over the main runway.
        /// </summary>
        private const double MinimumLengthFraction = 0.5;

        /// <summary>
        /// Difference of the headwind in knots below which two ends are equally good and the longer runway is chosen.
        /// </summary>
        private const double TieKnots = 1.0;

        /// <summary>
        /// Chooses the runway end that the wind favors.
        /// </summary>
        /// <param name="runways">Runways of the airport.</param>
        /// <param name="windDirectionDegrees">Direction the wind blows from, or null when it is not known.</param>
        /// <param name="isWindVariable">Whether the direction is variable.</param>
        /// <param name="windSpeedKnots">Mean wind speed in knots, or null when it is not known.</param>
        /// <returns>The favored end, or null when the wind is too weak, variable or not known, or no runway has usable coordinates.</returns>
        public static RunwayWindResult? Choose(IReadOnlyList<Runway> runways, int? windDirectionDegrees, bool isWindVariable, int? windSpeedKnots)
        {
            if (isWindVariable || !windDirectionDegrees.HasValue || !windSpeedKnots.HasValue || windSpeedKnots.Value < MinimumWindKnots)
            {
                return null;
            }

            List<Runway> usable = runways
                .Where(runway => runway.LowEndLatitude != runway.HighEndLatitude || runway.LowEndLongitude != runway.HighEndLongitude)
                .ToList();

            if (usable.Count == 0)
            {
                return null;
            }

            int longest = usable.Max(runway => runway.LengthFeet);
            RunwayWindResult? best = null;
            int bestLength = 0;

            foreach (Runway runway in usable.Where(runway => runway.LengthFeet >= longest * MinimumLengthFraction))
            {
                double lowHeading = GeoMath.BearingDegrees(runway.LowEndLatitude, runway.LowEndLongitude, runway.HighEndLatitude, runway.HighEndLongitude);

                for (int end = 0; end < 2; end++)
                {
                    bool isHigh = end == 1;
                    double heading = isHigh ? (lowHeading + 180.0) % 360.0 : lowHeading;
                    double angle = NormalizeDifference(windDirectionDegrees.Value - heading) * Math.PI / 180.0;
                    double headwind = windSpeedKnots.Value * Math.Cos(angle);
                    double crosswind = windSpeedKnots.Value * Math.Sin(angle);
                    RunwayWindResult candidate = new RunwayWindResult(runway, isHigh, isHigh ? runway.HighEndName : runway.LowEndName, heading, headwind, crosswind);

                    if (IsBetter(candidate, best, bestLength))
                    {
                        best = candidate;
                        bestLength = runway.LengthFeet;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Tells whether a runway end is a better choice than the best one so far: a clearly larger headwind wins; for ends that are about equally good the longer runway wins, then the larger headwind, then the lower designator.
        /// </summary>
        /// <param name="candidate">The end being considered.</param>
        /// <param name="best">The best end so far, or null.</param>
        /// <param name="bestLength">Length of the runway of the best end in feet.</param>
        /// <returns>True when the candidate replaces the best end.</returns>
        private static bool IsBetter(RunwayWindResult candidate, RunwayWindResult? best, int bestLength)
        {
            if (best == null)
            {
                return true;
            }

            double difference = candidate.HeadwindKnots - best.HeadwindKnots;

            if (Math.Abs(difference) > TieKnots)
            {
                return difference > 0.0;
            }

            if (candidate.Runway.LengthFeet != bestLength)
            {
                return candidate.Runway.LengthFeet > bestLength;
            }

            if (Math.Abs(difference) > 0.01)
            {
                return difference > 0.0;
            }

            return string.CompareOrdinal(candidate.EndName, best.EndName) < 0;
        }

        /// <summary>
        /// Brings the difference of two directions to the range from -180 to 180 degrees.
        /// </summary>
        /// <param name="degrees">Difference in degrees.</param>
        /// <returns>The same difference in the range from -180 to 180 degrees.</returns>
        private static double NormalizeDifference(double degrees)
        {
            double result = degrees % 360.0;

            if (result > 180.0)
            {
                result -= 360.0;
            }
            else if (result <= -180.0)
            {
                result += 360.0;
            }

            return result;
        }
    }
}
