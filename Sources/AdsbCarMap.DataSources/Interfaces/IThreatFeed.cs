using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Interfaces
{
    /// <summary>
    /// What a threat feed is doing now.
    /// </summary>
    public enum ThreatFeedState
    {
        /// <summary>
        /// The feed is switched off and holds no data.
        /// </summary>
        Stopped,

        /// <summary>
        /// The feed is connecting and has no data yet.
        /// </summary>
        Connecting,

        /// <summary>
        /// The data arrives over a permanent connection.
        /// </summary>
        Streaming,

        /// <summary>
        /// The data is downloaded periodically.
        /// </summary>
        Polling,

        /// <summary>
        /// The last attempt failed and the feed waits before it tries again.
        /// </summary>
        Offline,
    }

    /// <summary>
    /// Source of the current airborne threats. Unlike an aircraft source it is not asked for an area: it keeps the whole list up to date while it is started.
    /// </summary>
    public interface IThreatFeed : IDataSource
    {
        /// <summary>
        /// Event raised, on any thread, after the list of the threats or the state of the feed changed.
        /// </summary>
        event Action? Changed;

        /// <summary>
        /// Current state of the feed.
        /// </summary>
        ThreatFeedState State { get; }

        /// <summary>
        /// The current threats. A new list replaces the old one at every change, so the list can be read without a lock.
        /// </summary>
        IReadOnlyList<Threat> Threats { get; }

        /// <summary>
        /// Time of the last data received from the source, or null when nothing was received yet. It is kept after the feed stopped.
        /// </summary>
        DateTime? LastUpdateUtc { get; }

        /// <summary>
        /// The last failure, or null when there was none. It is kept after the feed stopped.
        /// </summary>
        ThreatFeedFailure? LastFailure { get; }

        /// <summary>
        /// Starts keeping the list up to date. Does nothing when the feed is already started.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the feed, closes the connection and forgets the data. Does nothing when the feed is stopped.
        /// </summary>
        void Stop();
    }
}
