using System.Globalization;
using System.Text;
using AdsbCarMap.DataSources.Management.RateLimiting;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Builds the text of the source status card.
    /// </summary>
    public static class SourceStateText
    {
        /// <summary>
        /// Describes the sources, grouped by what they are used for, one line per source and an extra line for its last failure.
        /// </summary>
        /// <param name="states">States of the sources in the order of preference.</param>
        /// <param name="utcNow">Current time in UTC, used for the age of a failure.</param>
        /// <returns>The text, empty when there are no sources.</returns>
        public static string Format(IReadOnlyList<SourceState> states, DateTime utcNow)
        {
            StringBuilder builder = new StringBuilder();
            string? group = null;

            foreach (SourceState state in states)
            {
                if (!string.Equals(group, state.Group, StringComparison.Ordinal))
                {
                    group = state.Group;

                    if (builder.Length > 0)
                    {
                        builder.Append('\n');
                    }

                    builder.Append(group).Append(":\n");
                }

                builder.Append("• ").Append(state.Name).Append(" — ").Append(DescribeAvailability(state.Status));

                if (state.IsLastUsed)
                {
                    builder.Append(" (ostatnio użyte)");
                }

                builder.Append('\n');

                SourceFailure? failure = state.Status.LastFailure;

                if (failure != null)
                {
                    builder.Append("   ostatni błąd: ").Append(failure.Description).Append(", ").Append(DescribeAge(utcNow - failure.OccurredUtc)).Append('\n');
                }
            }

            return builder.ToString().TrimEnd('\n');
        }

        /// <summary>
        /// Says whether the source can be queried now or how long it is paused.
        /// </summary>
        /// <param name="status">State of the limits.</param>
        /// <returns>The description.</returns>
        public static string DescribeAvailability(RateLimitStatus status)
        {
            if (status.CanQuery)
            {
                return "dostępne";
            }

            return string.Format(CultureInfo.CurrentCulture, "wstrzymane jeszcze {0:0} s", Math.Ceiling(status.RemainingBlock.TotalSeconds));
        }

        /// <summary>
        /// Describes how long ago something happened.
        /// </summary>
        /// <param name="age">Time since the event; a negative value is treated as zero.</param>
        /// <returns>The description, e.g. "5 s temu".</returns>
        public static string DescribeAge(TimeSpan age)
        {
            if (age < TimeSpan.Zero)
            {
                age = TimeSpan.Zero;
            }

            if (age.TotalMinutes < 1.0)
            {
                return string.Format(CultureInfo.CurrentCulture, "{0:0} s temu", Math.Floor(age.TotalSeconds));
            }

            if (age.TotalHours < 1.0)
            {
                return string.Format(CultureInfo.CurrentCulture, "{0:0} min temu", Math.Floor(age.TotalMinutes));
            }

            return string.Format(CultureInfo.CurrentCulture, "{0:0} h temu", Math.Floor(age.TotalHours));
        }
    }
}
