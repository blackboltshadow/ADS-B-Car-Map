using System.Globalization;
using System.Text.Json;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Rso
{
    /// <summary>
    /// Converts the JSON of the notices published by the regional warning system through TVP to the warning model. Reading is tolerant: a notice that cannot be read is skipped and unknown fields are ignored, because the feed is not documented and can change without notice. Several quirks of the feed are handled: numbers written as text, the alarm flag written as "0" or "1", extra keys that hold date formats, and the regions given as an object keyed by a number.
    /// </summary>
    public static class RsoParser
    {
        /// <summary>
        /// Time zone in which the feed writes its dates.
        /// </summary>
        private static readonly TimeZoneInfo FeedTimeZone = FindTimeZone();

        /// <summary>
        /// Reads the answer of the feed.
        /// </summary>
        /// <param name="json">JSON text with an object that has a "newses" list, or null.</param>
        /// <param name="category">Category the answer was asked for; it is put in every notice, because the feed does not say it.</param>
        /// <returns>The notices that could be read; empty for missing text or an answer without the list.</returns>
        /// <exception cref="JsonException">The text is not JSON.</exception>
        public static IReadOnlyList<Warning> ParseWarnings(string? json, string category)
        {
            List<Warning> warnings = new List<Warning>();

            if (string.IsNullOrWhiteSpace(json))
            {
                return warnings;
            }

            using JsonDocument document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("newses", out JsonElement list)
                || list.ValueKind != JsonValueKind.Array)
            {
                return warnings;
            }

            foreach (JsonElement item in list.EnumerateArray())
            {
                Warning? warning = ReadWarning(item, category);

                if (warning != null)
                {
                    warnings.Add(warning);
                }
            }

            return warnings;
        }

        /// <summary>
        /// Converts a date and time of the feed, written in the local time of Poland, to UTC.
        /// </summary>
        /// <param name="text">Text such as "2026-11-03 17:17:00".</param>
        /// <returns>The moment in UTC, or null when the text is not a date.</returns>
        public static DateTime? ParseLocalDate(string? text)
        {
            if (text == null || !DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime local))
            {
                return null;
            }

            DateTime unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

            if (FeedTimeZone.IsInvalidTime(unspecified))
            {
                unspecified = unspecified.AddHours(1);
            }

            return TimeZoneInfo.ConvertTimeToUtc(unspecified, FeedTimeZone);
        }

        /// <summary>
        /// Reads one notice.
        /// </summary>
        /// <param name="item">The notice object.</param>
        /// <param name="category">Category of the answer.</param>
        /// <returns>The notice, or null when it has no identifier or no date to put it on the time line.</returns>
        private static Warning? ReadWarning(JsonElement item, string category)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? id = ReadText(item, "id");
            DateTime? updated = ParseLocalDate(ReadText(item, "updated_at")) ?? ParseLocalDate(ReadText(item, "created_at"));
            DateTime? validFrom = ParseLocalDate(ReadText(item, "valid_from")) ?? updated;

            if (string.IsNullOrWhiteSpace(id) || validFrom == null)
            {
                return null;
            }

            double? level = ReadNumber(item, "water_level_value");
            double? warningLevel = ReadNumber(item, "water_level_warning_status_value");
            double? alarmLevel = ReadNumber(item, "water_level_alarm_status_value");
            WarningWaterLevel? water = level == null && warningLevel == null && alarmLevel == null ? null : new WarningWaterLevel(level, warningLevel, alarmLevel);

            return new Warning(
                id.Trim(),
                category,
                ReadText(item, "title")?.Trim() ?? string.Empty,
                ReadText(item, "shortcut")?.Trim() ?? string.Empty,
                ReadText(item, "content")?.Trim() ?? string.Empty,
                ReadText(item, "rso_alarm")?.Trim() == "1",
                validFrom.Value,
                ParseLocalDate(ReadText(item, "valid_to")),
                updated ?? validFrom.Value,
                ReadProvinces(item),
                ReadText(item, "river_name")?.Trim() ?? string.Empty,
                ReadText(item, "location_name")?.Trim() ?? string.Empty,
                ReadNumber(item, "latitude"),
                ReadNumber(item, "longitude"),
                water);
        }

        /// <summary>
        /// Reads the regions of a notice, which the feed gives as an object keyed by the number of the region.
        /// </summary>
        /// <param name="item">The notice object.</param>
        /// <returns>The regions; empty when the notice has none.</returns>
        private static List<WarningProvince> ReadProvinces(JsonElement item)
        {
            List<WarningProvince> provinces = new List<WarningProvince>();

            if (!item.TryGetProperty("provinces", out JsonElement value))
            {
                return provinces;
            }

            IEnumerable<JsonElement> entries = value.ValueKind == JsonValueKind.Object
                ? value.EnumerateObject().Select((JsonProperty property) => property.Value)
                : value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : Enumerable.Empty<JsonElement>();

            foreach (JsonElement entry in entries)
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string? slug = ReadText(entry, "slug_name");
                string? name = ReadText(entry, "name");

                if (!string.IsNullOrWhiteSpace(slug))
                {
                    provinces.Add(new WarningProvince(slug.Trim(), string.IsNullOrWhiteSpace(name) ? slug.Trim() : name.Trim()));
                }
            }

            return provinces;
        }

        /// <summary>
        /// Reads a property that may be a string or a number, as text.
        /// </summary>
        /// <param name="element">Object to read from.</param>
        /// <param name="name">Name of the property.</param>
        /// <returns>The text, or null when the property is missing, null or of another kind.</returns>
        private static string? ReadText(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
            {
                return null;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return value.GetString();
                case JsonValueKind.Number:
                    return value.GetRawText();
                default:
                    return null;
            }
        }

        /// <summary>
        /// Reads a property that may be a number or a number written as text.
        /// </summary>
        /// <param name="element">Object to read from.</param>
        /// <param name="name">Name of the property.</param>
        /// <returns>The number, or null when the property is missing, empty or not a number.</returns>
        private static double? ReadNumber(JsonElement element, string name)
        {
            string? text = ReadText(element, name);

            if (text != null && double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && !double.IsNaN(number) && !double.IsInfinity(number))
            {
                return number;
            }

            return null;
        }

        /// <summary>
        /// Finds the time zone of Poland under its name on the platform: the IANA name on Android and Linux, the Windows name when the IANA one is not known.
        /// </summary>
        /// <returns>The time zone.</returns>
        private static TimeZoneInfo FindTimeZone()
        {
            foreach (string id in new string[] { "Europe/Warsaw", "Central European Standard Time" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.CreateCustomTimeZone("CET", TimeSpan.FromHours(1), "CET", "CET");
        }
    }
}
