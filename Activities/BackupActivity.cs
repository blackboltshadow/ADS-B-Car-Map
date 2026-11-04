using System.Globalization;
using System.Text;
using AdsbCarMap.Data;
using AdsbCarMap.Localization;
using AdsbCarMap.Services;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace AdsbCarMap.Activities
{
    /// <summary>
    /// Screen of the backup of the application data: the choice of what the system backup contains, its size and state, buttons that prepare and delete it, and the export and import of the same data to a file.
    /// </summary>
    [Activity(Label = "Kopia zapasowa", Exported = false, Theme = "@android:style/Theme.Material")]
    public class BackupActivity : Activity
    {
        /// <summary>
        /// Code of the request that asks for the place of the exported file.
        /// </summary>
        private const int ExportRequestCode = 4101;

        /// <summary>
        /// Code of the request that asks for the file to import.
        /// </summary>
        private const int ImportRequestCode = 4102;

        /// <summary>
        /// Time in seconds that the button that deletes the backup stays disabled after the question is shown.
        /// </summary>
        private const int DeleteDelaySeconds = 10;

        /// <summary>
        /// Tag used in logs.
        /// </summary>
        private const string LogTag = "BackupActivity";

        /// <summary>
        /// Check boxes of the categories by identifier.
        /// </summary>
        private readonly Dictionary<string, CheckBox> boxes = new Dictionary<string, CheckBox>();

        /// <summary>
        /// Sizes of the data of the categories in bytes, or null while they are being measured.
        /// </summary>
        private Dictionary<string, long>? sizes;

        /// <summary>
        /// Switch of the system backup.
        /// </summary>
        private Switch? enabledSwitch;

        /// <summary>
        /// What the system backup holds compared with the current data, or null while it is being worked out.
        /// </summary>
        private BackupSyncState? syncState;

        /// <summary>
        /// Line that says at once whether the newest data is in the backup.
        /// </summary>
        private TextView? markerTitle;

        /// <summary>
        /// Explanation under the marker.
        /// </summary>
        private TextView? markerDetail;

        /// <summary>
        /// Text with the state of the backup.
        /// </summary>
        private TextView? statusText;

        /// <summary>
        /// Text with the size of the whole backup.
        /// </summary>
        private TextView? totalText;

        /// <summary>
        /// Whether the check boxes and the switch are being set from the saved state, which must not be taken as a change by the user.
        /// </summary>
        private bool updatingControls;

        /// <summary>
        /// Builds the screen.
        /// </summary>
        /// <param name="savedInstanceState">Saved state, not used.</param>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Title = Loc.T("Kopia zapasowa");

            ActionBar?.SetDisplayHomeAsUpEnabled(true);
            SetContentView(BuildContent());
        }

        /// <summary>
        /// Shows the current state and measures the data every time the screen comes back.
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();

            RefreshControls();
            MeasureInBackground();
        }

        /// <summary>
        /// Closes the screen on the arrow in the action bar.
        /// </summary>
        /// <param name="item">Chosen item.</param>
        /// <returns>True when handled.</returns>
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
        /// Handles the places chosen in the system pickers: writes the exported file or reads the file to import.
        /// </summary>
        /// <param name="requestCode">Code of the request.</param>
        /// <param name="resultCode">Result of the picker.</param>
        /// <param name="data">Intent with the chosen file.</param>
        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (resultCode != Result.Ok || data?.Data == null)
            {
                return;
            }

            if (requestCode == ExportRequestCode)
            {
                WriteExport(data.Data);
            }
            else if (requestCode == ImportRequestCode)
            {
                ReadImport(data.Data);
            }
        }

        /// <summary>
        /// Creates the whole view: the explanation, the state, the choice of the categories and the buttons.
        /// </summary>
        /// <returns>The root view.</returns>
        private View BuildContent()
        {
            ScrollView scrollView = new ScrollView(this);
            scrollView.SetFitsSystemWindows(true);

            LinearLayout root = new LinearLayout(this);
            root.Orientation = Orientation.Vertical;
            root.SetPadding(Dp(12), Dp(8), Dp(12), Dp(24));
            scrollView.AddView(root);

            LinearLayout info = CreateCard(root);
            info.AddView(CreateText(Loc.T("Kopię zapasową wykonuje system Android i zapisuje ją, zaszyfrowaną, na Dysku Google. Dzieje się to zwykle raz na dobę, gdy telefon się ładuje, ma Wi-Fi i jest bezczynny. Aplikacja nie może tego wymusić, może tylko zgłosić prośbę. Kopia wraca sama przy instalacji aplikacji z Google Play i przy przenoszeniu danych na nowy telefon, o ile kopia Google jest włączona w ustawieniach systemu. Wersja Debug i wydanie mają osobne kopie. Pamięć podręczna, kafelki i Dev log nigdy nie trafiają do kopii."), 13f, Color.Rgb(190, 190, 190)));

            LinearLayout state = CreateCard(root);
            state.AddView(CreateText(Loc.T("Stan kopii"), 18f, Color.White, true));

            markerTitle = CreateText(string.Empty, 15f, Color.White, true);
            markerTitle.SetPadding(0, Dp(6), 0, 0);
            markerTitle.Visibility = ViewStates.Gone;
            state.AddView(markerTitle);

            markerDetail = CreateText(string.Empty, 12f, Color.Rgb(190, 190, 190));
            markerDetail.SetPadding(0, 0, 0, Dp(8));
            markerDetail.Visibility = ViewStates.Gone;
            state.AddView(markerDetail);

            enabledSwitch = new Switch(this);
            enabledSwitch.Text = Loc.T("Zapisuj kopię na koncie Google");
            enabledSwitch.CheckedChange += (sender, args) => OnEnabledChanged(args.IsChecked);
            state.AddView(enabledSwitch);
            state.AddView(CreateText(Loc.T("Wyłączenie usuwa kopię z konta Google przy najbliższej kopii systemowej."), 12f, Color.Rgb(160, 160, 160)));

            statusText = CreateText(string.Empty, 13f, Color.White);
            statusText.SetPadding(0, Dp(8), 0, 0);
            state.AddView(statusText);

            LinearLayout categories = CreateCard(root);
            categories.AddView(CreateText(Loc.T("Co zapisywać w kopii"), 18f, Color.White, true));

            foreach (BackupCategory category in BackupCategories.All)
            {
                AddCategoryRow(categories, category);
            }

            totalText = CreateText(string.Empty, 14f, Color.White, true);
            totalText.SetPadding(0, Dp(10), 0, 0);
            categories.AddView(totalText);

            LinearLayout actions = CreateCard(root);
            actions.AddView(CreateText(Loc.T("Kopia w systemie"), 18f, Color.White, true));

            Button saveButton = new Button(this);
            saveButton.Text = Loc.T("Zapisz kopię teraz");
            saveButton.Click += (sender, args) => SaveNow();
            actions.AddView(saveButton);

            Button deleteButton = new Button(this);
            deleteButton.Text = Loc.T("Usuń kopię…");
            deleteButton.SetTextColor(Color.Rgb(255, 120, 120));
            deleteButton.Click += (sender, args) => ConfirmDelete();
            actions.AddView(deleteButton);

            LinearLayout file = CreateCard(root);
            file.AddView(CreateText(Loc.T("Plik"), 18f, Color.White, true));
            file.AddView(CreateText(Loc.T("Te same dane, według tego samego wyboru, można zapisać do pliku (np. na Dysk Google lub w Pobranych) i wczytać na innym telefonie albo w innej wersji aplikacji, na przykład z wersji Debug do wydania."), 12f, Color.Rgb(160, 160, 160)));

            Button exportButton = new Button(this);
            exportButton.Text = Loc.T("Eksportuj do pliku");
            exportButton.Click += (sender, args) => PickExportFile();
            file.AddView(exportButton);

            Button importButton = new Button(this);
            importButton.Text = Loc.T("Importuj z pliku");
            importButton.Click += (sender, args) => PickImportFile();
            file.AddView(importButton);

            return scrollView;
        }

        /// <summary>
        /// Adds the row of a category: the check box with the name and the size, and the description.
        /// </summary>
        /// <param name="container">Card of the categories.</param>
        /// <param name="category">Category.</param>
        private void AddCategoryRow(LinearLayout container, BackupCategory category)
        {
            CheckBox box = new CheckBox(this);
            box.Text = Loc.T(category.Title);
            box.TextSize = 15f;
            box.CheckedChange += (sender, args) => OnCategoryChanged();
            boxes[category.Id] = box;
            container.AddView(box);

            TextView description = CreateText(Loc.T(category.Description), 12f, Color.Rgb(160, 160, 160));
            description.SetPadding(Dp(36), 0, 0, Dp(4));
            container.AddView(description);
        }

        /// <summary>
        /// Sets the switch, the check boxes and the texts from the saved state.
        /// </summary>
        private void RefreshControls()
        {
            updatingControls = true;

            enabledSwitch!.Checked = BackupConfigStore.Enabled;
            List<string> selection = BackupConfigStore.Selection;

            foreach (BackupCategory category in BackupCategories.All)
            {
                bool waiting = syncState != null && syncState.Active && syncState.PendingIds.Contains(category.Id) && selection.Contains(category.Id);

                boxes[category.Id].Checked = selection.Contains(category.Id);
                boxes[category.Id].Text = Loc.T(category.Title) + (sizes == null ? string.Empty : " · " + FormatSize(sizes[category.Id])) + (waiting ? " ⏳" : string.Empty);
                boxes[category.Id].Enabled = BackupConfigStore.Enabled;
            }

            updatingControls = false;
            UpdateMarker();

            totalText!.Text = sizes == null
                ? Loc.T("Liczę rozmiar…")
                : Loc.F("Razem w kopii: {0}", FormatSize(selection.Sum((string id) => sizes.TryGetValue(id, out long size) ? size : 0L)));
            statusText!.Text = BuildStatus();
        }

        /// <summary>
        /// Shows at the top of the state whether the newest data is in the backup that the system holds, and which categories wait to be taken. The system takes the data by itself, so the text says that the application cannot hurry it.
        /// </summary>
        private void UpdateMarker()
        {
            if (syncState == null)
            {
                markerTitle!.Visibility = ViewStates.Gone;
                markerDetail!.Visibility = ViewStates.Gone;

                return;
            }

            markerTitle!.Visibility = ViewStates.Visible;

            if (!syncState.Active)
            {
                markerTitle.Text = Loc.T("Kopia nie jest przesyłana");
                markerTitle.SetTextColor(Color.Rgb(160, 160, 160));
                markerDetail!.Text = BackupConfigStore.Enabled
                    ? Loc.T("Nie wybrano żadnej kategorii, więc nic nie trafia na konto Google.")
                    : Loc.T("Kopia jest wyłączona, więc nic nie trafia na konto Google.");
                markerDetail.Visibility = ViewStates.Visible;

                return;
            }

            if (syncState.IsUpToDate)
            {
                markerTitle.Text = Loc.T("✓ Kopia na koncie Google jest aktualna");
                markerTitle.SetTextColor(Color.Rgb(120, 220, 140));
                markerDetail!.Visibility = ViewStates.Gone;

                return;
            }

            string waiting = string.Join(", ", syncState.PendingIds.Select((string id) => Loc.T(BackupCategories.Find(id)!.Title)));

            markerTitle.Text = Loc.T("⏳ Najnowsza wersja czeka na przesłanie");
            markerTitle.SetTextColor(Color.Rgb(255, 190, 80));
            markerDetail!.Text = (syncState.EverDelivered
                ? Loc.T("System zrobi to sam, zwykle w nocy, gdy telefon się ładuje i ma Wi-Fi. Aplikacja nie ma na to wpływu. Do tego czasu kopia na koncie Google nie zawiera najnowszych zmian w: ")
                : Loc.T("System nie przesłał jeszcze tych danych na konto Google. Zrobi to sam, zwykle w nocy, gdy telefon się ładuje i ma Wi-Fi, a aplikacja nie ma na to wpływu. Czeka: ")) + waiting + ".";
            markerDetail.Visibility = ViewStates.Visible;
        }

        /// <summary>
        /// Builds the text with the state of the backup.
        /// </summary>
        /// <returns>The lines of the state.</returns>
        private string BuildStatus()
        {
            StringBuilder text = new StringBuilder();

            text.AppendLine(BackupConfigStore.Enabled ? Loc.T("Kopia: włączona") : Loc.T("Kopia: wyłączona"));

            DateTime? prepared = BackupConfigStore.PreparedUtc;
            text.AppendLine(prepared == null
                ? Loc.T("Plik kopii nie jest przygotowany.")
                : Loc.F("Przygotowano: {0} ({1})", FormatTime(prepared.Value), FormatSize(BackupConfigStore.PreparedBytes)));

            DateTime? requested = BackupConfigStore.RequestedUtc;
            text.AppendLine(requested == null ? Loc.T("Prośba o kopię: jeszcze nie zgłoszona.") : Loc.F("Prośba o kopię zgłoszona: {0}", FormatTime(requested.Value)));

            DateTime? systemRead = BackupConfigStore.SystemReadUtc;
            text.AppendLine(systemRead == null
                ? Loc.T("System jeszcze nie pobrał kopii.")
                : Loc.F("System pobrał kopię: {0} ({1})", FormatTime(systemRead.Value), FormatSize(BackupConfigStore.SystemReadBytes)));

            DateTime? restored = BackupConfigStore.RestoredUtc;

            if (restored != null)
            {
                text.AppendLine(Loc.F("Ostatnie przywrócenie z kopii: {0}", FormatTime(restored.Value)));
            }

            DateTime? deleted = BackupConfigStore.DeletedUtc;

            if (deleted != null)
            {
                text.AppendLine(Loc.F("Kopia usunięta: {0}", FormatTime(deleted.Value)));
            }

            return text.ToString().TrimEnd();
        }

        /// <summary>
        /// Measures the data of the categories on a background thread and shows the sizes.
        /// </summary>
        private void MeasureInBackground()
        {
            Task.Run(() =>
            {
                try
                {
                    Dictionary<string, long> measured = BackupService.MeasureCategories(this);
                    BackupSyncState state = BackupService.GetSyncState(this);

                    RunOnUiThread(() =>
                    {
                        sizes = measured;
                        syncState = state;
                        RefreshControls();
                    });
                }
                catch (Exception exception)
                {
                    DevLogger.Error(LogTag, "Nie udało się zmierzyć danych kopii", exception);
                }
            });
        }

        /// <summary>
        /// Switches the system backup on or off. Switching it on prepares the backup again, switching it off removes the prepared file, so the system replaces the backup in the account with an empty one.
        /// </summary>
        /// <param name="enabled">New state of the switch.</param>
        private void OnEnabledChanged(bool enabled)
        {
            if (updatingControls)
            {
                return;
            }

            BackupConfigStore.Enabled = enabled;
            PrepareInBackground(false, null);
            RefreshControls();
        }

        /// <summary>
        /// Saves the choice of the categories and prepares the backup again.
        /// </summary>
        private void OnCategoryChanged()
        {
            if (updatingControls)
            {
                return;
            }

            BackupConfigStore.Selection = boxes.Where((KeyValuePair<string, CheckBox> pair) => pair.Value.Checked).Select((KeyValuePair<string, CheckBox> pair) => pair.Key).ToList();
            PrepareInBackground(false, null);
            RefreshControls();
        }

        /// <summary>
        /// Prepares the backup file now and asks the system for a backup.
        /// </summary>
        private void SaveNow()
        {
            if (!BackupConfigStore.Enabled)
            {
                Toast.MakeText(this, Loc.T("Kopia jest wyłączona. Włącz ją przełącznikiem powyżej."), ToastLength.Long)?.Show();

                return;
            }

            if (BackupConfigStore.Selection.Count == 0)
            {
                Toast.MakeText(this, Loc.T("Nie wybrano żadnej kategorii."), ToastLength.Long)?.Show();

                return;
            }

            PrepareInBackground(true, Loc.T("Kopia przygotowana. System wyśle ją na konto Google przy najbliższej okazji, zwykle w ciągu doby, gdy telefon się ładuje i ma Wi-Fi."));
        }

        /// <summary>
        /// Prepares the backup file on a background thread, optionally asks the system for a backup and shows a message.
        /// </summary>
        /// <param name="requestBackup">Whether to ask the system for a backup afterwards.</param>
        /// <param name="message">Message shown when the work is done, or null for none.</param>
        private void PrepareInBackground(bool requestBackup, string? message)
        {
            Task.Run(() =>
            {
                try
                {
                    BackupService.StageForSystem(this);

                    if (requestBackup || !BackupConfigStore.Enabled)
                    {
                        BackupService.RequestSystemBackup(this);
                    }

                    RunOnUiThread(() =>
                    {
                        RefreshControls();
                        MeasureInBackground();

                        if (message != null)
                        {
                            Toast.MakeText(this, message, ToastLength.Long)?.Show();
                        }
                    });
                }
                catch (Exception exception)
                {
                    DevLogger.Error(LogTag, "Nie udało się przygotować kopii", exception);
                    RunOnUiThread(() => Toast.MakeText(this, Loc.T("Nie udało się przygotować kopii: ") + exception.Message, ToastLength.Long)?.Show());
                }
            });
        }

        /// <summary>
        /// Asks whether to delete the backup. The button that confirms stays disabled for a few seconds, so that it cannot be tapped by accident.
        /// </summary>
        private void ConfirmDelete()
        {
            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(Loc.T("Usunąć kopię?"));
            builder.SetMessage(Loc.T("Aplikacja przestanie przygotowywać kopię (kopia zostanie wyłączona), a system zastąpi kopię na koncie Google pustą przy najbliższej kopii. Możesz ją też usunąć ręcznie: Ustawienia systemu → Google → Kopia zapasowa. Dane na tym telefonie nie zostaną zmienione."));
            builder.SetPositiveButton(Loc.T("Usuń"), (sender, args) => DeleteBackup());
            builder.SetNegativeButton(Loc.T("Anuluj"), (sender, args) => { });

            AlertDialog dialog = builder.Create()!;
            dialog.Show();

            Button? confirm = dialog.GetButton((int)DialogButtonType.Positive);

            if (confirm == null)
            {
                return;
            }

            confirm.Enabled = false;

            Handler handler = new Handler(Looper.MainLooper!);
            int remaining = DeleteDelaySeconds;
            bool open = true;
            Action? tick = null;

            dialog.DismissEvent += (sender, args) => open = false;
            tick = () =>
            {
                if (!open)
                {
                    return;
                }

                if (remaining <= 0)
                {
                    confirm.Text = Loc.T("Usuń");
                    confirm.Enabled = true;

                    return;
                }

                confirm.Text = Loc.T("Usuń") + " (" + remaining + ")";
                remaining--;
                handler.PostDelayed(tick!, 1000);
            };
            tick();
        }

        /// <summary>
        /// Switches the backup off, removes the prepared file and asks the system for a backup, which then replaces the backup in the account with an empty one.
        /// </summary>
        private void DeleteBackup()
        {
            BackupConfigStore.Enabled = false;

            Task.Run(() =>
            {
                try
                {
                    BackupService.DeleteStaged(this);
                    BackupConfigStore.RecordDeleted();
                    BackupService.RequestSystemBackup(this);
                }
                catch (Exception exception)
                {
                    DevLogger.Error(LogTag, "Nie udało się usunąć kopii", exception);
                }

                RunOnUiThread(() =>
                {
                    RefreshControls();
                    MeasureInBackground();
                    Toast.MakeText(this, Loc.T("Kopia usunięta i wyłączona. System zastąpi kopię na koncie Google pustą przy najbliższej kopii."), ToastLength.Long)?.Show();
                });
            });
        }

        /// <summary>
        /// Opens the system dialog that asks where to save the exported file.
        /// </summary>
        private void PickExportFile()
        {
            if (BackupConfigStore.Selection.Count == 0)
            {
                Toast.MakeText(this, Loc.T("Nie wybrano żadnej kategorii."), ToastLength.Long)?.Show();

                return;
            }

            Intent intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("application/json");
            intent.PutExtra(Intent.ExtraTitle, "adsbcarmap-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".json");

            try
            {
                StartActivityForResult(intent, ExportRequestCode);
            }
            catch (ActivityNotFoundException)
            {
                Toast.MakeText(this, Loc.T("Brak aplikacji do wyboru plików"), ToastLength.Long)?.Show();
            }
        }

        /// <summary>
        /// Writes the backup of the chosen categories to the file that the user picked.
        /// </summary>
        /// <param name="uri">Place of the file.</param>
        private void WriteExport(Android.Net.Uri uri)
        {
            try
            {
                string text = BackupService.BuildBundle(this, BackupConfigStore.Selection).Serialize();

                using (Stream? output = ContentResolver?.OpenOutputStream(uri, "wt"))
                {
                    if (output == null)
                    {
                        throw new IOException("Brak dostępu do pliku");
                    }

                    byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                    output.Write(bytes, 0, bytes.Length);
                }

                Toast.MakeText(this, Loc.F("Zapisano plik ({0})", FormatSize(Encoding.UTF8.GetByteCount(text))), ToastLength.Long)?.Show();
            }
            catch (Exception exception)
            {
                DevLogger.Error(LogTag, "Eksport kopii do pliku nie powiódł się", exception);
                Toast.MakeText(this, Loc.T("Nie udało się zapisać pliku: ") + exception.Message, ToastLength.Long)?.Show();
            }
        }

        /// <summary>
        /// Opens the system file picker to choose the backup file.
        /// </summary>
        private void PickImportFile()
        {
            Intent intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");

            try
            {
                StartActivityForResult(intent, ImportRequestCode);
            }
            catch (ActivityNotFoundException)
            {
                Toast.MakeText(this, Loc.T("Brak aplikacji do wyboru plików"), ToastLength.Long)?.Show();
            }
        }

        /// <summary>
        /// Reads the chosen file and asks which of its categories to import.
        /// </summary>
        /// <param name="uri">Place of the file.</param>
        private void ReadImport(Android.Net.Uri uri)
        {
            string text;

            try
            {
                using (Stream? input = ContentResolver?.OpenInputStream(uri))
                {
                    if (input == null)
                    {
                        throw new IOException("Brak dostępu do pliku");
                    }

                    using (StreamReader reader = new StreamReader(input, Encoding.UTF8))
                    {
                        text = reader.ReadToEnd();
                    }
                }
            }
            catch (Exception exception)
            {
                DevLogger.Error(LogTag, "Odczyt pliku kopii nie powiódł się", exception);
                Toast.MakeText(this, Loc.T("Nie udało się odczytać pliku: ") + exception.Message, ToastLength.Long)?.Show();

                return;
            }

            BackupParseStatus status = BackupBundle.TryParse(text, out BackupBundle? bundle);

            if (status == BackupParseStatus.NewerFormat)
            {
                Toast.MakeText(this, Loc.T("Plik pochodzi z nowszej wersji aplikacji. Zaktualizuj aplikację, aby go wczytać."), ToastLength.Long)?.Show();

                return;
            }

            if (status != BackupParseStatus.Ok || bundle == null)
            {
                Toast.MakeText(this, Loc.T("To nie jest plik kopii zapasowej tej aplikacji."), ToastLength.Long)?.Show();

                return;
            }

            ChooseImportedCategories(bundle);
        }

        /// <summary>
        /// Shows the categories that the file contains and imports the ones the user confirms.
        /// </summary>
        /// <param name="bundle">Backup read from the file.</param>
        private void ChooseImportedCategories(BackupBundle bundle)
        {
            List<BackupCategory> available = BackupCategories.All.Where((BackupCategory category) => bundle.Find(category.Id) != null).ToList();

            if (available.Count == 0)
            {
                Toast.MakeText(this, Loc.T("Plik nie zawiera żadnych danych do wczytania."), ToastLength.Long)?.Show();

                return;
            }

            string[] labels = available.Select((BackupCategory category) => Loc.T(category.Title)).ToArray();
            bool[] chosen = available.Select((BackupCategory category) => true).ToArray();

            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(Loc.F("Wczytać z pliku (kopia z {0}, wersja {1})", FormatTime(bundle.CreatedUtc), bundle.AppVersion));
            builder.SetMultiChoiceItems(labels, chosen, (sender, args) => chosen[args.Which] = args.IsChecked);
            builder.SetPositiveButton(Loc.T("Zastąp dane na tym telefonie"), (sender, args) =>
            {
                List<string> ids = available.Where((BackupCategory category, int index) => chosen[index]).Select((BackupCategory category) => category.Id).ToList();

                ApplyImport(bundle, ids);
            });
            builder.SetNegativeButton(Loc.T("Anuluj"), (sender, args) => { });
            builder.Show();
        }

        /// <summary>
        /// Writes the chosen categories of the imported backup and tells the user to start the application again, because the data in memory is old.
        /// </summary>
        /// <param name="bundle">Backup to apply.</param>
        /// <param name="ids">Identifiers of the categories to write.</param>
        private void ApplyImport(BackupBundle bundle, List<string> ids)
        {
            if (ids.Count == 0)
            {
                return;
            }

            try
            {
                List<string> applied = BackupService.ApplyBundle(this, bundle, ids);

                DevLogger.Info(LogTag, "Wczytano z pliku kategorie: " + string.Join(", ", applied));
            }
            catch (Exception exception)
            {
                DevLogger.Error(LogTag, "Wczytanie kopii z pliku nie powiodło się", exception);
                Toast.MakeText(this, Loc.T("Nie udało się wczytać danych: ") + exception.Message, ToastLength.Long)?.Show();

                return;
            }

            AlertDialog.Builder builder = new AlertDialog.Builder(this)!;
            builder.SetTitle(Loc.T("Dane wczytane"));
            builder.SetMessage(Loc.T("Aplikacja zamknie się teraz, żeby użyć wczytanych danych. Uruchom ją ponownie."));
            builder.SetCancelable(false);
            builder.SetPositiveButton(Loc.T("Zamknij aplikację"), (sender, args) =>
            {
                FinishAffinity();
                Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
            });
            builder.Show();
        }

        /// <summary>
        /// Writes a size for the screen, with the decimal separator of the language of the application.
        /// </summary>
        /// <param name="bytes">Size in bytes.</param>
        /// <returns>The text.</returns>
        private static string FormatSize(long bytes)
        {
            return ByteSizeFormatter.Format(bytes, Loc.Language == AppLanguage.Polish ? new CultureInfo("pl-PL") : CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Writes a time in the local time zone.
        /// </summary>
        /// <param name="timeUtc">Time in UTC.</param>
        /// <returns>Text such as "04.11.2026 12:30".</returns>
        private static string FormatTime(DateTime timeUtc)
        {
            return timeUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Creates a card in a container.
        /// </summary>
        /// <param name="container">Container of the screen.</param>
        /// <returns>The card.</returns>
        private LinearLayout CreateCard(LinearLayout container)
        {
            LinearLayout card = new LinearLayout(this);
            card.Orientation = Orientation.Vertical;
            card.SetPadding(Dp(16), Dp(12), Dp(16), Dp(12));
            card.Background = HistoryViews.CardBackground(this);

            LinearLayout.LayoutParams parameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            parameters.SetMargins(0, Dp(5), 0, Dp(5));
            container.AddView(card, parameters);

            return card;
        }

        /// <summary>
        /// Creates a text.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="size">Size in sp.</param>
        /// <param name="color">Color.</param>
        /// <param name="bold">Whether the text is bold.</param>
        /// <returns>The text view.</returns>
        private TextView CreateText(string text, float size, Color color, bool bold = false)
        {
            TextView view = new TextView(this);
            view.Text = text;
            view.TextSize = size;
            view.SetTextColor(color);

            if (bold)
            {
                view.SetTypeface(null, TypefaceStyle.Bold);
            }

            return view;
        }

        /// <summary>
        /// Converts dp to pixels.
        /// </summary>
        /// <param name="value">Value in dp.</param>
        /// <returns>Value in pixels.</returns>
        private int Dp(int value)
        {
            return HistoryViews.Dp(this, value);
        }
    }
}
