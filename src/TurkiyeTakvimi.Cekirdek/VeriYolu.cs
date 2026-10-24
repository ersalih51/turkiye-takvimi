using System.Reflection;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>
/// Uygulamanın tüm yerel verisinin (ayarlar, önbellek, hata kaydı) tek kökü.
/// Uygulama klasöründe "tasinabilir.txt" dosyası varsa taşınabilir sürümdür: veri, uygulamanın yanındaki "Veri" klasörüne yazılır
/// ve bilgisayarda başka hiçbir yere iz bırakılmaz. Yoksa kurulu sürümdür: veri %LocalAppData%\TurkiyeTakvimi altındadır.
/// </summary>
public static class VeriYolu
{
    public const string TasinabilirDosyaAdi = "tasinabilir.txt";

    public static bool Tasinabilir { get; } = File.Exists(Path.Combine(AppContext.BaseDirectory, TasinabilirDosyaAdi));

    public static string Kok { get; } = Tasinabilir
        ? Path.Combine(AppContext.BaseDirectory, "Veri")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TurkiyeTakvimi");

    /// <summary>Sürüm metni: "2026.10" (üçüncü bölüm 0 değilse "2026.10.1").</summary>
    public static string SurumMetni(Version? v) =>
        v is null ? "?" : v.Build > 0 ? $"{v.Major}.{v.Minor}.{v.Build}" : $"{v.Major}.{v.Minor}";

    public static string UygulamaSurumu => SurumMetni(Assembly.GetEntryAssembly()?.GetName().Version);
}
