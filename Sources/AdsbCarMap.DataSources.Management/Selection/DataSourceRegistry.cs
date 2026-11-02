using AdsbCarMap.DataSources.Management.RateLimiting;

namespace AdsbCarMap.DataSources.Management.Selection
{
    /// <summary>
    /// Ordered list of data sources of one kind, answering whether any source, or another source than the current one, can be queried now.
    /// </summary>
    /// <typeparam name="TSource">Kind of the sources, e.g. IAircraftSource.</typeparam>
    public sealed class DataSourceRegistry<TSource> where TSource : class, IRateLimitedSource
    {
        /// <summary>
        /// Sources in the order of preference, the first one is the preferred one.
        /// </summary>
        private readonly List<TSource> sources;

        /// <summary>
        /// Creates a registry.
        /// </summary>
        /// <param name="sources">Sources in the order of preference.</param>
        /// <exception cref="ArgumentException">The list is empty.</exception>
        public DataSourceRegistry(IEnumerable<TSource> sources)
        {
            this.sources = new List<TSource>(sources);

            if (this.sources.Count == 0)
            {
                throw new ArgumentException("At least one data source is required.", nameof(sources));
            }
        }

        /// <summary>
        /// Sources in the order of preference.
        /// </summary>
        public IReadOnlyList<TSource> Sources => sources;

        /// <summary>
        /// Returns the sources that can be queried now, in the order of preference.
        /// </summary>
        /// <returns>The available sources, possibly none.</returns>
        public IReadOnlyList<TSource> GetAvailable()
        {
            List<TSource> result = new List<TSource>();

            foreach (TSource source in sources)
            {
                if (source.GetRateLimitStatus().CanQuery)
                {
                    result.Add(source);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the most preferred source that can be queried now, optionally skipping one source.
        /// </summary>
        /// <param name="exclude">Source to skip, usually the one that has just failed, or null.</param>
        /// <returns>The source or null when none can be queried now.</returns>
        public TSource? GetFirstAvailable(TSource? exclude = null)
        {
            foreach (TSource source in sources)
            {
                if (!ReferenceEquals(source, exclude) && source.GetRateLimitStatus().CanQuery)
                {
                    return source;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks whether a source other than the given one can be queried now.
        /// </summary>
        /// <param name="current">The source currently in use.</param>
        /// <returns>True when an alternative is available.</returns>
        public bool HasAlternative(TSource current)
        {
            return GetFirstAvailable(current) != null;
        }

        /// <summary>
        /// Returns how long to wait until at least one source can be queried.
        /// </summary>
        /// <returns>Zero when a source can be queried now, otherwise the shortest remaining pause.</returns>
        public TimeSpan GetTimeToAnyAvailable()
        {
            TimeSpan shortest = TimeSpan.MaxValue;

            foreach (TSource source in sources)
            {
                TimeSpan remaining = source.GetRateLimitStatus().RemainingBlock;

                if (remaining < shortest)
                {
                    shortest = remaining;
                }
            }

            return shortest;
        }
    }
}
