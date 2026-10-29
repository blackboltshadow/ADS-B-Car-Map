namespace AdsbCarMap.Data;

/// <summary>
/// Pojedynczy samolot z ostatnim znanym położeniem i parametrami lotu.
/// </summary>
public sealed class Aircraft
{
    /// <summary>
    /// Tworzy opis samolotu.
    /// </summary>
    /// <param name="hex">Adres ICAO w zapisie szesnastkowym.</param>
    /// <param name="callsign">Znak wywoławczy lub null, gdy nie jest nadawany.</param>
    /// <param name="latitude">Szerokość geograficzna.</param>
    /// <param name="longitude">Długość geograficzna.</param>
    /// <param name="altitudeFeet">Wysokość barometryczna w stopach lub null, gdy nieznana.</param>
    /// <param name="onGround">Informacja, czy samolot jest na ziemi.</param>
    /// <param name="groundSpeedKnots">Prędkość względem ziemi w węzłach lub null, gdy nieznana.</param>
    /// <param name="trackDegrees">Kurs względem ziemi w stopniach lub null, gdy nieznany.</param>
    /// <param name="secondsSincePosition">Wiek pozycji w sekundach w chwili odpowiedzi serwera.</param>
    /// <param name="typeCode">Kod typu samolotu ICAO, np. "B738", lub null, gdy nieznany.</param>
    /// <param name="registration">Znaki rejestracyjne lub null, gdy nieznane.</param>
    /// <param name="category">Kategoria nadajnika ADS-B, np. "A3", lub null, gdy nie jest nadawana.</param>
    /// <param name="isMilitary">Informacja, czy baza oznacza samolot jako wojskowy.</param>
    public Aircraft(
        string hex,
        string? callsign,
        double latitude,
        double longitude,
        int? altitudeFeet,
        bool onGround,
        double? groundSpeedKnots,
        double? trackDegrees,
        double secondsSincePosition,
        string? typeCode,
        string? registration,
        string? category = null,
        bool isMilitary = false)
    {
        Category = category;
        IsMilitary = isMilitary;
        Hex = hex;
        Callsign = callsign;
        Latitude = latitude;
        Longitude = longitude;
        AltitudeFeet = altitudeFeet;
        OnGround = onGround;
        GroundSpeedKnots = groundSpeedKnots;
        TrackDegrees = trackDegrees;
        SecondsSincePosition = secondsSincePosition;
        TypeCode = typeCode;
        Registration = registration;
    }

    /// <summary>
    /// Pole zapamiętujące rodzaj samolotu, wyliczany przy pierwszym użyciu.
    /// </summary>
    private AircraftKind? kind;

    /// <summary>
    /// Kategoria nadajnika ADS-B, np. "A3", lub null, gdy nie jest nadawana.
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// Informacja, czy baza oznacza samolot jako wojskowy.
    /// </summary>
    public bool IsMilitary { get; }

    /// <summary>
    /// Rodzaj samolotu ustalony z kategorii, znacznika wojskowego i kodu typu.
    /// </summary>
    public AircraftKind Kind => kind ??= AircraftClassifier.Classify(this);

    /// <summary>
    /// Adres ICAO w zapisie szesnastkowym.
    /// </summary>
    public string Hex { get; }

    /// <summary>
    /// Znak wywoławczy lub null, gdy nie jest nadawany.
    /// </summary>
    public string? Callsign { get; }

    /// <summary>
    /// Szerokość geograficzna.
    /// </summary>
    public double Latitude { get; }

    /// <summary>
    /// Długość geograficzna.
    /// </summary>
    public double Longitude { get; }

    /// <summary>
    /// Wysokość barometryczna w stopach lub null, gdy nieznana.
    /// </summary>
    public int? AltitudeFeet { get; }

    /// <summary>
    /// Informacja, czy samolot jest na ziemi.
    /// </summary>
    public bool OnGround { get; }

    /// <summary>
    /// Prędkość względem ziemi w węzłach lub null, gdy nieznana.
    /// </summary>
    public double? GroundSpeedKnots { get; }

    /// <summary>
    /// Kurs względem ziemi w stopniach lub null, gdy nieznany.
    /// </summary>
    public double? TrackDegrees { get; }

    /// <summary>
    /// Wiek pozycji w sekundach w chwili odpowiedzi serwera.
    /// </summary>
    public double SecondsSincePosition { get; }

    /// <summary>
    /// Kod typu samolotu ICAO, np. "B738", lub null, gdy nieznany.
    /// </summary>
    public string? TypeCode { get; }

    /// <summary>
    /// Znaki rejestracyjne lub null, gdy nieznane.
    /// </summary>
    public string? Registration { get; }
}
