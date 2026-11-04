using System.Security.Cryptography;
using System.Text;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// State of the backup compared with the current data: whether the system has taken the newest version of the data, and which categories changed since.
    /// </summary>
    /// <param name="Active">Whether a backup is made at all: it is switched on and at least one category is chosen.</param>
    /// <param name="EverDelivered">Whether the system has already taken a backup.</param>
    /// <param name="PendingIds">Identifiers of the chosen categories whose newest data the system has not taken yet, and of the categories that the system has but are not chosen any more.</param>
    public sealed record BackupSyncState(bool Active, bool EverDelivered, IReadOnlyList<string> PendingIds)
    {
        /// <summary>
        /// Whether the backup that the system holds is the newest version of all the chosen data.
        /// </summary>
        public bool IsUpToDate => Active && PendingIds.Count == 0;
    }

    /// <summary>
    /// Short fingerprints of the data of the backup categories, used to tell which data the system backup already contains.
    /// </summary>
    public static class BackupHashes
    {
        /// <summary>
        /// Number of bytes of the hash that are kept, which gives 16 hexadecimal digits.
        /// </summary>
        private const int HashBytes = 8;

        /// <summary>
        /// Calculates the fingerprint of the data of a category.
        /// </summary>
        /// <param name="content">Data of the category as text.</param>
        /// <returns>16 lower-case hexadecimal digits.</returns>
        public static string Hash(string content)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)), 0, HashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Calculates the fingerprints of the sections of a backup.
        /// </summary>
        /// <param name="sections">Sections.</param>
        /// <returns>Fingerprints by identifier of the category.</returns>
        public static Dictionary<string, string> OfSections(IEnumerable<BackupSection> sections)
        {
            Dictionary<string, string> hashes = new Dictionary<string, string>();

            foreach (BackupSection section in sections)
            {
                hashes[section.Id] = Hash(section.Content);
            }

            return hashes;
        }

        /// <summary>
        /// Writes fingerprints as text.
        /// </summary>
        /// <param name="hashes">Fingerprints by identifier of the category; unknown categories are dropped.</param>
        /// <returns>Pairs "id=hash" in the order of the catalog, separated with commas.</returns>
        public static string Format(IReadOnlyDictionary<string, string> hashes)
        {
            return string.Join(',', BackupCategories.All.Where((BackupCategory category) => hashes.ContainsKey(category.Id)).Select((BackupCategory category) => category.Id + "=" + hashes[category.Id]));
        }

        /// <summary>
        /// Reads fingerprints written by <see cref="Format"/>.
        /// </summary>
        /// <param name="text">Saved text, or null.</param>
        /// <returns>Fingerprints by identifier of the category; damaged pairs and unknown categories are skipped.</returns>
        public static Dictionary<string, string> Parse(string? text)
        {
            Dictionary<string, string> hashes = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(text))
            {
                return hashes;
            }

            foreach (string pair in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int separator = pair.IndexOf('=');

                if (separator <= 0 || separator == pair.Length - 1)
                {
                    continue;
                }

                string id = pair.Substring(0, separator);

                if (BackupCategories.Find(id) != null)
                {
                    hashes[id] = pair.Substring(separator + 1);
                }
            }

            return hashes;
        }

        /// <summary>
        /// Finds the categories whose newest data is not in the backup that the system holds.
        /// </summary>
        /// <param name="current">Fingerprints of the data that would be backed up now.</param>
        /// <param name="delivered">Fingerprints of the data that the system took last time, or null when it has not taken any yet.</param>
        /// <returns>Identifiers in the order of the catalog: categories that are new or changed since, and categories that the system holds but are not chosen any more.</returns>
        public static List<string> FindPending(IReadOnlyDictionary<string, string> current, IReadOnlyDictionary<string, string>? delivered)
        {
            return BackupCategories.All
                .Where((BackupCategory category) =>
                {
                    bool inCurrent = current.TryGetValue(category.Id, out string? now);
                    bool inDelivered = delivered != null && delivered.TryGetValue(category.Id, out string? before);

                    if (inCurrent && !inDelivered)
                    {
                        return true;
                    }

                    if (!inCurrent)
                    {
                        return inDelivered;
                    }

                    return delivered![category.Id] != now;
                })
                .Select((BackupCategory category) => category.Id)
                .ToList();
        }
    }
}
