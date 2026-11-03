namespace AdsbCarMap.DataSources.Neptun
{
    /// <summary>
    /// Permanent connection that delivers the frames of the threat stream as text. The interface lets the feed be tested without a network.
    /// </summary>
    public interface IThreatConnection : IDisposable
    {
        /// <summary>
        /// Opens the connection.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task finished when the connection is open.</returns>
        Task ConnectAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Waits for the next frame.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The text of the frame, or null when the other side closed the connection.</returns>
        Task<string?> ReceiveAsync(CancellationToken cancellationToken);
    }
}
