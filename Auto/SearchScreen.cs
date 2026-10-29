using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Services;
using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using CarAction = AndroidX.Car.App.Model.Action;

namespace AdsbCarMap.Auto;

/// <summary>
/// Ekran Android Auto do wyszukiwania samolotów po locie, rejestracji, typie i trasie. Wybór samolotu centruje na nim mapę.
/// </summary>
public class SearchScreen : Screen
{
    /// <summary>
    /// Maksymalna liczba wyników na liście.
    /// </summary>
    private const int MaxResults = 6;

    /// <summary>
    /// Kontroler mapy, który ma podążać za wybranym samolotem.
    /// </summary>
    private readonly MapController controller;

    /// <summary>
    /// Bieżący tekst wyszukiwania.
    /// </summary>
    private string query = string.Empty;

    /// <summary>
    /// Tworzy ekran wyszukiwania powiązany z kontrolerem mapy.
    /// </summary>
    /// <param name="carContext">Kontekst samochodu.</param>
    /// <param name="controller">Kontroler mapy.</param>
    public SearchScreen(CarContext carContext, MapController controller) : base(carContext)
    {
        this.controller = controller;
    }

    /// <summary>
    /// Buduje szablon wyszukiwania z listą pasujących samolotów.
    /// </summary>
    /// <returns>Szablon wyświetlany na ekranie samochodu.</returns>
    public override ITemplate OnGetTemplate()
    {
        ItemList.Builder listBuilder = new ItemList.Builder();
        IReadOnlyList<Aircraft> aircraft = LiveTraffic.GetSnapshot().Aircraft;
        List<Aircraft> results = AircraftSearch.Search(aircraft, AppServices.Routes, query, MaxResults);

        foreach (Aircraft item in results)
        {
            string hex = item.Hex;
            Row.Builder rowBuilder = new Row.Builder()
                .SetTitle(AircraftSearch.GetName(item))
                .SetOnClickListener(new DelegateClickListener(() => SelectAircraft(hex)));

            string description = AircraftSearch.Describe(item, AppServices.Routes.Get(item.Callsign));

            if (!string.IsNullOrEmpty(description))
            {
                rowBuilder.AddText(description);
            }

            listBuilder.AddItem(rowBuilder.Build());
        }

        listBuilder.SetNoItemsMessage(string.IsNullOrWhiteSpace(query)
            ? "Wpisz lot, rejestrację, typ lub lotnisko"
            : "Brak pasujących samolotów");

        return new SearchTemplate.Builder(new DelegateSearchCallback(OnQueryChanged))
            .SetHeaderAction(CarAction.Back)
            .SetSearchHint("Lot, typ, lotnisko…")
            .SetInitialSearchText(query)
            .SetShowKeyboardByDefault(true)
            .SetItemList(listBuilder.Build())
            .Build();
    }

    /// <summary>
    /// Zapamiętuje nowy tekst wyszukiwania i odświeża listę.
    /// </summary>
    /// <param name="text">Tekst wpisany przez użytkownika.</param>
    private void OnQueryChanged(string text)
    {
        query = text;
        Invalidate();
    }

    /// <summary>
    /// Ustawia mapę na śledzenie wybranego samolotu i wraca do mapy.
    /// </summary>
    /// <param name="hex">Adres ICAO samolotu.</param>
    private void SelectAircraft(string hex)
    {
        controller.FollowAircraft(hex);
        ScreenManager.Pop();
    }
}

/// <summary>
/// Adapter przekazujący zmiany tekstu wyszukiwania Android Auto do delegata C#.
/// </summary>
public class DelegateSearchCallback : Java.Lang.Object, SearchTemplate.ISearchCallback
{
    /// <summary>
    /// Delegat wywoływany po zmianie lub zatwierdzeniu tekstu.
    /// </summary>
    private readonly System.Action<string> callback;

    /// <summary>
    /// Tworzy callback wywołujący podany delegat.
    /// </summary>
    /// <param name="callback">Delegat wywoływany z bieżącym tekstem.</param>
    public DelegateSearchCallback(System.Action<string> callback)
    {
        this.callback = callback;
    }

    /// <summary>
    /// Przekazuje zmieniony tekst do delegata.
    /// </summary>
    /// <param name="searchText">Bieżący tekst wyszukiwania.</param>
    public void OnSearchTextChanged(string searchText)
    {
        callback(searchText);
    }

    /// <summary>
    /// Przekazuje zatwierdzony tekst do delegata.
    /// </summary>
    /// <param name="searchText">Zatwierdzony tekst wyszukiwania.</param>
    public void OnSearchSubmitted(string searchText)
    {
        callback(searchText);
    }
}
