using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// One saved value of a SharedPreferences file.
    /// </summary>
    /// <param name="Key">Name of the value.</param>
    /// <param name="Value">The value: a string, an int, a long, a float, a bool or a list of strings (a string set).</param>
    public sealed record PreferenceEntry(string Key, object Value);

    /// <summary>
    /// Text format for all the values of a SharedPreferences file, so that they can be put in a backup and written back with their types.
    /// </summary>
    public static class PreferenceSnapshot
    {
        /// <summary>
        /// Writes the values as JSON.
        /// </summary>
        /// <param name="entries">Values to write; values of other types than the supported ones and floats that are not finite are skipped.</param>
        /// <returns>JSON array with one object per value.</returns>
        public static string Serialize(IEnumerable<PreferenceEntry> entries)
        {
            using MemoryStream stream = new MemoryStream();
            JsonWriterOptions options = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

            using (Utf8JsonWriter writer = new Utf8JsonWriter(stream, options))
            {
                writer.WriteStartArray();

                foreach (PreferenceEntry entry in entries)
                {
                    WriteEntry(writer, entry);
                }

                writer.WriteEndArray();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Reads values written by <see cref="Serialize"/>.
        /// </summary>
        /// <param name="json">Text to read, or null.</param>
        /// <returns>The values; damaged entries are skipped, and null is returned when the whole text is not a list of values.</returns>
        public static List<PreferenceEntry>? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                List<PreferenceEntry> entries = new List<PreferenceEntry>();

                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    PreferenceEntry? entry = ParseEntry(element);

                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }

                return entries;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Writes one value with a tag of its type.
        /// </summary>
        /// <param name="writer">JSON writer.</param>
        /// <param name="entry">Value to write.</param>
        private static void WriteEntry(Utf8JsonWriter writer, PreferenceEntry entry)
        {
            if (entry.Value is float single && !float.IsFinite(single))
            {
                return;
            }

            string? tag = entry.Value switch
            {
                string => "s",
                int => "i",
                long => "l",
                float => "f",
                bool => "b",
                IEnumerable<string> => "set",
                _ => null
            };

            if (tag == null)
            {
                return;
            }

            writer.WriteStartObject();
            writer.WriteString("k", entry.Key);
            writer.WriteString("t", tag);

            switch (entry.Value)
            {
                case string text:
                    writer.WriteString("v", text);
                    break;
                case int number:
                    writer.WriteNumber("v", number);
                    break;
                case long number:
                    writer.WriteNumber("v", number);
                    break;
                case float number:
                    writer.WriteNumber("v", number);
                    break;
                case bool flag:
                    writer.WriteBoolean("v", flag);
                    break;
                case IEnumerable<string> items:
                    writer.WriteStartArray("v");

                    foreach (string item in items)
                    {
                        writer.WriteStringValue(item);
                    }

                    writer.WriteEndArray();
                    break;
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Reads one value.
        /// </summary>
        /// <param name="element">JSON element of the value.</param>
        /// <returns>The value or null when it is damaged.</returns>
        private static PreferenceEntry? ParseEntry(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty("k", out JsonElement key) || key.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(key.GetString())
                || !element.TryGetProperty("t", out JsonElement tag) || tag.ValueKind != JsonValueKind.String
                || !element.TryGetProperty("v", out JsonElement value))
            {
                return null;
            }

            string name = key.GetString()!;

            switch (tag.GetString())
            {
                case "s" when value.ValueKind == JsonValueKind.String:
                    return new PreferenceEntry(name, value.GetString() ?? string.Empty);
                case "i" when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int integer):
                    return new PreferenceEntry(name, integer);
                case "l" when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number):
                    return new PreferenceEntry(name, number);
                case "f" when value.ValueKind == JsonValueKind.Number:
                    return new PreferenceEntry(name, value.GetSingle());
                case "b" when value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False:
                    return new PreferenceEntry(name, value.GetBoolean());
                case "set" when value.ValueKind == JsonValueKind.Array:
                    List<string> items = new List<string>();

                    foreach (JsonElement item in value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            items.Add(item.GetString() ?? string.Empty);
                        }
                    }

                    return new PreferenceEntry(name, items);
                default:
                    return null;
            }
        }
    }
}
