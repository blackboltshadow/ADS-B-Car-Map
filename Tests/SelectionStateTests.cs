using AdsbCarMap.Data;
using AdsbCarMap.Map;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the selection and follow state shared between the phone and the car.
    /// </summary>
    public class SelectionStateTests
    {
        /// <summary>
        /// Creates a snapshot with the given primary hex.
        /// </summary>
        /// <param name="hex">Primary hex or null.</param>
        /// <returns>The snapshot.</returns>
        private static SelectionSnapshot CreateSnapshot(string? hex)
        {
            HashSet<string> hexes = new HashSet<string>();

            if (hex != null)
            {
                hexes.Add(hex);
            }

            return new SelectionSnapshot(hexes, hex, false, false, null, DateTime.MinValue);
        }

        /// <summary>
        /// The initial state is empty and follows the user at version zero.
        /// </summary>
        [Fact]
        public void InitialState_IsEmpty()
        {
            SelectionState state = new SelectionState();

            Assert.Empty(state.Current.Hexes);
            Assert.Null(state.Current.PrimaryHex);
            Assert.True(state.CurrentFollow.FollowUser);
            Assert.Null(state.CurrentFollow.Hex);
            Assert.Equal(0, state.CurrentFollow.Version);
        }

        /// <summary>
        /// Publishing stores the snapshot and notifies listeners with the source.
        /// </summary>
        [Fact]
        public void Publish_StoresSnapshotAndRaisesChanged()
        {
            SelectionState state = new SelectionState();
            object source = new object();
            object? receivedSource = null;
            SelectionSnapshot? received = null;
            state.Changed += (sender, snapshot) =>
            {
                receivedSource = sender;
                received = snapshot;
            };
            SelectionSnapshot published = CreateSnapshot("abc123");

            state.Publish(source, published);

            Assert.Same(published, state.Current);
            Assert.Same(source, receivedSource);
            Assert.Same(published, received);
        }

        /// <summary>
        /// Publishing without listeners does not throw.
        /// </summary>
        [Fact]
        public void Publish_WithoutListeners_DoesNotThrow()
        {
            SelectionState state = new SelectionState();

            state.Publish(new object(), CreateSnapshot(null));
            state.PublishFollow(new object(), null, true);
        }

        /// <summary>
        /// Every follow publication increases the version and notifies listeners.
        /// </summary>
        [Fact]
        public void PublishFollow_IncrementsVersionAndRaisesEvent()
        {
            SelectionState state = new SelectionState();
            List<FollowSnapshot> received = new List<FollowSnapshot>();
            state.FollowChanged += (sender, snapshot) => received.Add(snapshot);

            FollowSnapshot first = state.PublishFollow(new object(), "abc123", false);
            FollowSnapshot second = state.PublishFollow(new object(), null, true);

            Assert.Equal(1, first.Version);
            Assert.Equal("abc123", first.Hex);
            Assert.False(first.FollowUser);
            Assert.Equal(2, second.Version);
            Assert.Equal(2, state.CurrentFollow.Version);
            Assert.Equal(2, received.Count);
        }

        /// <summary>
        /// The selection snapshot keeps the optional airport code and aircraft.
        /// </summary>
        [Fact]
        public void SelectionSnapshot_CarriesAirportAndAircraft()
        {
            Aircraft aircraft = new Aircraft("abc123", "LOT1", 52.0, 21.0, 10000, false, 400.0, 90.0, 1.0, "B738", null);
            SelectionSnapshot snapshot = new SelectionSnapshot(new HashSet<string> { "abc123" }, "abc123", true, true, aircraft, DateTime.UtcNow, "EPWA");

            Assert.Equal("EPWA", snapshot.AirportIcao);
            Assert.Same(aircraft, snapshot.PrimaryAircraft);
            Assert.True(snapshot.Isolated);
            Assert.True(snapshot.MilitaryOnly);
        }

        /// <summary>
        /// Concurrent follow publications never lose a version number.
        /// </summary>
        [Fact]
        public void PublishFollow_IsThreadSafe()
        {
            SelectionState state = new SelectionState();

            Parallel.For(0, 1000, index => state.PublishFollow(this, null, true));

            Assert.Equal(1000, state.CurrentFollow.Version);
        }
    }
}
