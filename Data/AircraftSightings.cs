using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// One sample of the position of a tracked aircraft.
    /// </summary>
    /// <param name="TimeUtc">Time of the sample in UTC.</param>
    /// <param name="Latitude">Latitude in degrees.</param>
    /// <param name="Longitude">Longitude in degrees.</param>
    /// <param name="AltitudeFeet">Altitude in feet or null when unknown.</param>
    /// <param name="SpeedKnots">Ground speed in knots or null when unknown.</param>
    /// <param name="OnGround">Whether the aircraft was on the ground.</param>
    public sealed record Sighting(DateTime TimeUtc, double Latitude, double Longitude, int? AltitudeFeet, double? SpeedKnots, bool OnGround);

    /// <summary>
    /// One flight of an aircraft: the samples that were taken while it was seen under one callsign without a long break.
    /// </summary>
    public sealed class FlightSegment
    {
        /// <summary>
        /// Creates a flight with its first sample.
        /// </summary>
        /// <param name="callsign">Callsign or null.</param>
        /// <param name="first">First sample.</param>
        public FlightSegment(string? callsign, Sighting first)
        {
            Callsign = callsign;
            Points.Add(first);
            LastSeenUtc = first.TimeUtc;
        }

        /// <summary>
        /// Callsign of the flight or null when it was not known.
        /// </summary>
        public string? Callsign { get; set; }

        /// <summary>
        /// Code of the departure airport or null when the route is not known.
        /// </summary>
        public string? OriginCode { get; set; }

        /// <summary>
        /// City of the departure airport or null.
        /// </summary>
        public string? OriginCity { get; set; }

        /// <summary>
        /// Code of the arrival airport or null when the route is not known.
        /// </summary>
        public string? DestinationCode { get; set; }

        /// <summary>
        /// City of the arrival airport or null.
        /// </summary>
        public string? DestinationCity { get; set; }

        /// <summary>
        /// Airline name or null.
        /// </summary>
        public string? Airline { get; set; }

        /// <summary>
        /// Time of the last notice of the aircraft in this flight, in UTC; it can be later than the last sample.
        /// </summary>
        public DateTime LastSeenUtc { get; set; }

        /// <summary>
        /// Samples from the oldest to the newest.
        /// </summary>
        public List<Sighting> Points { get; } = new List<Sighting>();

        /// <summary>
        /// Time of the first sample in UTC.
        /// </summary>
        public DateTime FirstSeenUtc => Points[0].TimeUtc;

        /// <summary>
        /// Key of the route, such as "WAW>LHR", or null when the route is not known.
        /// </summary>
        public string? RouteKey => string.IsNullOrEmpty(OriginCode) || string.IsNullOrEmpty(DestinationCode) ? null : OriginCode + ">" + DestinationCode;
    }

    /// <summary>
    /// What is new in a flight compared with the earlier flights of the same aircraft.
    /// </summary>
    [Flags]
    public enum FlightNovelty
    {
        /// <summary>
        /// Nothing new.
        /// </summary>
        None = 0,

        /// <summary>
        /// The callsign (flight number) was not seen on this aircraft before.
        /// </summary>
        NewCallsign = 1,

        /// <summary>
        /// The pair of the departure and the arrival airport was not seen on this aircraft before.
        /// </summary>
        NewRoute = 2
    }

    /// <summary>
    /// A flight with the information about what is new in it.
    /// </summary>
    /// <param name="Flight">The flight.</param>
    /// <param name="Novelty">What is new compared with the earlier flights of the aircraft.</param>
    public sealed record FlightInsight(FlightSegment Flight, FlightNovelty Novelty);

    /// <summary>
    /// Log of the flights of the tracked aircraft, built from the samples of the downloaded data, with a text format for saving it.
    /// </summary>
    public sealed class AircraftSightingLog
    {
        /// <summary>
        /// Shortest time between two samples of one aircraft, unless it moved at least <see cref="MinimumMoveKilometers"/>.
        /// </summary>
        public static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Break in the sightings after which the next sighting starts a new flight.
        /// </summary>
        public static readonly TimeSpan FlightGap = TimeSpan.FromMinutes(40);

        /// <summary>
        /// Distance that makes a sample worth taking before <see cref="SampleInterval"/> passes.
        /// </summary>
        public const double MinimumMoveKilometers = 5.0;

        /// <summary>
        /// Number of samples kept in one flight; the oldest ones are dropped first.
        /// </summary>
        public const int MaxPointsPerFlight = 200;

        /// <summary>
        /// Number of flights kept for one aircraft; the oldest ones are dropped first.
        /// </summary>
        public const int MaxFlightsPerAircraft = 40;

        /// <summary>
        /// Flights by ICAO address, each list from the oldest to the newest flight.
        /// </summary>
        private readonly Dictionary<string, List<FlightSegment>> flights = new Dictionary<string, List<FlightSegment>>();

        /// <summary>
        /// Number of aircraft that have flights in the log.
        /// </summary>
        public int AircraftCount => flights.Count;

        /// <summary>
        /// Notes one sighting of an aircraft: adds a sample to the current flight or starts a new flight.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="callsign">Callsign or null.</param>
        /// <param name="sample">Position sample.</param>
        /// <param name="originCode">Code of the departure airport or null when the route is not known.</param>
        /// <param name="originCity">City of the departure airport or null.</param>
        /// <param name="destinationCode">Code of the arrival airport or null when the route is not known.</param>
        /// <param name="destinationCity">City of the arrival airport or null.</param>
        /// <param name="airline">Airline name or null.</param>
        /// <returns>True when the log changed.</returns>
        public bool Observe(string hex, string? callsign, Sighting sample, string? originCode, string? originCity, string? destinationCode, string? destinationCity, string? airline)
        {
            string key = hex.Trim().ToLowerInvariant();

            if (key.Length == 0)
            {
                return false;
            }

            string? name = NormalizeCallsign(callsign);

            if (!flights.TryGetValue(key, out List<FlightSegment>? list))
            {
                list = new List<FlightSegment>();
                flights[key] = list;
            }

            FlightSegment? current = list.Count == 0 ? null : list[list.Count - 1];
            bool startsNewFlight = current == null
                || sample.TimeUtc - current.LastSeenUtc > FlightGap
                || (name != null && current.Callsign != null && name != current.Callsign);

            if (startsNewFlight)
            {
                current = new FlightSegment(name, sample);
                list.Add(current);

                if (list.Count > MaxFlightsPerAircraft)
                {
                    list.RemoveRange(0, list.Count - MaxFlightsPerAircraft);
                }

                ApplyRoute(current, originCode, originCity, destinationCode, destinationCity, airline);

                return true;
            }

            bool changed = false;

            if (current!.Callsign == null && name != null)
            {
                current.Callsign = name;
                changed = true;
            }

            changed |= ApplyRoute(current, originCode, originCity, destinationCode, destinationCity, airline);

            if (sample.TimeUtc > current.LastSeenUtc)
            {
                current.LastSeenUtc = sample.TimeUtc;
            }

            Sighting last = current.Points[current.Points.Count - 1];
            bool dueByTime = sample.TimeUtc - last.TimeUtc >= SampleInterval;
            bool dueByMove = GeoMath.DistanceKilometers(last.Latitude, last.Longitude, sample.Latitude, sample.Longitude) >= MinimumMoveKilometers;

            if (!dueByTime && !dueByMove)
            {
                return changed;
            }

            current.Points.Add(sample);

            if (current.Points.Count > MaxPointsPerFlight)
            {
                current.Points.RemoveRange(0, current.Points.Count - MaxPointsPerFlight);
            }

            return true;
        }

        /// <summary>
        /// Gives the flights of an aircraft with what is new in each of them.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <returns>The flights, the newest first. The first flight that was ever logged is never marked as new, because there is nothing to compare it with.</returns>
        public List<FlightInsight> GetFlights(string hex)
        {
            List<FlightInsight> result = new List<FlightInsight>();

            if (!flights.TryGetValue(hex.Trim().ToLowerInvariant(), out List<FlightSegment>? list))
            {
                return result;
            }

            HashSet<string> seenCallsigns = new HashSet<string>();
            HashSet<string> seenRoutes = new HashSet<string>();

            for (int index = 0; index < list.Count; index++)
            {
                FlightSegment flight = list[index];
                FlightNovelty novelty = FlightNovelty.None;

                if (index > 0)
                {
                    if (flight.Callsign != null && !seenCallsigns.Contains(flight.Callsign))
                    {
                        novelty |= FlightNovelty.NewCallsign;
                    }

                    if (flight.RouteKey != null && seenRoutes.Count > 0 && !seenRoutes.Contains(flight.RouteKey))
                    {
                        novelty |= FlightNovelty.NewRoute;
                    }
                }

                if (flight.Callsign != null)
                {
                    seenCallsigns.Add(flight.Callsign);
                }

                if (flight.RouteKey != null)
                {
                    seenRoutes.Add(flight.RouteKey);
                }

                result.Add(new FlightInsight(flight, novelty));
            }

            result.Reverse();

            return result;
        }

        /// <summary>
        /// Removes the flights of an aircraft.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        public void Remove(string hex)
        {
            flights.Remove(hex.Trim().ToLowerInvariant());
        }

        /// <summary>
        /// Removes the flights of all aircraft that are not accepted by a filter.
        /// </summary>
        /// <param name="keep">Tells whether the aircraft with the given address stays in the log.</param>
        public void RemoveWhere(Func<string, bool> keep)
        {
            foreach (string key in flights.Keys.Where((string hex) => !keep(hex)).ToList())
            {
                flights.Remove(key);
            }
        }

        /// <summary>
        /// Writes the log as JSON.
        /// </summary>
        /// <returns>JSON array with one object per aircraft.</returns>
        public string Serialize()
        {
            using MemoryStream stream = new MemoryStream();

            using (Utf8JsonWriter writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();

                foreach (KeyValuePair<string, List<FlightSegment>> pair in flights)
                {
                    writer.WriteStartObject();
                    writer.WriteString("hex", pair.Key);
                    writer.WriteStartArray("flights");

                    foreach (FlightSegment flight in pair.Value)
                    {
                        WriteFlight(writer, flight);
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Reads a log written by <see cref="Serialize"/>.
        /// </summary>
        /// <param name="json">Saved text or null.</param>
        /// <returns>The log; damaged text gives an empty one and damaged flights are skipped.</returns>
        public static AircraftSightingLog Parse(string? json)
        {
            AircraftSightingLog log = new AircraftSightingLog();

            if (string.IsNullOrWhiteSpace(json))
            {
                return log;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return log;
                }

                foreach (JsonElement aircraft in document.RootElement.EnumerateArray())
                {
                    string? hex = aircraft.ValueKind == JsonValueKind.Object ? ReadString(aircraft, "hex")?.Trim().ToLowerInvariant() : null;

                    if (string.IsNullOrEmpty(hex) || !aircraft.TryGetProperty("flights", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    List<FlightSegment> parsed = new List<FlightSegment>();

                    foreach (JsonElement element in list.EnumerateArray())
                    {
                        FlightSegment? flight = ParseFlight(element);

                        if (flight != null)
                        {
                            parsed.Add(flight);
                        }
                    }

                    if (parsed.Count > 0)
                    {
                        log.flights[hex] = parsed;
                    }
                }
            }
            catch (JsonException)
            {
                return new AircraftSightingLog();
            }

            return log;
        }

        /// <summary>
        /// Normalizes a callsign: trimmed, upper case, null when empty.
        /// </summary>
        /// <param name="callsign">Callsign or null.</param>
        /// <returns>The normalized callsign or null.</returns>
        private static string? NormalizeCallsign(string? callsign)
        {
            return string.IsNullOrWhiteSpace(callsign) ? null : callsign.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Stores a route in a flight that has none yet.
        /// </summary>
        /// <param name="flight">Flight to update.</param>
        /// <param name="originCode">Code of the departure airport or null.</param>
        /// <param name="originCity">City of the departure airport or null.</param>
        /// <param name="destinationCode">Code of the arrival airport or null.</param>
        /// <param name="destinationCity">City of the arrival airport or null.</param>
        /// <param name="airline">Airline name or null.</param>
        /// <returns>True when the flight changed.</returns>
        private static bool ApplyRoute(FlightSegment flight, string? originCode, string? originCity, string? destinationCode, string? destinationCity, string? airline)
        {
            if (flight.RouteKey != null || string.IsNullOrEmpty(originCode) || string.IsNullOrEmpty(destinationCode))
            {
                return false;
            }

            flight.OriginCode = originCode;
            flight.OriginCity = originCity;
            flight.DestinationCode = destinationCode;
            flight.DestinationCity = destinationCity;
            flight.Airline = airline;

            return true;
        }

        /// <summary>
        /// Writes one flight.
        /// </summary>
        /// <param name="writer">JSON writer.</param>
        /// <param name="flight">Flight to write.</param>
        private static void WriteFlight(Utf8JsonWriter writer, FlightSegment flight)
        {
            writer.WriteStartObject();
            WriteOptional(writer, "callsign", flight.Callsign);
            WriteOptional(writer, "oc", flight.OriginCode);
            WriteOptional(writer, "ocity", flight.OriginCity);
            WriteOptional(writer, "dc", flight.DestinationCode);
            WriteOptional(writer, "dcity", flight.DestinationCity);
            WriteOptional(writer, "airline", flight.Airline);
            writer.WriteNumber("last", flight.LastSeenUtc.Ticks);
            writer.WriteStartArray("points");

            foreach (Sighting point in flight.Points)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(point.TimeUtc.Ticks);
                writer.WriteNumberValue(Math.Round(point.Latitude, 5));
                writer.WriteNumberValue(Math.Round(point.Longitude, 5));

                if (point.AltitudeFeet.HasValue)
                {
                    writer.WriteNumberValue(point.AltitudeFeet.Value);
                }
                else
                {
                    writer.WriteNullValue();
                }

                if (point.SpeedKnots.HasValue)
                {
                    writer.WriteNumberValue(Math.Round(point.SpeedKnots.Value, 1));
                }
                else
                {
                    writer.WriteNullValue();
                }

                writer.WriteNumberValue(point.OnGround ? 1 : 0);
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes a text property when the value is not null.
        /// </summary>
        /// <param name="writer">JSON writer.</param>
        /// <param name="name">Property name.</param>
        /// <param name="value">Value or null.</param>
        private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
        {
            if (value != null)
            {
                writer.WriteString(name, value);
            }
        }

        /// <summary>
        /// Reads one flight.
        /// </summary>
        /// <param name="element">JSON element of the flight.</param>
        /// <returns>The flight, or null when it is damaged or has no samples.</returns>
        private static FlightSegment? ParseFlight(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("points", out JsonElement points) || points.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            FlightSegment? flight = null;

            foreach (JsonElement item in points.EnumerateArray())
            {
                Sighting? sample = ParseSample(item);

                if (sample == null)
                {
                    continue;
                }

                if (flight == null)
                {
                    flight = new FlightSegment(ReadString(element, "callsign"), sample);
                }
                else
                {
                    flight.Points.Add(sample);
                }
            }

            if (flight == null)
            {
                return null;
            }

            flight.OriginCode = ReadString(element, "oc");
            flight.OriginCity = ReadString(element, "ocity");
            flight.DestinationCode = ReadString(element, "dc");
            flight.DestinationCity = ReadString(element, "dcity");
            flight.Airline = ReadString(element, "airline");

            if (element.TryGetProperty("last", out JsonElement last) && last.ValueKind == JsonValueKind.Number && last.TryGetInt64(out long ticks)
                && ticks >= flight.Points[flight.Points.Count - 1].TimeUtc.Ticks && ticks <= DateTime.MaxValue.Ticks)
            {
                flight.LastSeenUtc = new DateTime(ticks, DateTimeKind.Utc);
            }

            return flight;
        }

        /// <summary>
        /// Reads one sample written as an array.
        /// </summary>
        /// <param name="item">JSON element of the sample.</param>
        /// <returns>The sample, or null when it is damaged.</returns>
        private static Sighting? ParseSample(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() != 6)
            {
                return null;
            }

            JsonElement time = item[0];
            JsonElement latitude = item[1];
            JsonElement longitude = item[2];

            if (time.ValueKind != JsonValueKind.Number || !time.TryGetInt64(out long ticks) || ticks <= 0 || ticks > DateTime.MaxValue.Ticks
                || latitude.ValueKind != JsonValueKind.Number || longitude.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            int? altitude = item[3].ValueKind == JsonValueKind.Number && item[3].TryGetInt32(out int feet) ? feet : null;
            double? speed = item[4].ValueKind == JsonValueKind.Number ? item[4].GetDouble() : null;
            bool onGround = item[5].ValueKind == JsonValueKind.Number && item[5].TryGetInt32(out int ground) && ground == 1;

            return new Sighting(new DateTime(ticks, DateTimeKind.Utc), latitude.GetDouble(), longitude.GetDouble(), altitude, speed, onGround);
        }

        /// <summary>
        /// Reads a text property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The text or null when the property is missing or is not a string.</returns>
        private static string? ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
    }

    /// <summary>
    /// Texts of the flight log that do not depend on the language of the interface.
    /// </summary>
    public static class SightingFormat
    {
        /// <summary>
        /// Formats a position as degrees with the side of the world, such as "52.17°N 21.01°E".
        /// </summary>
        /// <param name="latitude">Latitude in degrees.</param>
        /// <param name="longitude">Longitude in degrees.</param>
        /// <returns>The text.</returns>
        public static string Coordinates(double latitude, double longitude)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{Math.Abs(latitude):0.00}°{(latitude >= 0 ? "N" : "S")} {Math.Abs(longitude):0.00}°{(longitude >= 0 ? "E" : "W")}");
        }

        /// <summary>
        /// Formats a duration as minutes or as hours with minutes, such as "33 min" or "2 h 05 min".
        /// </summary>
        /// <param name="duration">Duration.</param>
        /// <returns>The text; durations under one minute give "&lt;1 min".</returns>
        public static string Duration(TimeSpan duration)
        {
            int minutes = (int)duration.TotalMinutes;

            if (minutes < 1)
            {
                return "<1 min";
            }

            return minutes < 60 ? minutes + " min" : (minutes / 60) + " h " + (minutes % 60).ToString("00", CultureInfo.InvariantCulture) + " min";
        }
    }
}
