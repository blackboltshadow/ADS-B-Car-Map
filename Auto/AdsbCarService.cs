using Android.App;
using Android.Content;
using AndroidX.Car.App;
using AndroidX.Car.App.Validation;

namespace AdsbCarMap.Auto
{
    /// <summary>
    /// Usługa Car App rejestrowana w Android Auto jako aplikacja nawigacyjna.
    /// </summary>
    [Service(Exported = true)]
    [IntentFilter(
        new string[] { "androidx.car.app.CarAppService" },
        Categories = new string[] { "androidx.car.app.category.NAVIGATION" })]
    public class AdsbCarService : CarAppService
    {
        /// <summary>
        /// Zwraca walidator hosta. Wariant dopuszczający wszystkich hostów jest przeznaczony wyłącznie do PoC.
        /// </summary>
        /// <returns>Walidator akceptujący każdego hosta.</returns>
        public override HostValidator CreateHostValidator()
        {
            return HostValidator.AllowAllHostsValidator;
        }

        /// <summary>
        /// Tworzy sesję dla połączenia z samochodem.
        /// </summary>
        /// <returns>Nowa sesja aplikacji.</returns>
        public override Session OnCreateSession()
        {
            Android.Util.Log.Info("AdsbCarMap", "OnCreateSession");
            return new AdsbSession();
        }
    }

    /// <summary>
    /// Sesja Android Auto, która tworzy ekran z mapą.
    /// </summary>
    public class AdsbSession : Session
    {
        /// <summary>
        /// Tworzy pierwszy ekran sesji.
        /// </summary>
        /// <param name="intent">Intencja, która uruchomiła sesję.</param>
        /// <returns>Ekran z mapą samolotów.</returns>
        public override Screen OnCreateScreen(Intent intent)
        {
            Android.Util.Log.Info("AdsbCarMap", "OnCreateScreen");
            return new MapScreen(CarContext);
        }
    }
}
