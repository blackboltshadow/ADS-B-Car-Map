using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.Neptun
{
    /// <summary>
    /// The current threats built from the snapshots and the changes of the stream. The list is replaced as a whole at every change, so it can be read without a lock.
    /// </summary>
    public sealed class ThreatStore
    {
        /// <summary>
        /// Lock protecting changes.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// The threats by identifier, in the order in which they appeared.
        /// </summary>
        private readonly Dictionary<string, Threat> byId = new Dictionary<string, Threat>();

        /// <summary>
        /// Position at which the heading of a threat was last worked out, or at which it was first seen, by identifier.
        /// </summary>
        private readonly Dictionary<string, (double Latitude, double Longitude)> anchors = new Dictionary<string, (double Latitude, double Longitude)>();

        /// <summary>
        /// Distance in kilometers that a threat has to cover before a heading is worked out from it. Positions are rounded to a locality or less, so a shorter distance would only give noise.
        /// </summary>
        public const double MinimumMoveKilometers = 1.5;

        /// <summary>
        /// The current list, replaced at every change.
        /// </summary>
        private IReadOnlyList<Threat> threats = new List<Threat>();

        /// <summary>
        /// The current threats.
        /// </summary>
        public IReadOnlyList<Threat> Threats => Volatile.Read(ref threats);

        /// <summary>
        /// Event raised, on the thread that made the change, after the list changed.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Replaces all the threats. A threat that is already resolved is left out.
        /// </summary>
        /// <param name="items">The threats of the snapshot.</param>
        public void ApplySnapshot(IEnumerable<Threat> items)
        {
            Change(() =>
            {
                Dictionary<string, Threat> previous = new Dictionary<string, Threat>(byId);

                byId.Clear();

                foreach (Threat item in items)
                {
                    if (item.Status != ThreatStatus.Resolved)
                    {
                        previous.TryGetValue(item.Id, out Threat? before);
                        byId[item.Id] = WithHeading(item, before);
                    }
                }

                foreach (string id in anchors.Keys.Where((string id) => !byId.ContainsKey(id)).ToList())
                {
                    anchors.Remove(id);
                }

                return true;
            });
        }

        /// <summary>
        /// Adds a threat or replaces the one with the same identifier. A resolved threat is removed.
        /// </summary>
        /// <param name="item">The new state of the threat.</param>
        public void Upsert(Threat item)
        {
            Change(() =>
            {
                if (item.Status == ThreatStatus.Resolved)
                {
                    anchors.Remove(item.Id);

                    return byId.Remove(item.Id);
                }

                byId.TryGetValue(item.Id, out Threat? before);
                byId[item.Id] = WithHeading(item, before);

                return true;
            });
        }

        /// <summary>
        /// Removes a threat.
        /// </summary>
        /// <param name="id">Identifier of the threat.</param>
        public void Remove(string id)
        {
            Change(() =>
            {
                anchors.Remove(id);

                return byId.Remove(id);
            });
        }

        /// <summary>
        /// Removes every threat.
        /// </summary>
        public void Clear()
        {
            Change(() =>
            {
                if (byId.Count == 0)
                {
                    return false;
                }

                byId.Clear();
                anchors.Clear();

                return true;
            });
        }

        /// <summary>
        /// Gives a threat that has no heading one worked out from its movement: from its trail, or from the distance it covered since the position at which it was last worked out. A threat that has not moved far enough keeps the heading it already had, and one of a source-given heading is left as it is.
        /// </summary>
        /// <param name="item">The new state of the threat.</param>
        /// <param name="before">The previous state of the same threat, or null when it is new.</param>
        /// <returns>The threat, with a derived heading when one could be worked out.</returns>
        private Threat WithHeading(Threat item, Threat? before)
        {
            if (item.HeadingDegrees != null || item.AreaOnly)
            {
                anchors[item.Id] = (item.Latitude, item.Longitude);

                return item;
            }

            if (item.Trail.Count >= 2)
            {
                ThreatTrailPoint from = item.Trail[item.Trail.Count - 2];
                ThreatTrailPoint to = item.Trail[item.Trail.Count - 1];

                if (ThreatGeo.DistanceKilometers(from.Latitude, from.Longitude, to.Latitude, to.Longitude) >= MinimumMoveKilometers)
                {
                    anchors[item.Id] = (item.Latitude, item.Longitude);

                    return item with { HeadingDegrees = ThreatGeo.BearingDegrees(from.Latitude, from.Longitude, to.Latitude, to.Longitude), IsHeadingDerived = true };
                }
            }

            if (!anchors.TryGetValue(item.Id, out (double Latitude, double Longitude) anchor))
            {
                anchors[item.Id] = (item.Latitude, item.Longitude);

                return item;
            }

            if (ThreatGeo.DistanceKilometers(anchor.Latitude, anchor.Longitude, item.Latitude, item.Longitude) >= MinimumMoveKilometers)
            {
                anchors[item.Id] = (item.Latitude, item.Longitude);

                return item with { HeadingDegrees = ThreatGeo.BearingDegrees(anchor.Latitude, anchor.Longitude, item.Latitude, item.Longitude), IsHeadingDerived = true };
            }

            if (before != null && before.IsHeadingDerived && before.HeadingDegrees != null)
            {
                return item with { HeadingDegrees = before.HeadingDegrees, IsHeadingDerived = true };
            }

            return item;
        }

        /// <summary>
        /// Applies a change under the lock, publishes the new list and raises <see cref="Changed"/>.
        /// </summary>
        /// <param name="change">Edits the dictionary and returns true when something changed.</param>
        private void Change(Func<bool> change)
        {
            lock (syncRoot)
            {
                if (!change())
                {
                    return;
                }

                Volatile.Write(ref threats, byId.Values.ToList());
            }

            Changed?.Invoke();
        }
    }
}
