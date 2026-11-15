
namespace AdsbCarMap.Data
{
    /// <summary>
    /// How the text operand of a condition is compared with the value of the aircraft.
    /// </summary>
    public enum FilterMatchMode
    {
        /// <summary>
        /// The value starts with the text.
        /// </summary>
        StartsWith,

        /// <summary>
        /// The value contains the text anywhere.
        /// </summary>
        Contains,

        /// <summary>
        /// The value ends with the text.
        /// </summary>
        EndsWith,

        /// <summary>
        /// The value equals the text.
        /// </summary>
        Exact,
    }
}
