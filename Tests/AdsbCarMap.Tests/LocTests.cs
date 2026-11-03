using System.Text.RegularExpressions;
using AdsbCarMap.Localization;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the translation module. They pass the language explicitly, so they do not depend on the global language.
    /// </summary>
    public class LocTests
    {
        /// <summary>
        /// Polish returns the source text unchanged.
        /// </summary>
        [Fact]
        public void Translate_Polish_ReturnsSource()
        {
            Assert.Equal("Ustawienia", Loc.Translate("Ustawienia", AppLanguage.Polish));
        }

        /// <summary>
        /// English returns the translation of a known text.
        /// </summary>
        [Fact]
        public void Translate_English_ReturnsTranslation()
        {
            Assert.Equal("Settings", Loc.Translate("Ustawienia", AppLanguage.English));
        }

        /// <summary>
        /// A text without a translation falls back to the Polish source.
        /// </summary>
        [Fact]
        public void Translate_English_UnknownTextFallsBack()
        {
            Assert.Equal("Nieznany tekst xyz", Loc.Translate("Nieznany tekst xyz", AppLanguage.English));
        }

        /// <summary>
        /// Every translation is non-empty and keeps the same format placeholders as its source.
        /// </summary>
        [Fact]
        public void Table_PlaceholdersMatchSource()
        {
            Regex placeholder = new Regex(@"\{(\d+)[^}]*\}");

            foreach (KeyValuePair<string, string> entry in EnglishTranslations.Table)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Value), entry.Key);

                List<string> source = placeholder.Matches(entry.Key).Select(match => match.Value).OrderBy(value => value, StringComparer.Ordinal).ToList();
                List<string> target = placeholder.Matches(entry.Value).Select(match => match.Value).OrderBy(value => value, StringComparer.Ordinal).ToList();

                Assert.True(source.SequenceEqual(target), entry.Key);
            }
        }

        /// <summary>
        /// Unknown stored values give Polish.
        /// </summary>
        [Fact]
        public void FromSettingValue_UnknownValue_ReturnsPolish()
        {
            Assert.Equal(AppLanguage.Polish, Loc.FromSettingValue(7));
            Assert.Equal(AppLanguage.English, Loc.FromSettingValue(1));
        }
    }
}
