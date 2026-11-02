using AdsbCarMap.DataSources.Management.Selection;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of the list of orders that the user can choose from.
    /// </summary>
    public class SourceOrderTests
    {
        /// <summary>
        /// The given order is first, so a stored index keeps its meaning.
        /// </summary>
        [Fact]
        public void GetOrders_TwoNames_GivenOrderIsFirst()
        {
            IReadOnlyList<IReadOnlyList<string>> orders = SourceOrder.GetOrders(new List<string> { "a", "b" });

            Assert.Equal(2, orders.Count);
            Assert.Equal("a → b", SourceOrder.Describe(orders[0]));
            Assert.Equal("b → a", SourceOrder.Describe(orders[1]));
        }

        /// <summary>
        /// Three names give six different orders.
        /// </summary>
        [Fact]
        public void GetOrders_ThreeNames_SixDistinctOrders()
        {
            IReadOnlyList<IReadOnlyList<string>> orders = SourceOrder.GetOrders(new List<string> { "a", "b", "c" });

            Assert.Equal(6, orders.Count);
            Assert.Equal(6, orders.Select(SourceOrder.Describe).Distinct().Count());
            Assert.Equal("a → b → c", SourceOrder.Describe(orders[0]));
        }

        /// <summary>
        /// The position of an order is found regardless of the letter case and a missing order gives -1.
        /// </summary>
        [Fact]
        public void IndexOf_FindsOrder()
        {
            IReadOnlyList<IReadOnlyList<string>> orders = SourceOrder.GetOrders(new List<string> { "a", "b" });

            Assert.Equal(1, SourceOrder.IndexOf(orders, new List<string> { "B", "a" }));
            Assert.Equal(0, SourceOrder.IndexOf(orders, new List<string> { "a", "b" }));
            Assert.Equal(-1, SourceOrder.IndexOf(orders, new List<string> { "a", "c" }));
        }

        /// <summary>
        /// Dragging a name moves it and shifts the others, and positions outside the list are limited to its ends.
        /// </summary>
        [Fact]
        public void Move_MovesNameAndLimitsPosition()
        {
            IReadOnlyList<string> order = new List<string> { "a", "b", "c" };

            Assert.Equal(new List<string> { "b", "c", "a" }, SourceOrder.Move(order, 0, 2));
            Assert.Equal(new List<string> { "c", "a", "b" }, SourceOrder.Move(order, 2, 0));
            Assert.Equal(new List<string> { "a", "c", "b" }, SourceOrder.Move(order, 1, 99));
            Assert.Equal(new List<string> { "b", "a", "c" }, SourceOrder.Move(order, 0, 1));
            Assert.Equal(order, SourceOrder.Move(order, 5, 0));
            Assert.Equal(order, SourceOrder.Move(order, 1, 1));
        }
    }
}
