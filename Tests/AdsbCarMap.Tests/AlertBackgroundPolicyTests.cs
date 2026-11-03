using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the decision when the background checking of the alerts runs.
    /// </summary>
    public class AlertBackgroundPolicyTests
    {
        /// <summary>
        /// The checking runs only when every condition is met.
        /// </summary>
        [Theory]
        [InlineData(true, true, 1, true)]
        [InlineData(true, true, 5, true)]
        [InlineData(false, true, 1, false)]
        [InlineData(true, false, 1, false)]
        [InlineData(true, true, 0, false)]
        public void ShouldRun_DependsOnAllConditions(bool background, bool alerts, int rules, bool expected)
        {
            Assert.Equal(expected, AlertBackgroundPolicy.ShouldRun(background, alerts, rules));
        }

        /// <summary>
        /// The start after boot additionally needs the permission of the user.
        /// </summary>
        [Theory]
        [InlineData(true, true, true, 1, true)]
        [InlineData(false, true, true, 1, false)]
        [InlineData(true, false, true, 1, false)]
        [InlineData(true, true, false, 1, false)]
        [InlineData(true, true, true, 0, false)]
        public void ShouldStartOnBoot_NeedsSettingAndRunConditions(bool startOnBoot, bool background, bool alerts, int rules, bool expected)
        {
            Assert.Equal(expected, AlertBackgroundPolicy.ShouldStartOnBoot(startOnBoot, background, alerts, rules));
        }
    }
}
