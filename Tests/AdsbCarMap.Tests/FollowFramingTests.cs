using AdsbCarMap.Map;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the framing of a followed aircraft above a card that covers the bottom of the map.
    /// </summary>
    public class FollowFramingTests
    {
        /// <summary>
        /// Without a covering view the aircraft stays in the middle of the screen.
        /// </summary>
        [Fact]
        public void GetTargetY_NothingCovered_IsMiddle()
        {
            Assert.Equal(1000.0, FollowFraming.GetTargetY(1280.0, 2000.0, new List<(double, double, double, double)>()));
        }

        /// <summary>
        /// A card at the bottom moves the aircraft to the middle of the part above it.
        /// </summary>
        [Fact]
        public void GetTargetY_BottomCard_IsMiddleOfVisiblePart()
        {
            List<(double, double, double, double)> covered = new List<(double, double, double, double)> { (40.0, 1400.0, 1240.0, 2000.0) };

            Assert.Equal(700.0, FollowFraming.GetTargetY(1280.0, 2000.0, covered));
        }

        /// <summary>
        /// The highest of several covering views decides.
        /// </summary>
        [Fact]
        public void GetTargetY_UsesHighestCoveringView()
        {
            List<(double, double, double, double)> covered = new List<(double, double, double, double)>
            {
                (40.0, 1600.0, 1240.0, 2000.0),
                (40.0, 1200.0, 1240.0, 2000.0)
            };

            Assert.Equal(600.0, FollowFraming.GetTargetY(1280.0, 2000.0, covered));
        }

        /// <summary>
        /// Narrow views, views that do not reach the bottom edge and views at the top do not count.
        /// </summary>
        [Fact]
        public void GetTargetY_IgnoresViewsThatAreNotBottomSheets()
        {
            List<(double, double, double, double)> covered = new List<(double, double, double, double)>
            {
                (0.0, 1000.0, 300.0, 2000.0),
                (0.0, 1000.0, 1280.0, 1500.0),
                (0.0, 0.0, 1280.0, 400.0)
            };

            Assert.Equal(1000.0, FollowFraming.GetTargetY(1280.0, 2000.0, covered));
        }

        /// <summary>
        /// A very tall card does not push the aircraft under the top buttons.
        /// </summary>
        [Fact]
        public void GetTargetY_TallCard_KeepsMinimumDistanceFromTop()
        {
            List<(double, double, double, double)> covered = new List<(double, double, double, double)> { (0.0, 200.0, 1280.0, 2000.0) };

            Assert.Equal(240.0, FollowFraming.GetTargetY(1280.0, 2000.0, covered));
        }

        /// <summary>
        /// The ease moves towards the target, does not overshoot and snaps when close.
        /// </summary>
        [Fact]
        public void Ease_MovesTowardsTarget()
        {
            double step = FollowFraming.Ease(0.0, 100.0, 0.05);

            Assert.InRange(step, 1.0, 99.0);
            Assert.True(FollowFraming.Ease(step, 100.0, 0.05) > step);
            Assert.Equal(100.0, FollowFraming.Ease(99.8, 100.0, 0.016));
            Assert.Equal(0.0, FollowFraming.Ease(0.0, 100.0, 0.0));
            Assert.Equal(100.0, FollowFraming.Ease(0.0, 100.0, 10.0), 3);
        }
    }
}
