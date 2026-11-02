namespace AdsbCarMap.DataSources.Interfaces
{
    /// <summary>
    /// Common part of every data source client.
    /// </summary>
    public interface IDataSource
    {
        /// <summary>
        /// Display name of the source, e.g. "adsb.lol".
        /// </summary>
        string Name { get; }
    }
}
