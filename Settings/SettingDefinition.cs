namespace AdsbCarMap.Settings
{
    /// <summary>
    /// Kind of control used to edit a setting on the settings screen.
    /// </summary>
    public enum SettingKind
    {
        /// <summary>
        /// Numeric text field.
        /// </summary>
        Number,

        /// <summary>
        /// On/off switch; the stored value is 0 or 1.
        /// </summary>
        Switch,

        /// <summary>
        /// Drop-down list; the stored value is the zero-based index of the chosen option.
        /// </summary>
        Choice,

        /// <summary>
        /// Slider moving in steps of <see cref="SettingDefinition.Step"/>.
        /// </summary>
        Slider,

        /// <summary>
        /// One threshold of the altitude color scale. All such settings are edited together in a single scale editor.
        /// </summary>
        Scale,
    }

    /// <summary>
    /// Description of one numeric application setting with its default value, allowed range and the kind of control that edits it.
    /// </summary>
    /// <param name="Key">Key in SharedPreferences.</param>
    /// <param name="Label">Name shown on the settings screen.</param>
    /// <param name="DefaultValue">Default value.</param>
    /// <param name="Minimum">Smallest allowed value.</param>
    /// <param name="Maximum">Largest allowed value.</param>
    /// <param name="IsInteger">Whether the value is an integer.</param>
    /// <param name="Kind">Kind of control that edits the setting.</param>
    /// <param name="Choices">Names of the options of a <see cref="SettingKind.Choice"/> setting, or null for other kinds.</param>
    /// <param name="Step">Step of a <see cref="SettingKind.Slider"/> setting, or 0 for other kinds.</param>
    /// <param name="Help">Description shown in the info popup next to the setting, or null when the setting has none.</param>
    public sealed record SettingDefinition(string Key, string Label, double DefaultValue, double Minimum, double Maximum, bool IsInteger, SettingKind Kind = SettingKind.Number, IReadOnlyList<string>? Choices = null, double Step = 0.0, string? Help = null)
    {
        /// <summary>
        /// Creates a setting edited with a slider.
        /// </summary>
        /// <param name="key">Key in SharedPreferences.</param>
        /// <param name="label">Name shown on the settings screen.</param>
        /// <param name="defaultValue">Default value.</param>
        /// <param name="minimum">Smallest allowed value.</param>
        /// <param name="maximum">Largest allowed value.</param>
        /// <param name="step">Distance between two neighboring slider positions.</param>
        /// <returns>Setting definition.</returns>
        public static SettingDefinition CreateSlider(string key, string label, double defaultValue, double minimum, double maximum, double step)
        {
            return new SettingDefinition(key, label, defaultValue, minimum, maximum, false, SettingKind.Slider, null, step);
        }

        /// <summary>
        /// Creates an on/off setting stored as 0 or 1.
        /// </summary>
        /// <param name="key">Key in SharedPreferences.</param>
        /// <param name="label">Name shown on the settings screen.</param>
        /// <param name="defaultValue">Whether the setting is on by default.</param>
        /// <param name="help">Description shown in the info popup, or null.</param>
        /// <returns>Setting definition.</returns>
        public static SettingDefinition CreateSwitch(string key, string label, bool defaultValue, string? help = null)
        {
            return new SettingDefinition(key, label, defaultValue ? 1.0 : 0.0, 0.0, 1.0, true, SettingKind.Switch, null, 0.0, help);
        }

        /// <summary>
        /// Creates a drop-down setting stored as the index of the chosen option.
        /// </summary>
        /// <param name="key">Key in SharedPreferences.</param>
        /// <param name="label">Name shown on the settings screen.</param>
        /// <param name="defaultIndex">Index of the option chosen by default.</param>
        /// <param name="help">Description shown in the info popup, or null.</param>
        /// <param name="choices">Names of the options.</param>
        /// <returns>Setting definition.</returns>
        public static SettingDefinition CreateChoice(string key, string label, int defaultIndex, string? help, params string[] choices)
        {
            return new SettingDefinition(key, label, defaultIndex, 0.0, choices.Length - 1, true, SettingKind.Choice, choices, 0.0, help);
        }

        /// <summary>
        /// Limits the value to the range of the setting and rounds it when the setting is an integer.
        /// </summary>
        /// <param name="value">Value to correct.</param>
        /// <returns>Value within the range.</returns>
        public double Normalize(double value)
        {
            double clamped = Math.Clamp(value, Minimum, Maximum);

            return IsInteger ? Math.Round(clamped) : clamped;
        }
    }
}
