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
    /// Aircraft and airport selection, following, shared selection state and user location of the map controller.
    /// </summary>
    public sealed partial class MapController
    {
        /// <summary>
        /// Makes the map center follow an aircraft and turns off following of the user position.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        public void FollowAircraft(string hex)
        {
            FollowAircraft(hex, false);
        }

        /// <summary>
        /// When the aircraft is not in this map's data, e.g. because it was selected on the other screen far from this map's area, copies it from the data shared with the other map, moves the map to it and requests an immediate download around it.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft, or null.</param>
        private void RevealAircraftIfMissing(string? hex)
        {
            if (hex == null)
            {
                return;
            }

            lock (stateLock)
            {
                if (latestAircraft.Any(item => item.Hex == hex))
                {
                    return;
                }
            }

            SelectionSnapshot published = selectionState.Current;
            Aircraft? found = published.PrimaryHex == hex ? published.PrimaryAircraft : null;
            DateTime foundTimestampUtc = published.PrimaryAircraftTimestampUtc;

            if (found == null)
            {
                (IReadOnlyList<Aircraft> Aircraft, DateTime TimestampUtc) shared = LiveTraffic.GetSnapshot();

                found = shared.Aircraft.FirstOrDefault(item => item.Hex == hex);
                foundTimestampUtc = shared.TimestampUtc;
            }

            if (found == null)
            {
                return;
            }

            (double Latitude, double Longitude) position = MapMath.Extrapolate(found, (DateTime.UtcNow - foundTimestampUtc).TotalSeconds);

            lock (stateLock)
            {
                if (!latestAircraft.Any(item => item.Hex == hex))
                {
                    List<Aircraft> merged = new List<Aircraft>(latestAircraft);
                    merged.Add(found);
                    latestAircraft = merged;
                }

                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
                centerLatitude = position.Latitude;
                centerLongitude = position.Longitude;
                followedHex = hex;
                followLocation = false;
            }

            dataLoop.Wake();
        }

        /// <summary>
        /// Handles the request to show an aircraft found in the search: follows it, selects it and isolates it.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        private void FocusAircraft(string hex)
        {
            FollowAircraft(hex, true);
        }

        /// <summary>
        /// Handles the request to show several aircraft together, for example after a tap on the notification of an alert about many aircraft: selects them all in the multi-select mode (on the phone), without isolating, and moves the map so that they all fit on the screen.
        /// </summary>
        /// <param name="hexes">ICAO addresses of the aircraft; the first one becomes the primary selection.</param>
        private void FocusAircraftGroup(IReadOnlyList<string> hexes)
        {
            if (hexes.Count == 0)
            {
                return;
            }

            (IReadOnlyList<Aircraft> Aircraft, DateTime TimestampUtc) shared = LiveTraffic.GetSnapshot();
            bool found;

            lock (stateLock)
            {
                List<Aircraft> merged = new List<Aircraft>(latestAircraft);

                foreach (string hex in hexes)
                {
                    if (!merged.Any(item => item.Hex == hex))
                    {
                        Aircraft? missing = shared.Aircraft.FirstOrDefault(item => item.Hex == hex);

                        if (missing != null)
                        {
                            merged.Add(missing);
                        }
                    }
                }

                latestAircraft = merged;

                List<string> present = hexes.Where(hex => merged.Any(item => item.Hex == hex)).ToList();
                found = present.Count > 0;

                if (found)
                {
                    double elapsedSeconds = (DateTime.UtcNow - latestAircraftTimestampUtc).TotalSeconds;
                    List<(double Latitude, double Longitude)> positions = new List<(double Latitude, double Longitude)>();

                    foreach (string hex in present)
                    {
                        positions.Add(MapMath.Extrapolate(merged.First(item => item.Hex == hex), elapsedSeconds));
                    }

                    double minimumLatitude = positions.Min(position => position.Latitude);
                    double maximumLatitude = positions.Max(position => position.Latitude);
                    double minimumLongitude = positions.Min(position => position.Longitude);
                    double maximumLongitude = positions.Max(position => position.Longitude);

                    flingVelocityX = 0.0;
                    flingVelocityY = 0.0;
                    centerLatitude = (minimumLatitude + maximumLatitude) / 2.0;
                    centerLongitude = (minimumLongitude + maximumLongitude) / 2.0;
                    zoomTarget = FindZoomThatFits(minimumLatitude, maximumLatitude, minimumLongitude, maximumLongitude);
                    followedHex = null;
                    followLocation = false;
                    selectedHexes = new HashSet<string>(present);
                    primaryHex = present[0];
                    selectedAirport = null;
                    isolateSelected = false;
                    multiSelect = !ShowSelectionPopup;
                }
            }

            if (!found)
            {
                return;
            }

            dataLoop.Wake();
            NotifySelectionChanged();
            PublishFollow();
        }

        /// <summary>
        /// Finds the highest zoom at which a box of the Earth fits on the screen with a margin.
        /// </summary>
        /// <param name="minimumLatitude">Southern edge.</param>
        /// <param name="maximumLatitude">Northern edge.</param>
        /// <param name="minimumLongitude">Western edge.</param>
        /// <param name="maximumLongitude">Eastern edge.</param>
        /// <returns>The zoom within the limits of the settings. Call it only with the state lock held.</returns>
        private double FindZoomThatFits(double minimumLatitude, double maximumLatitude, double minimumLongitude, double maximumLongitude)
        {
            double minimumZoom = AppSettings.MinZoom;
            double maximumZoom = Math.Min(AppSettings.MaxZoom, GroupMaximumZoom);
            double availableWidth = Math.Max(1, surfaceWidth) * GroupScreenFraction;
            double availableHeight = Math.Max(1, surfaceHeight) * GroupScreenFraction;

            for (double candidate = maximumZoom; candidate > minimumZoom; candidate -= GroupZoomStep)
            {
                (double westX, double northY) = WebMercator.LatLonToWorldPixel(maximumLatitude, minimumLongitude, candidate);
                (double eastX, double southY) = WebMercator.LatLonToWorldPixel(minimumLatitude, maximumLongitude, candidate);

                if (Math.Abs(eastX - westX) <= availableWidth && Math.Abs(southY - northY) <= availableHeight)
                {
                    return candidate;
                }
            }

            return minimumZoom;
        }

        /// <summary>
        /// Publishes the selection and the filters to the other map and raises <see cref="SelectionChanged"/>.
        /// </summary>
        private void NotifySelectionChanged()
        {
            SelectionSnapshot snapshot;
            bool primaryChanged;

            lock (stateLock)
            {
                Aircraft? primaryAircraft = primaryHex == null ? null : latestAircraft.FirstOrDefault(item => item.Hex == primaryHex);

                snapshot = new SelectionSnapshot(selectedHexes, primaryHex, isolateSelected, militaryOnly, primaryAircraft, latestAircraftTimestampUtc, selectedAirport?.Icao);
                primaryChanged = primaryHex != lastRecordedPrimaryHex;
                lastRecordedPrimaryHex = primaryHex;
            }

            if (primaryChanged && snapshot.PrimaryHex != null)
            {
                AircraftHistoryStore.Record(snapshot.PrimaryHex, snapshot.PrimaryAircraft?.Callsign, snapshot.PrimaryAircraft?.Registration, snapshot.PrimaryAircraft?.TypeCode, false);
            }

            selectionState.Publish(this, snapshot);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Takes over the selection and the filters changed by the other map.
        /// </summary>
        /// <param name="source">Map that changed the state.</param>
        /// <param name="snapshot">New state.</param>
        private void OnSharedSelectionChanged(object source, SelectionSnapshot snapshot)
        {
            if (ReferenceEquals(source, this))
            {
                return;
            }

            string? previousPrimary;

            lock (stateLock)
            {
                previousPrimary = primaryHex;
            }

            ApplySelection(snapshot);

            if (snapshot.PrimaryHex != null && snapshot.PrimaryHex != previousPrimary)
            {
                RevealAircraftIfMissing(snapshot.PrimaryHex);

                lock (stateLock)
                {
                    if (latestAircraft.Any(item => item.Hex == snapshot.PrimaryHex))
                    {
                        flingVelocityX = 0.0;
                        flingVelocityY = 0.0;
                        followedHex = snapshot.PrimaryHex;
                        followLocation = false;
                    }
                }
            }

            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Replaces the selection and the filters with the given state.
        /// </summary>
        /// <param name="snapshot">State to apply.</param>
        private void ApplySelection(SelectionSnapshot snapshot)
        {
            bool militaryChanged;

            lock (stateLock)
            {
                selectedHexes = snapshot.Hexes;
                primaryHex = snapshot.PrimaryHex;
                isolateSelected = snapshot.Isolated;
                militaryChanged = militaryOnly != snapshot.MilitaryOnly;
                militaryOnly = snapshot.MilitaryOnly;
                selectedAirport = AppServices.Airports.Find(snapshot.AirportIcao);
            }

            if (militaryChanged)
            {
                dataLoop.Wake();
            }
        }

        /// <summary>
        /// Stops the map from moving when the area that a view covers changes, which is wanted while that view fills the screen: the followed aircraft or the centered airport stays where it is until the view is brought back to its size.
        /// </summary>
        /// <param name="frozen">Whether the position at which the followed aircraft or the centered airport is kept is frozen.</param>
        public void SetFramingFrozen(bool frozen)
        {
            Volatile.Write(ref framingFrozen, frozen ? 1 : 0);
        }

        /// <summary>
        /// Sets the area of the screen covered by a view drawn over the map. Aircraft in this area are not selectable.
        /// </summary>
        /// <param name="area">Covered area in map pixels, or null when nothing covers the map.</param>
        public void SetCoveredArea(RectF? area)
        {
            Volatile.Write(ref coveredArea, area);
        }

        /// <summary>
        /// Checks whether a point of the map is covered by the popup or by a view drawn over the map.
        /// </summary>
        /// <param name="popup">Area of the popup, or null.</param>
        /// <param name="x">Horizontal position in pixels.</param>
        /// <param name="y">Vertical position in pixels.</param>
        /// <returns>True when the point is covered.</returns>
        private bool IsCovered(RectF? popup, float x, float y)
        {
            RectF? covered = Volatile.Read(ref coveredArea);

            return (popup != null && popup.Contains(x, y)) || (covered != null && covered.Contains(x, y));
        }

        /// <summary>
        /// Makes the map center follow an aircraft, selects it and optionally isolates it.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="isolate">Whether the aircraft is also isolated, so that only it stays visible.</param>
        public void FollowAircraft(string hex, bool isolate)
        {
            lock (stateLock)
            {
                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
                followedHex = hex;
                followLocation = false;
                selectedHexes = new HashSet<string> { hex };
                primaryHex = hex;
                selectedAirport = null;

                if (isolate)
                {
                    isolateSelected = true;
                }
            }

            RevealAircraftIfMissing(hex);

            NotifySelectionChanged();
            PublishFollow();
        }

        /// <summary>
        /// Makes the map center follow the primary selected aircraft without changing the selection.
        /// </summary>
        public void FollowPrimary()
        {
            lock (stateLock)
            {
                if (primaryHex == null)
                {
                    return;
                }

                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
                followedHex = primaryHex;
                followLocation = false;
            }

            PublishFollow();
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Publishes what this map centers on, so that the other map centers on it too.
        /// </summary>
        private void PublishFollow()
        {
            string? hex;
            bool followUser;

            lock (stateLock)
            {
                hex = followedHex;
                followUser = followLocation;
            }

            appliedFollowVersion = selectionState.PublishFollow(this, hex, followUser).Version;
        }

        /// <summary>
        /// Takes over the follow target published by the other map.
        /// </summary>
        /// <param name="source">Map that changed the follow target.</param>
        /// <param name="snapshot">New follow target.</param>
        private void OnSharedFollowChanged(object source, FollowSnapshot snapshot)
        {
            if (ReferenceEquals(source, this))
            {
                return;
            }

            ApplyFollow(snapshot);
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Starts following the given target.
        /// </summary>
        /// <param name="snapshot">Follow target to apply.</param>
        private void ApplyFollow(FollowSnapshot snapshot)
        {
            lock (stateLock)
            {
                appliedFollowVersion = snapshot.Version;
                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
                followedHex = snapshot.Hex;
                followLocation = snapshot.FollowUser;

                if (snapshot.Hex != null || snapshot.FollowUser)
                {
                    centeredAirport = null;
                }

                if (snapshot.FollowUser && hasUserLocation)
                {
                    centerLatitude = userLatitude;
                    centerLongitude = userLongitude;
                }
            }

            RevealAircraftIfMissing(snapshot.Hex);
        }

        /// <summary>
        /// Clears the aircraft selection and turns off the isolation.
        /// </summary>
        public void ClearSelection()
        {
            bool followReleased;

            lock (stateLock)
            {
                selectedHexes = new HashSet<string>();
                primaryHex = null;
                isolateSelected = false;
                selectedAirport = null;
                followReleased = ReleaseFollowedAircraft();
            }

            NotifySelectionChanged();

            if (followReleased)
            {
                PublishFollow();
            }
        }

        /// <summary>
        /// Stops following an aircraft, which is done when nothing is selected any more, so that the follow ring does not stay around an aircraft that has no card. Call it only with the state lock held.
        /// </summary>
        /// <returns>True when an aircraft was followed and is not any more.</returns>
        private bool ReleaseFollowedAircraft()
        {
            if (followedHex == null)
            {
                return false;
            }

            followedHex = null;

            return true;
        }

        /// <summary>
        /// Returns the data of the primary selected aircraft with its current, extrapolated position and the user position, or null when nothing is selected.
        /// </summary>
        /// <returns>Data shown on the aircraft card, or null.</returns>
        public SelectedAircraftInfo? GetPrimaryInfo()
        {
            lock (stateLock)
            {
                string? hex = primaryHex;

                if (hex == null)
                {
                    return null;
                }

                Aircraft? aircraft = latestAircraft.FirstOrDefault(item => item.Hex == hex);
                double ageSeconds = (DateTime.UtcNow - latestAircraftTimestampUtc).TotalSeconds;

                if (aircraft == null)
                {
                    return new SelectedAircraftInfo(hex, null, 0.0, 0.0, ageSeconds, hasUserLocation, userLatitude, userLongitude, selectedHexes.Count);
                }

                (double Latitude, double Longitude) position = MapMath.Extrapolate(aircraft, ageSeconds);

                return new SelectedAircraftInfo(hex, aircraft, position.Latitude, position.Longitude, ageSeconds, hasUserLocation, userLatitude, userLongitude, selectedHexes.Count);
            }
        }

        /// <summary>
        /// Turns the filter that shows only military aircraft on or off.
        /// </summary>
        /// <param name="militaryCount">Number of military aircraft in the latest data.</param>
        /// <returns>True when the filter is now on.</returns>
        public bool ToggleMilitaryOnly(out int militaryCount)
        {
            bool enabled;

            lock (stateLock)
            {
                militaryOnly = !militaryOnly;
                enabled = militaryOnly;
                militaryCount = latestAircraft.Count(item => item.IsMilitary);
            }

            NotifySelectionChanged();
            dataLoop.Wake();

            return enabled;
        }

        /// <summary>
        /// Turns the multi-select mode on or off.
        /// </summary>
        /// <returns>True when the mode is now on.</returns>
        public bool ToggleMultiSelect()
        {
            bool enabled;

            lock (stateLock)
            {
                multiSelect = !multiSelect;
                enabled = multiSelect;
            }

            SelectionChanged?.Invoke();

            return enabled;
        }

        /// <summary>
        /// Turns the isolation of the selected aircraft on, or off if it is on.
        /// </summary>
        /// <param name="isolated">Whether the isolation is on after the call.</param>
        /// <returns>False when the isolation cannot be turned on because nothing is selected.</returns>
        public bool TryToggleIsolation(out bool isolated)
        {
            lock (stateLock)
            {
                if (isolateSelected)
                {
                    isolateSelected = false;
                }
                else if (selectedHexes.Count > 0)
                {
                    isolateSelected = true;
                }
                else
                {
                    isolated = false;

                    return false;
                }

                isolated = isolateSelected;
            }

            NotifySelectionChanged();

            return true;
        }

        /// <summary>
        /// Handles a tap on the map: selects the aircraft nearest to the finger. In the multi-select mode it adds the aircraft to the selection or removes it, and in the normal mode it replaces the selection. A tap on an empty spot clears the selection outside the multi-select mode.
        /// </summary>
        /// <param name="x">Horizontal tap position in pixels.</param>
        /// <param name="y">Vertical tap position in pixels.</param>
        public void Tap(float x, float y)
        {
            if (IsCovered(Volatile.Read(ref popupBounds), x, y))
            {
                Log.Info(LogTag, string.Format(CultureInfo.InvariantCulture, "Dotknięcie {0:0},{1:0} pominięte: obszar zasłonięty", x, y));

                return;
            }

            IReadOnlyList<(string Hex, float X, float Y)> hits = Volatile.Read(ref screenHits);
            float maximumDistance = TapRadius * density;
            float bestDistance = maximumDistance * maximumDistance;
            string? tapped = null;

            foreach ((string Hex, float X, float Y) hit in hits)
            {
                float distance = (hit.X - x) * (hit.X - x) + (hit.Y - y) * (hit.Y - y);

                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    tapped = hit.Hex;
                }
            }

            Log.Info(LogTag, string.Format(CultureInfo.InvariantCulture, "Dotknięcie {0:0},{1:0}: samolotów do wyboru {2}, promień {3:0} px, wybrany {4}", x, y, hits.Count, maximumDistance, tapped ?? "brak"));

            if (tapped == null)
            {
                Threat? tappedThreat = FindTappedThreat(x, y);

                if (tappedThreat != null)
                {
                    ThreatTapped?.Invoke(tappedThreat);

                    return;
                }

                Airport? tappedAirport = FindTappedAirport(x, y);

                if (tappedAirport != null)
                {
                    SelectAirport(tappedAirport);

                    return;
                }

                if (!HasSelectionToClear())
                {
                    AirspaceZoneView? tappedAirspace = FindTappedAirspace(x, y);

                    if (tappedAirspace != null)
                    {
                        AirspaceTapped?.Invoke(tappedAirspace);

                        return;
                    }
                }
            }

            bool followReleased = false;

            lock (stateLock)
            {
                if (tapped != null)
                {
                    selectedAirport = null;
                }

                if (tapped == null)
                {
                    if (selectedAirport != null)
                    {
                        selectedAirport = null;
                    }
                    else if (multiSelect || selectedHexes.Count == 0)
                    {
                        return;
                    }
                    else
                    {
                        selectedHexes = new HashSet<string>();
                    }
                }
                else if (multiSelect)
                {
                    HashSet<string> next = new HashSet<string>(selectedHexes);

                    if (next.Remove(tapped))
                    {
                        if (primaryHex == tapped)
                        {
                            primaryHex = next.FirstOrDefault();
                        }
                    }
                    else
                    {
                        next.Add(tapped);
                        primaryHex = tapped;
                    }

                    selectedHexes = next;
                }
                else
                {
                    selectedHexes = new HashSet<string> { tapped };
                    primaryHex = tapped;
                }

                if (selectedHexes.Count == 0)
                {
                    isolateSelected = false;
                    primaryHex = null;
                    followReleased = ReleaseFollowedAircraft();
                }
            }

            NotifySelectionChanged();

            if (followReleased)
            {
                PublishFollow();
            }
        }

        /// <summary>
        /// Tells whether a tap on an empty place has something to clear: a selected airport, or selected aircraft outside the multi-select mode. Such a tap clears the selection and does not open the card of an airspace area under it.
        /// </summary>
        /// <returns>True when a tap on an empty place would clear the selection.</returns>
        private bool HasSelectionToClear()
        {
            lock (stateLock)
            {
                return selectedAirport != null || (!multiSelect && selectedHexes.Count > 0);
            }
        }

        /// <summary>
        /// Finds the threat marker closest to a tap.
        /// </summary>
        /// <param name="x">Horizontal tap position in pixels.</param>
        /// <param name="y">Vertical tap position in pixels.</param>
        /// <returns>The threat, or null when no marker is close enough.</returns>
        private Threat? FindTappedThreat(float x, float y)
        {
            IReadOnlyList<(Threat Threat, float X, float Y)> hits = Volatile.Read(ref threatHits);
            float maximumDistance = TapRadius * density;
            float bestDistance = maximumDistance * maximumDistance;
            Threat? tapped = null;

            foreach ((Threat Threat, float X, float Y) hit in hits)
            {
                float distance = (hit.X - x) * (hit.X - x) + (hit.Y - y) * (hit.Y - y);

                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    tapped = hit.Threat;
                }
            }

            return tapped;
        }

        /// <summary>
        /// Finds the airport marker closest to a tap.
        /// </summary>
        /// <param name="x">Horizontal tap position in pixels.</param>
        /// <param name="y">Vertical tap position in pixels.</param>
        /// <returns>The airport, or null when no marker is close enough.</returns>
        private Airport? FindTappedAirport(float x, float y)
        {
            IReadOnlyList<(Airport Airport, float X, float Y)> hits = Volatile.Read(ref airportHits);
            float maximumDistance = TapRadius * density;
            float bestDistance = maximumDistance * maximumDistance;
            Airport? tapped = null;

            foreach ((Airport Airport, float X, float Y) hit in hits)
            {
                float distance = (hit.X - x) * (hit.X - x) + (hit.Y - y) * (hit.Y - y);

                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    tapped = hit.Airport;
                }
            }

            return tapped;
        }

        /// <summary>
        /// Selects an airport and clears the aircraft selection.
        /// </summary>
        /// <param name="airport">Airport to select.</param>
        private void SelectAirport(Airport airport)
        {
            lock (stateLock)
            {
                selectedAirport = airport;
                selectedHexes = new HashSet<string>();
                primaryHex = null;
                isolateSelected = false;
                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
                followedHex = null;
                followLocation = false;
                centeredAirport = airport;
            }

            NotifySelectionChanged();
            PublishFollow();
        }

        /// <summary>
        /// Returns the user position.
        /// </summary>
        /// <returns>Whether the position is known, the latitude and the longitude.</returns>
        public (bool Known, double Latitude, double Longitude) GetUserLocation()
        {
            lock (stateLock)
            {
                return (hasUserLocation, userLatitude, userLongitude);
            }
        }

        /// <summary>
        /// Returns the selected airport.
        /// </summary>
        /// <returns>The airport, or null when no airport is selected.</returns>
        public Airport? GetSelectedAirport()
        {
            lock (stateLock)
            {
                return selectedAirport;
            }
        }

        /// <summary>
        /// Centers the map on the selected airport and stops following an aircraft or the user.
        /// </summary>
        public void CenterOnSelectedAirport()
        {
            lock (stateLock)
            {
                if (selectedAirport == null)
                {
                    return;
                }

                CenterOnAirportLocked(selectedAirport);
            }

            PublishFollow();
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Selects an airport, centers the map on it and zooms in far enough for the airport to be visible. Called when the user picks an airport in the search window.
        /// </summary>
        /// <param name="icao">ICAO code of the airport.</param>
        private void FocusAirport(string icao)
        {
            Airport? airport = AppServices.Airports.Find(icao);

            if (airport == null)
            {
                return;
            }

            lock (stateLock)
            {
                selectedAirport = airport;
                selectedHexes = new HashSet<string>();
                primaryHex = null;
                isolateSelected = false;
                CenterOnAirportLocked(airport);
            }

            NotifySelectionChanged();
            PublishFollow();
        }

        /// <summary>
        /// Moves the map to an airport. Call with the state lock held.
        /// </summary>
        /// <param name="airport">Airport.</param>
        private void CenterOnAirportLocked(Airport airport)
        {
            flingVelocityX = 0.0;
            flingVelocityY = 0.0;
            followedHex = null;
            followLocation = false;
            centeredAirport = airport;
            centerLatitude = airport.Latitude;
            centerLongitude = airport.Longitude;
            zoomTarget = Math.Clamp(Math.Max(zoomTarget, AirportFocusZoom), AppSettings.MinZoom, AppSettings.MaxZoom);
        }

        /// <summary>
        /// Stores the user position and, in the following mode, moves the map center to it.
        /// </summary>
        /// <param name="latitude">Latitude of the user.</param>
        /// <param name="longitude">Longitude of the user.</param>
        public void SetUserLocation(double latitude, double longitude)
        {
            lock (stateLock)
            {
                userLatitude = latitude;
                LiveTraffic.SetUserLocation(latitude, longitude);
                userLongitude = longitude;
                userLocationAtUtc = DateTime.UtcNow;
                hasUserLocation = true;

                if (followLocation)
                {
                    centerLatitude = latitude;
                    centerLongitude = longitude;
                }
            }
        }

        /// <summary>
        /// Turns on following of the user instead of an aircraft and, if the position is known, centers the map on it.
        /// </summary>
        public void FollowUser()
        {
            lock (stateLock)
            {
                flingVelocityX = 0.0;
                flingVelocityY = 0.0;
                followLocation = true;
                followedHex = null;
                centeredAirport = null;

                if (hasUserLocation)
                {
                    centerLatitude = userLatitude;
                    centerLongitude = userLongitude;
                }
            }

            PublishFollow();
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Starts the position updates if the surface is active. Called after the permission is granted.
        /// </summary>
        public void StartLocationUpdates()
        {
            if (cancellation != null)
            {
                locationTracker.Start();
            }
        }
    }
}
