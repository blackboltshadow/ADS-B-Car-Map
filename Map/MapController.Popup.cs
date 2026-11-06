using AdsbCarMap.Localization;
using AdsbCarMap.DataSources.Models;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Management.Caching;
using AdsbCarMap.DataSources.Management.Enrichment;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AdsbCarMap.Data;
using AdsbCarMap.Services;
using AdsbCarMap.Settings;
using Android.Graphics;
using Android.Util;
using Android.Views;
using AndroidPath = Android.Graphics.Path;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Tabs of the airport popup.
    /// </summary>
    internal enum AirportPopupTab
    {
        /// <summary>
        /// Basic data, runways and the runway in use.
        /// </summary>
        Info,

        /// <summary>
        /// Current weather and forecast.
        /// </summary>
        Weather,

        /// <summary>
        /// Radio frequencies.
        /// </summary>
        Frequencies,
    }

    /// <summary>
    /// Selection popup content, measuring and drawing of the map controller.
    /// </summary>
    public sealed partial class MapController
    {
        /// <summary>
        /// Returns the lines of the selected aircraft popup, rebuilding them when the selection changed or they are older than <see cref="PopupRefreshInterval"/>. Call from the drawing thread only.
        /// </summary>
        /// <param name="nowUtc">Start of the current frame, in UTC.</param>
        /// <returns>Popup lines, or null when no aircraft is selected.</returns>
        private IReadOnlyList<PopupLine>? GetPopupLines(DateTime nowUtc)
        {
            string? hex;
            Airport? airport;

            lock (stateLock)
            {
                hex = primaryHex;
                airport = selectedAirport;
            }

            if (hex == null && airport != null)
            {
                string key = "airport:" + airport.Icao;

                if (popupLines == null || popupHex != key || nowUtc - popupBuiltUtc >= PopupRefreshInterval)
                {
                    if (popupHex != key)
                    {
                        popupTab = AirportPopupTab.Info;
                    }

                    popupLines = BuildAirportPopupLines(airport);
                    popupHex = key;
                    popupBuiltUtc = nowUtc;
                }

                return popupLines;
            }

            popupTabs = null;
            popupTabIds = null;

            if (hex == null)
            {
                popupLines = null;
                popupHex = null;

                return null;
            }

            IReadOnlyList<PopupLine>? current = popupLines;

            if (current != null && popupHex == hex && nowUtc - popupBuiltUtc < PopupRefreshInterval)
            {
                return current;
            }

            SelectedAircraftInfo? info = GetPrimaryInfo();

            if (info == null)
            {
                popupLines = null;
                popupHex = null;

                return null;
            }

            popupLines = BuildPopupLines(info);
            popupHex = hex;
            popupBuiltUtc = nowUtc;

            return popupLines;
        }

        /// <summary>
        /// Builds the popup lines of the shown tab from the data of the selected airport and sets the tabs of the popup.
        /// </summary>
        /// <param name="airport">Selected airport.</param>
        /// <returns>Popup lines.</returns>
        private List<PopupLine> BuildAirportPopupLines(Airport airport)
        {
            List<PopupLine> lines = new List<PopupLine>();
            bool known;
            double latitude;
            double longitude;

            lock (stateLock)
            {
                known = hasUserLocation;
                latitude = userLatitude;
                longitude = userLongitude;
            }

            IReadOnlyList<Runway> runways = AppServices.Airports.GetRunways(airport);
            List<string> frequencies = AppSettings.ShowAirportFrequencies ? AirportCardText.BuildFrequencyLines(AppServices.Airports.GetFrequencies(airport)) : new List<string>();
            bool hasWeather = AppSettings.ShowAirportWeather || AppSettings.ShowAirportForecast;
            List<AirportPopupTab> tabIds = new List<AirportPopupTab> { AirportPopupTab.Info };
            List<string> tabTitles = new List<string> { Loc.T("Informacje") };

            if (hasWeather)
            {
                tabIds.Add(AirportPopupTab.Weather);
                tabTitles.Add(Loc.T("Pogoda"));
            }

            if (frequencies.Count > 0)
            {
                tabIds.Add(AirportPopupTab.Frequencies);
                tabTitles.Add(Loc.T("Częstotliwości"));
            }

            AirportPopupTab shown = tabIds.Contains(popupTab) ? popupTab : AirportPopupTab.Info;

            popupTabIds = tabIds;
            popupTabs = tabIds.Count > 1 ? tabTitles : null;

            lines.Add(new PopupLine(AirportCardText.BuildTitle(airport), PopupLineStyle.Bold, 0.0f));

            if (shown == AirportPopupTab.Info)
            {
                lines.Add(new PopupLine(AirportCardText.BuildDescriptor(airport) + (AppServices.Favorites.Contains(airport.Icao) ? Loc.T(" · ★ ulubione") : string.Empty), PopupLineStyle.Accent, 0.0f));

                foreach (string text in AirportCardText.BuildLines(airport, runways, known, latitude, longitude))
                {
                    lines.Add(new PopupLine(text, PopupLineStyle.Normal, 0.0f));
                }

                string trafficLine = AirportRunwayTraffic.BuildLine(airport, runways);

                if (trafficLine.Length > 0)
                {
                    lines.Add(new PopupLine(trafficLine, PopupLineStyle.Normal, 0.0f));
                }
            }
            else if (shown == AirportPopupTab.Weather)
            {
                AirportWeatherLoader.Snapshot snapshot = AirportWeatherLoader.Request(airport);
                DisplayUnits units = AppSettings.Units;

                if (AppSettings.ShowAirportWeather)
                {
                    List<string> weather = AirportWeatherText.BuildPopupLines(snapshot.Weather, snapshot.WeatherLoading, units, DateTime.UtcNow, runways);

                    for (int index = 0; index < weather.Count; index++)
                    {
                        lines.Add(new PopupLine(weather[index], index == 0 ? PopupLineStyle.Accent : PopupLineStyle.Normal, 0.0f));
                    }
                }

                if (AppSettings.ShowAirportForecast)
                {
                    foreach (string text in AirportForecastText.BuildPopupLines(snapshot.Forecast, snapshot.ForecastLoading, units, AirportForecastText.GetZone(AppSettings.ForecastUsesLocalTime), DateTime.UtcNow))
                    {
                        lines.Add(new PopupLine(text, PopupLineStyle.Accent, 0.0f));
                    }
                }
            }
            else
            {
                lines.Add(new PopupLine(Loc.T("Częstotliwości (MHz):"), PopupLineStyle.Accent, 0.0f));

                foreach (string text in frequencies)
                {
                    lines.Add(new PopupLine(text, PopupLineStyle.Normal, 0.0f));
                }
            }

            return lines;
        }

        /// <summary>
        /// Switches the tab of the airport popup when the tap hit its tab strip.
        /// </summary>
        /// <param name="x">Horizontal tap position in pixels.</param>
        /// <param name="y">Vertical tap position in pixels.</param>
        /// <returns>True when the tap was used to switch the tab.</returns>
        private bool TrySelectPopupTab(float x, float y)
        {
            RectF? area = Volatile.Read(ref popupBounds);
            IReadOnlyList<string>? tabs = popupTabs;
            List<AirportPopupTab>? ids = popupTabIds;

            if (area == null || tabs == null || ids == null)
            {
                return false;
            }

            int index = PopupRenderer.HitTab(area, tabs.Count, x, y, density);

            if (index < 0 || index >= ids.Count)
            {
                return false;
            }

            popupTab = ids[index];
            popupBuiltUtc = DateTime.MinValue;

            return true;
        }

        /// <summary>
        /// Builds the popup lines from the data of the primary selected aircraft.
        /// </summary>
        /// <param name="info">Data of the selected aircraft and the user position.</param>
        /// <returns>Popup lines.</returns>
        private List<PopupLine> BuildPopupLines(SelectedAircraftInfo info)
        {
            List<PopupLine> lines = new List<PopupLine>();
            Aircraft? aircraft = info.Aircraft;
            string title = AircraftCardText.BuildTitle(info);

            if (aircraft == null)
            {
                lines.Add(new PopupLine(title, PopupLineStyle.Bold, 0.0f));
                lines.Add(new PopupLine(Loc.T("Samolot jest poza zasięgiem danych."), PopupLineStyle.Normal, 0.0f));

                return lines;
            }

            if (!string.IsNullOrEmpty(aircraft.Registration))
            {
                title += "  ·  " + aircraft.Registration;
            }

            if (aircraft.Callsign != popupPrioritizedCallsign)
            {
                routeCache.Prioritize(aircraft.Callsign);
                popupPrioritizedCallsign = aircraft.Callsign;
            }

            FlightRoute? route = routeCache.Get(aircraft.Callsign);
            RouteLookupState routeState = routeCache.GetState(aircraft.Callsign);
            string descriptors = AircraftCardText.BuildDescriptors(aircraft, route);

            lines.Add(new PopupLine(title, PopupLineStyle.Bold, 0.0f));

            if (descriptors.Length > 0)
            {
                lines.Add(new PopupLine(descriptors, PopupLineStyle.Accent, 0.0f));
            }

            lines.Add(new PopupLine(AircraftCardText.BuildRouteText(route, routeState), route == null ? PopupLineStyle.Normal : PopupLineStyle.Bold, 0.0f));

            if (AircraftCardText.TryBuildProgress(info, aircraft, route, out float fraction, out string progress))
            {
                lines.Add(new PopupLine(progress, PopupLineStyle.Progress, fraction));
            }

            List<string> leftLines = AircraftCardText.BuildLeftLines(info, aircraft);

            for (int index = 0; index < leftLines.Count - 1; index++)
            {
                lines.Add(new PopupLine(leftLines[index], PopupLineStyle.Normal, 0.0f));
            }

            foreach (string rightLine in AircraftCardText.BuildRightLines(info, aircraft))
            {
                lines.Add(new PopupLine(rightLine, PopupLineStyle.Normal, 0.0f));
            }

            return lines;
        }
    }
}
