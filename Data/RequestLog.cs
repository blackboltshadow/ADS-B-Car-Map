namespace AdsbCarMap.Data
{
    /// <summary>
    /// Bounded record of the requests sent to external servers, newest first, kept in memory only.
    /// </summary>
    public sealed class RequestLog
    {
        /// <summary>
        /// Default number of requests kept.
        /// </summary>
        public const int DefaultCapacity = 500;

        /// <summary>
        /// Lock protecting the entries.
        /// </summary>
        private readonly object entriesLock = new object();

        /// <summary>
        /// Entries, the newest first.
        /// </summary>
        private readonly List<RequestLogEntry> entries = new List<RequestLogEntry>();

        /// <summary>
        /// Number of requests kept; older ones are dropped.
        /// </summary>
        private readonly int capacity;

        /// <summary>
        /// Creates a log with the default capacity.
        /// </summary>
        public RequestLog()
            : this(DefaultCapacity)
        {
        }

        /// <summary>
        /// Creates a log.
        /// </summary>
        /// <param name="capacity">Number of requests kept, at least 1.</param>
        public RequestLog(int capacity)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
            this.capacity = capacity;
        }

        /// <summary>
        /// Raised, on any thread, after the log changed.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Adds a request, dropping the oldest one when the log is full.
        /// </summary>
        /// <param name="entry">The request.</param>
        public void Add(RequestLogEntry entry)
        {
            lock (entriesLock)
            {
                entries.Insert(0, entry);

                if (entries.Count > capacity)
                {
                    entries.RemoveRange(capacity, entries.Count - capacity);
                }
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Removes every request.
        /// </summary>
        public void Clear()
        {
            lock (entriesLock)
            {
                entries.Clear();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Returns a copy of the requests.
        /// </summary>
        /// <returns>The requests, the newest first.</returns>
        public List<RequestLogEntry> GetEntries()
        {
            lock (entriesLock)
            {
                return new List<RequestLogEntry>(entries);
            }
        }

        /// <summary>
        /// Returns the hosts that appear in the log.
        /// </summary>
        /// <returns>The distinct hosts in alphabetical order.</returns>
        public List<string> GetHosts()
        {
            lock (entriesLock)
            {
                List<string> hosts = new List<string>();

                foreach (RequestLogEntry entry in entries)
                {
                    if (!hosts.Contains(entry.Host))
                    {
                        hosts.Add(entry.Host);
                    }
                }

                hosts.Sort(StringComparer.OrdinalIgnoreCase);

                return hosts;
            }
        }

        /// <summary>
        /// Selects the requests that match a filter.
        /// </summary>
        /// <param name="source">Requests to filter.</param>
        /// <param name="onlyErrors">Whether to keep only the failed requests.</param>
        /// <param name="host">Host to keep, or null for every host.</param>
        /// <returns>The matching requests in the original order.</returns>
        public static List<RequestLogEntry> Filter(IEnumerable<RequestLogEntry> source, bool onlyErrors, string? host)
        {
            List<RequestLogEntry> result = new List<RequestLogEntry>();

            foreach (RequestLogEntry entry in source)
            {
                if ((!onlyErrors || entry.IsError) && (host == null || string.Equals(entry.Host, host, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(entry);
                }
            }

            return result;
        }
    }
}
