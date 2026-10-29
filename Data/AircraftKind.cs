namespace AdsbCarMap.Data
{
    /// <summary>
    /// Rodzaj statku powietrznego lub obiektu, od którego zależy kształt ikony na mapie.
    /// </summary>
    public enum AircraftKind
    {
        /// <summary>
        /// Nie da się ustalić rodzaju, rysowana jest ogólna strzałka.
        /// </summary>
        Unknown,

        /// <summary>
        /// Odrzutowy samolot pasażerski średniej wielkości.
        /// </summary>
        Airliner,

        /// <summary>
        /// Duży samolot szerokokadłubowy lub czterosilnikowy.
        /// </summary>
        Heavy,

        /// <summary>
        /// Mały odrzutowiec, np. biznesowy lub regionalny.
        /// </summary>
        SmallJet,

        /// <summary>
        /// Samolot turbośmigłowy z prostym skrzydłem, np. regionalny lub transportowy.
        /// </summary>
        Turboprop,

        /// <summary>
        /// Lekki samolot tłokowy, np. awionetka.
        /// </summary>
        LightPlane,

        /// <summary>
        /// Śmigłowiec.
        /// </summary>
        Helicopter,

        /// <summary>
        /// Wojskowy samolot bojowy lub szkolno-bojowy.
        /// </summary>
        Fighter,

        /// <summary>
        /// Wojskowy samolot transportowy lub tankowiec.
        /// </summary>
        MilitaryTransport,

        /// <summary>
        /// Szybowiec.
        /// </summary>
        Glider,

        /// <summary>
        /// Balon.
        /// </summary>
        Balloon,

        /// <summary>
        /// Bezzałogowy statek powietrzny.
        /// </summary>
        Drone,

        /// <summary>
        /// Pojazd lub obiekt naziemny, np. wieża albo pojazd lotniskowy.
        /// </summary>
        Ground,
    }
}
