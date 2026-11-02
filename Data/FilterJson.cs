using System.Text;
using System.Text.Json;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Converts filters to JSON and back. Reading is tolerant: a filter that cannot be read is skipped.
    /// </summary>
    public static class FilterJson
    {
        /// <summary>
        /// Maximum nesting depth of an expression accepted when reading, counting every group and every negation. It only protects against damaged data; the editor limits the filters well below it.
        /// </summary>
        public const int MaxDepth = 256;

        /// <summary>
        /// Maximum depth of the JSON document itself. A group takes two levels and a negation one, so it is well above what <see cref="MaxDepth"/> allows.
        /// </summary>
        private const int MaxDocumentDepth = 1000;

        /// <summary>
        /// Writes filters as a JSON array.
        /// </summary>
        /// <param name="filters">Filters to write.</param>
        /// <returns>JSON text.</returns>
        public static string Serialize(IReadOnlyList<AircraftFilter> filters)
        {
            using MemoryStream stream = new MemoryStream();

            using (Utf8JsonWriter writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();

                foreach (AircraftFilter filter in filters)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", filter.Id);
                    writer.WriteString("name", filter.Name);
                    writer.WriteString("mode", filter.Mode.ToString());
                    writer.WritePropertyName("expr");
                    WriteNode(writer, filter.Expression);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Reads filters from a JSON array.
        /// </summary>
        /// <param name="json">JSON text, or null.</param>
        /// <returns>The filters that could be read; empty for missing or damaged text.</returns>
        public static List<AircraftFilter> Parse(string? json)
        {
            List<AircraftFilter> filters = new List<AircraftFilter>();

            if (string.IsNullOrWhiteSpace(json))
            {
                return filters;
            }

            try
            {
                JsonDocumentOptions options = new JsonDocumentOptions();

                options.MaxDepth = MaxDocumentDepth;

                using JsonDocument document = JsonDocument.Parse(json, options);

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return filters;
                }

                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    AircraftFilter? filter = ParseFilter(element);

                    if (filter != null)
                    {
                        filters.Add(filter);
                    }
                }
            }
            catch (JsonException)
            {
                return new List<AircraftFilter>();
            }

            return filters;
        }

        /// <summary>
        /// Writes an expression node.
        /// </summary>
        /// <param name="writer">JSON writer.</param>
        /// <param name="node">Node to write.</param>
        private static void WriteNode(Utf8JsonWriter writer, FilterNode node)
        {
            writer.WriteStartObject();

            if (node is FilterNot negation)
            {
                writer.WriteString("type", "not");
                writer.WritePropertyName("item");
                WriteNode(writer, negation.Inner);
            }
            else if (node is FilterGroup group)
            {
                writer.WriteString("type", group.Mode == FilterGroupMode.All ? "all" : "any");
                writer.WritePropertyName("items");
                writer.WriteStartArray();

                foreach (FilterNode child in group.Children)
                {
                    WriteNode(writer, child);
                }

                writer.WriteEndArray();
            }
            else if (node is FilterCondition condition)
            {
                writer.WriteString("type", "cond");
                writer.WriteString("field", condition.Field.ToString());

                if (condition.Text != null)
                {
                    writer.WriteString("text", condition.Text);
                }

                if (condition.Field == FilterField.Kind)
                {
                    writer.WriteString("kind", condition.Kind.ToString());
                }

                if (condition.Family)
                {
                    writer.WriteBoolean("family", true);
                }

                if (condition.Minimum.HasValue)
                {
                    writer.WriteNumber("min", condition.Minimum.Value);
                }

                if (condition.Maximum.HasValue)
                {
                    writer.WriteNumber("max", condition.Maximum.Value);
                }
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Reads one filter.
        /// </summary>
        /// <param name="element">JSON object of the filter.</param>
        /// <returns>The filter or null when it is incomplete or invalid.</returns>
        private static AircraftFilter? ParseFilter(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? id = GetString(element, "id");
            string? name = GetString(element, "name");
            string? modeText = GetString(element, "mode");

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || !Enum.TryParse(modeText, out FilterMode mode) || !Enum.IsDefined(mode))
            {
                return null;
            }

            if (!element.TryGetProperty("expr", out JsonElement expression))
            {
                return null;
            }

            FilterNode? node = ParseNode(expression, 0);

            return node == null ? null : new AircraftFilter(id, name, mode, node);
        }

        /// <summary>
        /// Reads an expression node.
        /// </summary>
        /// <param name="element">JSON object of the node.</param>
        /// <param name="depth">Current nesting depth.</param>
        /// <returns>The node or null when it is invalid.</returns>
        private static FilterNode? ParseNode(JsonElement element, int depth)
        {
            if (element.ValueKind != JsonValueKind.Object || depth > MaxDepth)
            {
                return null;
            }

            switch (GetString(element, "type"))
            {
                case "not":
                    {
                        if (!element.TryGetProperty("item", out JsonElement item))
                        {
                            return null;
                        }

                        FilterNode? inner = ParseNode(item, depth + 1);

                        return inner == null ? null : new FilterNot(inner);
                    }

                case "all":
                case "any":
                    {
                        if (!element.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
                        {
                            return null;
                        }

                        List<FilterNode> children = new List<FilterNode>();

                        foreach (JsonElement child in items.EnumerateArray())
                        {
                            FilterNode? node = ParseNode(child, depth + 1);

                            if (node == null)
                            {
                                return null;
                            }

                            children.Add(node);
                        }

                        return new FilterGroup(GetString(element, "type") == "all" ? FilterGroupMode.All : FilterGroupMode.Any, children);
                    }

                case "cond":
                    return ParseCondition(element);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Reads a condition.
        /// </summary>
        /// <param name="element">JSON object of the condition.</param>
        /// <returns>The condition or null when it is invalid.</returns>
        private static FilterCondition? ParseCondition(JsonElement element)
        {
            if (!Enum.TryParse(GetString(element, "field"), out FilterField field) || !Enum.IsDefined(field))
            {
                return null;
            }

            AircraftKind kind = AircraftKind.Unknown;

            if (field == FilterField.Kind && (!Enum.TryParse(GetString(element, "kind"), out kind) || !Enum.IsDefined(kind)))
            {
                return null;
            }

            bool family = element.TryGetProperty("family", out JsonElement familyValue) && familyValue.ValueKind == JsonValueKind.True;

            return FilterCondition.Create(field, GetString(element, "text"), kind, GetNumber(element, "min"), GetNumber(element, "max"), family);
        }

        /// <summary>
        /// Reads a text property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The text or null when the property is missing or is not text.</returns>
        private static string? GetString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        /// <summary>
        /// Reads a number property.
        /// </summary>
        /// <param name="element">JSON object.</param>
        /// <param name="name">Property name.</param>
        /// <returns>The number or null when the property is missing or is not a number.</returns>
        private static double? GetNumber(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
        }
    }
}
