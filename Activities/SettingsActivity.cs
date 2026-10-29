using System.Globalization;
using AdsbCarMap.Settings;
using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities;

/// <summary>
/// Okno ustawień aplikacji współdzielonych przez mapę telefonu i Android Auto.
/// </summary>
[Activity(Label = "Ustawienia", Exported = false, Theme = "@android:style/Theme.Material")]
public class SettingsActivity : Activity
{
    /// <summary>
    /// Pola edycji ustawień według ustawienia.
    /// </summary>
    private readonly Dictionary<SettingDefinition, EditText> settingInputs = new Dictionary<SettingDefinition, EditText>();

    /// <summary>
    /// Buduje ekran ustawień i włącza strzałkę powrotu.
    /// </summary>
    /// <param name="savedInstanceState">Zapisany stan Activity.</param>
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        ActionBar?.SetDisplayHomeAsUpEnabled(true);
        SetContentView(BuildContent());
    }

    /// <summary>
    /// Zamyka okno po dotknięciu strzałki powrotu.
    /// </summary>
    /// <param name="item">Wybrana pozycja paska.</param>
    /// <returns>True, gdy obsłużono pozycję.</returns>
    public override bool OnOptionsItemSelected(IMenuItem item)
    {
        if (item.ItemId == Android.Resource.Id.Home)
        {
            Finish();

            return true;
        }

        return base.OnOptionsItemSelected(item);
    }

    /// <summary>
    /// Tworzy cały widok ekranu.
    /// </summary>
    /// <returns>Przewijany widok z polami ustawień i przyciskami.</returns>
    private View BuildContent()
    {
        ScrollView scrollView = new ScrollView(this);
        scrollView.SetFitsSystemWindows(true);

        LinearLayout root = new LinearLayout(this);
        root.Orientation = Orientation.Vertical;
        root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(32));
        scrollView.AddView(root);

        TextView version = new TextView(this);
        version.Text = "ADS-B Car Map v" + BuildInfo.Version;
        version.TextSize = 12f;
        root.AddView(version);

        foreach (SettingDefinition definition in AppSettings.All)
        {
            TextView label = new TextView(this);
            label.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}  (domyślnie {1}, zakres {2}–{3})",
                definition.Label,
                FormatValue(definition, definition.DefaultValue),
                FormatValue(definition, definition.Minimum),
                FormatValue(definition, definition.Maximum));
            label.SetPadding(0, Dp(8), 0, 0);
            label.TextSize = 13f;
            root.AddView(label);

            EditText input = new EditText(this);
            input.InputType = definition.IsInteger
                ? InputTypes.ClassNumber
                : InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;
            input.SetSingleLine(true);
            root.AddView(input);

            settingInputs[definition] = input;
        }

        LinearLayout buttons = new LinearLayout(this);
        buttons.Orientation = Orientation.Horizontal;
        buttons.SetPadding(0, Dp(16), 0, 0);
        root.AddView(buttons);

        Button saveButton = new Button(this);
        saveButton.Text = "Zapisz";
        saveButton.Click += (sender, args) => SaveSettings();
        buttons.AddView(saveButton);

        Button resetButton = new Button(this);
        resetButton.Text = "Przywróć domyślne";
        resetButton.Click += (sender, args) => ResetSettings();
        buttons.AddView(resetButton);

        LoadSettingInputs();

        return scrollView;
    }

    /// <summary>
    /// Wpisuje bieżące wartości ustawień do pól edycji.
    /// </summary>
    private void LoadSettingInputs()
    {
        foreach (KeyValuePair<SettingDefinition, EditText> entry in settingInputs)
        {
            entry.Value.Text = FormatValue(entry.Key, AppSettings.Get(entry.Key));
        }
    }

    /// <summary>
    /// Odczytuje pola edycji i zapisuje ustawienia. Puste lub niepoprawne pola pozostawiają dotychczasową wartość.
    /// </summary>
    private void SaveSettings()
    {
        Dictionary<SettingDefinition, double> values = new Dictionary<SettingDefinition, double>();

        foreach (KeyValuePair<SettingDefinition, EditText> entry in settingInputs)
        {
            string text = (entry.Value.Text ?? string.Empty).Trim().Replace(',', '.');

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                values[entry.Key] = value;
            }
        }

        AppSettings.Save(values);
        LoadSettingInputs();
        Toast.MakeText(this, "Zapisano ustawienia", ToastLength.Short)?.Show();
    }

    /// <summary>
    /// Przywraca ustawienia domyślne i odświeża pola edycji.
    /// </summary>
    private void ResetSettings()
    {
        AppSettings.ResetToDefaults();
        LoadSettingInputs();
        Toast.MakeText(this, "Przywrócono ustawienia domyślne", ToastLength.Short)?.Show();
    }

    /// <summary>
    /// Formatuje wartość ustawienia do wyświetlenia w polu edycji.
    /// </summary>
    /// <param name="definition">Ustawienie.</param>
    /// <param name="value">Wartość.</param>
    /// <returns>Tekst z kropką dziesiętną.</returns>
    private static string FormatValue(SettingDefinition definition, double value)
    {
        return value.ToString(definition.IsInteger ? "0" : "0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Przelicza dp na piksele ekranu telefonu.
    /// </summary>
    /// <param name="value">Wartość w dp.</param>
    /// <returns>Wartość w pikselach.</returns>
    private int Dp(int value)
    {
        return (int)(value * (Resources?.DisplayMetrics?.Density ?? 1.0f));
    }
}
