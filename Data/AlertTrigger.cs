namespace AdsbCarMap.Data
{
    /// <summary>
    /// Event that caused an alert to fire.
    /// </summary>
    public enum AlertTrigger
    {
        /// <summary>
        /// The aircraft was seen outside the area and is now inside it.
        /// </summary>
        Entered,

        /// <summary>
        /// The aircraft showed up in the data already satisfying the alert, for example after taking off inside the area.
        /// </summary>
        Appeared,

        /// <summary>
        /// The aircraft was inside the area and is now outside it.
        /// </summary>
        Exited,
    }
}
