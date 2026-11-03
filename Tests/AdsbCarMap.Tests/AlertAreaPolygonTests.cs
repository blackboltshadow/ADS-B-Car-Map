using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the validation of alert polygons: edges that cross, touch or fold back make a polygon invalid.
    /// </summary>
    public class AlertAreaPolygonTests
    {
        /// <summary>
        /// Creates a list of vertices from latitude and longitude pairs.
        /// </summary>
        /// <param name="coordinates">Latitude and longitude pairs.</param>
        /// <returns>The vertices.</returns>
        private static List<GeoPoint> Points(params double[] coordinates)
        {
            List<GeoPoint> points = new List<GeoPoint>();

            for (int i = 0; i + 1 < coordinates.Length; i += 2)
            {
                points.Add(new GeoPoint(coordinates[i], coordinates[i + 1]));
            }

            return points;
        }

        /// <summary>
        /// Convex and concave polygons without crossings are valid, in either direction.
        /// </summary>
        [Fact]
        public void IsSimple_ValidPolygons()
        {
            Assert.True(AlertArea.IsSimple(Points(0, 0, 0, 2, 2, 2, 2, 0)));
            Assert.True(AlertArea.IsSimple(Points(2, 0, 2, 2, 0, 2, 0, 0)));
            Assert.True(AlertArea.IsSimple(Points(0, 0, 0, 4, 4, 4, 4, 0, 2, 2)));
            Assert.True(AlertArea.IsSimple(Points(0, 0, 0, 1, 0, 2, 2, 1)));
        }

        /// <summary>
        /// A bow tie, whose edges cross, is invalid.
        /// </summary>
        [Fact]
        public void IsSimple_BowTie_IsInvalid()
        {
            Assert.False(AlertArea.IsSimple(Points(0, 0, 2, 2, 0, 2, 2, 0)));
        }

        /// <summary>
        /// The closing edge from the last vertex to the first counts as well.
        /// </summary>
        [Fact]
        public void IsSimple_ClosingEdgeCrossing_IsInvalid()
        {
            Assert.False(AlertArea.IsSimple(Points(0, 0, 0, 4, 4, 4, 4, 1, -2, 1)));
        }

        /// <summary>
        /// Edges that only touch each other, a repeated vertex and a path that folds back are invalid.
        /// </summary>
        [Fact]
        public void IsSimple_TouchingRepeatedAndFoldedBack_AreInvalid()
        {
            Assert.False(AlertArea.IsSimple(Points(0, 0, 0, 4, 4, 4, 4, 0, 0, 2)));
            Assert.False(AlertArea.IsSimple(Points(0, 0, 0, 2, 0, 2, 2, 1)));
            Assert.False(AlertArea.IsSimple(Points(0, 0, 0, 4, 0, 2, 2, 2)));
        }

        /// <summary>
        /// Too few vertices and vertices on one line (no area) are invalid.
        /// </summary>
        [Fact]
        public void IsSimple_TooFewOrFlat_AreInvalid()
        {
            Assert.False(AlertArea.IsSimple(Points(0, 0, 1, 1)));
            Assert.False(AlertArea.IsSimple(Points(0, 0, 1, 1, 2, 2)));
        }

        /// <summary>
        /// A rule with crossing edges is dropped when the rules are read.
        /// </summary>
        [Fact]
        public void Parse_CrossingPolygon_IsDropped()
        {
            Assert.Empty(AlertRuleJson.Parse("[{\"id\":\"a\",\"area\":\"Polygon\",\"vertices\":[[0,0],[2,2],[0,2],[2,0]]}]"));
        }
    }
}
