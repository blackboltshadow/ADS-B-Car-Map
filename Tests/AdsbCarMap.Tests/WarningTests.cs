using System.Net;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Management.Selection;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Localization;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the logic of the screen with the official notices: the filter, the texts and the polling feed.
    /// </summary>
    public class WarningTests
    {
        /// <summary>
        /// The moment used by the tests.
        /// </summary>
        private static readonly DateTime Now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

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
            return new Warning(id, category, "Tytuł " + id, "Skrót", "Treść", alarm, Now.AddHours(-48), validToHours == null ? null : Now.AddHours(validToHours.Value), Now.AddHours(updatedHours), provinces.Select((string slug) => new WarningProvince(slug, slug.ToUpperInvariant())).ToList(), string.Empty, string.Empty, null, null, null);
        }

        /// <summary>
        /// The default configuration shows the warnings and hides the roads and the gauges, shows all voivodeships and hides notices that are over.
        /// </summary>
        [Fact]
        public void Default_ShowsWarningsOnly()
        {
            WarningFilterConfig config = WarningFilterConfig.Default;

            Assert.True(config.IsCategoryShown("ogolne"));
            Assert.True(config.IsCategoryShown("HYDROLOGICZNE"));
            Assert.True(config.IsCategoryShown("meteorologiczne"));
            Assert.False(config.IsCategoryShown("informacje-drogowe"));
            Assert.False(config.IsCategoryShown("stany-wod"));
            Assert.False(config.IsCategoryShown("newcategory"));
            Assert.Empty(config.Provinces);
            Assert.True(config.IsProvinceWanted("slaskie"));
            Assert.False(config.ShowExpired);
            Assert.False(config.OnlyAlarms);
            Assert.Equal(new List<string> { "ogolne", "meteorologiczne", "hydrologiczne" }, config.ShownCategories);
        }

        /// <summary>
        /// A notice passes when its category is shown, and a category that was switched on later shows its notices at once.
        /// </summary>
        [Fact]
        public void Passes_FollowsTheCategory()
        {
            Warning road = Create("1", "informacje-drogowe");

            Assert.False(WarningFilterConfig.Default.Passes(road, Now));
            Assert.True(WarningFilterConfig.Default.WithCategory("informacje-drogowe", true).Passes(road, Now));
            Assert.False(WarningFilterConfig.Default.WithCategory("ogolne", false).Passes(Create("2"), Now));
        }

        /// <summary>
        /// With chosen voivodeships a notice passes when it applies to one of them; a notice without a voivodeship applies to the whole country.
        /// </summary>
        [Fact]
        public void Passes_FollowsTheVoivodeships()
        {
            WarningFilterConfig config = WarningFilterConfig.Default.WithProvinces(new List<string> { "slaskie", "opolskie" });

            Assert.True(config.Passes(Create("1", "ogolne", false, 24, -1, "slaskie"), Now));
            Assert.True(config.Passes(Create("2", "ogolne", false, 24, -1, "lubuskie", "OPOLSKIE"), Now));
            Assert.False(config.Passes(Create("3", "ogolne", false, 24, -1, "lubuskie"), Now));
            Assert.True(config.Passes(Create("4"), Now));
            Assert.Equal(new List<string> { "opolskie", "slaskie" }, config.Provinces);
            Assert.True(config.IsProvinceWanted("SLASKIE"));
            Assert.False(config.IsProvinceWanted("lubuskie"));
            Assert.Empty(config.WithProvinces(new List<string>()).Provinces);
        }

        /// <summary>
        /// A notice that is over is hidden unless the over ones are shown, one without an end never ends, and one that has not started yet is shown.
        /// </summary>
        [Fact]
        public void Passes_HandlesValidity()
        {
            Warning over = Create("1", "ogolne", false, -1);
            Warning open = Create("2", "ogolne", false, null);
            Warning future = new Warning("3", "ogolne", "t", string.Empty, string.Empty, false, Now.AddHours(5), Now.AddHours(10), Now, new List<WarningProvince>(), string.Empty, string.Empty, null, null, null);

            Assert.False(WarningFilterConfig.Default.Passes(over, Now));
            Assert.True(WarningFilterConfig.Default.WithShowExpired(true).Passes(over, Now));
            Assert.True(WarningFilterConfig.Default.Passes(open, Now));
            Assert.True(WarningFilterConfig.Default.Passes(future, Now));
        }

        /// <summary>
        /// In the alarm-only mode only the notices marked as an alarm pass.
        /// </summary>
        [Fact]
        public void Passes_OnlyAlarms()
        {
            WarningFilterConfig config = WarningFilterConfig.Default.WithOnlyAlarms(true);

            Assert.True(config.OnlyAlarms);
            Assert.True(config.Passes(Create("1", "ogolne", true), Now));
            Assert.False(config.Passes(Create("2"), Now));
        }

        /// <summary>
        /// Selecting puts alarms first, then the most recently changed notices, and drops what does not pass.
        /// </summary>
        [Fact]
        public void Select_SortsAlarmsFirstThenNewest()
        {
            List<Warning> warnings = new List<Warning>
            {
                Create("old", "ogolne", false, 24, -10),
                Create("alarm-old", "ogolne", true, 24, -20),
                Create("new", "ogolne", false, 24, -1),
                Create("road", "informacje-drogowe", true, 24, -1),
                Create("alarm-new", "hydrologiczne", true, 24, -2),
                Create("gone", "ogolne", false, -5, 0),
            };

            IReadOnlyList<Warning> selected = WarningFilterConfig.Default.Select(warnings, Now);

            Assert.Equal(new List<string> { "alarm-new", "alarm-old", "new", "old" }, selected.Select((Warning warning) => warning.Id).ToList());
        }

        /// <summary>
        /// The configuration is written and read back, including a category whose code has a hyphen.
        /// </summary>
        [Fact]
        public void SerializeAndParse_RoundTrip()
        {
            WarningFilterConfig config = WarningFilterConfig.Default.WithCategory("stany-wod", false).WithCategory("informacje-drogowe", true).WithProvinces(new List<string> { "slaskie", "kujawsko-pomorskie" }).WithShowExpired(true).WithOnlyAlarms(true);

            WarningFilterConfig back = WarningFilterConfig.Parse(config.Serialize());

            Assert.Equal(config.Serialize(), back.Serialize());
            Assert.True(back.IsCategoryShown("informacje-drogowe"));
            Assert.False(back.IsCategoryShown("stany-wod"));
            Assert.Equal(new List<string> { "kujawsko-pomorskie", "slaskie" }, back.Provinces);
            Assert.True(back.ShowExpired);
            Assert.True(back.OnlyAlarms);
        }

        /// <summary>
        /// The stored text has a fixed, sorted form.
        /// </summary>
        [Fact]
        public void Serialize_IsStable()
        {
            WarningFilterConfig config = new WarningFilterConfig(new Dictionary<string, bool> { { "ogolne", true }, { "stany-wod", false } }, new List<string> { "slaskie" }, false, true);

            Assert.Equal("v1;ogolne+,stany-wod|;slaskie;0;1", config.Serialize());
        }

        /// <summary>
        /// Missing, foreign or damaged text gives the default configuration.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("v2;ogolne+;;0;0")]
        [InlineData("v1;ogolne+;;0")]
        [InlineData("v1;ogolne+;;2;0")]
        [InlineData("v1;ogolne+;;0;x")]
        public void Parse_ReturnsDefaultForBadText(string? text)
        {
            Assert.Equal(WarningFilterConfig.Default.Serialize(), WarningFilterConfig.Parse(text).Serialize());
        }

        /// <summary>
        /// A damaged entry is skipped and the rest is kept; everything can be switched off.
        /// </summary>
        [Fact]
        public void Parse_SkipsDamagedEntries()
        {
            WarningFilterConfig config = WarningFilterConfig.Parse("v1;ogolne+,,x,+,y?,meteorologiczne|;, slaskie ,;1;0");

            Assert.True(config.IsCategoryShown("ogolne"));
            Assert.False(config.IsCategoryShown("meteorologiczne"));
            Assert.False(config.IsCategoryShown("x"));
            Assert.Equal(new List<string> { "slaskie" }, config.Provinces);
            Assert.True(config.ShowExpired);
            Assert.Empty(WarningFilterConfig.Parse("v1;;;0;0").ShownCategories);
        }

        /// <summary>
        /// Changing the configuration gives a new object and leaves the old one unchanged.
        /// </summary>
        [Fact]
        public void With_DoesNotChangeTheOriginal()
        {
            WarningFilterConfig original = WarningFilterConfig.Default;

            WarningFilterConfig changed = original.WithCategory("stany-wod", true).WithProvinces(new List<string> { "slaskie" }).WithShowExpired(true).WithOnlyAlarms(true);

            Assert.False(original.IsCategoryShown("stany-wod"));
            Assert.Empty(original.Provinces);
            Assert.False(original.ShowExpired);
            Assert.False(original.OnlyAlarms);
            Assert.True(changed.IsCategoryShown("stany-wod"));
        }

        /// <summary>
        /// Known categories have names and an order, an unknown one shows its code and comes last.
        /// </summary>
        [Fact]
        public void Categories_HaveNamesAndOrder()
        {
            Loc.Language = AppLanguage.Polish;

            Assert.Equal("Ogólne", WarningCategories.GetName("ogolne"));
            Assert.Equal("Meteorologiczne", WarningCategories.GetName("METEOROLOGICZNE"));
            Assert.Equal("Hydrologiczne", WarningCategories.GetName("hydrologiczne"));
            Assert.Equal("Informacje drogowe", WarningCategories.GetName("informacje-drogowe"));
            Assert.Equal("Stany wód", WarningCategories.GetName("stany-wod"));
            Assert.Equal("nowa", WarningCategories.GetName("nowa"));
            Assert.Equal("?", WarningCategories.GetName(" "));
            Assert.True(WarningCategories.GetOrder("ogolne") < WarningCategories.GetOrder("stany-wod"));
            Assert.True(WarningCategories.GetOrder("stany-wod") < WarningCategories.GetOrder("nowa"));
        }

        /// <summary>
        /// There are sixteen voivodeships with distinct identifiers, and their names are found without regard to case.
        /// </summary>
        [Fact]
        public void Provinces_AreSixteenAndNamed()
        {
            Assert.Equal(16, WarningProvinces.All.Count);
            Assert.Equal(16, WarningProvinces.All.Select((WarningProvince province) => province.Slug).Distinct().Count());
            Assert.Equal("Śląskie", WarningProvinces.GetName("SLASKIE"));
            Assert.Equal("Łódzkie", WarningProvinces.GetName("lodzkie"));
            Assert.Equal("nieznane", WarningProvinces.GetName("nieznane"));
        }

        /// <summary>
        /// The title of a notice is its title, or for a reading of a gauge the station, the river or the category.
        /// </summary>
        [Fact]
        public void GetTitle_FallsBackForGauges()
        {
            Warning gauge = new Warning("1", "stany-wod", string.Empty, string.Empty, string.Empty, false, Now, null, Now, new List<WarningProvince>(), "Barycz", "Odolanów / Barycz", null, null, null);
            Warning river = gauge with { LocationName = string.Empty };
            Warning bare = river with { RiverName = string.Empty };

            Loc.Language = AppLanguage.Polish;

            Assert.Equal("Tytuł 5", WarningText.GetTitle(Create("5")));
            Assert.Equal("Odolanów / Barycz", WarningText.GetTitle(gauge));
            Assert.Equal("Barycz", WarningText.GetTitle(river));
            Assert.Equal("Stany wód", WarningText.GetTitle(bare));
        }

        /// <summary>
        /// The subtitle names the category and the voivodeships, or says that the notice is for the whole country.
        /// </summary>
        [Fact]
        public void GetSubtitle_NamesCategoryAndRegions()
        {
            Loc.Language = AppLanguage.Polish;

            Assert.Equal("Ogólne · SLASKIE, OPOLSKIE", WarningText.GetSubtitle(Create("1", "ogolne", false, 24, -1, "slaskie", "opolskie")));
            Assert.Equal("Hydrologiczne · cały kraj", WarningText.GetSubtitle(Create("2", "hydrologiczne")));
        }

        /// <summary>
        /// The description lists the summary, the text, the validity and the time of the change in the given time zone; a summary equal to the text is not repeated.
        /// </summary>
        [Fact]
        public void GetLines_DescribeTheNotice()
        {
            Loc.Language = AppLanguage.Polish;
            Warning warning = Create("1", "ogolne", false, 24, -1);

            IReadOnlyList<string> lines = WarningText.GetLines(warning, Now, TimeZoneInfo.Utc);

            Assert.Equal("Skrót", lines[0]);
            Assert.Equal("Treść", lines[1]);
            Assert.Equal("Ważne: 01.11 12:00 – 04.11 12:00", lines[2]);
            Assert.Equal("Zmieniono: 03.11 11:00", lines[3]);
            Assert.Equal(4, lines.Count);
            Assert.Equal(3, WarningText.GetLines(warning with { Summary = "Treść" }, Now, TimeZoneInfo.Utc).Count);
        }

        /// <summary>
        /// The validity line says when a notice has no end and when it is over.
        /// </summary>
        [Fact]
        public void GetValidityLine_CoversOpenAndOver()
        {
            Loc.Language = AppLanguage.Polish;

            Assert.Equal("Ważne: od 01.11 12:00", WarningText.GetValidityLine(Create("1", "ogolne", false, null), Now, TimeZoneInfo.Utc));
            Assert.StartsWith("Wygasło: ", WarningText.GetValidityLine(Create("2", "ogolne", false, -1), Now, TimeZoneInfo.Utc));
        }

        /// <summary>
        /// A reading of a gauge is described with the levels, and the exceeded levels are named.
        /// </summary>
        [Fact]
        public void GetLines_DescribeGauge()
        {
            Loc.Language = AppLanguage.Polish;
            Warning gauge = new Warning("1", "stany-wod", string.Empty, string.Empty, string.Empty, false, Now, null, Now, new List<WarningProvince>(), "Barycz", "Odolanów", null, null, new WarningWaterLevel(67, 120, 150));

            IReadOnlyList<string> normal = WarningText.GetLines(gauge, Now, TimeZoneInfo.Utc);
            IReadOnlyList<string> warned = WarningText.GetLines(gauge with { WaterLevel = new WarningWaterLevel(130.5, 120, 150) }, Now, TimeZoneInfo.Utc);
            IReadOnlyList<string> alarmed = WarningText.GetLines(gauge with { WaterLevel = new WarningWaterLevel(150, 120, 150) }, Now, TimeZoneInfo.Utc);
            IReadOnlyList<string> partial = WarningText.GetLines(gauge with { WaterLevel = new WarningWaterLevel(10, null, null), RiverName = string.Empty }, Now, TimeZoneInfo.Utc);

            Assert.Contains("Rzeka: Barycz", normal);
            Assert.Contains("Stan wody: 67 cm", normal);
            Assert.Contains("Stan ostrzegawczy: 120 cm", normal);
            Assert.Contains("Stan alarmowy: 150 cm", normal);
            Assert.DoesNotContain("Przekroczony stan alarmowy", normal);
            Assert.DoesNotContain("Przekroczony stan ostrzegawczy", normal);
            Assert.Contains("Stan wody: 130,5 cm", warned);
            Assert.Contains("Przekroczony stan ostrzegawczy", warned);
            Assert.Contains("Przekroczony stan alarmowy", alarmed);
            Assert.DoesNotContain("Przekroczony stan ostrzegawczy", alarmed);
            Assert.Contains("Stan wody: 10 cm", partial);
            Assert.DoesNotContain(partial, (string line) => line.StartsWith("Rzeka", StringComparison.Ordinal));
        }

        /// <summary>
        /// The status line names the state of the feed.
        /// </summary>
        [Fact]
        public void BuildStateText_DescribesEveryState()
        {
            Loc.Language = AppLanguage.Polish;

            Assert.Equal("ładowanie…", WarningText.BuildStateText(PollingFeedState.Loading, 0));
            Assert.Equal("komunikatów: 4", WarningText.BuildStateText(PollingFeedState.Ready, 4));
            Assert.Equal("brak połączenia", WarningText.BuildStateText(PollingFeedState.Offline, 0));
            Assert.Equal("brak połączenia, dane mogą być nieaktualne, komunikatów: 2", WarningText.BuildStateText(PollingFeedState.Offline, 2));
            Assert.Equal("wstrzymane, komunikatów: 1", WarningText.BuildStateText(PollingFeedState.Stopped, 1));
        }

        /// <summary>
        /// The card of the sources lists the feed: its state, the last update and the last failure.
        /// </summary>
        [Fact]
        public void FormatFeed_DescribesTheSource()
        {
            Loc.Language = AppLanguage.Polish;
            PollingFeedFailure failure = new PollingFeedFailure("limit zapytań (429)", Now.AddMinutes(-10));

            string on = WarningText.FormatFeed(true, PollingFeedState.Ready, 12, Now.AddMinutes(-3), failure, Now);
            string stopped = WarningText.FormatFeed(true, PollingFeedState.Stopped, 0, null, null, Now);
            string loading = WarningText.FormatFeed(true, PollingFeedState.Loading, 0, null, null, Now);
            string offline = WarningText.FormatFeed(true, PollingFeedState.Offline, 0, null, null, Now);

            Assert.StartsWith("Ostrzeżenia:\n• RSO (TVP) — działa, komunikatów: 12\n", on);
            Assert.Contains("ostatnia aktualizacja: ", on);
            Assert.Contains("ostatni błąd: limit zapytań (429), ", on);
            Assert.Equal("Ostrzeżenia:\n• RSO (TVP) — wstrzymane, ekran ostrzeżeń jest zamknięty", stopped);
            Assert.Contains("pobieranie…", loading);
            Assert.Contains("brak połączenia, aplikacja ponawia próby", offline);
            Assert.Contains("wstrzymane", WarningText.FormatFeed(false, PollingFeedState.Ready, 5, null, null, Now));
        }

        /// <summary>
        /// The attribution and the warning have an English translation. The language is global state shared by the tests that run in parallel, so the table is asked directly.
        /// </summary>
        [Fact]
        public void Texts_AreTranslated()
        {
            Assert.Equal("Data: RSO (TVP)", Loc.Translate("Dane: RSO (TVP)", AppLanguage.English));
            Assert.Contains("not an official warning system", Loc.Translate("Komunikaty pochodzą z publicznego kanału Regionalnego Systemu Ostrzegania, który udostępnia TVP. To nie jest oficjalny system ostrzegania: aplikacja może pokazać komunikat ze spóźnieniem, w niepełnej postaci albo wcale. Alert RCB przychodzi SMS-em od operatora i nie jest tu dostępny. Zawsze kieruj się oficjalnymi komunikatami służb.", AppLanguage.English));
            Assert.Equal("Hydrological", Loc.Translate("Hydrologiczne", AppLanguage.English));
            Assert.Equal("Water levels", Loc.Translate("Stany wód", AppLanguage.English));
        }

        /// <summary>
        /// Source of the items for the feed tests.
        /// </summary>
        private sealed class Source
        {
            /// <summary>
            /// Gives the answer or throws.
            /// </summary>
            public Func<IReadOnlyList<string>> Answer { get; set; } = () => new List<string>();

            /// <summary>
            /// Number of the requests.
            /// </summary>
            public int Calls { get; private set; }

            /// <summary>
            /// Fetches the items.
            /// </summary>
            /// <param name="token">Cancellation token.</param>
            /// <returns>The items.</returns>
            public Task<IReadOnlyList<string>> FetchAsync(CancellationToken token)
            {
                Calls++;

                return Task.FromResult(Answer());
            }
        }

        /// <summary>
        /// Creates a feed over a source with a fixed clock and a delay that does nothing.
        /// </summary>
        /// <param name="source">Source of the items.</param>
        /// <param name="events">List that receives the reported events.</param>
        /// <param name="clock">Clock of the feed.</param>
        /// <returns>The feed.</returns>
        private static PollingFeed<string> CreateFeed(Source source, List<string> events, Func<DateTime> clock)
        {
            return new PollingFeed<string>("fake", source.FetchAsync, clock, (TimeSpan time, CancellationToken token) => Task.CompletedTask, () => TimeSpan.FromMinutes(5), (SourceEventKind kind, string message, Exception? exception) => events.Add(kind + ":" + message));
        }

        /// <summary>
        /// A successful download stores the items and the time and moves the feed to the ready state.
        /// </summary>
        [Fact]
        public async Task Feed_StoresItemsAndReturnsInterval()
        {
            Source source = new Source { Answer = () => new List<string> { "a", "b" } };
            PollingFeed<string> feed = CreateFeed(source, new List<string>(), () => Now);
            int changes = 0;
            feed.Changed += () => changes++;

            TimeSpan wait = await feed.RefreshAsync(CancellationToken.None);

            Assert.Equal(TimeSpan.FromMinutes(5), wait);
            Assert.Equal(2, feed.Items.Count);
            Assert.Equal(PollingFeedState.Ready, feed.State);
            Assert.Equal(Now, feed.LastUpdateUtc);
            Assert.Null(feed.LastFailure);
            Assert.Equal("fake", feed.Name);
            Assert.True(changes > 0);
        }

        /// <summary>
        /// A failure keeps the older items, reports it and backs off, but never beyond the normal interval.
        /// </summary>
        [Fact]
        public async Task Feed_BacksOffAfterFailures()
        {
            Source source = new Source { Answer = () => new List<string> { "a" } };
            List<string> events = new List<string>();
            PollingFeed<string> feed = CreateFeed(source, events, () => Now);
            await feed.RefreshAsync(CancellationToken.None);
            source.Answer = () => throw new HttpRequestException("down", null, HttpStatusCode.BadGateway);

            List<TimeSpan> waits = new List<TimeSpan>();

            for (int i = 0; i < 6; i++)
            {
                waits.Add(await feed.RefreshAsync(CancellationToken.None));
            }

            Assert.Equal(new List<TimeSpan> { TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(240), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5) }, waits);
            Assert.Single(feed.Items);
            Assert.Equal(PollingFeedState.Offline, feed.State);
            Assert.Equal("błąd serwera", feed.LastFailure?.Description);
            Assert.Equal(6, events.Count);
            Assert.StartsWith("Failure:fake: błąd serwera", events[0]);
        }

        /// <summary>
        /// A rate limit waits as long as the server asked, at least the normal interval; an invalid answer and a timeout are failures.
        /// </summary>
        [Fact]
        public async Task Feed_HandlesRateLimitInvalidAnswerAndTimeout()
        {
            Source source = new Source { Answer = () => throw new RateLimitedException(TimeSpan.FromMinutes(20)) };
            PollingFeed<string> feed = CreateFeed(source, new List<string>(), () => Now);

            Assert.Equal(TimeSpan.FromMinutes(20), await feed.RefreshAsync(CancellationToken.None));
            source.Answer = () => throw new RateLimitedException(TimeSpan.FromSeconds(10));
            Assert.Equal(TimeSpan.FromMinutes(5), await feed.RefreshAsync(CancellationToken.None));
            Assert.Equal("limit zapytań (429)", feed.LastFailure?.Description);
            source.Answer = () => throw new System.Text.Json.JsonException("bad");
            await feed.RefreshAsync(CancellationToken.None);
            Assert.Equal("nieprawidłowa odpowiedź", feed.LastFailure?.Description);
            source.Answer = () => throw new TaskCanceledException("timeout");
            await feed.RefreshAsync(CancellationToken.None);
            Assert.Equal("przekroczono czas oczekiwania", feed.LastFailure?.Description);
            source.Answer = () => throw new HttpRequestException("no network");
            await feed.RefreshAsync(CancellationToken.None);
            Assert.Equal("brak połączenia", feed.LastFailure?.Description);
        }

        /// <summary>
        /// A cancellation requested by the caller is not a failure.
        /// </summary>
        [Fact]
        public async Task Feed_PropagatesRequestedCancellation()
        {
            Source source = new Source { Answer = () => new List<string> { "a" } };
            PollingFeed<string> feed = CreateFeed(source, new List<string>(), () => Now);
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => feed.RefreshAsync(cancellation.Token));

            Assert.Empty(feed.Items);
            Assert.Null(feed.LastFailure);
        }

        /// <summary>
        /// The feed downloads only when the pause after the last download is over, and a failure lengthens the pause.
        /// </summary>
        [Fact]
        public async Task Feed_RefreshIfDueRespectsThePause()
        {
            DateTime clock = Now;
            Source source = new Source();
            PollingFeed<string> feed = CreateFeed(source, new List<string>(), () => clock);

            Assert.True(await feed.RefreshIfDueAsync(CancellationToken.None));
            Assert.False(await feed.RefreshIfDueAsync(CancellationToken.None));
            clock = Now.AddMinutes(5);
            Assert.True(await feed.RefreshIfDueAsync(CancellationToken.None));
            Assert.Equal(2, source.Calls);
            source.Answer = () => throw new HttpRequestException("down");
            clock = Now.AddMinutes(10);
            Assert.True(await feed.RefreshIfDueAsync(CancellationToken.None));
            clock = Now.AddMinutes(10).AddSeconds(29);
            Assert.False(await feed.RefreshIfDueAsync(CancellationToken.None));
            clock = Now.AddMinutes(10).AddSeconds(30);
            Assert.True(await feed.RefreshIfDueAsync(CancellationToken.None));
        }

        /// <summary>
        /// Starting downloads again and again, stopping ends the loop and keeps the data, and a second start or stop does nothing.
        /// </summary>
        [Fact]
        public async Task Feed_StartAndStopRunTheLoop()
        {
            Source source = new Source { Answer = () => new List<string> { "a" } };
            TaskCompletionSource<bool> twoCalls = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            PollingFeed<string> feed = new PollingFeed<string>(
                "fake",
                source.FetchAsync,
                () => Now,
                async (TimeSpan time, CancellationToken token) =>
                {
                    if (source.Calls >= 2)
                    {
                        twoCalls.TrySetResult(true);
                        await Task.Delay(Timeout.Infinite, token);
                    }
                },
                () => TimeSpan.FromMinutes(5),
                null);

            Assert.Equal(PollingFeedState.Stopped, feed.State);
            feed.Stop();
            feed.Start();
            feed.Start();
            await twoCalls.Task.WaitAsync(TimeSpan.FromSeconds(10));
            feed.Stop();
            feed.Stop();

            Assert.Equal(PollingFeedState.Stopped, feed.State);
            Assert.Single(feed.Items);
            Assert.NotNull(feed.LastUpdateUtc);

            feed.Start();
            Assert.Equal(PollingFeedState.Ready, feed.State);
            feed.Stop();
        }
    }
}
