using System.Windows;
using System.Windows.Media;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>
/// Windows yüksek kontrast temasında uygulamanın renkleri sistem renklerine bağlanır (metin, zemin, vurgu ve çerçeveler),
/// böylece kullanıcının seçtiği kontrast renkleri aynen kullanılır. Tema açılıp kapanınca uygulama yeniden başlatılmadan güncellenir.
/// Uygulama Windows ayarlarına dokunmaz; yalnızca okur.
/// </summary>
public static class YuksekKontrast
{
    // Uygulamanın kullandığı Fluent renk anahtarları -> sistem renkleri.
    private static readonly (string Anahtar, Func<Brush> Firca)[] Eslesme =
    {
        ("TextFillColorPrimaryBrush", () => SystemColors.WindowTextBrush),
        ("CardBackgroundFillColorDefaultBrush", () => SystemColors.WindowBrush),
        ("SolidBackgroundFillColorBaseBrush", () => SystemColors.WindowBrush),
        ("CardStrokeColorDefaultBrush", () => SystemColors.WindowTextBrush),
        ("AccentFillColorDefaultBrush", () => SystemColors.HighlightBrush),
        ("TextOnAccentFillColorPrimaryBrush", () => SystemColors.HighlightTextBrush),
        ("AccentFillColorSelectedTextBackgroundBrush", () => SystemColors.HighlightBrush),
        ("AccentTextFillColorPrimaryBrush", () => SystemColors.HotTrackBrush),
        ("SystemFillColorCriticalBrush", () => SystemColors.WindowTextBrush)
    };

    public static void Baslat(Application uygulama)
    {
        Uygula(uygulama, SystemParameters.HighContrast || Environment.GetEnvironmentVariable("TT_YUKSEK_KONTRAST_DENE") == "1");
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
                uygulama.Dispatcher.BeginInvoke(() => Uygula(uygulama, SystemParameters.HighContrast));
        };
    }

    public static void Uygula(Application uygulama, bool acik)
    {
        foreach (var (anahtar, firca) in Eslesme)
        {
            if (acik) uygulama.Resources[anahtar] = firca();
            else uygulama.Resources.Remove(anahtar);
        }
    }
}
