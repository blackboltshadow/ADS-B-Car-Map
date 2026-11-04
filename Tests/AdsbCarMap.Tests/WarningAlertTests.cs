using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the notifications about new official notices: the options, the engine that decides what to report and the texts.
    /// </summary>
    public class WarningAlertTests
    {
        /// <summary>
        /// The moment used by the tests.
        /// </summary>
        private static readonly DateTime Now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Options with the notifications on.
        /// </summary>
        private static readonly WarningAlertOptions On = new WarningAlertOptions(true, false);

        /// <summary>
        /// Creates a notice.
        /// </summary>
        /// <param name="id">Identifier.</param>
        /// <param name="category">Category.</param>
        /// <param name="alarm">Whether it is an alarm.</param>
        /// <param name="validToHours">End of the validity in hours from now, or null for none.</param>
        /// <param name="updatedHours">Time of the change in hours from now.</param>
        /// <param name="provinces">Slugs of the voivodeships.</param>
        /// <returns>The notice.</returns>
        private static Warning Create(string id, string category = "ogolne", bool alarm = false, double? validToHours = 24, double updatedHours = -1, params string[] provinces)
        {
            return new Warning(id, category, "Tytuł " + id, "Skrót " + id, "Treść " + id, alarm, Now.AddHours(-48), validToHours == null ? null : Now.AddHours(validToHours.Value), Now.AddHours(updatedHours), provinces.Select((string slug) => new WarningProvince(slug, slug.ToUpperInvariant())).ToList(), string.Empty, string.Empty, null, null, null);
        }

        /// <summary>
        /// The options are written and read back, and damaged text gives the defaults.
        /// </summary>
        [Fact]
        public void Options_RoundTripAndDefaults()
        {
            WarningAlertOptions options = new WarningAlertOptions(true, true);

            Assert.Equal("v1;1;1", options.Serialize());
            Assert.Equal(options, WarningAlertOptions.Parse(options.Serialize()));
            Assert.Equal(WarningAlertOptions.Default, WarningAlertOptions.Parse(null));
            Assert.Equal(WarningAlertOptions.Default, WarningAlertOptions.Parse("  "));
            Assert.Equal(WarningAlertOptions.Default, WarningAlertOptions.Parse("v2;1;1"));
            Assert.Equal(WarningAlertOptions.Default, WarningAlertOptions.Parse("v1;2;0"));
            Assert.Equal(WarningAlertOptions.Default, WarningAlertOptions.Parse("v1;1"));
            Assert.False(WarningAlertOptions.Default.Enabled);
        }

        /// <summary>
        /// A new notice of a shown category is reported once.
        /// </summary>
        [Fact]
        public void Evaluate_ReportsNewNoticeOnce()
        {
            WarningAlertEngine engine = new WarningAlertEngine(null);
            List<Warning> warnings = new List<Warning> { Create("1") };

            IReadOnlyList<Warning> first = engine.Evaluate(warnings, WarningFilterConfig.Default, On, Now, false);
            IReadOnlyList<Warning> second = engine.Evaluate(warnings, WarningFilterConfig.Default, On, Now.AddMinutes(10), false);

            Assert.Equal("1", Assert.Single(first).Id);
            Assert.Empty(second);
        }

        /// <summary>
        /// Nothing is reported or remembered while the notifications are off.
        /// </summary>
        [Fact]
        public void Evaluate_OffReportsNothing()
        {
            WarningAlertEngine engine = new WarningAlertEngine(null);
            List<Warning> warnings = new List<Warning> { Create("1") };

            Assert.Empty(engine.Evaluate(warnings, WarningFilterConfig.Default, WarningAlertOptions.Default, Now, false));
            Assert.Empty(engine.Reported);
            Assert.Single(engine.Evaluate(warnings, WarningFilterConfig.Default, On, Now, false));
        }

        /// <summary>
        /// Notices of a hidden category, of another voivodeship, and the readings of the gauges are not reported, also when the category of the gauges is shown.
        /// </summary>
        [Fact]
        public void Evaluate_FollowsCategoriesAndVoivodeshipsAndSkipsGauges()
        {
            WarningFilterConfig config = WarningFilterConfig.Default.WithCategory("stany-wod", true).WithProvinces(new List<string> { "slaskie" });
            List<Warning> warnings = new List<Warning>
            {
                Create("ok", "ogolne", false, 24, -1, "slaskie"),
                Create("nation"),
                Create("other", "ogolne", false, 24, -1, "lubuskie"),
                Create("road", "informacje-drogowe", false, 24, -1, "slaskie"),
                Create("gauge", "stany-wod", false, 24, -1, "slaskie"),
            };

            IReadOnlyList<Warning> result = new WarningAlertEngine(null).Evaluate(warnings, config, On, Now, false);

            Assert.Equal(new List<string> { "ok", "nation" }, result.Select((Warning warning) => warning.Id).OrderBy((string id) => id == "ok" ? 0 : 1).ToList());
            Assert.Equal(2, result.Count);
        }

        /// <summary>
        /// A notice that is over is not reported, one without an end is reported while it is not old, and one that is not yet valid is reported.
        /// </summary>
        [Fact]
        public void Evaluate_HandlesValidity()
        {
            List<Warning> warnings = new List<Warning>
            {
                Create("over", "ogolne", false, -2),
                Create("open", "ogolne", false, null, -5),
                Create("oldopen", "ogolne", false, null, -24 * 20),
                new Warning("future", "ogolne", "t", string.Empty, string.Empty, false, Now.AddHours(5), Now.AddHours(10), Now, new List<WarningProvince>(), string.Empty, string.Empty, null, null, null),
            };

            IReadOnlyList<Warning> result = new WarningAlertEngine(null).Evaluate(warnings, WarningFilterConfig.Default, On, Now, false);

            Assert.Equal(new List<string> { "open", "future" }.OrderBy((string id) => id).ToList(), result.Select((Warning warning) => warning.Id).OrderBy((string id) => id).ToList());
        }

        /// <summary>
        /// With the option for alarms only, notices that are not an alarm are neither reported nor remembered, so that they are reported if they become an alarm.
        /// </summary>
        [Fact]
        public void Evaluate_OnlyAlarms()
        {
            WarningAlertEngine engine = new WarningAlertEngine(null);
            WarningAlertOptions alarmsOnly = new WarningAlertOptions(true, true);

            IReadOnlyList<Warning> first = engine.Evaluate(new List<Warning> { Create("1"), Create("2", "ogolne", true) }, WarningFilterConfig.Default, alarmsOnly, Now, false);
            IReadOnlyList<Warning> second = engine.Evaluate(new List<Warning> { Create("1", "ogolne", true) }, WarningFilterConfig.Default, alarmsOnly, Now.AddMinutes(5), false);

            Assert.Equal("2", Assert.Single(first).Id);
            Assert.Equal("1", Assert.Single(second).Id);
        }

        /// <summary>
        /// A notice that was reported and later became an alarm is reported again, and not a third time.
        /// </summary>
        [Fact]
        public void Evaluate_ReportsAgainWhenNoticeBecomesAlarm()
        {
            WarningAlertEngine engine = new WarningAlertEngine(null);

            engine.Evaluate(new List<Warning> { Create("1") }, WarningFilterConfig.Default, On, Now, false);
            IReadOnlyList<Warning> upgraded = engine.Evaluate(new List<Warning> { Create("1", "ogolne", true) }, WarningFilterConfig.Default, On, Now.AddMinutes(5), false);
            IReadOnlyList<Warning> again = engine.Evaluate(new List<Warning> { Create("1", "ogolne", true) }, WarningFilterConfig.Default, On, Now.AddMinutes(10), false);

            Assert.True(Assert.Single(upgraded).IsAlarm);
            Assert.Empty(again);
        }

        /// <summary>
        /// The same identifier in two categories is two notices.
        /// </summary>
        [Fact]
        public void Evaluate_KeysIncludeTheCategory()
        {
            WarningFilterConfig config = WarningFilterConfig.Default;
            List<Warning> warnings = new List<Warning> { Create("7", "ogolne"), Create("7", "hydrologiczne") };

            Assert.Equal(2, new WarningAlertEngine(null).Evaluate(warnings, config, On, Now, false).Count);
        }

        /// <summary>
        /// In the silent mode the notices are remembered without being reported, and the next check reports only what is new.
        /// </summary>
        [Fact]
        public void Evaluate_SilentRemembersWithoutReporting()
        {
            WarningAlertEngine engine = new WarningAlertEngine(null);

            IReadOnlyList<Warning> silent = engine.Evaluate(new List<Warning> { Create("1"), Create("2") }, WarningFilterConfig.Default, On, Now, true);
            IReadOnlyList<Warning> later = engine.Evaluate(new List<Warning> { Create("1"), Create("2"), Create("3") }, WarningFilterConfig.Default, On, Now.AddMinutes(10), false);

            Assert.Empty(silent);
            Assert.Equal("3", Assert.Single(later).Id);
            Assert.Equal(3, engine.Reported.Count);
        }

        /// <summary>
        /// The memory can be restored, so that a restart does not repeat a notification; entries that ended more than a day ago are forgotten.
        /// </summary>
        [Fact]
        public void Evaluate_RestoresAndPrunesMemory()
        {
            WarningAlertEngine first = new WarningAlertEngine(null);
            List<Warning> warnings = new List<Warning> { Create("1") };
            first.Evaluate(warnings, WarningFilterConfig.Default, On, Now, false);
            Dictionary<string, WarningReport> saved = new Dictionary<string, WarningReport>(first.Reported);
            saved["ogolne|OLD"] = new WarningReport(Now.AddDays(-3), false);

            WarningAlertEngine restored = new WarningAlertEngine(saved);
            IReadOnlyList<Warning> again = restored.Evaluate(warnings, WarningFilterConfig.Default, On, Now.AddMinutes(1), false);

            Assert.Empty(again);
            Assert.Single(restored.Reported);
            Assert.False(restored.Reported.ContainsKey("ogolne|OLD"));
            Assert.Equal(Now.AddHours(24), restored.Reported["ogolne|1"].ExpiresUtc);
        }

        /// <summary>
        /// Alarms come first and then the most recently changed notices.
        /// </summary>
        [Fact]
        public void Evaluate_SortsAlarmsFirstThenNewest()
        {
            List<Warning> warnings = new List<Warning>
            {
                Create("old", "ogolne", false, 24, -10),
                Create("new", "ogolne", false, 24, -1),
                Create("alarm", "ogolne", true, 24, -20),
            };

            IReadOnlyList<Warning> result = new WarningAlertEngine(null).Evaluate(warnings, WarningFilterConfig.Default, On, Now, false);

            Assert.Equal(new List<string> { "alarm", "new", "old" }, result.Select((Warning warning) => warning.Id).ToList());
        }

        /// <summary>
        /// The region of a notice is wanted when no voivodeship is chosen, when the notice has none, or when it applies to a chosen one.
        /// </summary>
        [Fact]
        public void IsRegionWanted_FollowsTheChoice()
        {
            WarningFilterConfig chosen = WarningFilterConfig.Default.WithProvinces(new List<string> { "slaskie" });

            Assert.True(WarningFilterConfig.Default.IsRegionWanted(Create("1", "ogolne", false, 24, -1, "lubuskie")));
            Assert.True(chosen.IsRegionWanted(Create("2", "ogolne", false, 24, -1, "SLASKIE")));
            Assert.True(chosen.IsRegionWanted(Create("3")));
            Assert.False(chosen.IsRegionWanted(Create("4", "ogolne", false, 24, -1, "lubuskie")));
        }

        /// <summary>
        /// The title marks an alarm, the text holds the category, the voivodeships and the summary (or the text when there is no summary), and the group title counts the notices and the alarms.
        /// </summary>
        [Fact]
        public void Texts_DescribeTheNotice()
        {
            Loc.Language = AppLanguage.Polish;
            Warning notice = Create("1", "hydrologiczne", false, 24, -1, "slaskie");
            Warning alarm = Create("2", "ogolne", true);
            Warning noSummary = notice with { Summary = string.Empty };
            Warning bare = notice with { Summary = string.Empty, Content = string.Empty };

            Assert.Equal("Tytuł 1", WarningText.GetAlertTitle(notice));
            Assert.Equal("ALARM · Tytuł 2", WarningText.GetAlertTitle(alarm));
            Assert.Equal("Hydrologiczne · SLASKIE\nSkrót 1", WarningText.GetAlertBody(notice));
            Assert.Equal("Hydrologiczne · SLASKIE\nTreść 1", WarningText.GetAlertBody(noSummary));
            Assert.Equal("Hydrologiczne · SLASKIE", WarningText.GetAlertBody(bare));
            Assert.Equal("Nowe komunikaty RSO: 5", WarningText.GetGroupTitle(5, 0));
            Assert.Equal("Nowe komunikaty RSO: 5 (alarmów: 2)", WarningText.GetGroupTitle(5, 2));
        }

        /// <summary>
        /// The texts have an English translation. The language is global state shared by the tests that run in parallel, so the table is asked directly.
        /// </summary>
        [Fact]
        public void Texts_AreTranslated()
        {
            Assert.Equal("New RSO notices: {0}", Loc.Translate("Nowe komunikaty RSO: {0}", AppLanguage.English));
            Assert.Equal("New RSO notices: {0} (alarms: {1})", Loc.Translate("Nowe komunikaty RSO: {0} (alarmów: {1})", AppLanguage.English));
            Assert.Equal("RSO notices", Loc.Translate("Komunikaty RSO", AppLanguage.English));
        }
    }
}
