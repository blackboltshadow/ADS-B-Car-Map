namespace AdsbCarMap.Data
{
    /// <summary>
    /// Stan wyszukiwania trasy lotu dla znaku wywoławczego.
    /// </summary>
    public enum RouteLookupState
    {
        /// <summary>
        /// Brak znaku wywoławczego, więc trasy nie da się wyszukać.
        /// </summary>
        NotApplicable,

        /// <summary>
        /// Trasa czeka w kolejce albo jest właśnie pobierana.
        /// </summary>
        Pending,

        /// <summary>
        /// Trasa jest znana.
        /// </summary>
        Found,

        /// <summary>
        /// Serwer odpowiedział, że nie zna trasy tego lotu.
        /// </summary>
        NotFound,

        /// <summary>
        /// Ostatnie pobranie nie powiodło się, np. z powodu błędu sieci lub limitu zapytań.
        /// </summary>
        Failed,
    }
}
