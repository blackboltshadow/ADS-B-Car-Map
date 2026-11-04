using AdsbCarMap.Data;
using Android.App;
using Android.Content;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Keeps the choices of the user about the backup and what is known about its state, in a separate SharedPreferences file that is not itself put in the backup, because it describes this phone.
    /// </summary>
    public static class BackupConfigStore
    {
        /// <summary>
        /// Name of the SharedPreferences file.
        /// </summary>
        private const string PreferencesName = "adsb_car_map_backup";

        /// <summary>
        /// Key of the switch of the system backup.
        /// </summary>
        private const string EnabledKey = "enabled";

        /// <summary>
        /// Key of the choice of the categories.
        /// </summary>
        private const string SelectionKey = "selection";

        /// <summary>
        /// Key of the time when the backup was last prepared, in ticks.
        /// </summary>
        private const string PreparedKey = "preparedTicks";

        /// <summary>
        /// Key of the size of the backup that was last prepared.
        /// </summary>
        private const string PreparedBytesKey = "preparedBytes";

        /// <summary>
        /// Key of the time when the system was last asked for a backup, in ticks.
        /// </summary>
        private const string RequestedKey = "requestedTicks";

        /// <summary>
        /// Key of the time when the system last took the backup data, in ticks.
        /// </summary>
        private const string SystemReadKey = "systemReadTicks";

        /// <summary>
        /// Key of the size of the data that the system last took.
        /// </summary>
        private const string SystemReadBytesKey = "systemReadBytes";

        /// <summary>
        /// Key of the time when data was last restored from a backup, in ticks.
        /// </summary>
        private const string RestoredKey = "restoredTicks";

        /// <summary>
        /// Key of the time when the backup was last deleted, in ticks.
        /// </summary>
        private const string DeletedKey = "deletedTicks";

        /// <summary>
        /// Key of the fingerprints of the data in the backup file that was last prepared.
        /// </summary>
        private const string PreparedHashesKey = "preparedHashes";

        /// <summary>
        /// Key of the fingerprints of the data that the system last took, so that data changed since can be told.
        /// </summary>
        private const string DeliveredHashesKey = "deliveredHashes";

        /// <summary>
        /// Whether the system backup is on. It is on until the user switches it off or deletes the backup.
        /// </summary>
        public static bool Enabled
        {
            get => Open().GetBoolean(EnabledKey, true);
            set => Open().Edit()?.PutBoolean(EnabledKey, value)?.Commit();
        }

        /// <summary>
        /// Identifiers of the categories that are put in the backup.
        /// </summary>
        public static List<string> Selection
        {
            get => BackupCategories.ParseSelection(Open().GetString(SelectionKey, null));
            set => Open().Edit()?.PutString(SelectionKey, BackupCategories.FormatSelection(value))?.Commit();
        }

        /// <summary>
        /// Time when the backup was last prepared in the files folder, or null.
        /// </summary>
        public static DateTime? PreparedUtc => ReadTime(PreparedKey);

        /// <summary>
        /// Size in bytes of the backup that was last prepared.
        /// </summary>
        public static long PreparedBytes => Open().GetLong(PreparedBytesKey, 0L);

        /// <summary>
        /// Time when the system was last asked for a backup, or null.
        /// </summary>
        public static DateTime? RequestedUtc => ReadTime(RequestedKey);

        /// <summary>
        /// Time when the system last took the backup data, or null when it has not yet.
        /// </summary>
        public static DateTime? SystemReadUtc => ReadTime(SystemReadKey);

        /// <summary>
        /// Size in bytes of the data that the system last took.
        /// </summary>
        public static long SystemReadBytes => Open().GetLong(SystemReadBytesKey, 0L);

        /// <summary>
        /// Time when data was last restored from a backup, or null.
        /// </summary>
        public static DateTime? RestoredUtc => ReadTime(RestoredKey);

        /// <summary>
        /// Time when the backup was last deleted, or null.
        /// </summary>
        public static DateTime? DeletedUtc => ReadTime(DeletedKey);

        /// <summary>
        /// Fingerprints of the data that the system last took, or null when it has not taken any yet.
        /// </summary>
        public static Dictionary<string, string>? DeliveredHashes
        {
            get
            {
                string? text = Open().GetString(DeliveredHashesKey, null);

                return text == null ? null : BackupHashes.Parse(text);
            }
        }

        /// <summary>
        /// Notes that a backup was prepared.
        /// </summary>
        /// <param name="bytes">Size of the backup in bytes.</param>
        /// <param name="hashes">Fingerprints of the data in the backup, written by <see cref="BackupHashes.Format"/>.</param>
        public static void RecordPrepared(long bytes, string hashes)
        {
            Open().Edit()?.PutLong(PreparedKey, DateTime.UtcNow.Ticks)?.PutLong(PreparedBytesKey, bytes)?.PutString(PreparedHashesKey, hashes)?.Commit();
        }

        /// <summary>
        /// Forgets the prepared backup, which is done when no file is kept, so that the system takes nothing.
        /// </summary>
        public static void ClearPrepared()
        {
            Open().Edit()?.Remove(PreparedHashesKey)?.Commit();
        }

        /// <summary>
        /// Notes that data was restored from a backup, so that the backup in the account holds exactly this data.
        /// </summary>
        /// <param name="hashes">Fingerprints of the restored data, written by <see cref="BackupHashes.Format"/>.</param>
        public static void RecordDelivered(string hashes)
        {
            Open().Edit()?.PutString(DeliveredHashesKey, hashes)?.Commit();
        }

        /// <summary>
        /// Notes that the system was asked for a backup.
        /// </summary>
        public static void RecordRequested()
        {
            Open().Edit()?.PutLong(RequestedKey, DateTime.UtcNow.Ticks)?.Commit();
        }

        /// <summary>
        /// Notes that the system took the backup data.
        /// </summary>
        /// <param name="bytes">Size of the data in bytes.</param>
        public static void RecordSystemRead(long bytes)
        {
            ISharedPreferences preferences = Open();

            preferences.Edit()?.PutLong(SystemReadKey, DateTime.UtcNow.Ticks)?.PutLong(SystemReadBytesKey, bytes)?.PutString(DeliveredHashesKey, preferences.GetString(PreparedHashesKey, string.Empty))?.Commit();
        }

        /// <summary>
        /// Notes that data was restored from a backup.
        /// </summary>
        public static void RecordRestored()
        {
            Open().Edit()?.PutLong(RestoredKey, DateTime.UtcNow.Ticks)?.Commit();
        }

        /// <summary>
        /// Notes that the backup was deleted and forgets the state of the earlier one.
        /// </summary>
        public static void RecordDeleted()
        {
            Open().Edit()?.PutLong(DeletedKey, DateTime.UtcNow.Ticks)?.Remove(PreparedKey)?.Remove(PreparedBytesKey)?.Remove(PreparedHashesKey)?.Remove(DeliveredHashesKey)?.Remove(SystemReadKey)?.Remove(SystemReadBytesKey)?.Commit();
        }

        /// <summary>
        /// Reads a time saved in ticks.
        /// </summary>
        /// <param name="key">Key of the value.</param>
        /// <returns>The time in UTC, or null when it is not saved.</returns>
        private static DateTime? ReadTime(string key)
        {
            long ticks = Open().GetLong(key, 0L);

            return ticks > 0 && ticks <= DateTime.MaxValue.Ticks ? new DateTime(ticks, DateTimeKind.Utc) : null;
        }

        /// <summary>
        /// Opens the SharedPreferences file.
        /// </summary>
        /// <returns>The preferences.</returns>
        private static ISharedPreferences Open()
        {
            return Application.Context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)!;
        }
    }
}
