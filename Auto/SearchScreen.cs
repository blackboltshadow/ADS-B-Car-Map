using AdsbCarMap.Data;
using AdsbCarMap.Map;
using AdsbCarMap.Services;
using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using CarAction = AndroidX.Car.App.Model.Action;

namespace AdsbCarMap.Auto
{
    /// <summary>
    /// Android Auto screen for searching aircraft by flight, registration, type and route, and airports by code, name and city. Airports come first when they match equally well as aircraft. Picking an aircraft centers the map on it, isolates it and shows its popup; picking an airport selects it and centers the map on it.
    /// </summary>
    public class SearchScreen : Screen
    {
        /// <summary>
        /// Maximum number of results in the list.
        /// </summary>
        private const int MaxResults = 6;

        /// <summary>
        /// Number of rows kept for aircraft when many airports match.
        /// </summary>
        private const int ReservedForAircraft = 2;

        /// <summary>
        /// Current search text.
        /// </summary>
        private string query = string.Empty;

        /// <summary>
        /// Creates the search screen.
        /// </summary>
        /// <param name="carContext">Car context.</param>
        public SearchScreen(CarContext carContext) : base(carContext)
        {
        }

        /// <summary>
        /// Builds the search template with the list of matching airports and aircraft.
        /// </summary>
        /// <returns>Template shown on the car screen.</returns>
        public override ITemplate OnGetTemplate()
        {
            ItemList.Builder listBuilder = new ItemList.Builder();
            IReadOnlyList<Aircraft> aircraft = LiveTraffic.GetSnapshot().Aircraft;
            (double Latitude, double Longitude) center = LiveTraffic.GetCenter();
            List<SearchResult> results = AircraftSearch.SearchAll(aircraft, AppServices.Airports, AppServices.Routes, query, MaxResults, ReservedForAircraft, center.Latitude, center.Longitude);

            foreach (SearchResult result in results)
            {
                if (result.Airport != null)
                {
                    string icao = result.Airport.Icao;
                    Row.Builder airportRow = new Row.Builder()
                        .SetTitle(AircraftSearch.GetAirportTitle(result.Airport))
                        .SetOnClickListener(new DelegateClickListener(() => SelectAirport(icao)));

                    airportRow.AddText(AircraftSearch.DescribeAirport(result.Airport));
                    listBuilder.AddItem(airportRow.Build());

                    continue;
                }

                Aircraft item = result.Aircraft!;
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
                ? "Brak samolotów i lotnisk w zasięgu"
                : "Brak pasujących samolotów i lotnisk");

            return new SearchTemplate.Builder(new DelegateSearchCallback(OnQueryChanged))
                .SetHeaderAction(CarAction.Back)
                .SetSearchHint("Lot, typ, lotnisko…")
                .SetInitialSearchText(query)
                .SetShowKeyboardByDefault(true)
                .SetItemList(listBuilder.Build())
                .Build();
        }

        /// <summary>
        /// Stores the new search text and refreshes the list.
        /// </summary>
        /// <param name="text">Text typed by the user.</param>
        private void OnQueryChanged(string text)
        {
            query = text;
            Invalidate();
        }

        /// <summary>
        /// Requests showing the picked aircraft on all maps (the phone and the car): following, selection and isolation. Then returns to the map.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        private void SelectAircraft(string hex)
        {
            LiveTraffic.RequestFocus(hex);
            ScreenManager.Pop();
        }

        /// <summary>
        /// Requests selecting the picked airport and centering all maps on it (the phone and the car), then returns to the map.
        /// </summary>
        /// <param name="icao">ICAO code of the airport.</param>
        private void SelectAirport(string icao)
        {
            LiveTraffic.RequestAirportFocus(icao);
            ScreenManager.Pop();
        }
    }

    /// <summary>
    /// Adapter forwarding the Android Auto search text changes to a C# delegate.
    /// </summary>
    public class DelegateSearchCallback : Java.Lang.Object, SearchTemplate.ISearchCallback
    {
        /// <summary>
        /// Delegate invoked after the text changes or is submitted.
        /// </summary>
        private readonly System.Action<string> callback;

        /// <summary>
        /// Creates a callback that invokes the given delegate.
        /// </summary>
        /// <param name="callback">Delegate invoked with the current text.</param>
        public DelegateSearchCallback(System.Action<string> callback)
        {
            this.callback = callback;
        }

        /// <summary>
        /// Passes the changed text to the delegate.
        /// </summary>
        /// <param name="searchText">Current search text.</param>
        public void OnSearchTextChanged(string searchText)
        {
            callback(searchText);
        }

        /// <summary>
        /// Passes the submitted text to the delegate.
        /// </summary>
        /// <param name="searchText">Submitted search text.</param>
        public void OnSearchSubmitted(string searchText)
        {
            callback(searchText);
        }
    }
}
