using AdsbCarMap.DataSources.Models;
using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the aircraft type table and of the type filter that uses it. They replace the table used by the filters, so they run one after another.
    /// </summary>
    [Collection("TypeTable")]
    public class AircraftTypeTableTests
    {
        /// <summary>
        /// Route lookup that knows no route.
        /// </summary>
        private static readonly Func<string?, FlightRoute?> NoRoutes = (string? callsign) => null;

        /// <summary>
        /// Small table used by most tests.
        /// </summary>
        private static readonly string[] SampleLines = new string[]
        {
            "iata\ticao\tfamily\tname",
            "738\tB738\tB737\tBoeing 737-800",
            "73H\tB738\tB737\tBoeing 737-800 (winglets)",
            "7M8\tB38M\tB737\tBoeing 737 MAX 8",
            "733\tB733\tB737\tBoeing 737-300",
            "320\tA320\tA320\tAirbus A320",
            "32N\tA20N\tA320\tAirbus A320neo",
            "\tC172\t\tCessna 172",
            "# comment",
            "",
            "bad line",
        };

        /// <summary>
        /// Reads the table file bundled with the application.
        /// </summary>
        /// <returns>The table.</returns>
        private static AircraftTypeTable LoadBundled()
        {
            return AircraftTypeTable.Parse(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Assets", "aircraft-types.tsv")));
        }

        /// <summary>
        /// Creates an aircraft of a given type.
        /// </summary>
        /// <param name="typeCode">ICAO type code or null.</param>
        /// <returns>The aircraft.</returns>
        private static Aircraft CreateAircraft(string? typeCode)
        {
            return new Aircraft("489123", "LOT281", 52.0, 21.0, 35000, false, 450.0, 90.0, 0.0, typeCode, null, "A3");
        }

        /// <summary>
        /// Evaluates a type condition against a table.
        /// </summary>
        /// <param name="table">Table in use.</param>
        /// <param name="text">Text of the condition.</param>
        /// <param name="family">Whether the condition covers the family.</param>
        /// <param name="typeCode">Type of the aircraft.</param>
        /// <returns>Result of the condition.</returns>
        private static FilterResult Evaluate(AircraftTypeTable table, string text, bool family, string? typeCode)
        {
            AircraftTypeTable previous = AircraftTypeTable.Current;

            AircraftTypeTable.Current = table;

            try
            {
                return FilterCondition.OfType(text, family).Evaluate(CreateAircraft(typeCode), NoRoutes);
            }
            finally
            {
                AircraftTypeTable.Current = previous;
            }
        }

        /// <summary>
        /// Lines that are empty, comments or too short are skipped and the family defaults to the ICAO code.
        /// </summary>
        [Fact]
        public void Parse_SkipsInvalidLinesAndDefaultsFamily()
        {
            AircraftTypeTable table = AircraftTypeTable.Parse(SampleLines);

            Assert.Equal(6, table.TypeCount);
            Assert.Equal(7, table.Entries.Count);
            Assert.Equal("C172", table.Entries[6].Family);
        }

        /// <summary>
        /// Knowing a type and finding its name ignore the letter case and surrounding spaces.
        /// </summary>
        [Fact]
        public void IsKnownAndFindName_IgnoreCase()
        {
            AircraftTypeTable table = AircraftTypeTable.Parse(SampleLines);

            Assert.True(table.IsKnown(" b738 "));
            Assert.False(table.IsKnown("B739"));
            Assert.False(table.IsKnown(null));
            Assert.False(table.IsKnown("  "));
            Assert.Equal("Boeing 737-800", table.FindName("b738"));
            Assert.Null(table.FindName("B739"));
            Assert.Null(table.FindName(null));
        }

        /// <summary>
        /// The IATA code gives the ICAO codes of all rows with it, and an unknown text stays as typed.
        /// </summary>
        [Fact]
        public void Resolve_Exact_ExpandsIataAndKeepsTypedText()
        {
            AircraftTypeTable table = AircraftTypeTable.Parse(SampleLines);

            Assert.Contains("B738", table.Resolve("73h", false));
            Assert.DoesNotContain("B38M", table.Resolve("738", false));
            Assert.Contains("B38M", table.Resolve("B38M", false));
            Assert.Equal(new string[] { "ZZZ9" }, table.Resolve("zzz9", false).ToArray());
            Assert.Empty(table.Resolve("  ", false));
            Assert.Empty(table.Resolve(null, false));
        }

        /// <summary>
        /// With the family every type of the family is accepted, whether the family was reached by an ICAO code, an IATA code or its name.
        /// </summary>
        [Theory]
        [InlineData("738")]
        [InlineData("B738")]
        [InlineData("7m8")]
        [InlineData("B737")]
        public void Resolve_Family_CoversWholeFamily(string text)
        {
            AircraftTypeTable table = AircraftTypeTable.Parse(SampleLines);
            HashSet<string> codes = table.Resolve(text, true);

            Assert.Contains("B738", codes);
            Assert.Contains("B38M", codes);
            Assert.Contains("B733", codes);
            Assert.DoesNotContain("A320", codes);
        }

        /// <summary>
        /// The name of a family is not an exact type, so it matches nothing in the exact mode.
        /// </summary>
        [Fact]
        public void Resolve_Exact_DoesNotUseFamilyName()
        {
            AircraftTypeTable table = AircraftTypeTable.Parse(SampleLines);

            Assert.DoesNotContain("B738", table.Resolve("B737", false));
        }

        /// <summary>
        /// The type condition accepts an IATA code and the whole family when asked for it, and stays unknown without a type.
        /// </summary>
        [Fact]
        public void TypeCondition_UsesTableForIataAndFamily()
        {
            AircraftTypeTable table = AircraftTypeTable.Parse(SampleLines);

            Assert.Equal(FilterResult.True, Evaluate(table, "738", false, "B738"));
            Assert.Equal(FilterResult.False, Evaluate(table, "738", false, "B38M"));
            Assert.Equal(FilterResult.True, Evaluate(table, "738", true, "B38M"));
            Assert.Equal(FilterResult.False, Evaluate(table, "738", true, "A320"));
            Assert.Equal(FilterResult.Unknown, Evaluate(table, "738", true, null));
        }

        /// <summary>
        /// Without a table the condition still accepts the exact ICAO code, as before the table existed.
        /// </summary>
        [Fact]
        public void TypeCondition_WithEmptyTable_MatchesExactCode()
        {
            Assert.Equal(FilterResult.True, Evaluate(AircraftTypeTable.Empty, "b738", false, "B738"));
            Assert.Equal(FilterResult.False, Evaluate(AircraftTypeTable.Empty, "738", false, "B738"));
        }

        /// <summary>
        /// A condition computed against one table is recomputed when the table is replaced.
        /// </summary>
        [Fact]
        public void TypeCondition_RecomputesWhenTableChanges()
        {
            FilterCondition condition = FilterCondition.OfType("738");
            Aircraft aircraft = CreateAircraft("B738");
            AircraftTypeTable previous = AircraftTypeTable.Current;

            try
            {
                AircraftTypeTable.Current = AircraftTypeTable.Empty;
                Assert.Equal(FilterResult.False, condition.Evaluate(aircraft, NoRoutes));

                AircraftTypeTable.Current = AircraftTypeTable.Parse(SampleLines);
                Assert.Equal(FilterResult.True, condition.Evaluate(aircraft, NoRoutes));
            }
            finally
            {
                AircraftTypeTable.Current = previous;
            }
        }

        /// <summary>
        /// The family flag is kept in JSON, in the draft and in the descriptions, and only for the type field.
        /// </summary>
        [Fact]
        public void Family_IsStoredDescribedAndCopied()
        {
            AircraftFilter filter = new AircraftFilter("1", "737", FilterMode.Show, FilterCondition.OfType("738", true));
            AircraftFilter restored = FilterJson.Parse(FilterJson.Serialize(new List<AircraftFilter> { filter }))[0];
            FilterCondition condition = (FilterCondition)restored.Expression;

            Assert.True(condition.Family);
            Assert.Equal("rodzina typu 738", FilterDescriber.DescribeCondition(condition));
            Assert.Equal("Pokazuje każdy samolot, który należy do rodziny typu 738.", FilterDescriber.DescribeSentence(restored));
            Assert.False(FilterCondition.Create(FilterField.CallsignPrefix, "LOT", AircraftKind.Unknown, null, null, true).Family);

            FilterDraftCondition draft = new FilterDraftCondition();

            draft.Field = FilterField.TypeCode;
            draft.Text = "738";
            draft.Family = true;

            Assert.True(((FilterCondition)draft.Clone().ToNode()).Family);
        }

        /// <summary>
        /// An old filter without the family flag is read as the exact mode.
        /// </summary>
        [Fact]
        public void Family_MissingInJson_IsExact()
        {
            string json = "[{\"id\":\"1\",\"name\":\"n\",\"mode\":\"Show\",\"expr\":{\"type\":\"cond\",\"field\":\"TypeCode\",\"text\":\"B738\"}}]";

            Assert.False(((FilterCondition)FilterJson.Parse(json)[0].Expression).Family);
        }

        /// <summary>
        /// The bundled table has no duplicated rows, every row has a name and the families reach the main types.
        /// </summary>
        [Fact]
        public void BundledTable_IsConsistent()
        {
            AircraftTypeTable table = LoadBundled();
            HashSet<string> rows = new HashSet<string>(StringComparer.Ordinal);

            Assert.True(table.TypeCount > 150);

            foreach (AircraftTypeEntry entry in table.Entries)
            {
                Assert.True(rows.Add(entry.Iata + "|" + entry.Icao), "Duplicated row " + entry.Iata + " " + entry.Icao);
                Assert.False(string.IsNullOrWhiteSpace(entry.Name), "No name for " + entry.Icao);
                Assert.Matches("^[A-Z0-9]{2,4}$", entry.Icao);
                Assert.True(entry.Iata.Length == 0 || entry.Iata.Length == 3, "IATA code of " + entry.Icao + " is not 3 characters");
            }

            HashSet<string> boeing737 = table.Resolve("738", true);

            Assert.Contains("B38M", boeing737);
            Assert.Contains("B739", boeing737);
            Assert.Contains("A21N", table.Resolve("320", true));
            Assert.Equal("Airbus A321neo", table.FindName("A21N"));
        }
    }
}
