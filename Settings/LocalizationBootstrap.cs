using System.Runtime.CompilerServices;
using AdsbCarMap.Localization;

namespace AdsbCarMap.Settings
{
    /// <summary>
    /// Connects the translation module to the language stored in the application settings.
    /// </summary>
    internal static class LocalizationBootstrap
    {
        /// <summary>
        /// Registers the provider that reads the language from the settings when the first text is translated.
        /// </summary>
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Register()
        {
            Loc.LanguageProvider = () => AppSettings.Language;
        }
    }
}
