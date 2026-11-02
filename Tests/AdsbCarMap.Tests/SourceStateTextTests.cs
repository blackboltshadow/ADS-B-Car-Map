using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Management.RateLimiting;
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
