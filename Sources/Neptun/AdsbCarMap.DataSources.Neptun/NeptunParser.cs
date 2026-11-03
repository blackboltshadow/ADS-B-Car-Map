using System.Globalization;
using System.Text.Json;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Neptun
{
    /// <summary>
    /// Kind of a frame of the Neptun stream.
    /// </summary>
    public enum NeptunFrameKind
    {
        /// <summary>
        /// Frame that is not understood; it is ignored.
        /// </summary>
        Unknown,

        /// <summary>
        /// The whole list of the threats, sent after connecting.
        /// </summary>
        Snapshot,

        /// <summary>
        /// One threat appeared or changed.
        /// </summary>
        Upsert,

        /// <summary>
        /// One threat disappeared.
        /// </summary>
        Remove,

        /// <summary>
        /// Keep-alive message.
        /// </summary>
        Heartbeat,

        /// <summary>
        /// State of the air raid alerts; not used by the application.
        /// </summary>
        Alerts,
    }

    /// <summary>
    /// One frame of the Neptun stream.
    /// </summary>
    /// <param name="Kind">Kind of the frame.</param>
    /// <param name="Threats">The threats of a snapshot, or the single threat of an upsert; empty for other frames.</param>
    /// <param name="RemovedId">Identifier of the removed threat for a remove frame, otherwise null.</param>
    public sealed record NeptunFrame(NeptunFrameKind Kind, IReadOnlyList<Threat> Threats, string? RemovedId);

    /// <summary>
    /// Converts the JSON of the Neptun API to the threat model. Reading is tolerant: a threat that cannot be read is skipped and unknown fields are ignored, because the API is not versioned and can change without notice.
    /// </summary>
    public static class NeptunParser
    {
        /// <summary>
        /// Reads the answer of the threats endpoint.
        /// </summary>
        /// <param name="json">JSON text, or null.</param>
        /// <returns>The threats that could be read; empty for missing or damaged text.</returns>
        /// <exception cref="JsonException">The text is not JSON.</exception>
        public static IReadOnlyList<Threat> ParseThreats(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<Threat>();
            }

            using JsonDocument document = JsonDocument.Parse(json);

            return ReadThreatList(document.RootElement);
        }

        /// <summary>
        /// Reads one frame of the stream.
        /// </summary>
        /// <param name="json">JSON text of the frame, or null.</param>
        /// <returns>The frame; a frame of the kind <see cref="NeptunFrameKind.Unknown"/> when the text is damaged or not understood.</returns>
        public static NeptunFrame ParseFrame(string? json)
        {
            NeptunFrame unknown = new NeptunFrame(NeptunFrameKind.Unknown, new List<Threat>(), null);

            if (string.IsNullOrWhiteSpace(json))
            {
                return unknown;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    return unknown;
                }

                string? type = ReadString(root, "type");
                root.TryGetProperty("data", out JsonElement data);

                switch (type)
                {
                    case "snapshot":
                        return new NeptunFrame(NeptunFrameKind.Snapshot, ReadThreatList(data), null);
                    case "upsert":
                        Threat? threat = ParseThreat(data);

                        return threat == null ? unknown : new NeptunFrame(NeptunFrameKind.Upsert, new List<Threat> { threat }, null);
                    case "remove":
                        string? id = data.ValueKind == JsonValueKind.Object ? ReadString(data, "id") : null;

                        return string.IsNullOrEmpty(id) ? unknown : new NeptunFrame(NeptunFrameKind.Remove, new List<Threat>(), id);
                    case "heartbeat":
                        return new NeptunFrame(NeptunFrameKind.Heartbeat, new List<Threat>(), null);
                    case "alerts":
                        return new NeptunFrame(NeptunFrameKind.Alerts, new List<Threat>(), null);
                    default:
                        return unknown;
                }
            }
            catch (JsonException)
            {
                return unknown;
            }
        }

        /// <summary>
        /// Reads the "threats" array of an object.
        /// </summary>
        /// <param name="container">Object that holds the "threats" array.</param>
        /// <returns>The threats that could be read.</returns>
        private static List<Threat> ReadThreatList(JsonElement container)
        {
            List<Threat> threats = new List<Threat>();

            if (container.ValueKind != JsonValueKind.Object || !container.TryGetProperty("threats", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                return threats;
            }

            foreach (JsonElement element in list.EnumerateArray())
            {
                Threat? threat = ParseThreat(element);

                if (threat != null)
                {
                    threats.Add(threat);
                }
            }

            return threats;
        }

        /// <summary>
        /// Reads one threat.
        /// </summary>
        /// <param name="element">JSON object of the threat.</param>
        /// <returns>The threat, or null when it has no identifier or no valid position.</returns>
        private static Threat? ParseThreat(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? id = ReadString(element, "id");
            double? latitude = ReadNumber(element, "lat");
            double? longitude = ReadNumber(element, "lon");

            if (string.IsNullOrEmpty(id) || latitude == null || longitude == null || Math.Abs(latitude.Value) > 90.0 || Math.Abs(longitude.Value) > 180.0)
            {
                return null;
            }

            double? speed = null;
            double? heading = ReadNumber(element, "heading");

            if (element.TryGetProperty("velocity", out JsonElement velocity) && velocity.ValueKind == JsonValueKind.Object)
            {
                speed = ReadNumber(velocity, "speedKmh");
                heading ??= ReadNumber(velocity, "bearingDeg");
            }

            return new Threat(
                id,
                ParseKind(ReadString(element, "type")),
                ReadString(element, "title") ?? string.Empty,
                ReadString(element, "region") ?? string.Empty,
                ReadString(element, "district") ?? string.Empty,
                ReadString(element, "locality") ?? string.Empty,
                latitude.Value,
                longitude.Value,
                heading,
                speed,
                ParseConfidence(ReadString(element, "confidenceLevel")),
                (int)Math.Max(0.0, ReadNumber(element, "sourceCount") ?? 0.0),
                (int)Math.Max(0.0, ReadNumber(element, "count") ?? 0.0),
                ReadTime(element, "updatedAt"),
                ReadTime(element, "confirmedAt"),
                ParseStatus(ReadString(element, "status")),
                Math.Max(0.0, ReadNumber(element, "uncertaintyKm") ?? 0.0),
                string.Equals(ReadString(element, "positionQuality"), "approx", StringComparison.OrdinalIgnoreCase),
                ReadBoolean(element, "presumptiveCourse"),
                ReadBoolean(element, "sea"),
                ReadBoolean(element, "advisory"),
                ReadBoolean(element, "areaOnly"),
                ReadString(element, "explanationShort") ?? string.Empty,
                ParseTrail(element));
        }

        /// <summary>
        /// Reads the recent path of a threat.
        /// </summary>
        /// <param name="element">JSON object of the threat.</param>
        /// <returns>The valid points of the path, empty when there is none.</returns>
        private static List<ThreatTrailPoint> ParseTrail(JsonElement element)
        {
            List<ThreatTrailPoint> trail = new List<ThreatTrailPoint>();

            if (!element.TryGetProperty("trail", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                return trail;
            }

            foreach (JsonElement point in list.EnumerateArray())
            {
                if (point.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                double? latitude = ReadNumber(point, "lat");
                double? longitude = ReadNumber(point, "lon");

                if (latitude != null && longitude != null && Math.Abs(latitude.Value) <= 90.0 && Math.Abs(longitude.Value) <= 180.0)
                {
                    trail.Add(new ThreatTrailPoint(latitude.Value, longitude.Value, ReadTime(point, "t")));
                }
            }

            return trail;
        }

        /// <summary>
        /// Converts the type name of the API to a kind.
        /// </summary>
        /// <param name="type">Type name, or null.</param>
        /// <returns>The kind, <see cref="ThreatKind.Unknown"/> for a name that is not known.</returns>
        private static ThreatKind ParseKind(string? type)
        {
            switch (type?.Trim().ToLowerInvariant())
            {
                case "uav":
                    return ThreatKind.Uav;
                case "fpv":
                    return ThreatKind.Fpv;
                case "recon":
                    return ThreatKind.Recon;
                case "missile":
                    return ThreatKind.Missile;
                case "ballistic":
                    return ThreatKind.Ballistic;
                case "kab":
                    return ThreatKind.Kab;
                case "mig31k":
                    return ThreatKind.Mig31k;
                default:
                    return ThreatKind.Unknown;
            }
        }

        /// <summary>
        /// Converts the confidence name of the API.
        /// </summary>
        /// <param name="level">Confidence name, or null.</param>
        /// <returns>The confidence, low for a name that is not known.</returns>
        private static ThreatConfidence ParseConfidence(string? level)
        {
            switch (level?.Trim().ToLowerInvariant())
            {
                case "high":
                    return ThreatConfidence.High;
                case "medium":
                    return ThreatConfidence.Medium;
                default:
                    return ThreatConfidence.Low;
            }
        }

        /// <summary>
        /// Converts the status name of the API.
        /// </summary>
        /// <param name="status">Status name, or null.</param>
        /// <returns>The status, active for a name that is not known.</returns>
        private static ThreatStatus ParseStatus(string? status)
        {
            switch (status?.Trim().ToLowerInvariant())
            {
                case "stale":
                    return ThreatStatus.Stale;
                case "resolved":
                    return ThreatStatus.Resolved;
                default:
                    return ThreatStatus.Active;
            }
        }

        /// <summary>
        /// Reads a text property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The text, or null when the property is missing or is not text.</returns>
        private static string? ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        /// <summary>
        /// Reads a numeric property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The number, or null when the property is missing, null or not a number.</returns>
        private static double? ReadNumber(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
        }

        /// <summary>
        /// Reads a flag. The API writes some flags as true or leaves them out, so anything but true means false.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>True when the property is true.</returns>
        private static bool ReadBoolean(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
        }

        /// <summary>
        /// Reads a time property written as an ISO 8601 text.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The time in UTC, or null when the property is missing or cannot be read.</returns>
        private static DateTime? ReadTime(JsonElement element, string name)
        {
            string? text = ReadString(element, name);

            if (text != null && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
            {
                return parsed.UtcDateTime;
            }

            return null;
        }
    }
}
