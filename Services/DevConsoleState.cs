using AdsbCarMap.DataSources.Management.Privacy;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Remembers the last area sent to the aircraft sources, so that the developer console can show what really leaves the device.
    /// </summary>
    public static class DevConsoleState
    {
        /// <summary>
        /// Guards the remembered values, which are written by the download thread and read by the interface thread.
        /// </summary>
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// Area of the last query, or null when no query has been sent yet.
        /// </summary>
        private static QueryArea? lastSent;

        /// <summary>
        /// Moment of the last query, in UTC.
        /// </summary>
        private static DateTime? lastSentAtUtc;

        /// <summary>
        /// Remembers the area of a query that is about to be sent.
        /// </summary>
        /// <param name="area">Area after the rounding.</param>
        public static void RecordSent(QueryArea area)
        {
            lock (SyncRoot)
            {
                lastSent = area;
                lastSentAtUtc = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Reads the last query.
        /// </summary>
        /// <returns>The area and the moment of the last query, both null when no query has been sent yet.</returns>
        public static (QueryArea? Area, DateTime? AtUtc) GetLastSent()
        {
            lock (SyncRoot)
            {
                return (lastSent, lastSentAtUtc);
            }
        }
    }
}
