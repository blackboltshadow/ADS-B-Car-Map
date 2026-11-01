using System.Globalization;
using System.Text;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// Text normalization used to compare search terms with airport and aircraft data regardless of case and diacritics.
    /// </summary>
    public static class TextFolding
    {
        /// <summary>
        /// Removes diacritics and changes the text to lower case, so that "Kraków" can be found with "krakow".
        /// </summary>
        /// <param name="text">Text to normalize.</param>
        /// <returns>Lower-case text without diacritics.</returns>
        public static string Fold(string text)
        {
            string decomposed;

            try
            {
                decomposed = text.Normalize(NormalizationForm.FormD);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is PlatformNotSupportedException)
            {
                decomposed = text;
            }

            StringBuilder builder = new StringBuilder(decomposed.Length);

            foreach (char character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(Substitute(char.ToLowerInvariant(character)));
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Replaces lower-case letters that have no Unicode decomposition with their usual ASCII spelling.
        /// </summary>
        /// <param name="character">Lower-case character.</param>
        /// <returns>ASCII replacement, or the character itself when it has none.</returns>
        private static string Substitute(char character)
        {
            return character switch
            {
                'ł' => "l",
                'ø' => "o",
                'đ' => "d",
                'ð' => "d",
                'ı' => "i",
                'ß' => "ss",
                'æ' => "ae",
                'œ' => "oe",
                _ => character.ToString(),
            };
        }
    }
}
