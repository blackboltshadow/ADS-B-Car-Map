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
                    popupLines = BuildAirportPopupLines(airport);
                    popupHex = key;
                    popupBuiltUtc = nowUtc;
                }

                return popupLines;
            }

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
        /// Builds the popup lines from the data of the selected airport.
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

            lines.Add(new PopupLine(AirportCardText.BuildTitle(airport), PopupLineStyle.Bold, 0.0f));
            lines.Add(new PopupLine(AirportCardText.BuildDescriptor(airport) + (AppServices.Favorites.Contains(airport.Icao) ? " · ★ ulubione" : string.Empty), PopupLineStyle.Accent, 0.0f));

            foreach (string text in AirportCardText.BuildLines(airport, AppServices.Airports.GetRunways(airport), known, latitude, longitude))
            {
                lines.Add(new PopupLine(text, PopupLineStyle.Normal, 0.0f));
            }

            return lines;
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
                lines.Add(new PopupLine("Samolot jest poza zasięgiem danych.", PopupLineStyle.Normal, 0.0f));

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
