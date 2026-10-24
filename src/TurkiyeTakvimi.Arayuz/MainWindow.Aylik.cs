using System.Globalization;
using System.IO;
using System.Windows;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Aylık vakit çizelgesi sekmesi: seçili ayın her günü için yedi ana vakit; yazdırma, PDF, paylaşma.</summary>
public partial class MainWindow
{
    private DateOnly _aylikAy; // gösterilen ayın ilk günü
    private string _aylikAnahtar = "";
    private bool _aylikYukleniyor;
    private List<AylikSatir> _aylikSatirlar = new();
    private string _aylikAdi = "";

    private void KopyalaAylik_Click(object sender, RoutedEventArgs e) => Kopyala(AylikListesi);
    private void OncekiAy_Click(object sender, RoutedEventArgs e) => AyDegistir(-1);
    private void SonrakiAy_Click(object sender, RoutedEventArgs e) => AyDegistir(+1);

    private void BuAy_Click(object sender, RoutedEventArgs e)
    {
        var bugun = DateOnly.FromDateTime(SehirSimdi);
        _aylikAy = new DateOnly(bugun.Year, bugun.Month, 1);
        _ = AylikYukle(duyur: true);
    }

    private void AyDegistir(int fark)
    {
        if (_aylikAy == default) _aylikAy = new DateOnly(SehirSimdi.Year, SehirSimdi.Month, 1);
        _aylikAy = _aylikAy.AddMonths(fark);
        _ = AylikYukle(duyur: true);
    }

    private static string AyAdi(DateOnly ay)
    {
        string s = ay.ToString("MMMM yyyy", new CultureInfo("tr-TR"));
        return char.ToUpper(s[0], new CultureInfo("tr-TR")) + s[1..];
    }

    /// <summary>Gösterilen ayın vakitlerini hazırlar (yıllık önbellekten; başka bir yıla geçince gerekirse internetten).</summary>
    private async Task AylikYukle(bool zorla = false, bool duyur = false)
    {
        if (!SehirSecili || _aylikYukleniyor) return;

        var bugun = DateOnly.FromDateTime(SehirSimdi);
        var buAy = new DateOnly(bugun.Year, bugun.Month, 1);
        if (_aylikAy == default) _aylikAy = buAy;

        string anahtar = $"{SehirKodu}|{_aylikAy:yyyyMM}|{bugun:yyyyMMdd}";
        if (!zorla && anahtar == _aylikAnahtar) return;

        _aylikYukleniyor = true;
        string kod = SehirKodu;
        var ay = _aylikAy;
        AylikBilgisi.Text = "Aylık vakitler hazırlanıyor…";
        BuAyDugmesi.Gecersiz = ay == buAy;
        try
        {
            var satirlar = new List<AylikSatir>();
            for (int g = 1; g <= DateTime.DaysInMonth(ay.Year, ay.Month); g++)
            {
                var tarih = new DateOnly(ay.Year, ay.Month, g);
                var sonuc = await _vakitler.GunuGetirAsync(kod, tarih);
                satirlar.Add(new AylikSatir(tarih, VakitAdlari.Ana.Select(v => sonuc.Gun.Saatler[v]).ToArray(), tarih == bugun));
            }
            if (kod != SehirKodu) return; // hazırlanırken şehir değişti

            _aylikSatirlar = satirlar;
            _aylikAdi = AyAdi(ay);
            AylikListesi.ItemsSource = satirlar;
            int sira = satirlar.FindIndex(s => s.Bugun);
            AylikListesi.SelectedIndex = sira >= 0 ? sira : 0;
            AylikListesi.ScrollIntoView(satirlar[AylikListesi.SelectedIndex]);
            AylikBilgisi.Text = $"{_aylikAdi}, {SehirMetni}";
            _aylikAnahtar = anahtar;
            if (duyur) Duyur($"{_aylikAdi} vakitleri gösteriliyor.");
        }
        catch (VeriHatasi)
        {
            _aylikSatirlar = new List<AylikSatir>();
            AylikListesi.ItemsSource = null;
            AylikBilgisi.Text = $"{AyAdi(ay)} vakitleri alınamadı. İnternete bağlanıp yeniden deneyin.";
            if (duyur) Duyur(AylikBilgisi.Text);
        }
        finally
        {
            _aylikYukleniyor = false;
        }
    }

    // ---------- Yazdırma, PDF, paylaşma ----------

    private void AylikYazdir_Click(object sender, RoutedEventArgs e) => AylikYazdir(false);
    private void AylikPdf_Click(object sender, RoutedEventArgs e) => AylikYazdir(true);

    private void AylikYazdir(bool pdf)
    {
        if (_aylikSatirlar.Count == 0)
        {
            Duyur("Yazdırılacak aylık çizelge yok. Vakitler hazır olunca yeniden deneyin.");
            return;
        }
        BelgeyiYazdir(AylikBelgesi.Olustur(_aylikAdi, SehirMetni, _aylikSatirlar), "Türkiye Takvimi Aylık Vakitler", "Aylık çizelge", pdf);
    }

    private void PaylasAylik_Click(object sender, RoutedEventArgs e)
    {
        if (_aylikSatirlar.Count == 0)
        {
            Duyur("Paylaşılacak aylık çizelge yok.");
            return;
        }

        string metin = $"{SehirMetni}, {_aylikAdi} vakitleri{Environment.NewLine}" +
                       string.Join(Environment.NewLine, _aylikSatirlar.Select(s => s.Kopya));
        string? resim = null;
        try
        {
            resim = Path.Combine(Path.GetTempPath(), $"Aylik-vakitler-{_aylikAy:yyyy-MM}.png");
            BelgeResmiKaydet(AylikBelgesi.Olustur(_aylikAdi, SehirMetni, _aylikSatirlar), resim);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            resim = null;
        }
        PaylasimiBaslat($"{_aylikAdi} vakitleri, {SehirMetni}", metin, resim);
    }
}
