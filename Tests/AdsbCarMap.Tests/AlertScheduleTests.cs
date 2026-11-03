using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the days and quiet hours of an alert schedule.
    /// </summary>
    public class AlertScheduleTests
    {
        /// <summary>
        /// Tuesday 2026-11-03 at the given time.
        /// </summary>
        /// <param name="hour">Hour.</param>
        /// <param name="minute">Minute.</param>
        /// <returns>The time.</returns>
        private static DateTime Tuesday(int hour, int minute)
        {
            return new DateTime(2026, 11, 3, hour, minute, 0);
        }

        /// <summary>
        /// The schedule without restrictions is always active.
        /// </summary>
        [Fact]
        public void Always_IsAlwaysActive()
        {
            Assert.True(AlertSchedule.Always.IsAlways);
            Assert.True(AlertSchedule.Always.IsActive(Tuesday(3, 0)));
            Assert.True(AlertSchedule.Always.IsActive(new DateTime(2026, 11, 8, 12, 0, 0)));
        }

        /// <summary>
        /// Quiet hours that pass midnight silence both sides of it and end exactly at the end minute.
        /// </summary>
        [Theory]
        [InlineData(21, 59, true)]
        [InlineData(22, 0, false)]
        [InlineData(23, 30, false)]
        [InlineData(0, 0, false)]
        [InlineData(5, 59, false)]
        [InlineData(6, 0, true)]
        [InlineData(12, 0, true)]
        public void QuietHoursAcrossMidnight(int hour, int minute, bool expected)
        {
            AlertSchedule schedule = new AlertSchedule(AlertSchedule.AllDays, 22 * 60, 6 * 60);

            Assert.Equal(expected, schedule.IsActive(Tuesday(hour, minute)));
        }

        /// <summary>
        /// Quiet hours within one day.
        /// </summary>
        [Fact]
        public void QuietHoursWithinDay()
        {
            AlertSchedule schedule = new AlertSchedule(AlertSchedule.AllDays, 9 * 60, 17 * 60);

            Assert.False(schedule.IsActive(Tuesday(9, 0)));
            Assert.False(schedule.IsActive(Tuesday(16, 59)));
            Assert.True(schedule.IsActive(Tuesday(17, 0)));
            Assert.True(schedule.IsActive(Tuesday(8, 59)));
        }

        /// <summary>
        /// A day that is switched off silences the whole day.
        /// </summary>
        [Fact]
        public void DaysOff_SilenceTheWholeDay()
        {
            AlertSchedule weekdays = new AlertSchedule((1 << (int)DayOfWeek.Monday) | (1 << (int)DayOfWeek.Tuesday), null, null);

            Assert.True(weekdays.IsActive(Tuesday(12, 0)));
            Assert.False(weekdays.IsActive(new DateTime(2026, 11, 4, 12, 0, 0)));
            Assert.True(weekdays.IsDayActive(DayOfWeek.Monday));
            Assert.False(weekdays.IsDayActive(DayOfWeek.Sunday));
            Assert.False(weekdays.IsAlways);
        }

        /// <summary>
        /// Quiet hours with equal or out of range ends are ignored.
        /// </summary>
        [Theory]
        [InlineData(600, 600)]
        [InlineData(-1, 100)]
        [InlineData(100, 1440)]
        public void InvalidQuietHours_AreIgnored(int start, int end)
        {
            AlertSchedule schedule = new AlertSchedule(AlertSchedule.AllDays, start, end);

            Assert.Null(schedule.QuietStartMinute);
            Assert.Null(schedule.QuietEndMinute);
            Assert.True(schedule.IsAlways);
        }

        /// <summary>
        /// Times are formatted as HH:mm.
        /// </summary>
        [Fact]
        public void FormatMinute_PadsWithZeros()
        {
            Assert.Equal("06:05", AlertSchedule.FormatMinute(6 * 60 + 5));
            Assert.Equal("23:59", AlertSchedule.FormatMinute(AlertSchedule.MinutesPerDay - 1));
        }
    }
}
