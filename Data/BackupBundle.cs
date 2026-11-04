using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// One part of a backup: the saved data of a category as text.
    /// </summary>
    /// <param name="Id">Identifier of the category, see <see cref="BackupCategories"/>.</param>
    /// <param name="Content">Saved data of the category.</param>
    public sealed record BackupSection(string Id, string Content);

    /// <summary>
    /// Result of reading a backup text.
    /// </summary>
    public enum BackupParseStatus
    {
        /// <summary>
        /// The text is a backup that this version can read.
        /// </summary>
        Ok,

        /// <summary>
        /// The text is not a backup, or it is damaged.
        /// </summary>
        Invalid,

        /// <summary>
        /// The backup was made by a newer version of the application with a format that this version cannot read.
        /// </summary>
        NewerFormat
    }

    /// <summary>
    /// The whole backup of the application data: a header and the sections of the chosen categories, with a text format used both for the system backup and for the export to a file.
    /// </summary>
    public sealed class BackupBundle
    {
        /// <summary>
        /// Name of the format written in every backup.
        /// </summary>
        public const string FormatName = "adsbcarmap-backup";

        /// <summary>
        /// Version of the format that this version of the application writes and reads.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>
        /// Creates a backup.
        /// </summary>
        /// <param name="createdUtc">Time of the creation in UTC.</param>
        /// <param name="appVersion">Version of the application that made the backup.</param>
        /// <param name="sections">Sections of the backup.</param>
        public BackupBundle(DateTime createdUtc, string appVersion, IReadOnlyList<BackupSection> sections)
        {
            CreatedUtc = createdUtc;
            AppVersion = appVersion;
            Sections = sections;
        }

        /// <summary>
        /// Time of the creation in UTC.
        /// </summary>
        public DateTime CreatedUtc { get; }

        /// <summary>
        /// Version of the application that made the backup.
        /// </summary>
        public string AppVersion { get; }

        /// <summary>
        /// Sections of the backup.
        /// </summary>
        public IReadOnlyList<BackupSection> Sections { get; }

        /// <summary>
        /// Finds a section.
        /// </summary>
        /// <param name="id">Identifier of the category.</param>
        /// <returns>The section or null when the backup has none for the category.</returns>
        public BackupSection? Find(string id)
        {
            return Sections.FirstOrDefault((BackupSection section) => section.Id == id);
        }

        /// <summary>
        /// Writes the backup as JSON.
        /// </summary>
        /// <returns>The text.</returns>
        public string Serialize()
        {
            using MemoryStream stream = new MemoryStream();
            JsonWriterOptions options = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

            using (Utf8JsonWriter writer = new Utf8JsonWriter(stream, options))
            {
                writer.WriteStartObject();
                writer.WriteString("format", FormatName);
                writer.WriteNumber("version", CurrentVersion);
                writer.WriteNumber("created", CreatedUtc.Ticks);
                writer.WriteString("app", AppVersion);
                writer.WriteStartArray("sections");

                foreach (BackupSection section in Sections)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", section.Id);
                    writer.WriteString("content", section.Content);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Reads a backup written by <see cref="Serialize"/>.
        /// </summary>
        /// <param name="json">Text to read, or null.</param>
        /// <param name="bundle">The backup when the status is <see cref="BackupParseStatus.Ok"/>, otherwise null. Damaged sections are skipped and only the first section of an identifier is kept.</param>
        /// <returns>Whether the text was read.</returns>
        public static BackupParseStatus TryParse(string? json, out BackupBundle? bundle)
        {
            bundle = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                return BackupParseStatus.Invalid;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object || ReadString(root, "format") != FormatName)
                {
                    return BackupParseStatus.Invalid;
                }

                if (!root.TryGetProperty("version", out JsonElement version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number < 1)
                {
                    return BackupParseStatus.Invalid;
                }

                if (number > CurrentVersion)
                {
                    return BackupParseStatus.NewerFormat;
                }

                if (!root.TryGetProperty("created", out JsonElement created) || created.ValueKind != JsonValueKind.Number || !created.TryGetInt64(out long ticks)
                    || ticks <= 0 || ticks > DateTime.MaxValue.Ticks
                    || !root.TryGetProperty("sections", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
                {
                    return BackupParseStatus.Invalid;
                }

                List<BackupSection> sections = new List<BackupSection>();

                foreach (JsonElement element in list.EnumerateArray())
                {
                    string? id = element.ValueKind == JsonValueKind.Object ? ReadString(element, "id") : null;
                    string? content = element.ValueKind == JsonValueKind.Object ? ReadString(element, "content") : null;

                    if (!string.IsNullOrEmpty(id) && content != null && !sections.Any((BackupSection section) => section.Id == id))
                    {
                        sections.Add(new BackupSection(id, content));
                    }
                }

                bundle = new BackupBundle(new DateTime(ticks, DateTimeKind.Utc), ReadString(root, "app") ?? string.Empty, sections);

                return BackupParseStatus.Ok;
            }
            catch (JsonException)
            {
                return BackupParseStatus.Invalid;
            }
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
