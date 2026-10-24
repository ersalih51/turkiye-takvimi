using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>HTML belgenin Esc tuşunu uygulamaya iletmesi için betikten çağrılan köprü.</summary>
[ComVisible(true)]
public sealed class BelgeKoprusu
{
    public Action? KapatIstegi { get; set; }

    public void Kapat() => KapatIstegi?.Invoke();
}

/// <summary>
/// Başlık seviyeli bir belgeyi gösterir (bölümler 2. seviye başlıktır). Ekran okuyucuda gezinti kipinde
/// H ve 1-6 tuşlarıyla başlıklar arasında dolaşılır. Belge yerel olarak üretilir, internetten bir şey yüklenmez.
/// </summary>
public partial class BelgePenceresi : Window
{
    private readonly string _html;
    private bool _yuklendi;

    /// <param name="kalinAnahtar">Doğruysa her satırda "Tuş: açıklama" biçimindeki tuş adı kalın yazılır (kısayol listesi).</param>
    public BelgePenceresi(string baslik, string ozet, IReadOnlyList<YardimKonusu> bolumler, double olcek, bool kalinAnahtar = false)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;

        if (olcek > 1.0)
        {
            Width = Math.Min(Width * olcek, SystemParameters.WorkArea.Width);
            Height = Math.Min(Height * olcek, SystemParameters.WorkArea.Height);
        }

        Title = baslik;
        Baslik.Text = baslik;
        AltBaslik.Text = ozet;
        _html = HtmlUret(baslik, bolumler, olcek, kalinAnahtar);

        Tarayici.ObjectForScripting = new BelgeKoprusu { KapatIstegi = Close };
        Loaded += (_, _) => Tarayici.NavigateToString(_html);
    }

    private string HtmlUret(string baslik, IReadOnlyList<YardimKonusu> bolumler, double olcek, bool kalinAnahtar)
    {
        // Renkler uygulamanın o anki temasından alınır (koyu ya da açık).
        Color Renk(string anahtar, Color varsayilan) => TryFindResource(anahtar) is SolidColorBrush f ? f.Color : varsayilan;
        var yazi = Renk("TextFillColorPrimaryBrush", Colors.Black);
        bool koyu = (0.299 * yazi.R + 0.587 * yazi.G + 0.114 * yazi.B) > 140;
        var zemin = koyu ? Color.FromRgb(0x27, 0x27, 0x27) : Color.FromRgb(0xFB, 0xFB, 0xFB);
        var vurgu = Renk("AccentTextFillColorPrimaryBrush", koyu ? Color.FromRgb(0x60, 0xCD, 0xFF) : Color.FromRgb(0x00, 0x5A, 0x9E));
        var cizgi = koyu ? Color.FromRgb(0x4A, 0x4A, 0x4A) : Color.FromRgb(0xD0, 0xD0, 0xD0);

        // Yüksek kontrastta belge de sistemin kontrast renklerini kullanır.
        if (SystemParameters.HighContrast)
        {
            yazi = SystemColors.WindowTextColor;
            zemin = SystemColors.WindowColor;
            vurgu = SystemColors.HotTrackColor;
            cizgi = SystemColors.WindowTextColor;
        }

        string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        int px = (int)Math.Round(18 * Math.Max(1.0, olcek));

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"tr\"><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><meta charset=\"utf-8\">");
        sb.Append($"<title>{WebUtility.HtmlEncode(baslik)}</title><style>");
        sb.Append($"html,body{{margin:0;background:{Hex(zemin)};color:{Hex(yazi)};font-family:'Segoe UI Variable Text','Segoe UI',sans-serif;font-size:{px}px;line-height:1.5}}");
        sb.Append("body{padding:6px 22px 24px 22px}");
        sb.Append($"h1{{font-size:1.5em;margin:14px 0 4px 0}}");
        sb.Append($"h2{{font-size:1.2em;margin:26px 0 8px 0;padding-top:14px;border-top:1px solid {Hex(cizgi)};color:{Hex(vurgu)}}}");
        sb.Append("p{margin:0 0 6px 0}b{font-weight:600}");
        sb.Append("</style></head><body>");
        sb.Append($"<h1>{WebUtility.HtmlEncode(baslik)}</h1>");

        foreach (var b in bolumler)
        {
            sb.Append($"<h2>{WebUtility.HtmlEncode(b.Baslik)}</h2>");
            foreach (string satir in b.Metin.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                int i = satir.IndexOf(": ", StringComparison.Ordinal);
                string html = kalinAnahtar && i is > 0 and <= 45
                    ? $"<b>{WebUtility.HtmlEncode(satir[..i])}</b>: {WebUtility.HtmlEncode(satir[(i + 2)..])}"
                    : WebUtility.HtmlEncode(satir);
                sb.Append($"<p>{html}</p>");
            }
        }

        sb.Append("<script>document.onkeydown=function(){try{if(window.event.keyCode==27){window.external.Kapat();}}catch(e){}};</script>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private void Tarayici_Navigating(object sender, NavigatingCancelEventArgs e)
    {
        // Yalnızca ilk (yerel) yükleme serbest; bağlantı ya da yönlendirme olursa iptal edilir.
        if (_yuklendi) e.Cancel = true;
    }

    private void Tarayici_LoadCompleted(object sender, NavigationEventArgs e)
    {
        _yuklendi = true;
        Tarayici.Focus();
    }

    private void Kapat_Click(object sender, RoutedEventArgs e) => Close();
}
