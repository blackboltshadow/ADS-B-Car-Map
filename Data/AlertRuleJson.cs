using System.Text;
using System.Text.Json;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Converts alert rules to JSON and back. The condition is stored only as the identifier of a saved filter, so a parsed rule has no condition until the store resolves it. Reading is tolerant: a rule that cannot be read is skipped.
    /// </summary>
    public static class AlertRuleJson
    {
        /// <summary>
        /// Writes rules as a JSON array.
        /// </summary>
        /// <param name="rules">Rules to write.</param>
        /// <returns>JSON text.</returns>
        public static string Serialize(IReadOnlyList<AlertRule> rules)
        {
            using MemoryStream stream = new MemoryStream();

            using (Utf8JsonWriter writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();

                foreach (AlertRule rule in rules)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", rule.Id);
                    writer.WriteString("name", rule.Name);
                    writer.WriteBoolean("enabled", rule.IsEnabled);
                    writer.WriteString("area", rule.Area.Kind.ToString());
                    writer.WriteNumber("radius", rule.Area.RadiusKilometers);

                    if (rule.Area.Center != null)
                    {
                        writer.WriteNumber("latitude", rule.Area.Center.Latitude);
                        writer.WriteNumber("longitude", rule.Area.Center.Longitude);
                    }

                    if (rule.Area.AirportIcao != null)
                    {
                        writer.WriteString("airport", rule.Area.AirportIcao);
                    }

                    if (rule.Area.Vertices != null)
                    {
                        writer.WriteStartArray("vertices");

                        foreach (GeoPoint vertex in rule.Area.Vertices)
                        {
                            writer.WriteStartArray();
                            writer.WriteNumberValue(vertex.Latitude);
                            writer.WriteNumberValue(vertex.Longitude);
                            writer.WriteEndArray();
                        }

                        writer.WriteEndArray();
                    }

                    if (rule.ConditionFilterId != null)
                    {
                        writer.WriteString("filter", rule.ConditionFilterId);
                    }

                    writer.WriteBoolean("entry", rule.NotifyOnEntry);
                    writer.WriteBoolean("appearance", rule.NotifyOnAppearance);
                    writer.WriteBoolean("exit", rule.NotifyOnExit);
                    writer.WriteNumber("cooldown", rule.CooldownMinutes);

                    if (rule.WatchedHex != null)
                    {
                        writer.WriteString("hex", rule.WatchedHex);
                    }

                    if (rule.Importance != AlertImportance.High)
                    {
                        writer.WriteString("importance", rule.Importance.ToString());
                    }

                    if (!rule.Schedule.IsAlways)
                    {
                        writer.WriteNumber("days", rule.Schedule.ActiveDays);

                        if (rule.Schedule.QuietStartMinute != null && rule.Schedule.QuietEndMinute != null)
                        {
                            writer.WriteNumber("quietFrom", rule.Schedule.QuietStartMinute.Value);
                            writer.WriteNumber("quietTo", rule.Schedule.QuietEndMinute.Value);
                        }
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Reads rules from a JSON array.
        /// </summary>
        /// <param name="json">JSON text, or null.</param>
        /// <returns>The rules that could be read; empty for missing or damaged text.</returns>
        public static List<AlertRule> Parse(string? json)
        {
            List<AlertRule> rules = new List<AlertRule>();

            if (string.IsNullOrWhiteSpace(json))
            {
                return rules;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return rules;
                }

                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    AlertRule? rule = ParseRule(element);

                    if (rule != null)
                    {
                        rules.Add(rule);
                    }
                }
            }
            catch (JsonException)
            {
                return new List<AlertRule>();
            }

            return rules;
        }

        /// <summary>
        /// Reads one rule.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <returns>The rule or null when it is incomplete or invalid.</returns>
        private static AlertRule? ParseRule(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? id = ReadString(element, "id");
            string? areaText = ReadString(element, "area");
            double? radius = ReadNumber(element, "radius");

            if (string.IsNullOrEmpty(id) || areaText == null || !Enum.TryParse(areaText, out AlertAreaKind kind))
            {
                return null;
            }

            if (kind != AlertAreaKind.Polygon && (radius == null || radius.Value <= 0.0))
            {
                return null;
            }

            GeoPoint? center = null;
            double? latitude = ReadNumber(element, "latitude");
            double? longitude = ReadNumber(element, "longitude");

            if (latitude != null && longitude != null)
            {
                center = new GeoPoint(latitude.Value, longitude.Value);
            }

            string? airport = ReadString(element, "airport");

            if (kind == AlertAreaKind.Circle && center == null)
            {
                return null;
            }

            if (kind == AlertAreaKind.AroundAirport && string.IsNullOrEmpty(airport))
            {
                return null;
            }

            AlertArea? area = kind == AlertAreaKind.Polygon ? ParsePolygon(element) : new AlertArea(kind, radius!.Value, center, airport);

            if (area == null)
            {
                return null;
            }

            double? cooldown = ReadNumber(element, "cooldown");

            return new AlertRule(
                id,
                ReadString(element, "name") ?? string.Empty,
                ReadBoolean(element, "enabled", true),
                area,
                null,
                ReadBoolean(element, "entry", true),
                ReadBoolean(element, "appearance", true),
                cooldown == null ? 30 : (int)cooldown.Value,
                ReadString(element, "filter"),
                ParseSchedule(element),
                Enum.TryParse(ReadString(element, "importance"), out AlertImportance importance) ? importance : AlertImportance.High,
                ReadBoolean(element, "exit", false),
                ReadString(element, "hex"));
        }

        /// <summary>
        /// Reads the polygon of a rule.
        /// </summary>
        /// <param name="element">Rule element.</param>
        /// <returns>The area or null when the vertices are missing, too few, too many or invalid.</returns>
        private static AlertArea? ParsePolygon(JsonElement element)
        {
            if (!element.TryGetProperty("vertices", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            List<GeoPoint> vertices = new List<GeoPoint>();

            foreach (JsonElement pair in list.EnumerateArray())
            {
                if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2)
                {
                    return null;
                }

                JsonElement latitude = pair[0];
                JsonElement longitude = pair[1];

                if (latitude.ValueKind != JsonValueKind.Number || longitude.ValueKind != JsonValueKind.Number)
                {
                    return null;
                }

                vertices.Add(new GeoPoint(latitude.GetDouble(), longitude.GetDouble()));
            }

            if (vertices.Count < AlertArea.MinimumVertices || vertices.Count > AlertArea.MaximumVertices || !AlertArea.IsSimple(vertices))
            {
                return null;
            }

            return AlertArea.CreatePolygon(vertices);
        }

        /// <summary>
        /// Reads the schedule of a rule.
        /// </summary>
        /// <param name="element">Rule element.</param>
        /// <returns>The schedule, without any restriction when the rule has none.</returns>
        private static AlertSchedule ParseSchedule(JsonElement element)
        {
            double? days = ReadNumber(element, "days");
            double? from = ReadNumber(element, "quietFrom");
            double? to = ReadNumber(element, "quietTo");

            return new AlertSchedule(days == null ? AlertSchedule.AllDays : (int)days.Value, from == null ? null : (int)from.Value, to == null ? null : (int)to.Value);
        }

        /// <summary>
        /// Reads a text property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The text or null when the property is missing or is not text.</returns>
        private static string? ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        /// <summary>
        /// Reads a numeric property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The number or null when the property is missing or is not a number.</returns>
        private static double? ReadNumber(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
        }

        /// <summary>
        /// Reads a boolean property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <param name="fallback">Value used when the property is missing or is not a boolean.</param>
        /// <returns>The value.</returns>
        private static bool ReadBoolean(JsonElement element, string name, bool fallback)
        {
            if (element.TryGetProperty(name, out JsonElement value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
            {
                return value.GetBoolean();
            }

            return fallback;
        }
    }
}
