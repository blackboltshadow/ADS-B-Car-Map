namespace AdsbCarMap.Data
{
    /// <summary>
    /// How loudly the notifications of an alert are shown.
    /// </summary>
    public enum AlertImportance
    {
        /// <summary>
        /// Sound and a heads-up banner.
        /// </summary>
        High,

        /// <summary>
        /// Sound without a banner.
        /// </summary>
        Default,

        /// <summary>
        /// No sound and no banner, the notification only appears in the list.
        /// </summary>
        Low,
    }
}
