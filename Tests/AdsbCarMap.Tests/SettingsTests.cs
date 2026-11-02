using AdsbCarMap.Settings;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of setting definitions, the change history and airport category flags.
    /// </summary>
    public class SettingsTests
    {
        /// <summary>
        /// Values are clamped to the allowed range.
        /// </summary>
        [Theory]
        [InlineData(-5.0, 1.0)]
        [InlineData(0.5, 1.0)]
        [InlineData(7.0, 7.0)]
        [InlineData(99.0, 10.0)]
        public void Normalize_ClampsToRange(double value, double expected)
        {
            SettingDefinition definition = new SettingDefinition("k", "Label", 5.0, 1.0, 10.0, false);

            Assert.Equal(expected, definition.Normalize(value));
        }

        /// <summary>
        /// Integer settings are rounded after clamping.
        /// </summary>
        [Fact]
        public void Normalize_IntegerSetting_Rounds()
        {
            SettingDefinition definition = new SettingDefinition("k", "Label", 5.0, 1.0, 10.0, true);

            Assert.Equal(4.0, definition.Normalize(3.6));
            Assert.Equal(10.0, definition.Normalize(10.4));
            Assert.Equal(10.0, definition.Normalize(50.0));
        }

        /// <summary>
        /// A slider keeps its step and unit.
        /// </summary>
        [Fact]
        public void CreateSlider_SetsKindStepAndUnit()
        {
            SettingDefinition definition = SettingDefinition.CreateSlider("size", "Size", 1.0, 0.5, 2.0, 0.1, " x", "help");

            Assert.Equal(SettingKind.Slider, definition.Kind);
            Assert.Equal(0.1, definition.Step);
            Assert.Equal(" x", definition.Unit);
            Assert.Equal("help", definition.Help);
            Assert.False(definition.IsInteger);
        }

        /// <summary>
        /// A switch is an integer from zero to one with the default stored as a number.
        /// </summary>
        [Theory]
        [InlineData(true, 1.0)]
        [InlineData(false, 0.0)]
        public void CreateSwitch_StoresDefaultAsNumber(bool defaultValue, double expected)
        {
            SettingDefinition definition = SettingDefinition.CreateSwitch("sw", "Switch", defaultValue);

            Assert.Equal(SettingKind.Switch, definition.Kind);
            Assert.Equal(expected, definition.DefaultValue);
            Assert.Equal(0.0, definition.Minimum);
            Assert.Equal(1.0, definition.Maximum);
            Assert.True(definition.IsInteger);
        }

        /// <summary>
        /// A choice ranges over the indexes of its options.
        /// </summary>
        [Fact]
        public void CreateChoice_RangeMatchesChoices()
        {
            SettingDefinition definition = SettingDefinition.CreateChoice("c", "Choice", 1, null, "a", "b", "c");

            Assert.Equal(SettingKind.Choice, definition.Kind);
            Assert.Equal(0.0, definition.Minimum);
            Assert.Equal(2.0, definition.Maximum);
            Assert.Equal(1.0, definition.DefaultValue);
            Assert.Equal(3, definition.Choices!.Count);
        }

        /// <summary>
        /// A multiple choice ranges over all bit combinations of its options.
        /// </summary>
        [Fact]
        public void CreateMultiChoice_MaximumIsAllBitsSet()
        {
            SettingDefinition definition = SettingDefinition.CreateMultiChoice("m", "Multi", 3, null, "a", "b", "c", "d");

            Assert.Equal(SettingKind.MultiChoice, definition.Kind);
            Assert.Equal(15.0, definition.Maximum);
            Assert.Equal(3.0, definition.DefaultValue);
        }

        /// <summary>
        /// Values are popped in the reverse order of pushing.
        /// </summary>
        [Fact]
        public void SettingsHistory_PopsInReverseOrder()
        {
            string key = "history-order";
            SettingsHistory.Push(key, 1.0);
            SettingsHistory.Push(key, 2.0);

            Assert.Equal(2, SettingsHistory.Count(key));
            Assert.True(SettingsHistory.TryPop(key, out double second));
            Assert.Equal(2.0, second);
            Assert.True(SettingsHistory.TryPop(key, out double first));
            Assert.Equal(1.0, first);
            Assert.False(SettingsHistory.TryPop(key, out double none));
            Assert.Equal(0.0, none);
            Assert.Equal(0, SettingsHistory.Count(key));
        }

        /// <summary>
        /// Histories of different keys are independent.
        /// </summary>
        [Fact]
        public void SettingsHistory_KeysAreIndependent()
        {
            SettingsHistory.Push("history-a", 10.0);

            Assert.Equal(0, SettingsHistory.Count("history-b"));
            Assert.False(SettingsHistory.TryPop("history-b", out double _));
            Assert.True(SettingsHistory.TryPop("history-a", out double _));
        }

        /// <summary>
        /// The history keeps only the newest 200 entries.
        /// </summary>
        [Fact]
        public void SettingsHistory_DropsOldestBeyondLimit()
        {
            string key = "history-limit";

            for (int index = 0; index < 250; index++)
            {
                SettingsHistory.Push(key, index);
            }

            Assert.Equal(200, SettingsHistory.Count(key));
            Assert.True(SettingsHistory.TryPop(key, out double newest));
            Assert.Equal(249.0, newest);

            double oldest = 0.0;

            while (SettingsHistory.TryPop(key, out double value))
            {
                oldest = value;
            }

            Assert.Equal(50.0, oldest);
        }

        /// <summary>
        /// Airport categories are distinct bit flags.
        /// </summary>
        [Fact]
        public void AirportCategory_FlagsDoNotOverlap()
        {
            AirportCategory all = AirportCategory.Large | AirportCategory.Medium | AirportCategory.Small | AirportCategory.Military | AirportCategory.Heliport;

            Assert.Equal(31, (int)all);
            Assert.Equal(AirportCategory.None, all & AirportCategory.None);
            Assert.True(all.HasFlag(AirportCategory.Heliport));
        }
    }
}
