using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the grouping of the notifications, the mute list, the history of the alerts and the importance of a rule.
    /// </summary>
    public class AlertNotificationTests
    {
        /// <summary>
        /// Creates a rule with the given identifier.
        /// </summary>
        /// <param name="id">Identifier of the rule.</param>
        /// <param name="importance">Importance of the rule.</param>
        /// <returns>The rule.</returns>
        private static AlertRule CreateRule(string id, AlertImportance importance = AlertImportance.High)
        {
            return new AlertRule(id, "Rule " + id, true, new AlertArea(AlertAreaKind.Circle, 50.0, new GeoPoint(52.0, 21.0)), null, true, true, 30, null, null, importance);
        }

        /// <summary>
        /// Creates a match of a rule with an aircraft.
        /// </summary>
        /// <param name="rule">Rule that fired.</param>
        /// <param name="hex">ICAO address.</param>
        /// <param name="callsign">Callsign or null.</param>
        /// <returns>The match.</returns>
        private static AlertMatch CreateMatch(AlertRule rule, string hex, string? callsign)
        {
            Aircraft aircraft = new Aircraft(hex, callsign, 52.1, 21.0, 30000, false, 400.0, 90.0, 0.0, "B738", null, "A3", false);

            return new AlertMatch(rule, aircraft, AlertTrigger.Appeared, 10.0);
        }

        /// <summary>
        /// Matches of one rule below the threshold stay single notifications.
        /// </summary>
        [Fact]
        public void Group_BelowThreshold_KeepsSingleNotices()
        {
            AlertRule rule = CreateRule("a");
            List<AlertMatch> matches = new List<AlertMatch> { CreateMatch(rule, "A1", "LOT1"), CreateMatch(rule, "A2", "LOT2") };

            List<AlertNotice> notices = AlertGrouper.Group(matches, 3);

            Assert.Equal(2, notices.Count);
            Assert.All(notices, (AlertNotice notice) => Assert.False(notice.IsGroup));
        }

        /// <summary>
        /// Matches of one rule from the threshold up are reported together, and other rules are not mixed in.
        /// </summary>
        [Fact]
        public void Group_FromThreshold_JoinsMatchesOfTheSameRule()
        {
            AlertRule first = CreateRule("a");
            AlertRule second = CreateRule("b");
            List<AlertMatch> matches = new List<AlertMatch>
            {
                CreateMatch(first, "A1", "LOT1"),
                CreateMatch(second, "B1", "RYR1"),
                CreateMatch(first, "A2", "LOT2"),
                CreateMatch(first, "A3", null),
            };

            List<AlertNotice> notices = AlertGrouper.Group(matches, 3);

            Assert.Equal(2, notices.Count);
            Assert.True(notices[0].IsGroup);
            Assert.Equal(3, notices[0].Matches.Count);
            Assert.Same(first, notices[0].Rule);
            Assert.False(notices[1].IsGroup);
            Assert.Same(second, notices[1].Rule);
        }

        /// <summary>
        /// A threshold below 2 never groups.
        /// </summary>
        [Fact]
        public void Group_ThresholdBelowTwo_NeverGroups()
        {
            AlertRule rule = CreateRule("a");
            List<AlertMatch> matches = new List<AlertMatch> { CreateMatch(rule, "A1", null), CreateMatch(rule, "A2", null), CreateMatch(rule, "A3", null) };

            Assert.Equal(3, AlertGrouper.Group(matches, 1).Count);
            Assert.Empty(AlertGrouper.Group(new List<AlertMatch>(), 3));
        }

        /// <summary>
        /// The text of a group names a few aircraft and counts the rest.
        /// </summary>
        [Fact]
        public void GroupText_NamesAircraftAndCountsTheRest()
        {
            AlertRule rule = CreateRule("a");
            List<AlertMatch> matches = new List<AlertMatch>();

            for (int i = 0; i < 6; i++)
            {
                matches.Add(CreateMatch(rule, "A" + i, i == 1 ? null : "LOT" + i));
            }

            AlertNotice notice = AlertGrouper.Group(matches, 3)[0];

            Assert.Equal("Rule a – samolotów: 6", AlertText.GroupTitle(notice));
            Assert.Equal("LOT0, A1, LOT2, LOT3, jeszcze 2", AlertText.GroupBody(notice));
            Assert.Equal("LOT0, A1", AlertText.GroupBody(new AlertNotice(rule, matches.Take(2).ToList(), true)));
        }

        /// <summary>
        /// A rule is silenced until the given time and audible again after it.
        /// </summary>
        [Fact]
        public void MuteList_MutesUntilTheEndTime()
        {
            DateTime now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
            AlertMuteList list = new AlertMuteList();

            list.Mute("a", now.AddHours(1));

            Assert.True(list.IsMuted("a", now));
            Assert.True(list.IsMuted("a", now.AddMinutes(59)));
            Assert.False(list.IsMuted("a", now.AddHours(1)));
            Assert.False(list.IsMuted("b", now));
            Assert.Equal(now.AddHours(1), list.GetMutedUntil("a", now));
            Assert.Null(list.GetMutedUntil("a", now.AddHours(2)));

            list.Unmute("a");

            Assert.False(list.IsMuted("a", now));
        }

        /// <summary>
        /// Ended silences are forgotten and the list survives saving as text.
        /// </summary>
        [Fact]
        public void MuteList_PruneAndRoundTrip()
        {
            DateTime now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
            AlertMuteList list = new AlertMuteList();
            list.Mute("old", now.AddMinutes(-1));
            list.Mute("new=1", now.AddMinutes(30));

            AlertMuteList parsed = AlertMuteList.Parse(list.Format());

            Assert.True(parsed.IsMuted("new=1", now));
            Assert.True(parsed.Prune(now));
            Assert.False(parsed.Prune(now));
            Assert.Equal("new=1=" + now.AddMinutes(30).Ticks, parsed.Format());
        }

        /// <summary>
        /// Damaged text gives an empty list.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("garbage")]
        [InlineData("=5;a=x;b=-1")]
        public void MuteList_Parse_DamagedText_IsEmpty(string? text)
        {
            Assert.Equal(string.Empty, AlertMuteList.Parse(text).Format());
        }

        /// <summary>
        /// The history keeps the newest entries within its capacity and returns them newest first.
        /// </summary>
        [Fact]
        public void History_KeepsNewestWithinCapacity()
        {
            AlertHistory history = new AlertHistory(3);
            DateTime start = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

            for (int i = 0; i < 5; i++)
            {
                history.Add(new AlertHistoryEntry(start.AddMinutes(i), "Rule", "A" + i, null, AlertTrigger.Appeared, "x"));
            }

            List<AlertHistoryEntry> entries = history.GetNewestFirst();

            Assert.Equal(3, history.Count);
            Assert.Equal(new List<string> { "A4", "A3", "A2" }, entries.Select((AlertHistoryEntry entry) => entry.Hex).ToList());

            history.Clear();

            Assert.Equal(0, history.Count);
        }

        /// <summary>
        /// The history survives a round trip and damaged entries are skipped.
        /// </summary>
        [Fact]
        public void History_RoundTripAndDamagedText()
        {
            AlertHistory history = new AlertHistory();
            DateTime time = new DateTime(2026, 11, 3, 12, 30, 0, DateTimeKind.Utc);
            history.Add(new AlertHistoryEntry(time, "Wojskowe", "ae1234", "RCH123", AlertTrigger.Entered, "RCH123 · C17 · FL350"));
            history.Add(new AlertHistoryEntry(time.AddMinutes(1), "Inne", "3c6444", null, AlertTrigger.Appeared, "3C6444"));

            AlertHistory parsed = AlertHistory.Parse(history.Serialize());
            List<AlertHistoryEntry> entries = parsed.GetNewestFirst();

            Assert.Equal(2, entries.Count);
            Assert.Equal("3c6444", entries[0].Hex);
            Assert.Null(entries[0].Callsign);
            Assert.Equal("Wojskowe", entries[1].RuleName);
            Assert.Equal(time, entries[1].TimeUtc);
            Assert.Equal(AlertTrigger.Entered, entries[1].Trigger);
            Assert.Equal("RCH123 · C17 · FL350", entries[1].Summary);
            Assert.Equal(0, AlertHistory.Parse("not json").Count);
            Assert.Equal(0, AlertHistory.Parse("[{\"time\":1},{\"hex\":\"a\"},5]").Count);
            Assert.Equal(0, AlertHistory.Parse(null).Count);
        }

        /// <summary>
        /// The importance of a rule is saved only when it is not the default, and survives a copy of the rule.
        /// </summary>
        [Fact]
        public void Importance_RoundTripAndCopy()
        {
            List<AlertRule> rules = new List<AlertRule> { CreateRule("loud"), CreateRule("quiet", AlertImportance.Low), CreateRule("normal", AlertImportance.Default) };

            string json = AlertRuleJson.Serialize(rules);
            List<AlertRule> parsed = AlertRuleJson.Parse(json);

            Assert.Equal(AlertImportance.High, parsed[0].Importance);
            Assert.Equal(AlertImportance.Low, parsed[1].Importance);
            Assert.Equal(AlertImportance.Default, parsed[2].Importance);
            Assert.Equal(AlertImportance.Low, parsed[1].WithEnabled(false).Importance);
            Assert.Equal(AlertImportance.Low, parsed[1].WithCondition(null).Importance);
            Assert.DoesNotContain("importance\":\"High", json);
        }
    }
}
