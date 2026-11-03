using System.Globalization;
using System.Text.Json;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Pansa
{
    /// <summary>
    /// Converts the GeoJSON of the PAŻP airspace map to the airspace model. Reading is tolerant: an area or a reservation that cannot be read is skipped and unknown fields are ignored, because the service has no documented API and can change without notice.
    /// </summary>
    public static class PansaParser
    {
        /// <summary>
        /// Reads the answer of the airspace map endpoint.
        /// </summary>
        /// <param name="json">JSON text with a list of features or an object with a "features" list, or null.</param>
        /// <returns>The areas that could be read; empty for missing text.</returns>
        /// <exception cref="JsonException">The text is not JSON.</exception>
        public static IReadOnlyList<AirspaceZone> ParseZones(string? json)
        {
            List<AirspaceZone> zones = new List<AirspaceZone>();

            if (string.IsNullOrWhiteSpace(json))
            {
                return zones;
            }

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement features = document.RootElement;

            if (features.ValueKind == JsonValueKind.Object && features.TryGetProperty("features", out JsonElement inner))
            {
                features = inner;
            }

            if (features.ValueKind != JsonValueKind.Array)
            {
                return zones;
            }

            foreach (JsonElement feature in features.EnumerateArray())
            {
                AirspaceZone? zone = ReadZone(feature);

                if (zone != null)
                {
                    zones.Add(zone);
                }
            }

            return zones;
        }

        /// <summary>
        /// Reads a height limit written as the service does: "GND" for the ground, "A035" for 3500 ft of altitude, "F095" or "FL095" for flight level 95.
        /// </summary>
        /// <param name="text">Text of the limit.</param>
        /// <returns>The limit, or null when the text is not understood.</returns>
        public static AirspaceLimit? ParseLimit(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string value = text.Trim().ToUpperInvariant();

            if (value == "GND" || value == "SFC")
            {
                return new AirspaceLimit(0, AirspaceLimitReference.Ground);
            }

            AirspaceLimitReference reference;
            string digits;

            if (value.StartsWith("FL", StringComparison.Ordinal))
            {
                reference = AirspaceLimitReference.FlightLevel;
                digits = value.Substring(2);
            }
            else if (value.StartsWith("F", StringComparison.Ordinal))
            {
                reference = AirspaceLimitReference.FlightLevel;
                digits = value.Substring(1);
            }
            else if (value.StartsWith("A", StringComparison.Ordinal))
            {
                reference = AirspaceLimitReference.Altitude;
                digits = value.Substring(1);
            }
            else
            {
                return null;
            }

            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int hundreds))
            {
                return null;
            }

            return new AirspaceLimit(hundreds * 100, reference);
        }

        /// <summary>
        /// Reads one feature of the map.
        /// </summary>
        /// <param name="feature">The GeoJSON feature.</param>
        /// <returns>The area, or null when it has no designation or no usable outline.</returns>
        private static AirspaceZone? ReadZone(JsonElement feature)
        {
            if (feature.ValueKind != JsonValueKind.Object
                || !feature.TryGetProperty("properties", out JsonElement properties)
                || properties.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? designator = ReadString(properties, "designator");

            if (string.IsNullOrWhiteSpace(designator))
            {
                return null;
            }

            if (!feature.TryGetProperty("geometry", out JsonElement geometry))
            {
                return null;
            }

            List<IReadOnlyList<AirspaceVertex>> outlines = ReadOutlines(geometry);

            if (outlines.Count == 0)
            {
                return null;
            }

            List<AirspaceReservation> reservations = new List<AirspaceReservation>();

            if (properties.TryGetProperty("airspaceReservations", out JsonElement list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in list.EnumerateArray())
                {
                    AirspaceReservation? reservation = ReadReservation(item);

                    if (reservation != null)
                    {
                        reservations.Add(reservation);
                    }
                }
            }

            return new AirspaceZone(
                designator.Trim(),
                ReadString(properties, "airspaceElementType")?.Trim() ?? string.Empty,
                outlines,
                CalculateBounds(outlines),
                reservations);
        }

        /// <summary>
        /// Reads the outer outlines of a Polygon or MultiPolygon geometry.
        /// </summary>
        /// <param name="geometry">The GeoJSON geometry.</param>
        /// <returns>The outlines that have at least three corners.</returns>
        private static List<IReadOnlyList<AirspaceVertex>> ReadOutlines(JsonElement geometry)
        {
            List<IReadOnlyList<AirspaceVertex>> outlines = new List<IReadOnlyList<AirspaceVertex>>();

            if (geometry.ValueKind != JsonValueKind.Object
                || !geometry.TryGetProperty("coordinates", out JsonElement coordinates)
                || coordinates.ValueKind != JsonValueKind.Array)
            {
                return outlines;
            }

            string? type = ReadString(geometry, "type");

            if (type == "Polygon")
            {
                AddOutline(outlines, coordinates);
            }
            else if (type == "MultiPolygon")
            {
                foreach (JsonElement polygon in coordinates.EnumerateArray())
                {
                    if (polygon.ValueKind == JsonValueKind.Array)
                    {
                        AddOutline(outlines, polygon);
                    }
                }
            }

            return outlines;
        }

        /// <summary>
        /// Adds the outer ring of one polygon to the list when it can be read.
        /// </summary>
        /// <param name="outlines">List to add to.</param>
        /// <param name="polygon">List of rings of the polygon; the first one is the outer ring.</param>
        private static void AddOutline(List<IReadOnlyList<AirspaceVertex>> outlines, JsonElement polygon)
        {
            if (polygon.GetArrayLength() == 0 || polygon[0].ValueKind != JsonValueKind.Array)
            {
                return;
            }

            List<AirspaceVertex> ring = new List<AirspaceVertex>();

            foreach (JsonElement position in polygon[0].EnumerateArray())
            {
                if (position.ValueKind != JsonValueKind.Array
                    || position.GetArrayLength() < 2
                    || position[0].ValueKind != JsonValueKind.Number
                    || position[1].ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                ring.Add(new AirspaceVertex(position[1].GetDouble(), position[0].GetDouble()));
            }

            if (ring.Count >= 3)
            {
                outlines.Add(ring);
            }
        }

        /// <summary>
        /// Finds the rectangle that contains all the outlines.
        /// </summary>
        /// <param name="outlines">Outlines of an area, at least one with a corner.</param>
        /// <returns>The rectangle.</returns>
        private static AirspaceBounds CalculateBounds(List<IReadOnlyList<AirspaceVertex>> outlines)
        {
            double minLatitude = double.MaxValue;
            double maxLatitude = double.MinValue;
            double minLongitude = double.MaxValue;
            double maxLongitude = double.MinValue;

            foreach (IReadOnlyList<AirspaceVertex> outline in outlines)
            {
                foreach (AirspaceVertex vertex in outline)
                {
                    minLatitude = Math.Min(minLatitude, vertex.Latitude);
                    maxLatitude = Math.Max(maxLatitude, vertex.Latitude);
                    minLongitude = Math.Min(minLongitude, vertex.Longitude);
                    maxLongitude = Math.Max(maxLongitude, vertex.Longitude);
                }
            }

            return new AirspaceBounds(minLatitude, maxLatitude, minLongitude, maxLongitude);
        }

        /// <summary>
        /// Reads one reservation.
        /// </summary>
        /// <param name="item">The reservation object.</param>
        /// <returns>The reservation, or null when the period or a limit cannot be read.</returns>
        private static AirspaceReservation? ReadReservation(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            DateTime? start = ReadDate(item, "startDate");
            DateTime? end = ReadDate(item, "endDate");
            AirspaceLimit? lower = ParseLimit(ReadString(item, "lowerAltitude"));
            AirspaceLimit? upper = ParseLimit(ReadString(item, "upperAltitude"));

            if (!start.HasValue || !end.HasValue || !lower.HasValue || !upper.HasValue)
            {
                return null;
            }

            string? remarks = ReadString(item, "remarks");

            return new AirspaceReservation(
                start.Value,
                end.Value,
                lower.Value,
                upper.Value,
                ReadStatus(ReadString(item, "reservationStatus")),
                ReadString(item, "unit")?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim());
        }

        /// <summary>
        /// Converts the state written by the service.
        /// </summary>
        /// <param name="text">State text, or null.</param>
        /// <returns>The state; <see cref="AirspaceReservationStatus.Other"/> for an unknown text.</returns>
        private static AirspaceReservationStatus ReadStatus(string? text)
        {
            if (string.Equals(text, "ACTIVATED", StringComparison.OrdinalIgnoreCase))
            {
                return AirspaceReservationStatus.Activated;
            }

            if (string.Equals(text, "PLANNED", StringComparison.OrdinalIgnoreCase))
            {
                return AirspaceReservationStatus.Planned;
            }

            return AirspaceReservationStatus.Other;
        }

        /// <summary>
        /// Reads a string property.
        /// </summary>
        /// <param name="element">Object to read from.</param>
        /// <param name="name">Name of the property.</param>
        /// <returns>The text, or null when the property is missing or is not a string.</returns>
        private static string? ReadString(JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }

        /// <summary>
        /// Reads a date property written in UTC.
        /// </summary>
        /// <param name="element">Object to read from.</param>
        /// <param name="name">Name of the property.</param>
        /// <returns>The date as UTC, or null when it is missing or invalid.</returns>
        private static DateTime? ReadDate(JsonElement element, string name)
        {
            string? text = ReadString(element, name);

            if (text != null && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime value))
            {
                return value;
            }

            return null;
        }
    }
}
