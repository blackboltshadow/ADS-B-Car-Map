using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.RateLimiting;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the text of the source status card.
    /// </summary>
    public class SourceStateTextTests
    {
        /// <summary>
        /// Moment used as "now" in the tests.
        /// </summary>
        private static readonly DateTime Now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// A source that can be queried is described as available and marked when it was used last.
        /// </summary>
        [Fact]
        public void Format_AvailableLastUsed()
        {
            List<SourceState> states = new List<SourceState> { new SourceState("Samoloty", "adsb.lol", RateLimitStatus.None, true) };

            Assert.Equal("Samoloty:\n• adsb.lol — dostępne (ostatnio użyte)", SourceStateText.Format(states, Now));
        }

        /// <summary>
        /// A paused source shows the remaining pause and its last failure with the status code and age.
        /// </summary>
        [Fact]
        public void Format_PausedWithFailure()
        {
            SourceFailure failure = new SourceFailure(Now - TimeSpan.FromSeconds(30), 429, "Przekroczono limit zapytań (429)");
            RateLimitStatus status = new RateLimitStatus(Now + TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(11.2), 2, failure);
            List<SourceState> states = new List<SourceState> { new SourceState("Trasy", "hexdb.io", status, false) };

            string text = SourceStateText.Format(states, Now);

            Assert.Contains("wstrzymane jeszcze 12 s", text);
            Assert.Contains("ostatni błąd: Przekroczono limit zapytań (429), 30 s temu", text);
            Assert.DoesNotContain("ostatnio użyte", text);
        }

        /// <summary>
        /// Sources are grouped under one heading per group.
        /// </summary>
        [Fact]
        public void Format_GroupsHeadings()
        {
            List<SourceState> states = new List<SourceState>
            {
                new SourceState("Samoloty", "a", RateLimitStatus.None, false),
                new SourceState("Samoloty", "b", RateLimitStatus.None, false),
                new SourceState("Trasy", "c", RateLimitStatus.None, false),
            };

            string text = SourceStateText.Format(states, Now);

            Assert.Equal("Samoloty:\n• a — dostępne\n• b — dostępne\n\nTrasy:\n• c — dostępne", text);
        }

        /// <summary>
        /// A working threat feed is described with its state, the way of obtaining the data and the age of the last update.
        /// </summary>
        [Fact]
        public void FormatThreatFeed_Streaming()
        {
            string text = SourceStateText.FormatThreatFeed(true, ThreatFeedState.Streaming, 0, 12, Now.AddSeconds(-5), null, Now);

            Assert.Equal("Zagrożenia:\n• NEPTUN — połączone (WebSocket), zagrożeń: 12\n   tryb: WebSocket z zapasowym pobieraniem\n   ostatnia aktualizacja: 5 s temu", text);
        }

        /// <summary>
        /// The last failure is shown with its age, also when the layer is switched off or the feed is stopped.
        /// </summary>
        [Fact]
        public void FormatThreatFeed_ShowsLastFailure()
        {
            ThreatFeedFailure failure = new ThreatFeedFailure("Połączenie nie powiodło się: brak sieci", Now.AddMinutes(-3));

            Assert.EndsWith("ostatni błąd: Połączenie nie powiodło się: brak sieci, 3 min temu", SourceStateText.FormatThreatFeed(true, ThreatFeedState.Offline, 1, 0, null, failure, Now));
            Assert.Contains("warstwa wyłączona", SourceStateText.FormatThreatFeed(false, ThreatFeedState.Stopped, 2, 0, null, failure, Now));
        }

        /// <summary>
        /// Every state of the feed and every way of obtaining the data has its own description.
        /// </summary>
        [Fact]
        public void DescribeThreatFeed_EveryStateAndTransport()
        {
            List<string> states = new List<string>();

            foreach (ThreatFeedState state in Enum.GetValues<ThreatFeedState>())
            {
                states.Add(SourceStateText.DescribeThreatFeed(true, state, 3));
            }

            Assert.Equal(states.Count, states.Distinct().Count());
            Assert.Equal("wstrzymane, mapa jest w tle", SourceStateText.DescribeThreatFeed(true, ThreatFeedState.Stopped, 0));
            Assert.Equal("pobieranie co 5 s (REST), zagrożeń: 3", SourceStateText.DescribeThreatFeed(true, ThreatFeedState.Polling, 3));
            Assert.Equal("tylko WebSocket", SourceStateText.DescribeThreatTransport(1));
            Assert.Equal("tylko pobieranie (REST)", SourceStateText.DescribeThreatTransport(2));
            Assert.Equal("WebSocket z zapasowym pobieraniem", SourceStateText.DescribeThreatTransport(0));
        }

        /// <summary>
        /// The age is given in seconds, minutes or hours and never negative.
        /// </summary>
        [Theory]
        [InlineData(-5, "0 s temu")]
        [InlineData(59, "59 s temu")]
        [InlineData(120, "2 min temu")]
        [InlineData(7300, "2 h temu")]
        public void DescribeAge_Units(int seconds, string expected)
        {
            Assert.Equal(expected, SourceStateText.DescribeAge(TimeSpan.FromSeconds(seconds)));
        }
    }
}
