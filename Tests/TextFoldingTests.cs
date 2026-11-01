using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the text normalization and of the airport model that depends on it.
    /// </summary>
    public class TextFoldingTests
    {
        /// <summary>
        /// Diacritics are removed and letters are lowered.
        /// </summary>
        [Theory]
        [InlineData("Kraków", "krakow")]
        [InlineData("ÓDŹ", "odz")]
        [InlineData("żółć gęślą jaźń", "zolc gesla jazn")]
        [InlineData("São Paulo", "sao paulo")]
        [InlineData("Düsseldorf", "dusseldorf")]
        [InlineData("EPWA", "epwa")]
        [InlineData("", "")]
        public void Fold_RemovesDiacriticsAndLowersCase(string text, string expected)
        {
            Assert.Equal(expected, TextFolding.Fold(text));
        }


        /// <summary>
        /// Letters without a Unicode decomposition are replaced with their ASCII spelling, so "lodz" matches "Łódź".
        /// </summary>
        [Theory]
        [InlineData("Łódź", "lodz")]
        [InlineData("ŁÓDŹ", "lodz")]
        [InlineData("Zażółć gęślą jaźń", "zazolc gesla jazn")]
        [InlineData("Jana Pawła II", "jana pawla ii")]
        [InlineData("Ørland", "orland")]
        [InlineData("Reykjavík Đakovo Þ", "reykjavik dakovo þ")]
        [InlineData("Straße", "strasse")]
        [InlineData("Œuvre Æsir", "oeuvre aesir")]
        public void Fold_LettersWithoutDecomposition_AreReplaced(string text, string expected)
        {
            Assert.Equal(expected, TextFolding.Fold(text));
        }

        /// <summary>
        /// A search term typed without diacritics is found in the search text of an airport with Polish letters.
        /// </summary>
        [Fact]
        public void Airport_SearchText_ContainsTermTypedWithoutPolishLetters()
        {
            Airport airport = new Airport("EPLL", "LCJ", AirportType.Medium, "Łódź Władysław Reymont", "Łódź", "PL", 51.72, 19.39, 604, true, false);

            Assert.Contains(TextFolding.Fold("lodz"), airport.SearchText);
            Assert.Contains(TextFolding.Fold("wladyslaw"), airport.SearchText);
        }

        /// <summary>
        /// Folding twice gives the same result as folding once.
        /// </summary>
        [Fact]
        public void Fold_IsIdempotent()
        {
            string once = TextFolding.Fold("Świnoujście Ørland");

            Assert.Equal(once, TextFolding.Fold(once));
        }

        /// <summary>
        /// A pre-composed and a decomposed form of the same letter fold to the same text.
        /// </summary>
        [Fact]
        public void Fold_ComposedAndDecomposedForms_Match()
        {
            Assert.Equal(TextFolding.Fold("é"), TextFolding.Fold("é"));
        }

        /// <summary>
        /// The search text of an airport contains codes, name and city folded together.
        /// </summary>
        [Fact]
        public void Airport_SearchText_IsFoldedCombination()
        {
            Airport airport = new Airport("EPKK", "KRK", AirportType.Large, "Kraków Jana Pawła II", "Kraków", "PL", 50.07, 19.78, 791, true, false);

            Assert.Equal("epkk krk krakow jana pawla ii krakow", airport.SearchText);
        }

        /// <summary>
        /// The displayed code is IATA when known and ICAO otherwise.
        /// </summary>
        [Fact]
        public void Airport_Code_PrefersIata()
        {
            Airport withIata = new Airport("EPWA", "WAW", AirportType.Large, "Chopin", "Warsaw", "PL", 52.16, 20.97, 362, true, false);
            Airport withoutIata = new Airport("EPMB", string.Empty, AirportType.Small, "Malbork", "Malbork", "PL", 54.02, 19.13, 20, false, true);

            Assert.Equal("WAW", withIata.Code);
            Assert.Equal("EPMB", withoutIata.Code);
            Assert.True(withoutIata.IsMilitary);
            Assert.False(withoutIata.HasScheduledService);
        }

        /// <summary>
        /// The airport keeps the values given to the constructor, and the long paved runway flag defaults to false.
        /// </summary>
        [Fact]
        public void Airport_KeepsConstructorValues()
        {
            Airport airport = new Airport("EPWA", "WAW", AirportType.Medium, "Chopin", "Warsaw", "PL", 52.16, 20.97, null, true, false);

            Assert.Equal("EPWA", airport.Icao);
            Assert.Equal(AirportType.Medium, airport.Type);
            Assert.Equal("PL", airport.Country);
            Assert.Equal(52.16, airport.Latitude);
            Assert.Equal(20.97, airport.Longitude);
            Assert.Null(airport.ElevationFeet);
            Assert.False(airport.HasLongPavedRunway);
        }

        /// <summary>
        /// Runway records compare by value.
        /// </summary>
        [Fact]
        public void Runway_ComparesByValue()
        {
            Runway first = new Runway(10000, 150, "ASP", true, "11", 52.0, 20.9, "29", 52.1, 21.0);
            Runway second = new Runway(10000, 150, "ASP", true, "11", 52.0, 20.9, "29", 52.1, 21.0);

            Assert.Equal(first, second);
        }
    }
}
