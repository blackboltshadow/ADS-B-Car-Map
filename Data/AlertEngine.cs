using AdsbCarMap.DataSources.Models;


namespace AdsbCarMap.Data
{
    /// <summary>
    /// Decides which aircraft should raise an alert. Keeps what it has seen between calls, so one instance has to live as long as the data loop. Not thread safe.
    /// </summary>
    public sealed class AlertEngine
    {
        /// <summary>
        /// What is remembered about one aircraft for one rule.
        /// </summary>
        private sealed class AircraftState
        {
            /// <summary>
            /// Whether the aircraft was inside the area at the previous evaluation.
            /// </summary>
            public bool WasInside;

            /// <summary>
            /// Whether the aircraft satisfied the whole rule at the previous evaluation.
            /// </summary>
            public bool WasMatching;

            /// <summary>
            /// Time of the last notification or null when there was none.
            /// </summary>
            public DateTime? LastNotified;

            /// <summary>
            /// Whether the aircraft was present in the data at the latest evaluation.
            /// </summary>
            public bool Seen;
        }

        /// <summary>
        /// State per rule identifier and aircraft address.
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, AircraftState>> _states = new Dictionary<string, Dictionary<string, AircraftState>>();

        /// <summary>
        /// Checks the current aircraft against the rules and returns the ones that should raise a notification now.
        /// </summary>
        /// <param name="rules">All rules; disabled rules and rules whose center cannot be determined are skipped.</param>
        /// <param name="aircraft">Aircraft from the latest download.</param>
        /// <param name="myPosition">Current position of the user or null when it is not known.</param>
        /// <param name="airportLookup">Returns the position of an airport by its ICAO code or null when it is not known.</param>
        /// <param name="routeLookup">Returns the route of a callsign or null when it is not known yet.</param>
        /// <param name="now">Current time.</param>
        /// <returns>Aircraft that triggered a rule, at most one match per rule and aircraft.</returns>
        public List<AlertMatch> Evaluate(
            IReadOnlyList<AlertRule> rules,
            IReadOnlyList<Aircraft> aircraft,
            GeoPoint? myPosition,
            Func<string, GeoPoint?> airportLookup,
            Func<string?, FlightRoute?> routeLookup,
            DateTime now)
        {
            List<AlertMatch> matches = new List<AlertMatch>();
            HashSet<string> ruleIds = new HashSet<string>();

            foreach (AlertRule rule in rules)
            {
                ruleIds.Add(rule.Id);

                if (!rule.IsEnabled)
                {
                    _states.Remove(rule.Id);
                    continue;
                }

                GeoPoint? center = rule.Area.ResolveCenter(myPosition, airportLookup);

                if (center == null)
                {
                    continue;
                }

                EvaluateRule(rule, center, aircraft, routeLookup, now, matches);
            }

            List<string> staleRules = new List<string>();

            foreach (string ruleId in _states.Keys)
            {
                if (!ruleIds.Contains(ruleId))
                {
                    staleRules.Add(ruleId);
                }
            }

            foreach (string ruleId in staleRules)
            {
                _states.Remove(ruleId);
            }

            return matches;
        }

        /// <summary>
        /// Counts the aircraft that satisfy a rule at this moment, regardless of what was notified earlier. Used for previewing a rule.
        /// </summary>
        /// <param name="rule">Rule to test.</param>
        /// <param name="aircraft">Aircraft to test.</param>
        /// <param name="myPosition">Current position of the user or null when it is not known.</param>
        /// <param name="airportLookup">Returns the position of an airport by its ICAO code or null when it is not known.</param>
        /// <param name="routeLookup">Returns the route of a callsign or null when it is not known yet.</param>
        /// <returns>Number of aircraft inside the area that satisfy the condition.</returns>
        public static int CountMatching(
            AlertRule rule,
            IReadOnlyList<Aircraft> aircraft,
            GeoPoint? myPosition,
            Func<string, GeoPoint?> airportLookup,
            Func<string?, FlightRoute?> routeLookup)
        {
            GeoPoint? center = rule.Area.ResolveCenter(myPosition, airportLookup);

            if (center == null)
            {
                return 0;
            }

            int count = 0;

            foreach (Aircraft item in aircraft)
            {
                if (IsInside(rule.Area, center, item) && SatisfiesCondition(rule, item, routeLookup))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Forgets everything that was seen, so the next evaluation treats all aircraft as new.
        /// </summary>
        public void Reset()
        {
            _states.Clear();
        }

        /// <summary>
        /// Evaluates one enabled rule against the aircraft and updates its state.
        /// </summary>
        /// <param name="rule">Rule to evaluate.</param>
        /// <param name="center">Current center of the rule's area.</param>
        /// <param name="aircraft">Aircraft from the latest download.</param>
        /// <param name="routeLookup">Returns the route of a callsign or null when it is not known yet.</param>
        /// <param name="now">Current time.</param>
        /// <param name="matches">List receiving the aircraft that fired.</param>
        private void EvaluateRule(AlertRule rule, GeoPoint center, IReadOnlyList<Aircraft> aircraft, Func<string?, FlightRoute?> routeLookup, DateTime now, List<AlertMatch> matches)
        {
            if (!_states.TryGetValue(rule.Id, out Dictionary<string, AircraftState>? states))
            {
                states = new Dictionary<string, AircraftState>();
                _states[rule.Id] = states;
            }

            foreach (AircraftState state in states.Values)
            {
                state.Seen = false;
            }

            TimeSpan cooldown = TimeSpan.FromMinutes(Math.Max(0, rule.CooldownMinutes));

            foreach (Aircraft item in aircraft)
            {
                bool known = states.TryGetValue(item.Hex, out AircraftState? state);

                if (!known || state == null)
                {
                    state = new AircraftState();
                    states[item.Hex] = state;
                }

                bool inside = IsInside(rule.Area, center, item);
                bool matching = inside && SatisfiesCondition(rule, item, routeLookup);

                if (matching && !state.WasMatching)
                {
                    AlertTrigger trigger = known && !state.WasInside ? AlertTrigger.Entered : AlertTrigger.Appeared;
                    bool wanted = trigger == AlertTrigger.Entered ? rule.NotifyOnEntry : rule.NotifyOnAppearance;
                    bool cooledDown = state.LastNotified == null || now - state.LastNotified.Value >= cooldown;

                    if (wanted && cooledDown)
                    {
                        state.LastNotified = now;
                        matches.Add(new AlertMatch(rule, item, trigger, GeoMath.DistanceKilometers(center.Latitude, center.Longitude, item.Latitude, item.Longitude)));
                    }
                }

                state.WasInside = inside;
                state.WasMatching = matching;
                state.Seen = true;
            }

            List<string> gone = new List<string>();

            foreach (KeyValuePair<string, AircraftState> pair in states)
            {
                if (pair.Value.Seen)
                {
                    continue;
                }

                bool inCooldown = pair.Value.LastNotified != null && now - pair.Value.LastNotified.Value < cooldown;

                if (inCooldown)
                {
                    pair.Value.WasInside = false;
                    pair.Value.WasMatching = false;
                }
                else
                {
                    gone.Add(pair.Key);
                }
            }

            foreach (string hex in gone)
            {
                states.Remove(hex);
            }
        }

        /// <summary>
        /// Checks whether an aircraft lies within the radius of an area.
        /// </summary>
        /// <param name="area">Area to test.</param>
        /// <param name="center">Current center of the area.</param>
        /// <param name="aircraft">Aircraft to test.</param>
        /// <returns>True when the aircraft is inside.</returns>
        private static bool IsInside(AlertArea area, GeoPoint center, Aircraft aircraft)
        {
            return GeoMath.DistanceKilometers(center.Latitude, center.Longitude, aircraft.Latitude, aircraft.Longitude) <= area.RadiusKilometers;
        }

        /// <summary>
        /// Checks the condition of a rule. An unknown result does not satisfy it.
        /// </summary>
        /// <param name="rule">Rule whose condition is checked.</param>
        /// <param name="aircraft">Aircraft to test.</param>
        /// <param name="routeLookup">Returns the route of a callsign or null when it is not known yet.</param>
        /// <returns>True when the aircraft satisfies the condition.</returns>
        private static bool SatisfiesCondition(AlertRule rule, Aircraft aircraft, Func<string?, FlightRoute?> routeLookup)
        {
            return rule.Condition == null || rule.Condition.Evaluate(aircraft, routeLookup) == FilterResult.True;
        }
    }
}
