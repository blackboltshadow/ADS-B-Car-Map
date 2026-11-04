using AdsbCarMap.Localization;
using System.Globalization;
using System.Text;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Models;

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
                    builder.Append(Loc.T(" (ostatnio użyte)"));
                }

                builder.Append('\n');

                SourceFailure? failure = state.Status.LastFailure;

                if (failure != null)
                {
                    builder.Append(Loc.T("   ostatni błąd: ")).Append(failure.Description).Append(", ").Append(DescribeAge(utcNow - failure.OccurredUtc)).Append('\n');
                }
            }

            return builder.ToString().TrimEnd('\n');
        }

        /// <summary>
        /// Describes the threat feed in the same form as the other sources: a group with one line for the source, a line with the chosen way of obtaining the data, and lines for the last update and the last failure.
        /// </summary>
        /// <param name="layerOn">Whether the threat layer is switched on in the settings.</param>
        /// <param name="state">State of the feed.</param>
        /// <param name="transportIndex">Index of the chosen way of obtaining the data: 0 stream with the download as a fallback, 1 stream only, 2 download only.</param>
        /// <param name="count">Number of the threats the feed holds.</param>
        /// <param name="lastUpdateUtc">Time of the last data received, or null when nothing was received yet.</param>
        /// <param name="failure">The last failure, or null when there was none.</param>
        /// <param name="utcNow">Current time in UTC, used for the age of the update and of the failure.</param>
        /// <returns>The text of the group, without a trailing new line.</returns>
        public static string FormatThreatFeed(bool layerOn, ThreatFeedState state, int transportIndex, int count, DateTime? lastUpdateUtc, ThreatFeedFailure? failure, DateTime utcNow)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(Loc.T("Zagrożenia")).Append(":\n• NEPTUN — ").Append(DescribeThreatFeed(layerOn, state, count)).Append('\n');
            builder.Append(Loc.T("   tryb: ")).Append(DescribeThreatTransport(transportIndex)).Append('\n');

            if (lastUpdateUtc != null)
            {
                builder.Append(Loc.T("   ostatnia aktualizacja: ")).Append(DescribeAge(utcNow - lastUpdateUtc.Value)).Append('\n');
            }

            if (failure != null)
            {
                builder.Append(Loc.T("   ostatni błąd: ")).Append(failure.Description).Append(", ").Append(DescribeAge(utcNow - failure.OccurredUtc)).Append('\n');
            }

            return builder.ToString().TrimEnd('\n');
        }

        /// <summary>
        /// Describes the airspace feed in the same form as the other sources: a group with one line for the source and lines for the last update and the last failure.
        /// </summary>
        /// <param name="layerOn">Whether the airspace layer is switched on in the settings.</param>
        /// <param name="state">State of the feed.</param>
        /// <param name="count">Number of the areas the feed holds.</param>
        /// <param name="lastUpdateUtc">Time of the last successful download, or null when there was none.</param>
        /// <param name="failure">The last failure, or null when there was none.</param>
        /// <param name="utcNow">Current time in UTC, used for the age of the update and of the failure.</param>
        /// <returns>The text of the group, without a trailing new line.</returns>
        public static string FormatAirspaceFeed(bool layerOn, PollingFeedState state, int count, DateTime? lastUpdateUtc, PollingFeedFailure? failure, DateTime utcNow)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(Loc.T("Strefy przestrzeni powietrznej")).Append(":\n• PAŻP — ").Append(DescribeAirspaceFeed(layerOn, state, count)).Append('\n');

            if (lastUpdateUtc != null)
            {
                builder.Append(Loc.T("   ostatnia aktualizacja: ")).Append(DescribeAge(utcNow - lastUpdateUtc.Value)).Append('\n');
            }

            if (failure != null)
            {
                builder.Append(Loc.T("   ostatni błąd: ")).Append(Loc.T(failure.Description)).Append(", ").Append(DescribeAge(utcNow - failure.OccurredUtc)).Append('\n');
            }

            return builder.ToString().TrimEnd('\n');
        }

        /// <summary>
        /// Describes the state of the airspace feed.
        /// </summary>
        /// <param name="layerOn">Whether the airspace layer is switched on in the settings.</param>
        /// <param name="state">State of the feed.</param>
        /// <param name="count">Number of the areas the feed holds.</param>
        /// <returns>The description.</returns>
        public static string DescribeAirspaceFeed(bool layerOn, PollingFeedState state, int count)
        {
            if (!layerOn)
            {
                return Loc.T("warstwa wyłączona, brak połączenia z serwisem");
            }

            switch (state)
            {
                case PollingFeedState.Loading:
                    return Loc.T("pobieranie…");
                case PollingFeedState.Ready:
                    return Loc.F("działa, stref: {0}", count);
                case PollingFeedState.Offline:
                    return Loc.T("brak połączenia, aplikacja ponawia próby");
                default:
                    return Loc.T("wstrzymane, mapa jest w tle");
            }
        }

        /// <summary>
        /// Describes the state of the threat feed.
        /// </summary>
        /// <param name="layerOn">Whether the threat layer is switched on in the settings.</param>
        /// <param name="state">State of the feed.</param>
        /// <param name="count">Number of the threats the feed holds.</param>
        /// <returns>The description.</returns>
        public static string DescribeThreatFeed(bool layerOn, ThreatFeedState state, int count)
        {
            if (!layerOn)
            {
                return Loc.T("warstwa wyłączona, brak połączenia z serwisem");
            }

            switch (state)
            {
                case ThreatFeedState.Connecting:
                    return Loc.T("łączenie…");
                case ThreatFeedState.Streaming:
                    return Loc.F("połączone (WebSocket), zagrożeń: {0}", count);
                case ThreatFeedState.Polling:
                    return Loc.F("pobieranie co 5 s (REST), zagrożeń: {0}", count);
                case ThreatFeedState.Offline:
                    return Loc.T("brak połączenia, aplikacja ponawia próby");
                default:
                    return Loc.T("wstrzymane, mapa jest w tle");
            }
        }

        /// <summary>
        /// Names the chosen way of obtaining the threats.
        /// </summary>
        /// <param name="transportIndex">Index of the way: 0 stream with the download as a fallback, 1 stream only, 2 download only.</param>
        /// <returns>The name.</returns>
        public static string DescribeThreatTransport(int transportIndex)
        {
            switch (transportIndex)
            {
                case 1:
                    return Loc.T("tylko WebSocket");
                case 2:
                    return Loc.T("tylko pobieranie (REST)");
                default:
                    return Loc.T("WebSocket z zapasowym pobieraniem");
            }
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
                return Loc.T("dostępne");
            }

            return Loc.F("wstrzymane jeszcze {0:0} s", Math.Ceiling(status.RemainingBlock.TotalSeconds));
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
                return Loc.F("{0:0} s temu", Math.Floor(age.TotalSeconds));
            }

            if (age.TotalHours < 1.0)
            {
                return Loc.F("{0:0} min temu", Math.Floor(age.TotalMinutes));
            }

            return Loc.F("{0:0} h temu", Math.Floor(age.TotalHours));
        }
    }
}
