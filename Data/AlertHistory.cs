using System.Text;
using System.Text.Json;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// One notification that was shown, kept in the history of the alerts.
    /// </summary>
    public sealed class AlertHistoryEntry
    {
        /// <summary>
        /// Creates an entry.
        /// </summary>
        /// <param name="timeUtc">Time of the notification in UTC.</param>
        /// <param name="ruleName">Name of the rule that fired.</param>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="callsign">Callsign of the aircraft or null.</param>
        /// <param name="trigger">Event that fired the rule.</param>
        /// <param name="summary">Text with the type, altitude and distance, as in the notification.</param>
        public AlertHistoryEntry(DateTime timeUtc, string ruleName, string hex, string? callsign, AlertTrigger trigger, string summary)
        {
            TimeUtc = timeUtc;
            RuleName = ruleName;
            Hex = hex;
            Callsign = callsign;
            Trigger = trigger;
            Summary = summary;
        }

        /// <summary>
        /// Time of the notification in UTC.
        /// </summary>
        public DateTime TimeUtc { get; }

        /// <summary>
        /// Name of the rule that fired.
        /// </summary>
        public string RuleName { get; }

        /// <summary>
        /// ICAO address of the aircraft.
        /// </summary>
        public string Hex { get; }

        /// <summary>
        /// Callsign of the aircraft or null.
        /// </summary>
        public string? Callsign { get; }

        /// <summary>
        /// Event that fired the rule.
        /// </summary>
        public AlertTrigger Trigger { get; }

        /// <summary>
        /// Text with the type, altitude and distance, as in the notification.
        /// </summary>
        public string Summary { get; }
    }

    /// <summary>
    /// Bounded list of the notifications that the alerts showed, newest last, with a text format for saving it.
    /// </summary>
    public sealed class AlertHistory
    {
        /// <summary>
        /// Default number of entries that are kept.
        /// </summary>
        public const int DefaultCapacity = 200;

        /// <summary>
        /// Entries from the oldest to the newest.
        /// </summary>
        private readonly List<AlertHistoryEntry> entries = new List<AlertHistoryEntry>();

        /// <summary>
        /// Number of entries that are kept.
        /// </summary>
        private readonly int capacity;

        /// <summary>
        /// Creates an empty history.
        /// </summary>
        /// <param name="capacity">Number of entries that are kept, at least 1; the oldest ones are dropped first.</param>
        public AlertHistory(int capacity = DefaultCapacity)
        {
            this.capacity = Math.Max(1, capacity);
        }

        /// <summary>
        /// Number of entries.
        /// </summary>
        public int Count => entries.Count;

        /// <summary>
        /// Adds an entry and drops the oldest ones above the capacity.
        /// </summary>
        /// <param name="entry">Entry to add.</param>
        public void Add(AlertHistoryEntry entry)
        {
            entries.Add(entry);

            if (entries.Count > capacity)
            {
                entries.RemoveRange(0, entries.Count - capacity);
            }
        }

        /// <summary>
        /// Removes all entries.
        /// </summary>
        public void Clear()
        {
            entries.Clear();
        }

        /// <summary>
        /// Gives the entries for display.
        /// </summary>
        /// <returns>A copy of the entries, the newest first.</returns>
        public List<AlertHistoryEntry> GetNewestFirst()
        {
            List<AlertHistoryEntry> copy = new List<AlertHistoryEntry>(entries);
            copy.Reverse();

            return copy;
        }

        /// <summary>
        /// Writes the history as JSON.
        /// </summary>
        /// <returns>JSON array of the entries from the oldest to the newest.</returns>
        public string Serialize()
        {
            using MemoryStream stream = new MemoryStream();

            using (Utf8JsonWriter writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();

                foreach (AlertHistoryEntry entry in entries)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("time", entry.TimeUtc.Ticks);
                    writer.WriteString("rule", entry.RuleName);
                    writer.WriteString("hex", entry.Hex);

                    if (entry.Callsign != null)
                    {
                        writer.WriteString("callsign", entry.Callsign);
                    }

                    writer.WriteString("trigger", entry.Trigger.ToString());
                    writer.WriteString("summary", entry.Summary);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Reads a history written by <see cref="Serialize"/>.
        /// </summary>
        /// <param name="json">Saved text or null.</param>
        /// <param name="capacity">Number of entries that are kept.</param>
        /// <returns>The history; damaged text gives an empty one and damaged entries are skipped.</returns>
        public static AlertHistory Parse(string? json, int capacity = DefaultCapacity)
        {
            AlertHistory history = new AlertHistory(capacity);

            if (string.IsNullOrWhiteSpace(json))
            {
                return history;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return history;
                }

                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    AlertHistoryEntry? entry = ParseEntry(element);

                    if (entry != null)
                    {
                        history.Add(entry);
                    }
                }
            }
            catch (JsonException)
            {
                return new AlertHistory(capacity);
            }

            return history;
        }

        /// <summary>
        /// Reads one entry.
        /// </summary>
        /// <param name="element">JSON element of the entry.</param>
        /// <returns>The entry or null when it is damaged.</returns>
        private static AlertHistoryEntry? ParseEntry(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty("time", out JsonElement time) || time.ValueKind != JsonValueKind.Number || !time.TryGetInt64(out long ticks)
                || ticks <= 0 || ticks > DateTime.MaxValue.Ticks)
            {
                return null;
            }

            string? hex = ReadString(element, "hex");
            string? trigger = ReadString(element, "trigger");

            if (string.IsNullOrEmpty(hex) || trigger == null || !Enum.TryParse(trigger, out AlertTrigger parsedTrigger))
            {
                return null;
            }

            return new AlertHistoryEntry(new DateTime(ticks, DateTimeKind.Utc), ReadString(element, "rule") ?? string.Empty, hex, ReadString(element, "callsign"), parsedTrigger, ReadString(element, "summary") ?? string.Empty);
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
}
