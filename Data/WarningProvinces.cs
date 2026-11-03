using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.Data
{
    /// <summary>
    /// The sixteen voivodeships of Poland as the feed of the notices names them.
    /// </summary>
    public static class WarningProvinces
    {
        /// <summary>
        /// The voivodeships in alphabetical order of their names. The identifiers are those the feed accepts in its address and uses in the notices.
        /// </summary>
        public static readonly IReadOnlyList<WarningProvince> All = new List<WarningProvince>
        {
            new WarningProvince("dolnoslaskie", "Dolnośląskie"),
            new WarningProvince("kujawsko-pomorskie", "Kujawsko-pomorskie"),
            new WarningProvince("lubelskie", "Lubelskie"),
            new WarningProvince("lubuskie", "Lubuskie"),
            new WarningProvince("lodzkie", "Łódzkie"),
            new WarningProvince("malopolskie", "Małopolskie"),
            new WarningProvince("mazowieckie", "Mazowieckie"),
            new WarningProvince("opolskie", "Opolskie"),
            new WarningProvince("podkarpackie", "Podkarpackie"),
            new WarningProvince("podlaskie", "Podlaskie"),
            new WarningProvince("pomorskie", "Pomorskie"),
            new WarningProvince("slaskie", "Śląskie"),
            new WarningProvince("swietokrzyskie", "Świętokrzyskie"),
            new WarningProvince("warminsko-mazurskie", "Warmińsko-mazurskie"),
            new WarningProvince("wielkopolskie", "Wielkopolskie"),
            new WarningProvince("zachodniopomorskie", "Zachodniopomorskie"),
        };

        /// <summary>
        /// Gives the name of a voivodeship to show to the user.
        /// </summary>
        /// <param name="slug">Identifier as the feed writes it.</param>
        /// <returns>The name, or the identifier of a region that is not in the list.</returns>
        public static string GetName(string slug)
        {
            foreach (WarningProvince province in All)
            {
                if (string.Equals(province.Slug, slug, StringComparison.OrdinalIgnoreCase))
                {
                    return province.Name;
                }
            }

            return slug;
        }
    }
}
