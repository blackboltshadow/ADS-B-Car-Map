namespace AdsbCarMap.Settings
{
    /// <summary>
    /// Opis jednego liczbowego ustawienia aplikacji wraz z wartością domyślną i dozwolonym zakresem.
    /// </summary>
    /// <param name="Key">Klucz w SharedPreferences.</param>
    /// <param name="Label">Nazwa wyświetlana na ekranie ustawień.</param>
    /// <param name="DefaultValue">Wartość domyślna.</param>
    /// <param name="Minimum">Najmniejsza dozwolona wartość.</param>
    /// <param name="Maximum">Największa dozwolona wartość.</param>
    /// <param name="IsInteger">Informacja, czy wartość jest liczbą całkowitą.</param>
    public sealed record SettingDefinition(string Key, string Label, double DefaultValue, double Minimum, double Maximum, bool IsInteger)
    {
        /// <summary>
        /// Ogranicza wartość do zakresu ustawienia i zaokrągla ją, jeśli ustawienie jest całkowite.
        /// </summary>
        /// <param name="value">Wartość do poprawienia.</param>
        /// <returns>Wartość mieszcząca się w zakresie.</returns>
        public double Normalize(double value)
        {
            double clamped = Math.Clamp(value, Minimum, Maximum);

            return IsInteger ? Math.Round(clamped) : clamped;
        }
    }
}
