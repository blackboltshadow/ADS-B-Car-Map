using AdsbCarMap.Data;

namespace AdsbCarMap.Map
{
    /// <summary>
    /// Aircraft selection and filters shared by the phone map and the Android Auto map, so that both screens show the same selection.
    /// </summary>
    public sealed class SelectionState
    {
        /// <summary>
        /// Lock protecting <see cref="current"/>.
        /// </summary>
        private readonly object gate = new object();

        /// <summary>
        /// Most recently published state.
        /// </summary>
        private SelectionSnapshot current = new SelectionSnapshot(new HashSet<string>(), null, false, false, null, DateTime.MinValue);

        /// <summary>
        /// Event raised after a map publishes a new state. The first argument is the map that published it.
        /// </summary>
        public event System.Action<object, SelectionSnapshot>? Changed;

        /// <summary>
        /// Most recently published follow target.
        /// </summary>
        private FollowSnapshot follow = new FollowSnapshot(null, true, 0);

        /// <summary>
        /// Event raised after a map publishes a new follow target. The first argument is the map that published it.
        /// </summary>
        public event System.Action<object, FollowSnapshot>? FollowChanged;

        /// <summary>
        /// Most recently published state.
        /// </summary>
        public SelectionSnapshot Current
        {
            get
            {
                lock (gate)
                {
                    return current;
                }
            }
        }

        /// <summary>
        /// Most recently published follow target.
        /// </summary>
        public FollowSnapshot CurrentFollow
        {
            get
            {
                lock (gate)
                {
                    return follow;
                }
            }
        }

        /// <summary>
        /// Stores a new follow target and notifies the other maps.
        /// </summary>
        /// <param name="source">Map that changed the follow target.</param>
        /// <param name="hex">ICAO address of the followed aircraft, or null.</param>
        /// <param name="followUser">Whether the user position is followed.</param>
        /// <returns>The published follow target with its version.</returns>
        public FollowSnapshot PublishFollow(object source, string? hex, bool followUser)
        {
            FollowSnapshot snapshot;

            lock (gate)
            {
                snapshot = new FollowSnapshot(hex, followUser, follow.Version + 1);
                follow = snapshot;
            }

            FollowChanged?.Invoke(source, snapshot);

            return snapshot;
        }

        /// <summary>
        /// Stores a new state and notifies the other maps.
        /// </summary>
        /// <param name="source">Map that changed the state.</param>
        /// <param name="snapshot">New state.</param>
        public void Publish(object source, SelectionSnapshot snapshot)
        {
            lock (gate)
            {
                current = snapshot;
            }

            Changed?.Invoke(source, snapshot);
        }
    }

    /// <summary>
    /// What the maps center on.
    /// </summary>
    /// <param name="Hex">ICAO address of the followed aircraft, or null when no aircraft is followed.</param>
    /// <param name="FollowUser">Whether the user position is followed.</param>
    /// <param name="Version">Number of the change, increasing with every published follow target. A map applies a target only when it has not applied this version yet.</param>
    public sealed record FollowSnapshot(string? Hex, bool FollowUser, int Version);

    /// <summary>
    /// Immutable copy of the selection and filters of a map.
    /// </summary>
    /// <param name="Hexes">ICAO addresses of the selected aircraft.</param>
    /// <param name="PrimaryHex">ICAO address of the primary selected aircraft, or null when nothing is selected.</param>
    /// <param name="Isolated">Whether only the selected aircraft are visible.</param>
    /// <param name="MilitaryOnly">Whether only military aircraft are visible.</param>
    /// <param name="PrimaryAircraft">Data of the primary selected aircraft known to the map that published the state, or null. Lets the other map show an aircraft that is outside its own data.</param>
    /// <param name="PrimaryAircraftTimestampUtc">Moment the data of <paramref name="PrimaryAircraft"/> was downloaded, in UTC.</param>
    public sealed record SelectionSnapshot(IReadOnlySet<string> Hexes, string? PrimaryHex, bool Isolated, bool MilitaryOnly, Aircraft? PrimaryAircraft, DateTime PrimaryAircraftTimestampUtc);
}
