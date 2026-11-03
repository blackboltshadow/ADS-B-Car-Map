namespace AdsbCarMap.Data
{
    /// <summary>
    /// The scale of the slider that chooses the band of heights of the airspace filter. A straight scale from the ground to flight level 660 would leave a few steps for the heights where most of the small aircraft fly, so the steps get coarser with the height: 500 ft up to 10 000 ft, 1000 ft up to 30 000 ft and 2000 ft above. The scale only holds the heights; how they are written is up to the unit chosen by the user.
    /// </summary>
    public static class AirspaceAltitudeScale
    {
        /// <summary>
        /// Heights of the steps in feet, in the increasing order.
        /// </summary>
        private static readonly IReadOnlyList<int> Steps = CreateSteps();

        /// <summary>
        /// Number of the steps.
        /// </summary>
        public static int Count => Steps.Count;

        /// <summary>
        /// Index of the last step, which stands for the greatest height of the airspace.
        /// </summary>
        public static int LastIndex => Steps.Count - 1;

        /// <summary>
        /// Gives the height of a step.
        /// </summary>
        /// <param name="index">Index of the step; a value out of range is moved to the nearest end.</param>
        /// <returns>Height in feet.</returns>
        public static int FeetAt(int index)
        {
            return Steps[Math.Clamp(index, 0, Steps.Count - 1)];
        }

        /// <summary>
        /// Finds the step nearest to a height.
        /// </summary>
        /// <param name="feet">Height in feet.</param>
        /// <returns>Index of the nearest step; the lower one when the height is exactly between two steps.</returns>
        public static int IndexOf(int feet)
        {
            int best = 0;

            for (int i = 1; i < Steps.Count; i++)
            {
                if (Math.Abs(Steps[i] - feet) < Math.Abs(Steps[best] - feet))
                {
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// Builds the list of the steps.
        /// </summary>
        /// <returns>Heights in feet.</returns>
        private static IReadOnlyList<int> CreateSteps()
        {
            List<int> steps = new List<int>();

            for (int feet = 0; feet <= 10000; feet += 500)
            {
                steps.Add(feet);
            }

            for (int feet = 11000; feet <= 30000; feet += 1000)
            {
                steps.Add(feet);
            }

            for (int feet = 32000; feet <= 66000; feet += 2000)
            {
                steps.Add(feet);
            }

            return steps;
        }
    }
}
