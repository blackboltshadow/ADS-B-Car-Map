namespace AdsbCarMap.DataSources.Management.Selection
{
    /// <summary>
    /// Builds the list of possible orders of the sources that the user can choose from.
    /// </summary>
    public static class SourceOrder
    {
        /// <summary>
        /// Separator between the names of the sources in the description of an order.
        /// </summary>
        public const string Separator = " → ";

        /// <summary>
        /// Returns all orders of the given names. The given order is first and the other orders follow in a stable order, so a stored index keeps its meaning when the list of names does not change.
        /// </summary>
        /// <param name="names">Names of the sources.</param>
        /// <returns>All permutations of the names.</returns>
        public static IReadOnlyList<IReadOnlyList<string>> GetOrders(IReadOnlyList<string> names)
        {
            List<IReadOnlyList<string>> result = new List<IReadOnlyList<string>>();
            Permute(new List<string>(), new List<string>(names), result);

            return result;
        }

        /// <summary>
        /// Describes an order with the names joined by an arrow, e.g. "adsb.lol → airplanes.live".
        /// </summary>
        /// <param name="order">Names in the order of preference.</param>
        /// <returns>The description.</returns>
        public static string Describe(IReadOnlyList<string> order)
        {
            return string.Join(Separator, order);
        }

        /// <summary>
        /// Finds the position of an order in the list of orders.
        /// </summary>
        /// <param name="orders">All orders, as returned by <see cref="GetOrders"/>.</param>
        /// <param name="order">Order to find.</param>
        /// <returns>Index of the order in the list, or -1 when the list does not contain it.</returns>
        public static int IndexOf(IReadOnlyList<IReadOnlyList<string>> orders, IReadOnlyList<string> order)
        {
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i].SequenceEqual(order, StringComparer.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Moves one name to another position, as when the user drags it in the list.
        /// </summary>
        /// <param name="order">Names in the current order.</param>
        /// <param name="from">Index of the name to move.</param>
        /// <param name="to">Index at which the name ends up; values outside the list are limited to its ends.</param>
        /// <returns>A new list; the same content when the position does not change.</returns>
        public static IReadOnlyList<string> Move(IReadOnlyList<string> order, int from, int to)
        {
            List<string> result = new List<string>(order);

            if (from < 0 || from >= result.Count)
            {
                return result;
            }

            string name = result[from];
            result.RemoveAt(from);
            result.Insert(Math.Clamp(to, 0, result.Count), name);

            return result;
        }

        /// <summary>
        /// Appends all orders that start with the chosen prefix.
        /// </summary>
        /// <param name="prefix">Names already chosen.</param>
        /// <param name="rest">Names not chosen yet.</param>
        /// <param name="result">List to which the finished orders are added.</param>
        private static void Permute(List<string> prefix, List<string> rest, List<IReadOnlyList<string>> result)
        {
            if (rest.Count == 0)
            {
                result.Add(new List<string>(prefix));

                return;
            }

            for (int i = 0; i < rest.Count; i++)
            {
                List<string> nextPrefix = new List<string>(prefix) { rest[i] };
                List<string> nextRest = new List<string>(rest);
                nextRest.RemoveAt(i);
                Permute(nextPrefix, nextRest, result);
            }
        }
    }
}
