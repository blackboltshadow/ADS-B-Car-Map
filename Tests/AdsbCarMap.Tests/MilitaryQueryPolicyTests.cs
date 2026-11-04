using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the decision how the aircraft are downloaded in the mode that shows only military aircraft.
    /// </summary>
    public class MilitaryQueryPolicyTests
    {
        /// <summary>
        /// The request for all the military aircraft is wanted only when the mode and the setting are both on.
        /// </summary>
        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, false)]
        public void UseGlobalRequest_NeedsModeAndSetting(bool militaryOnly, bool settingOn, bool expected)
        {
            Assert.Equal(expected, MilitaryQueryPolicy.UseGlobalRequest(militaryOnly, settingOn));
        }

        /// <summary>
        /// After a global request the longer of the two pauses is used, otherwise always the usual one.
        /// </summary>
        [Theory]
        [InlineData(true, 3, 30, 30)]
        [InlineData(true, 60, 30, 60)]
        [InlineData(true, 30, 30, 30)]
        [InlineData(false, 3, 30, 3)]
        [InlineData(false, 60, 30, 60)]
        public void GetInterval_UsesTheLongerPauseAfterGlobalRequest(bool globalUsed, int normalSeconds, int militarySeconds, int expectedSeconds)
        {
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), MilitaryQueryPolicy.GetInterval(globalUsed, TimeSpan.FromSeconds(normalSeconds), TimeSpan.FromSeconds(militarySeconds)));
        }
    }
}
