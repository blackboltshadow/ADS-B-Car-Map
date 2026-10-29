namespace AdsbCarMap.Data
{
    /// <summary>
    /// Ustala rodzaj samolotu na podstawie kategorii nadajnika ADS-B, znacznika wojskowego i kodu typu ICAO.
    /// </summary>
    public static class AircraftClassifier
    {
        /// <summary>
        /// Kody typu obiektów naziemnych.
        /// </summary>
        private static readonly HashSet<string> GroundTypes = CreateSet("TWR", "GND", "GRND");

        /// <summary>
        /// Kody typu szybowców i balonów.
        /// </summary>
        private static readonly HashSet<string> GliderTypes = CreateSet("GLID");

        /// <summary>
        /// Kody typu balonów.
        /// </summary>
        private static readonly HashSet<string> BalloonTypes = CreateSet("BALL");

        /// <summary>
        /// Kody typu śmigłowców.
        /// </summary>
        private static readonly HashSet<string> HelicopterTypes = CreateSet(
            "R22", "R44", "R66", "EC20", "EC25", "EC30", "EC35", "EC45", "EC55", "EC75", "EN48", "EN28", "AS32", "AS35", "AS50", "AS55", "AS65",
            "B06", "B105", "B407", "B412", "B429", "B430", "B47G", "A109", "A119", "A139", "A189", "S61", "S76", "S92", "H60", "H47", "H64", "H500",
            "NH90", "MI8", "MI24", "MI26", "KA32", "KA27", "UH1", "AW09", "G2CA", "SA34", "LYNX", "PUMA");

        /// <summary>
        /// Kody typu wojskowych samolotów bojowych i szkolno-bojowych.
        /// </summary>
        private static readonly HashSet<string> FighterTypes = CreateSet(
            "F16", "F15", "F14", "F18H", "F18S", "F22", "F35", "F5", "F4", "EUFI", "RFAL", "TORN", "TOR", "GRIF", "A10", "M346", "T38", "HAWK", "EF2K",
            "MIG2", "MG29", "MG31", "SU27", "SU30", "SU34", "SU35", "SU57", "JAS39", "J10", "J20", "AJET", "PC21", "T6", "TEX2", "M339", "L39", "L159", "FA18");

        /// <summary>
        /// Kody typu wojskowych samolotów transportowych i tankowców.
        /// </summary>
        private static readonly HashSet<string> MilitaryTransportTypes = CreateSet(
            "C130", "C30J", "C17", "A400", "C5M", "C5", "IL76", "A124", "A225", "K35R", "KC10", "KC46", "C160", "C295", "CN35", "C27J", "AN12", "AN32");

        /// <summary>
        /// Kody typu samolotów turbośmigłowych.
        /// </summary>
        private static readonly HashSet<string> TurbopropTypes = CreateSet(
            "AT43", "AT44", "AT45", "AT46", "AT72", "AT73", "AT75", "AT76", "DH8A", "DH8B", "DH8C", "DH8D", "SF34", "E120", "JS31", "JS32", "JS41",
            "L410", "PC12", "B190", "SW4", "SW3", "C208", "AN28", "AN24", "AN26", "D228", "D328", "BE20", "BE9L", "BE99", "PC6", "TBM7", "TBM8", "TBM9", "P180", "DHC6", "DHC7", "F50", "F27");

        /// <summary>
        /// Kody typu ciężkich samolotów szerokokadłubowych.
        /// </summary>
        private static readonly HashSet<string> HeavyTypes = CreateSet(
            "B741", "B742", "B743", "B744", "B748", "B762", "B763", "B764", "B772", "B773", "B77L", "B77W", "B788", "B789", "B78X", "A306", "A310",
            "A332", "A333", "A338", "A339", "A342", "A343", "A345", "A346", "A359", "A35K", "A388", "MD11", "IL96", "DC10", "L101");

        /// <summary>
        /// Kody typu małych odrzutowców, w tym biznesowych.
        /// </summary>
        private static readonly HashSet<string> SmallJetTypes = CreateSet(
            "C25A", "C25B", "C25C", "C510", "C525", "C550", "C560", "C56X", "C680", "C700", "C750", "E50P", "E55P", "FA10", "FA20", "FA50", "FA7X", "FA8X",
            "F900", "F2TH", "LJ31", "LJ35", "LJ40", "LJ45", "LJ55", "LJ60", "LJ75", "GLF2", "GLF3", "GLF4", "GLF5", "GLF6", "GL5T", "GL7T", "GLEX", "CL30", "CL35",
            "CL60", "H25B", "H25C", "PRM1", "E135", "E145", "E170", "E175", "CRJ1", "CRJ2", "CRJ7", "CRJ9", "SF50", "EA50", "HDJT", "ASTR");

        /// <summary>
        /// Kody typu lekkich samolotów tłokowych.
        /// </summary>
        private static readonly HashSet<string> LightPlaneTypes = CreateSet(
            "C150", "C152", "C162", "C170", "C172", "C177", "C180", "C182", "C185", "C206", "C210", "C310", "C340", "C414", "C421", "PA18", "PA22", "PA24", "PA28",
            "PA32", "PA34", "PA44", "PA46", "P28A", "P28B", "P28R", "P32R", "SR20", "SR22", "S22T", "DA20", "DA40", "DA42", "DA62", "AA5", "AT3T", "BE23", "BE24",
            "BE33", "BE35", "BE36", "BE58", "M20P", "M20T", "RV7", "RV8", "RV10", "RV12", "TOBA", "TB10", "TB20", "TB21", "G115", "CH7A", "CH7B", "JAB4", "J3", "P208", "YK52", "YK18");

        /// <summary>
        /// Ustala rodzaj samolotu. Kolejność reguł od najbardziej szczególnych: obiekty naziemne, balony, szybowce, drony, śmigłowce, wojskowe, a potem według wielkości.
        /// </summary>
        /// <param name="aircraft">Samolot do sklasyfikowania.</param>
        /// <returns>Rodzaj samolotu.</returns>
        public static AircraftKind Classify(Aircraft aircraft)
        {
            string type = (aircraft.TypeCode ?? string.Empty).ToUpperInvariant();
            string category = (aircraft.Category ?? string.Empty).ToUpperInvariant();
            bool hasCategory = category.Length > 1 && category != "A0";

            if (category.StartsWith('C') || GroundTypes.Contains(type))
            {
                return AircraftKind.Ground;
            }

            if (category == "B2" || BalloonTypes.Contains(type))
            {
                return AircraftKind.Balloon;
            }

            if (category == "B1" || GliderTypes.Contains(type))
            {
                return AircraftKind.Glider;
            }

            if (category == "B6")
            {
                return AircraftKind.Drone;
            }

            if (category == "A7" || HelicopterTypes.Contains(type))
            {
                return AircraftKind.Helicopter;
            }

            if (category == "A6" || FighterTypes.Contains(type))
            {
                return AircraftKind.Fighter;
            }

            if (MilitaryTransportTypes.Contains(type) || (aircraft.IsMilitary && (category == "A3" || category == "A4" || category == "A5") && !SmallJetTypes.Contains(type)))
            {
                return AircraftKind.MilitaryTransport;
            }

            if (TurbopropTypes.Contains(type))
            {
                return AircraftKind.Turboprop;
            }

            if (category == "A5" || HeavyTypes.Contains(type))
            {
                return AircraftKind.Heavy;
            }

            if (category == "A1" || category == "B4" || LightPlaneTypes.Contains(type))
            {
                return AircraftKind.LightPlane;
            }

            if (category == "A2" || SmallJetTypes.Contains(type))
            {
                return AircraftKind.SmallJet;
            }

            if (hasCategory || type.Length > 0)
            {
                return AircraftKind.Airliner;
            }

            return AircraftKind.Unknown;
        }

        /// <summary>
        /// Zwraca polską nazwę rodzaju samolotu do wyświetlenia.
        /// </summary>
        /// <param name="kind">Rodzaj samolotu.</param>
        /// <returns>Nazwa rodzaju lub pusty tekst dla rodzaju nieznanego.</returns>
        public static string GetDisplayName(AircraftKind kind)
        {
            return kind switch
            {
                AircraftKind.Airliner => "samolot pasażerski",
                AircraftKind.Heavy => "samolot szerokokadłubowy",
                AircraftKind.SmallJet => "mały odrzutowiec",
                AircraftKind.Turboprop => "samolot turbośmigłowy",
                AircraftKind.LightPlane => "samolot lekki",
                AircraftKind.Helicopter => "śmigłowiec",
                AircraftKind.Fighter => "samolot bojowy",
                AircraftKind.MilitaryTransport => "wojskowy transportowy",
                AircraftKind.Glider => "szybowiec",
                AircraftKind.Balloon => "balon",
                AircraftKind.Drone => "dron",
                AircraftKind.Ground => "obiekt naziemny",
                _ => string.Empty,
            };
        }

        /// <summary>
        /// Tworzy zbiór napisów porównywanych bez względu na wielkość liter.
        /// </summary>
        /// <param name="values">Elementy zbioru.</param>
        /// <returns>Zbiór kodów typu.</returns>
        private static HashSet<string> CreateSet(params string[] values)
        {
            return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }
    }
}
