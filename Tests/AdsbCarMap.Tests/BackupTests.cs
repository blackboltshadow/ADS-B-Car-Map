using System.Globalization;
using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the backup of the application data: the bundle, the snapshot of the preferences, the categories and the size format.
    /// </summary>
    public class BackupTests
    {
        /// <summary>
        /// Time used as the creation time in the tests.
        /// </summary>
        private static readonly DateTime Created = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// A bundle read back has the same header and sections, with Polish letters and quotes unchanged.
        /// </summary>
        [Fact]
        public void Bundle_RoundTrip()
        {
            BackupBundle bundle = new BackupBundle(Created, "0.7.0", new List<BackupSection>
            {
                new BackupSection("settings", "[{\"k\":\"zażółć\",\"t\":\"s\",\"v\":\"gęślą \\\"jaźń\\\"\"}]"),
                new BackupSection("aircraftHistory", "[]")
            });

            string text = bundle.Serialize();

            Assert.Contains("zażółć", text);
            Assert.Equal(BackupParseStatus.Ok, BackupBundle.TryParse(text, out BackupBundle? loaded));
            Assert.NotNull(loaded);
            Assert.Equal(Created, loaded.CreatedUtc);
            Assert.Equal("0.7.0", loaded.AppVersion);
            Assert.Equal(2, loaded.Sections.Count);
            Assert.Equal(bundle.Sections[0].Content, loaded.Find("settings")!.Content);
            Assert.Null(loaded.Find("filters"));
        }

        /// <summary>
        /// Text that is not a backup, damaged text and text of another format are invalid.
        /// </summary>
        [Fact]
        public void Bundle_InvalidText()
        {
            Assert.Equal(BackupParseStatus.Invalid, BackupBundle.TryParse(null, out BackupBundle? none));
            Assert.Null(none);
            Assert.Equal(BackupParseStatus.Invalid, BackupBundle.TryParse("  ", out _));
            Assert.Equal(BackupParseStatus.Invalid, BackupBundle.TryParse("not json", out _));
            Assert.Equal(BackupParseStatus.Invalid, BackupBundle.TryParse("[]", out _));
            Assert.Equal(BackupParseStatus.Invalid, BackupBundle.TryParse("{\"format\":\"other\",\"version\":1,\"created\":1,\"sections\":[]}", out _));
            Assert.Equal(BackupParseStatus.Invalid, BackupBundle.TryParse("{\"format\":\"adsbcarmap-backup\",\"version\":0,\"created\":1,\"sections\":[]}", out _));
            Assert.Equal(BackupParseStatus.Invalid, BackupBundle.TryParse("{\"format\":\"adsbcarmap-backup\",\"version\":1,\"created\":0,\"sections\":[]}", out _));
            Assert.Equal(BackupParseStatus.Invalid, BackupBundle.TryParse("{\"format\":\"adsbcarmap-backup\",\"version\":1,\"created\":1}", out _));
        }

        /// <summary>
        /// A backup of a newer format is reported separately, so that the user is told to update the application.
        /// </summary>
        [Fact]
        public void Bundle_NewerFormat()
        {
            string text = "{\"format\":\"adsbcarmap-backup\",\"version\":" + (BackupBundle.CurrentVersion + 1) + ",\"created\":1,\"sections\":[]}";

            Assert.Equal(BackupParseStatus.NewerFormat, BackupBundle.TryParse(text, out BackupBundle? bundle));
            Assert.Null(bundle);
        }

        /// <summary>
        /// Damaged sections and repeated identifiers are skipped when a bundle is read.
        /// </summary>
        [Fact]
        public void Bundle_SkipsDamagedAndRepeatedSections()
        {
            string text = "{\"format\":\"adsbcarmap-backup\",\"version\":1,\"created\":" + Created.Ticks + ",\"sections\":["
                + "1,{\"id\":\"\",\"content\":\"x\"},{\"id\":\"a\"},{\"id\":\"b\",\"content\":5},{\"id\":\"c\",\"content\":\"first\"},{\"id\":\"c\",\"content\":\"second\"}]}";

            Assert.Equal(BackupParseStatus.Ok, BackupBundle.TryParse(text, out BackupBundle? bundle));
            Assert.Equal("first", Assert.Single(bundle!.Sections).Content);
            Assert.Equal(string.Empty, bundle.AppVersion);
        }

        /// <summary>
        /// All kinds of values keep their type through the snapshot of the preferences.
        /// </summary>
        [Fact]
        public void Snapshot_RoundTripKeepsTypes()
        {
            List<PreferenceEntry> entries = new List<PreferenceEntry>
            {
                new PreferenceEntry("text", "zażółć \"x\""),
                new PreferenceEntry("integer", 42),
                new PreferenceEntry("long", 5_000_000_000L),
                new PreferenceEntry("float", 1.25f),
                new PreferenceEntry("flag", true),
                new PreferenceEntry("set", new List<string> { "a", "b" }),
                new PreferenceEntry("emptySet", new List<string>())
            };

            List<PreferenceEntry>? loaded = PreferenceSnapshot.Parse(PreferenceSnapshot.Serialize(entries));

            Assert.NotNull(loaded);
            Assert.Equal(7, loaded.Count);
            Assert.Equal("zażółć \"x\"", loaded[0].Value);
            Assert.Equal(42, Assert.IsType<int>(loaded[1].Value));
            Assert.Equal(5_000_000_000L, Assert.IsType<long>(loaded[2].Value));
            Assert.Equal(1.25f, Assert.IsType<float>(loaded[3].Value));
            Assert.True(Assert.IsType<bool>(loaded[4].Value));
            Assert.Equal(new List<string> { "a", "b" }, Assert.IsAssignableFrom<IEnumerable<string>>(loaded[5].Value).ToList());
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(loaded[6].Value));
        }

        /// <summary>
        /// Values of unsupported types and floats that are not finite are not written.
        /// </summary>
        [Fact]
        public void Snapshot_SkipsUnsupportedValues()
        {
            List<PreferenceEntry> entries = new List<PreferenceEntry>
            {
                new PreferenceEntry("object", new object()),
                new PreferenceEntry("nan", float.NaN),
                new PreferenceEntry("ok", 1)
            };

            List<PreferenceEntry>? loaded = PreferenceSnapshot.Parse(PreferenceSnapshot.Serialize(entries));

            Assert.Equal("ok", Assert.Single(loaded!).Key);
        }

        /// <summary>
        /// Damaged text gives no values at all, and damaged entries are skipped.
        /// </summary>
        [Fact]
        public void Snapshot_DamagedText()
        {
            Assert.Null(PreferenceSnapshot.Parse(null));
            Assert.Null(PreferenceSnapshot.Parse(" "));
            Assert.Null(PreferenceSnapshot.Parse("not json"));
            Assert.Null(PreferenceSnapshot.Parse("{}"));
            Assert.Empty(PreferenceSnapshot.Parse("[]")!);

            string text = "[1,{\"k\":\"\",\"t\":\"s\",\"v\":\"x\"},{\"k\":\"a\",\"t\":\"i\",\"v\":\"text\"},{\"k\":\"b\",\"t\":\"q\",\"v\":1},{\"k\":\"c\",\"t\":\"s\"},{\"k\":\"d\",\"t\":\"s\",\"v\":\"ok\"}]";

            Assert.Equal("d", Assert.Single(PreferenceSnapshot.Parse(text)!).Key);
        }

        /// <summary>
        /// Identifiers of the categories are unique, sources are named, and defaults leave out the large histories.
        /// </summary>
        [Fact]
        public void Categories_AreConsistent()
        {
            Assert.Equal(BackupCategories.All.Count, BackupCategories.All.Select((BackupCategory category) => category.Id).Distinct().Count());
            Assert.Equal(BackupCategories.All.Count, BackupCategories.All.Select((BackupCategory category) => category.Source).Distinct().Count());
            Assert.All(BackupCategories.All, (BackupCategory category) =>
            {
                Assert.False(string.IsNullOrWhiteSpace(category.Source));
                Assert.False(string.IsNullOrWhiteSpace(category.Title));
                Assert.False(string.IsNullOrWhiteSpace(category.Description));
            });

            List<string> defaults = BackupCategories.DefaultSelection();

            Assert.Contains("settings", defaults);
            Assert.Contains("alerts", defaults);
            Assert.DoesNotContain("aircraftSightings", defaults);
            Assert.DoesNotContain("alertHistory", defaults);
            Assert.Equal(new List<string> { "background" }, BackupCategories.Find("alerts")!.ExcludedKeys);
            Assert.Null(BackupCategories.Find("nothing"));
        }

        /// <summary>
        /// A choice is written in the order of the catalog, unknown identifiers are dropped and an empty text means no category.
        /// </summary>
        [Fact]
        public void Selection_FormatAndParse()
        {
            string text = BackupCategories.FormatSelection(new List<string> { "alerts", "unknown", "settings" });

            Assert.Equal("settings,alerts", text);
            Assert.Equal(new List<string> { "settings", "alerts" }, BackupCategories.ParseSelection(text));
            Assert.Equal(new List<string> { "settings", "alerts" }, BackupCategories.ParseSelection(" alerts , bogus,settings,"));
            Assert.Empty(BackupCategories.ParseSelection(string.Empty));
            Assert.Empty(BackupCategories.ParseSelection(BackupCategories.FormatSelection(new List<string>())));
            Assert.Equal(BackupCategories.DefaultSelection(), BackupCategories.ParseSelection(null));
        }

        /// <summary>
        /// Sizes are written as bytes, kilobytes or megabytes with the separator of the culture.
        /// </summary>
        [Fact]
        public void ByteSize_Formats()
        {
            CultureInfo polish = new CultureInfo("pl-PL");

            Assert.Equal("0 B", ByteSizeFormatter.Format(0, CultureInfo.InvariantCulture));
            Assert.Equal("1023 B", ByteSizeFormatter.Format(1023, CultureInfo.InvariantCulture));
            Assert.Equal("1.0 KB", ByteSizeFormatter.Format(1024, CultureInfo.InvariantCulture));
            Assert.Equal("12,5 KB", ByteSizeFormatter.Format(12800, polish));
            Assert.Equal("1.50 MB", ByteSizeFormatter.Format(1572864, CultureInfo.InvariantCulture));
        }
    }
}
