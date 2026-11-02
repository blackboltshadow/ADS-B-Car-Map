namespace AdsbCarMap.DataSources.Management.Caching
{
    /// <summary>
    /// Memory of flight information that can be told that a flight is still visible or has ended, without knowing what kind of information it holds.
    /// </summary>
    public interface IExpiringCache
    {
        /// <summary>
        /// Marks an item as seen now, so that it expires later, and cancels its closing.
        /// </summary>
        /// <param name="key">Key of the item.</param>
        /// <returns>True when a valid item was stored.</returns>
        bool Touch(string key);

        /// <summary>
        /// Closes an item, e.g. when its flight has landed: it expires after the given time even if the time chosen by the user is longer. An item that is already closed keeps its earlier end.
        /// </summary>
        /// <param name="key">Key of the item.</param>
        /// <param name="grace">Time after which the item expires.</param>
        /// <returns>True when a valid item was stored.</returns>
        bool Close(string key, TimeSpan grace);
    }
}
