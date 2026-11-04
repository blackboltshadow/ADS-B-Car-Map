using Android.App.Backup;
using Android.OS;
using Android.Runtime;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Agent of the system backup (Auto Backup). Just before the system takes the data it writes the backup file from the current data and the choice of the user, and the rules of the backup let the system take only that file. After the system restores the file, on a new phone or after the application was installed again, it writes the data back.
    /// </summary>
    [Register("com.blackboltshadow.adsbcarmap.AppBackupAgent")]
    public class AppBackupAgent : BackupAgent
    {
        /// <summary>
        /// Tag used in logs.
        /// </summary>
        private const string LogTag = "AppBackupAgent";

        /// <summary>
        /// Does nothing, because the application uses only the backup of files, not the key-value backup.
        /// </summary>
        /// <param name="oldState">Not used.</param>
        /// <param name="data">Not used.</param>
        /// <param name="newState">Not used.</param>
        public override void OnBackup(ParcelFileDescriptor? oldState, BackupDataOutput? data, ParcelFileDescriptor? newState)
        {
        }

        /// <summary>
        /// Does nothing, because the application uses only the backup of files, not the key-value backup.
        /// </summary>
        /// <param name="data">Not used.</param>
        /// <param name="appVersionCode">Not used.</param>
        /// <param name="newState">Not used.</param>
        public override void OnRestore(BackupDataInput? data, int appVersionCode, ParcelFileDescriptor? newState)
        {
        }

        /// <summary>
        /// Prepares the backup file and lets the system take it.
        /// </summary>
        /// <param name="data">Output that the system provides.</param>
        public override void OnFullBackup(FullBackupDataOutput? data)
        {
            long bytes = 0L;

            try
            {
                bytes = BackupService.StageForSystem(this);
            }
            catch (Exception exception)
            {
                DevLogger.Error(LogTag, "Nie udało się przygotować kopii zapasowej", exception);
            }

            base.OnFullBackup(data);
            BackupConfigStore.RecordSystemRead(bytes);
        }

        /// <summary>
        /// Writes the restored data back into the preferences and the files of the application.
        /// </summary>
        public override void OnRestoreFinished()
        {
            try
            {
                BackupService.RestoreFromStaged(this);
            }
            catch (Exception exception)
            {
                DevLogger.Error(LogTag, "Nie udało się przywrócić danych z kopii zapasowej", exception);
            }

            base.OnRestoreFinished();
        }

        /// <summary>
        /// Reports that the backup is too large for the quota of the system.
        /// </summary>
        /// <param name="backupDataBytes">Size of the data in bytes.</param>
        /// <param name="quotaBytes">Quota in bytes.</param>
        public override void OnQuotaExceeded(long backupDataBytes, long quotaBytes)
        {
            DevLogger.Warn(LogTag, "Kopia zapasowa przekracza limit systemu: " + backupDataBytes + " B z " + quotaBytes + " B");
            base.OnQuotaExceeded(backupDataBytes, quotaBytes);
        }
    }
}
