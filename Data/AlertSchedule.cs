namespace AdsbCarMap.Data
{
    /// <summary>
    /// Days of the week and quiet hours in which a rule may raise notifications. Outside of them the rule is still tracked, but it stays silent.
    /// </summary>
    public sealed class AlertSchedule
    {
        /// <summary>
        /// Mask with every day of the week set; bit number is the value of <see cref="DayOfWeek"/> (Sunday is bit 0).
        /// </summary>
        public const int AllDays = 0x7F;

        /// <summary>
        /// Number of minutes in a day.
        /// </summary>
        public const int MinutesPerDay = 24 * 60;

        /// <summary>
        /// Schedule without any restriction.
        /// </summary>
        public static readonly AlertSchedule Always = new AlertSchedule(AllDays, null, null);

        /// <summary>
        /// Creates a schedule.
        /// </summary>
        /// <param name="activeDays">Mask of the days of the week on which notifications are allowed; bit number is the value of <see cref="DayOfWeek"/>.</param>
        /// <param name="quietStartMinute">Minute of the day (0-1439) at which the quiet hours begin, or null when there are none.</param>
        /// <param name="quietEndMinute">Minute of the day (0-1439) at which the quiet hours end, or null when there are none. The quiet hours may pass midnight.</param>
        public AlertSchedule(int activeDays, int? quietStartMinute, int? quietEndMinute)
        {
            ActiveDays = activeDays & AllDays;
            bool hasQuietHours = quietStartMinute.HasValue && quietEndMinute.HasValue
                && quietStartMinute.Value >= 0 && quietStartMinute.Value < MinutesPerDay
                && quietEndMinute.Value >= 0 && quietEndMinute.Value < MinutesPerDay
                && quietStartMinute.Value != quietEndMinute.Value;
            QuietStartMinute = hasQuietHours ? quietStartMinute : null;
            QuietEndMinute = hasQuietHours ? quietEndMinute : null;
        }

        /// <summary>
        /// Mask of the days of the week on which notifications are allowed; bit number is the value of <see cref="DayOfWeek"/>.
        /// </summary>
        public int ActiveDays { get; }

        /// <summary>
        /// Minute of the day at which the quiet hours begin, or null when there are none.
        /// </summary>
        public int? QuietStartMinute { get; }

        /// <summary>
        /// Minute of the day at which the quiet hours end, or null when there are none.
        /// </summary>
        public int? QuietEndMinute { get; }

        /// <summary>
        /// Whether the schedule never silences the rule.
        /// </summary>
        public bool IsAlways => ActiveDays == AllDays && QuietStartMinute == null;

        /// <summary>
        /// Checks whether the rule may raise notifications at a time.
        /// </summary>
        /// <param name="localTime">Local time of the user.</param>
        /// <returns>False on a day that is switched off and within the quiet hours.</returns>
        public bool IsActive(DateTime localTime)
        {
            if ((ActiveDays & (1 << (int)localTime.DayOfWeek)) == 0)
            {
                return false;
            }

            if (QuietStartMinute is int start && QuietEndMinute is int end)
            {
                int minute = localTime.Hour * 60 + localTime.Minute;
                bool quiet = start < end ? minute >= start && minute < end : minute >= start || minute < end;

                return !quiet;
            }

            return true;
        }

        /// <summary>
        /// Checks whether a day of the week is allowed.
        /// </summary>
        /// <param name="day">Day of the week.</param>
        /// <returns>True when notifications are allowed on that day.</returns>
        public bool IsDayActive(DayOfWeek day)
        {
            return (ActiveDays & (1 << (int)day)) != 0;
        }

        /// <summary>
        /// Formats minutes of the day as HH:mm.
        /// </summary>
        /// <param name="minute">Minute of the day.</param>
        /// <returns>The time, for example "22:30".</returns>
        public static string FormatMinute(int minute)
        {
            return (minute / 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":" + (minute % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
