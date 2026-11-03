using System.Globalization;

namespace AdsbCarMap.Localization
{
    /// <summary>
    /// Translates the texts of the user interface. The Polish source text is the key; Polish returns it unchanged, other languages look it up in their table and fall back to the Polish text when there is no translation.
    /// </summary>
    public static class Loc
    {
        /// <summary>
        /// Language names shown in the language setting; each name is written in its own language, so it is never translated.
        /// </summary>
        public static readonly IReadOnlyList<string> LanguageNames = new List<string> { "Polski", "English" };

        /// <summary>
        /// Language used by <see cref="T"/> and <see cref="F"/>.
        /// </summary>
        private static volatile AppLanguage language = AppLanguage.Polish;

        /// <summary>
        /// Whether <see cref="language"/> has been set, either explicitly or by <see cref="LanguageProvider"/>.
        /// </summary>
        private static volatile bool initialized;

        /// <summary>
        /// Reads the stored language the first time a text is translated; null when the language is always set explicitly.
        /// </summary>
        public static Func<AppLanguage>? LanguageProvider { get; set; }

        /// <summary>
        /// Current language of the user interface.
        /// </summary>
        public static AppLanguage Language
        {
            get
            {
                EnsureInitialized();

                return language;
            }

            set
            {
                language = Enum.IsDefined(value) ? value : AppLanguage.Polish;
                initialized = true;
            }
        }

        /// <summary>
        /// Returns the language with the given stored value of the language setting.
        /// </summary>
        /// <param name="value">Stored value, the index of the language.</param>
        /// <returns>The language, or Polish when the value is unknown.</returns>
        public static AppLanguage FromSettingValue(int value)
        {
            return Enum.IsDefined((AppLanguage)value) ? (AppLanguage)value : AppLanguage.Polish;
        }

        /// <summary>
        /// Translates a text to the current language.
        /// </summary>
        /// <param name="polish">Polish source text.</param>
        /// <returns>Translation, or the source text when the current language is Polish or the text has no translation.</returns>
        public static string T(string polish)
        {
            return Translate(polish, Language);
        }

        /// <summary>
        /// Reads the language from <see cref="LanguageProvider"/> once; stays uninitialized when the provider is not available yet.
        /// </summary>
        private static void EnsureInitialized()
        {
            Func<AppLanguage>? provider = LanguageProvider;

            if (initialized || provider == null)
            {
                return;
            }

            try
            {
                AppLanguage value = provider();
                language = Enum.IsDefined(value) ? value : AppLanguage.Polish;
                initialized = true;
            }
            catch (Exception)
            {
                language = AppLanguage.Polish;
            }
        }

        /// <summary>
        /// Translates a text to the given language.
        /// </summary>
        /// <param name="polish">Polish source text.</param>
        /// <param name="target">Target language.</param>
        /// <returns>Translation, or the source text when the target language is Polish or the text has no translation.</returns>
        public static string Translate(string polish, AppLanguage target)
        {
            if (target == AppLanguage.English && EnglishTranslations.Table.TryGetValue(polish, out string? english))
            {
                return english;
            }

            return polish;
        }

        /// <summary>
        /// Translates a format string to the current language and fills in the arguments.
        /// </summary>
        /// <param name="polishFormat">Polish source format string.</param>
        /// <param name="arguments">Values for the placeholders.</param>
        /// <returns>Formatted text.</returns>
        public static string F(string polishFormat, params object?[] arguments)
        {
            return string.Format(CultureInfo.CurrentCulture, T(polishFormat), arguments);
        }
    }
}
