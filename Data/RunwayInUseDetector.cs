using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// A runway end that aircraft near the airport are using, with the number of aircraft that were seen lined up with it.
    /// </summary>
    /// <param name="Runway">The runway.</param>
    /// <param name="IsHighEnd">Whether the end is the high end of the runway.</param>
    /// <param name="EndName">Designator of the end, for example "27".</param>
    /// <param name="HeadingDegrees">Direction of the runway when used from this end, in degrees.</param>
    /// <param name="Approaching">Number of descending aircraft on the final approach to this end.</param>
    /// <param name="Departing">Number of climbing aircraft that took off from this end.</param>
    /// <param name="OnRunway">Number of aircraft rolling along the runway toward the other end, which is a landing or a takeoff run.</param>
    public sealed record RunwayTrafficEnd(Runway Runway, bool IsHighEnd, string EndName, double HeadingDegrees, int Approaching, int Departing, int OnRunway)
    {
        /// <summary>
        /// Number of aircraft that voted for this end.
        /// </summary>
        public int Total => Approaching + Departing + OnRunway;
    }

    /// <summary>
    /// Finds the runway ends that are in use from the aircraft seen near an airport: an aircraft that descends on the extended centerline of a runway with its track along it is landing there, a climbing aircraft behind the far end of the runway took off from it, and a fast aircraft on the ground along the runway is on its landing or takeoff run. This is a guess from the traffic of the moment, which complements the guess from the wind.
    /// </summary>
    public static class RunwayInUseDetector
    {
        /// <summary>
        /// Smallest number of aircraft that must be lined up with a runway end for it to be reported, so that a single aircraft passing by does not decide.
        /// </summary>
        public const int MinimumAircraft = 2;

        /// <summary>
        /// Distance from the airport in kilometers beyond which an aircraft is ignored.
        /// </summary>
        private const double SearchRadiusKilometers = 30.0;

        /// <summary>
        /// Length of the final approach in kilometers before the runway threshold in which a descending aircraft counts.
        /// </summary>
        private const double ApproachKilometers = 12.0;

        /// <summary>
        /// Length in kilometers behind the far end of the runway in which a climbing aircraft counts as a departure.
        /// </summary>
        private const double DepartureKilometers = 12.0;

        /// <summary>
        /// Largest difference in degrees between the track of an airborne aircraft and the runway direction.
        /// </summary>
        private const double AirborneTrackToleranceDegrees = 15.0;

        /// <summary>
        /// Largest difference in degrees between the track of an aircraft on the ground and the runway direction.
        /// </summary>
        private const double GroundTrackToleranceDegrees = 20.0;

        /// <summary>
        /// Smallest vertical rate in feet per minute, in either direction, that separates a climb or a descent from level flight.
        /// </summary>
        private const int MinimumVerticalRateFeetPerMinute = 200;

        /// <summary>
        /// Highest altitude above the airport in feet at which an aircraft still counts.
        /// </summary>
        private const int MaximumHeightFeet = 4000;

        /// <summary>
        /// Smallest ground speed in knots of an aircraft on the ground that counts as a landing or a takeoff run, so that taxiing aircraft do not.
        /// </summary>
        private const double MinimumRunSpeedKnots = 30.0;

        /// <summary>
        /// Largest distance in kilometers of an aircraft on the ground from the centerline of the runway.
        /// </summary>
        private const double RunwayHalfWidthKilometers = 0.15;

        /// <summary>
        /// Distance in kilometers from the centerline allowed for an airborne aircraft at the runway threshold.
        /// </summary>
        private const double BaseToleranceKilometers = 0.4;

        /// <summary>
        /// Growth of the allowed distance from the centerline in kilometers for each kilometer from the threshold, which is the width of the funnel of the approach and the climb-out.
        /// </summary>
        private const double ToleranceGrowth = 0.05;

        /// <summary>
        /// Oldest position of an aircraft in seconds that is used.
        /// </summary>
        private const double MaximumPositionAgeSeconds = 30.0;

        /// <summary>
        /// Length of one degree of latitude in kilometers.
        /// </summary>
        private const double KilometersPerDegree = 111.19;

        /// <summary>
        /// Finds the runway ends that the traffic uses.
        /// </summary>
        /// <param name="airport">The airport.</param>
        /// <param name="runways">Runways of the airport.</param>
        /// <param name="aircraft">Aircraft seen now; those far from the airport are ignored.</param>
        /// <returns>The ends that at least <see cref="MinimumAircraft"/> aircraft are lined up with, the busiest first. An end is left out when the opposite end of the same runway has as many aircraft or more. Empty when there is not enough traffic.</returns>
        public static IReadOnlyList<RunwayTrafficEnd> Detect(Airport airport, IReadOnlyList<Runway> runways, IReadOnlyList<Aircraft> aircraft)
        {
            List<EndGeometry> ends = BuildEnds(runways);

            if (ends.Count == 0 || aircraft.Count == 0)
            {
                return Array.Empty<RunwayTrafficEnd>();
            }

            int[] approaching = new int[ends.Count];
            int[] departing = new int[ends.Count];
            int[] onRunway = new int[ends.Count];

            foreach (Aircraft candidate in aircraft)
            {
                if (!candidate.TrackDegrees.HasValue
                    || candidate.SecondsSincePosition > MaximumPositionAgeSeconds
                    || GeoMath.DistanceKilometers(airport.Latitude, airport.Longitude, candidate.Latitude, candidate.Longitude) > SearchRadiusKilometers)
                {
                    continue;
                }

                int bestEnd = -1;
                Kind bestKind = Kind.Approaching;
                double bestOffset = double.MaxValue;

                for (int index = 0; index < ends.Count; index++)
                {
                    if (Classify(ends[index], candidate, airport.ElevationFeet, out Kind kind, out double offset) && offset < bestOffset)
                    {
                        bestEnd = index;
                        bestKind = kind;
                        bestOffset = offset;
                    }
                }

                if (bestEnd < 0)
                {
                    continue;
                }

                switch (bestKind)
                {
                    case Kind.Approaching:
                        approaching[bestEnd]++;
                        break;
                    case Kind.Departing:
                        departing[bestEnd]++;
                        break;
                    default:
                        onRunway[bestEnd]++;
                        break;
                }
            }

            List<RunwayTrafficEnd> result = new List<RunwayTrafficEnd>();

            for (int index = 0; index < ends.Count; index++)
            {
                int total = approaching[index] + departing[index] + onRunway[index];
                int opposite = index ^ 1;
                int oppositeTotal = approaching[opposite] + departing[opposite] + onRunway[opposite];

                if (total < MinimumAircraft || oppositeTotal >= total)
                {
                    continue;
                }

                EndGeometry end = ends[index];

                result.Add(new RunwayTrafficEnd(end.Runway, end.IsHighEnd, end.Name, end.HeadingDegrees, approaching[index], departing[index], onRunway[index]));
            }

            return result
                .OrderByDescending(item => item.Total)
                .ThenBy(item => item.EndName, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Kind of the movement that lines an aircraft up with a runway end.
        /// </summary>
        private enum Kind
        {
            /// <summary>
            /// Descending on the final approach.
            /// </summary>
            Approaching,

            /// <summary>
            /// Climbing after takeoff.
            /// </summary>
            Departing,

            /// <summary>
            /// Rolling along the runway on the ground.
            /// </summary>
            OnRunway,
        }

        /// <summary>
        /// A runway end as seen by an aircraft that uses the runway from it.
        /// </summary>
        private sealed class EndGeometry
        {
            /// <summary>
            /// The runway.
            /// </summary>
            public Runway Runway { get; init; } = null!;

            /// <summary>
            /// Whether this is the high end of the runway.
            /// </summary>
            public bool IsHighEnd { get; init; }

            /// <summary>
            /// Designator of the end.
            /// </summary>
            public string Name { get; init; } = string.Empty;

            /// <summary>
            /// Latitude of the threshold, which is this end of the runway.
            /// </summary>
            public double Latitude { get; init; }

            /// <summary>
            /// Longitude of the threshold, which is this end of the runway.
            /// </summary>
            public double Longitude { get; init; }

            /// <summary>
            /// Direction of use, from this end toward the other end, in degrees.
            /// </summary>
            public double HeadingDegrees { get; init; }

            /// <summary>
            /// Distance between the two ends of the runway in kilometers.
            /// </summary>
            public double LengthKilometers { get; init; }
        }

        /// <summary>
        /// Builds the two ends of every runway that has usable coordinates; the two ends of a runway are neighbors in the list, the low end first.
        /// </summary>
        /// <param name="runways">Runways of the airport.</param>
        /// <returns>The ends.</returns>
        private static List<EndGeometry> BuildEnds(IReadOnlyList<Runway> runways)
        {
            List<EndGeometry> ends = new List<EndGeometry>();

            foreach (Runway runway in runways)
            {
                if (runway.LowEndLatitude == runway.HighEndLatitude && runway.LowEndLongitude == runway.HighEndLongitude)
                {
                    continue;
                }

                double length = GeoMath.DistanceKilometers(runway.LowEndLatitude, runway.LowEndLongitude, runway.HighEndLatitude, runway.HighEndLongitude);

                ends.Add(new EndGeometry
                {
                    Runway = runway,
                    IsHighEnd = false,
                    Name = runway.LowEndName,
                    Latitude = runway.LowEndLatitude,
                    Longitude = runway.LowEndLongitude,
                    HeadingDegrees = GeoMath.BearingDegrees(runway.LowEndLatitude, runway.LowEndLongitude, runway.HighEndLatitude, runway.HighEndLongitude),
                    LengthKilometers = length,
                });
                ends.Add(new EndGeometry
                {
                    Runway = runway,
                    IsHighEnd = true,
                    Name = runway.HighEndName,
                    Latitude = runway.HighEndLatitude,
                    Longitude = runway.HighEndLongitude,
                    HeadingDegrees = GeoMath.BearingDegrees(runway.HighEndLatitude, runway.HighEndLongitude, runway.LowEndLatitude, runway.LowEndLongitude),
                    LengthKilometers = length,
                });
            }

            return ends;
        }

        /// <summary>
        /// Tells whether an aircraft is lined up with a runway end and in which way.
        /// </summary>
        /// <param name="end">The runway end.</param>
        /// <param name="aircraft">The aircraft, which has a known track.</param>
        /// <param name="elevationFeet">Elevation of the airport in feet, or null when unknown.</param>
        /// <param name="kind">Kind of the movement when the aircraft is lined up.</param>
        /// <param name="offset">Distance from the centerline as a fraction of the allowed distance, so that the end that fits best can be chosen among parallel runways.</param>
        /// <returns>True when the aircraft is lined up with the end.</returns>
        private static bool Classify(EndGeometry end, Aircraft aircraft, int? elevationFeet, out Kind kind, out double offset)
        {
            kind = Kind.Approaching;
            offset = 0.0;

            double headingRadians = end.HeadingDegrees * Math.PI / 180.0;
            double east = (aircraft.Longitude - end.Longitude) * KilometersPerDegree * Math.Cos(end.Latitude * Math.PI / 180.0);
            double north = (aircraft.Latitude - end.Latitude) * KilometersPerDegree;
            double along = east * Math.Sin(headingRadians) + north * Math.Cos(headingRadians);
            double cross = Math.Abs(east * Math.Cos(headingRadians) - north * Math.Sin(headingRadians));
            double trackDifference = Math.Abs(NormalizeDifference(aircraft.TrackDegrees!.Value - end.HeadingDegrees));

            if (aircraft.OnGround)
            {
                if (trackDifference > GroundTrackToleranceDegrees
                    || !aircraft.GroundSpeedKnots.HasValue
                    || aircraft.GroundSpeedKnots.Value < MinimumRunSpeedKnots
                    || along < -0.2
                    || along > end.LengthKilometers + 0.2
                    || cross > RunwayHalfWidthKilometers)
                {
                    return false;
                }

                kind = Kind.OnRunway;
                offset = cross / RunwayHalfWidthKilometers;

                return true;
            }

            if (trackDifference > AirborneTrackToleranceDegrees || !aircraft.VerticalRateFeetPerMinute.HasValue || !aircraft.AltitudeFeet.HasValue)
            {
                return false;
            }

            if (elevationFeet.HasValue && aircraft.AltitudeFeet.Value - elevationFeet.Value > MaximumHeightFeet)
            {
                return false;
            }

            int verticalRate = aircraft.VerticalRateFeetPerMinute.Value;

            if (verticalRate <= -MinimumVerticalRateFeetPerMinute && along >= -ApproachKilometers && along <= end.LengthKilometers)
            {
                double tolerance = BaseToleranceKilometers + ToleranceGrowth * Math.Max(0.0, -along);

                if (cross <= tolerance)
                {
                    kind = Kind.Approaching;
                    offset = cross / tolerance;

                    return true;
                }
            }

            if (verticalRate >= MinimumVerticalRateFeetPerMinute && along >= 0.0 && along <= end.LengthKilometers + DepartureKilometers)
            {
                double tolerance = BaseToleranceKilometers + ToleranceGrowth * Math.Max(0.0, along - end.LengthKilometers);

                if (cross <= tolerance)
                {
                    kind = Kind.Departing;
                    offset = cross / tolerance;

                    return true;
                }
            }

            return false;
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
