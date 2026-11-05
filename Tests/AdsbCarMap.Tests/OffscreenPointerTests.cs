using AdsbCarMap.Map;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the placement of the pointer towards an aircraft outside the visible part of the map.
    /// </summary>
    public class OffscreenPointerTests
    {
        /// <summary>
        /// An aircraft inside the visible part needs no pointer.
        /// </summary>
        [Fact]
        public void Place_Visible_IsNull()
        {
            Assert.Null(OffscreenPointer.Place(500.0, 800.0, 1000.0, 100.0, 1800.0, 50.0));
        }

        /// <summary>
        /// An aircraft far to the right gets a pointer on the right edge, pointing right.
        /// </summary>
        [Fact]
        public void Place_Right_IsOnRightEdge()
        {
            OffscreenPointer.Placement? placement = OffscreenPointer.Place(3000.0, 950.0, 1000.0, 100.0, 1800.0, 50.0);

            Assert.NotNull(placement);
            Assert.Equal(950.0f, placement.Value.X, 0.01f);
            Assert.Equal(950.0f, placement.Value.Y, 0.01f);
            Assert.Equal(90.0f, placement.Value.AngleDegrees, 0.01f);
        }

        /// <summary>
        /// An aircraft above the map gets a pointer on the top edge, pointing up.
        /// </summary>
        [Fact]
        public void Place_Above_IsOnTopEdgeBelowButtons()
        {
            OffscreenPointer.Placement? placement = OffscreenPointer.Place(500.0, -2000.0, 1000.0, 100.0, 1800.0, 50.0);

            Assert.NotNull(placement);
            Assert.Equal(500.0f, placement.Value.X, 0.01f);
            Assert.Equal(150.0f, placement.Value.Y, 0.01f);
            Assert.Equal(0.0f, placement.Value.AngleDegrees, 0.01f);
        }

        /// <summary>
        /// An aircraft under a card that covers the bottom gets a pointer above the card.
        /// </summary>
        [Fact]
        public void Place_UnderCard_IsAboveCard()
        {
            OffscreenPointer.Placement? placement = OffscreenPointer.Place(500.0, 1500.0, 1000.0, 100.0, 1000.0, 50.0);

            Assert.NotNull(placement);
            Assert.Equal(950.0f, placement.Value.Y, 0.01f);
            Assert.Equal(180.0f, placement.Value.AngleDegrees, 0.01f);
        }

        /// <summary>
        /// A diagonal direction gives an angle between the axes and a point on the edge.
        /// </summary>
        [Fact]
        public void Place_Diagonal_AngleIsBetweenAxes()
        {
            OffscreenPointer.Placement? placement = OffscreenPointer.Place(-5000.0, -5000.0, 1000.0, 0.0, 1000.0, 50.0);

            Assert.NotNull(placement);
            Assert.InRange(placement.Value.AngleDegrees, 270.0f, 360.0f);
            Assert.True(placement.Value.X >= 49.9f && placement.Value.Y >= 49.9f);
        }

        /// <summary>
        /// A visible part smaller than the margins gives no pointer.
        /// </summary>
        [Fact]
        public void Place_TooSmall_IsNull()
        {
            Assert.Null(OffscreenPointer.Place(5000.0, 0.0, 1000.0, 0.0, 80.0, 50.0));
        }

        /// <summary>
        /// An aircraft on the screen but in the strip reserved for the top buttons needs no pointer.
        /// </summary>
        [Fact]
        public void Place_InTopStrip_IsNull()
        {
            Assert.Null(OffscreenPointer.Place(500.0, 50.0, 1000.0, 100.0, 1800.0, 50.0));
        }
    }
}
