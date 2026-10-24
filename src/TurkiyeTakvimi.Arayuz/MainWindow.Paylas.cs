using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Sağ tık menüsündeki "Paylaş": Windows'un klasik Paylaş penceresini açar.</summary>
public partial class MainWindow
{
    private PaylasimServisi? _paylasim;

    private const string PaylasimImzasi = "Türkiye Takvimi · turktakvim.com";

    private void PaylasimiBaslat(string baslik, string metin, string? resimDosyasi = null)
    {
        _paylasim ??= new PaylasimServisi(this);
        Duyur(_paylasim.Paylas(baslik, $"{metin}{Environment.NewLine}{Environment.NewLine}{PaylasimImzasi}", resimDosyasi)
            ? "Paylaş penceresi açıldı."
            : "Paylaş penceresi açılamadı. Bu Windows sürümünde paylaşım desteklenmiyor olabilir; Panoya kopyala'yı kullanabilirsiniz.");
    }

    // ---------- Vakit listeleri ----------

    private string VakitlerMetni(bool ana)
    {
        if (_bugun is null) return "";
        var tr = new CultureInfo("tr-TR");
        var satirlar = new List<string> { $"{SehirMetni}, {SehirSimdi.ToString("d MMMM yyyy dddd", tr)}" };
        for (int i = 0; i < VakitAdlari.Hepsi.Length; i++)
        {
            if (VakitAdlari.AnaMi(i) != ana) continue;
            satirlar.Add($"{GosterAd(i)}, {VakitSaati.Ayristir(_bugun.Gun.Saatler[i]).Okunan}");
        }
        return string.Join(Environment.NewLine, satirlar);
    }

    private void PaylasAna_Click(object sender, RoutedEventArgs e) =>
        PaylasimiBaslat($"{SehirMetni} ana vakitler", VakitlerMetni(ana: true));

    private void PaylasDiger_Click(object sender, RoutedEventArgs e) =>
        PaylasimiBaslat($"{SehirMetni} diğer vakitler", VakitlerMetni(ana: false));

    // ---------- Haftalık ----------

    private void PaylasHafta_Click(object sender, RoutedEventArgs e)
    {
        var gunler = HaftaListesi.Items.OfType<GunSatiri>().ToList();
        if (gunler.Count == 0)
        {
            Duyur("Paylaşılacak haftalık vakit yok.");
            return;
        }
        string metin = $"{SehirMetni}, haftalık vakitler{Environment.NewLine}" +
                       string.Join(Environment.NewLine, gunler.Select(g => g.Kopya));
        PaylasimiBaslat($"{SehirMetni} haftalık vakitler", metin);
    }

    // ---------- İmsakiye ----------

    private void PaylasImsakiye_Click(object sender, RoutedEventArgs e)
    {
        if (_sonImsakiye is not { } im)
        {
            Duyur("Paylaşılacak imsakiye yok. İmsakiye hazır olunca yeniden deneyin.");
            return;
        }

        string metin = $"Ramezân-ı Şerîf İmsâkiyesi, Hicrî {im.Aralik.HicriYil}, Milâdî {im.MiladiYil}, {im.SehirAdi}{Environment.NewLine}" +
                       string.Join(Environment.NewLine, ImsakiyeListesi.Items.OfType<ImsakiyeSatiri>().Select(s => s.Kopya)) +
                       Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, _yazdirmaNotlari);

        // Yazdırılan A4 sayfanın resmi de eklenir; WhatsApp gibi uygulamalar resmi, e-posta ikisini birden alır.
        string? resim = null;
        try
        {
            resim = Path.Combine(Path.GetTempPath(), $"Imsakiye-{im.MiladiYil}.png");
            ImsakiyeResmiKaydet(im, resim);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            resim = null;
        }
        PaylasimiBaslat($"İmsakiye {im.MiladiYil}, {im.SehirAdi}", metin, resim);
    }

    // ---------- Takvim sekmesi ----------

    private string TakvimTarihi => _seciliTarih.ToString("d MMMM yyyy dddd", new CultureInfo("tr-TR"));

    /// <summary>Söz, Arka Yaprak ve Tarih/Günün Bilgileri alanları: seçili metin varsa onu, yoksa tamamını paylaşır.</summary>
    private void PaylasMetin_Click(object sender, RoutedEventArgs e)
    {
        if (((sender as MenuItem)?.Parent as ContextMenu)?.PlacementTarget is not TextBox kutu) return;

        string metin = kutu.SelectedText.Length > 0 ? kutu.SelectedText : kutu.Text;
        if (string.IsNullOrWhiteSpace(metin))
        {
            Duyur("Paylaşılacak metin yok.");
            return;
        }
        string ad = System.Windows.Automation.AutomationProperties.GetName(kutu);
        PaylasimiBaslat(ad, $"{ad}, {TakvimTarihi}{Environment.NewLine}{metin.Trim()}");
    }
}
