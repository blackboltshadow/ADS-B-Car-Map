using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Map;
using AdsbCarMap.Settings;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the texts and colors of the threat layer.
    /// </summary>
    public class ThreatTextTests
    {
        /// <summary>
        /// Current time used by the tests.
        /// </summary>
        private static readonly DateTime Now = new DateTime(2026, 11, 3, 18, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Creates a threat with the given properties.
        /// </summary>
        /// <param name="kind">Kind.</param>
        /// <param name="advisory">Whether it is only an observation.</param>
        /// <param name="areaOnly">Whether only the region is known.</param>
        /// <param name="heading">Heading or null.</param>
        /// <param name="count">Number of objects.</param>
        /// <returns>The threat.</returns>
        private static Threat Create(ThreatKind kind = ThreatKind.Uav, bool advisory = false, bool areaOnly = false, double? heading = 63.7, int count = 0)
        {
            return new Threat("a", kind, "БпЛА", "Чернігівська область", string.Empty, "Ніжин", 51.2, 31.8, heading, 150.0, ThreatConfidence.High, 3, count, Now.AddMinutes(-3), Now.AddMinutes(-4), ThreatStatus.Active, 10.0, false, false, false, advisory, areaOnly, string.Empty, new List<ThreatTrailPoint>());
        }

        /// <summary>
        /// Every kind has its own name and an unknown one gets a general name.
        /// </summary>
        [Fact]
        public void KindName_NamesEveryKind()
        {
            List<string> names = new List<string>();

            foreach (ThreatKind kind in Enum.GetValues<ThreatKind>())
            {
                names.Add(ThreatText.KindName(kind));
            }

            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.Equal("Dron", ThreatText.KindName(ThreatKind.Uav));
            Assert.Equal("Nieznane zagrożenie", ThreatText.KindName(ThreatKind.Unknown));
        }

        /// <summary>
        /// The confidence is named.
        /// </summary>
        [Fact]
        public void ConfidenceName_NamesEveryLevel()
        {
            Assert.Equal("wysoka", ThreatText.ConfidenceName(ThreatConfidence.High));
            Assert.Equal("średnia", ThreatText.ConfidenceName(ThreatConfidence.Medium));
            Assert.Equal("niska", ThreatText.ConfidenceName(ThreatConfidence.Low));
        }

        /// <summary>
        /// An observation is never drawn in a red tone, and every kind has a color.
        /// </summary>
        [Fact]
        public void ColorFor_AdvisoryIsNeutralAndKindsDiffer()
        {
            Assert.Equal(ThreatText.AdvisoryColor, ThreatText.ColorFor(Create(ThreatKind.Mig31k, advisory: true)));
            Assert.Equal(ThreatText.AdvisoryColor, ThreatText.ColorFor(Create(ThreatKind.Ballistic, advisory: true)));

            List<int> colors = new List<int>();

            foreach (ThreatKind kind in Enum.GetValues<ThreatKind>())
            {
                colors.Add(ThreatText.ColorFor(Create(kind)));
            }

            Assert.Equal(colors.Count, colors.Distinct().Count());
            Assert.DoesNotContain(ThreatText.AdvisoryColor, colors);
            Assert.All(colors, color => Assert.Equal(0xFF, (color >> 24) & 0xFF));
        }

        /// <summary>
        /// A threat of which only the region is known is not drawn as a point.
        /// </summary>
        [Fact]
        public void HasDrawablePosition_FalseForAreaOnly()
        {
            Assert.True(ThreatText.HasDrawablePosition(Create()));
            Assert.False(ThreatText.HasDrawablePosition(Create(areaOnly: true)));
        }

        /// <summary>
        /// The age is described in minutes and hours.
        /// </summary>
        [Fact]
        public void FormatAge_DescribesAge()
        {
            Assert.Equal(string.Empty, ThreatText.FormatAge(null, Now));
            Assert.Equal("przed chwilą", ThreatText.FormatAge(Now.AddSeconds(-20), Now));
            Assert.Equal("5 min temu", ThreatText.FormatAge(Now.AddMinutes(-5), Now));
            Assert.Equal("2 h temu", ThreatText.FormatAge(Now.AddHours(-2).AddMinutes(-10), Now));
            Assert.Equal("przed chwilą", ThreatText.FormatAge(Now.AddMinutes(5), Now));
        }

        /// <summary>
        /// The label shows the number of objects only for a group.
        /// </summary>
        [Fact]
        public void BuildLabel_AddsCountForGroups()
        {
            Assert.Equal("Dron", ThreatText.BuildLabel(Create(count: 0)));
            Assert.Equal("Dron", ThreatText.BuildLabel(Create(count: 1)));
            Assert.Equal("Dron ×5", ThreatText.BuildLabel(Create(count: 5)));
        }

        /// <summary>
        /// The description of a normal threat contains the place, the heading, the speed, the accuracy and the age.
        /// </summary>
        [Fact]
        public void BuildLines_DescribesThreat()
        {
            List<string> lines = ThreatText.BuildLines(Create(), Now, DisplayUnits.Default);

            Assert.Equal("Dron", lines[0]);
            Assert.Equal("Ніжин, Чернігівська область", lines[1]);
            Assert.Contains(lines, line => line.StartsWith("Kierunek: 64°"));
            Assert.Contains(lines, line => line.StartsWith("Prędkość: "));
            Assert.Contains(lines, line => line.StartsWith("Dokładność położenia: ±10"));
            Assert.Contains("Wiarygodność: wysoka, zgłoszeń: 3", lines);
            Assert.Contains("Aktualizacja: 3 min temu", lines);
        }

        /// <summary>
        /// For a threat of which only the region is known the description gives no heading, speed or accuracy.
        /// </summary>
        [Fact]
        public void BuildLines_AreaOnly_HasNoMovement()
        {
            List<string> lines = ThreatText.BuildLines(Create(areaOnly: true), Now, DisplayUnits.Default);

            Assert.Contains(lines, line => line.StartsWith("Znany jest tylko obwód"));
            Assert.DoesNotContain(lines, line => line.StartsWith("Kierunek"));
            Assert.DoesNotContain(lines, line => line.StartsWith("Prędkość"));
            Assert.DoesNotContain(lines, line => line.StartsWith("Dokładność"));
        }

        /// <summary>
        /// An observation says that it is not a signal to take shelter, and an assumed course is marked.
        /// </summary>
        [Fact]
        public void BuildLines_AdvisoryAndPresumptiveCourse_AreMarked()
        {
            Threat presumptive = Create(advisory: true) with { IsPresumptiveCourse = true, IsApproximate = true };

            List<string> lines = ThreatText.BuildLines(presumptive, Now, DisplayUnits.Default);

            Assert.Contains("Obserwacja, nie sygnał do schronienia.", lines);
            Assert.Contains(lines, line => line.StartsWith("Kierunek") && line.EndsWith("(zakładany)"));
            Assert.Contains(lines, line => line.EndsWith("(przybliżone)"));
        }

        /// <summary>
        /// A heading worked out from the movement is marked, and an assumed course keeps its own mark.
        /// </summary>
        [Fact]
        public void BuildLines_DerivedHeading_IsMarked()
        {
            Threat derived = Create() with { IsHeadingDerived = true };
            Threat both = Create() with { IsHeadingDerived = true, IsPresumptiveCourse = true };

            Assert.Contains(ThreatText.BuildLines(derived, Now, DisplayUnits.Default), line => line.StartsWith("Kierunek") && line.EndsWith("(z ruchu)"));
            Assert.Contains(ThreatText.BuildLines(both, Now, DisplayUnits.Default), line => line.StartsWith("Kierunek") && line.EndsWith("(zakładany)"));
        }

        /// <summary>
        /// The color of a kind is the color of a threat of that kind, unless the threat is only an observation.
        /// </summary>
        [Fact]
        public void ColorFor_KindMatchesThreat()
        {
            foreach (ThreatKind kind in Enum.GetValues<ThreatKind>())
            {
                Assert.Equal(ThreatText.ColorFor(kind), ThreatText.ColorFor(Create(kind)));
            }
        }

        /// <summary>
        /// Missing parts are left out and an old track is marked.
        /// </summary>
        [Fact]
        public void BuildLines_MissingParts_AreLeftOut()
        {
            Threat bare = Create(heading: null) with { Region = string.Empty, Locality = " ", SpeedKmh = null, UncertaintyKm = 0.0, UpdatedUtc = null };
            List<string> lines = ThreatText.BuildLines(bare, Now, DisplayUnits.Default);

            Assert.Equal(2, lines.Count);
            Assert.Equal("Dron", lines[0]);

            Threat stale = Create() with { Status = ThreatStatus.Stale };

            Assert.Contains(ThreatText.BuildLines(stale, Now, DisplayUnits.Default), line => line.EndsWith("(dawno niepotwierdzane)"));
        }

        /// <summary>
        /// The state text depends on the state of the feed.
        /// </summary>
        [Fact]
        public void BuildStateText_DescribesState()
        {
            Assert.Equal("zagrożeń: 7", ThreatText.BuildStateText(true, false, false, 7));
            Assert.Equal("zagrożeń: 0", ThreatText.BuildStateText(false, true, false, 0));
            Assert.Equal("łączenie…", ThreatText.BuildStateText(false, false, true, 0));
            Assert.Equal("brak połączenia", ThreatText.BuildStateText(false, false, false, 0));
        }

        /// <summary>
        /// The attribution and the warning are not empty.
        /// </summary>
        [Fact]
        public void AttributionAndDisclaimer_AreSet()
        {
            Assert.Equal("Dane: NEPTUN", ThreatText.Attribution);
            Assert.Contains("oficjalnymi sygnałami", ThreatText.Disclaimer);
            Assert.Contains("oficjalne sygnały alarmowe", ThreatText.ShortDisclaimer);
        }
    }
}
