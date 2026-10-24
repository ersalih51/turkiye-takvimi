using System.Globalization;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>İmsakiye sekmesi: Ramazan günleri ve vakitleri, turktakvim.com verisinden.</summary>
public partial class MainWindow
{
    private ImsakiyeServisi? _imsakiyeServisi;
    private string _imsakiyeAnahtar = "";
    private bool _imsakiyeYukleniyor;

    private ImsakiyeServisi ImsakiyeServisiGetir() => _imsakiyeServisi ??= new ImsakiyeServisi(_istemci, _icerik, _vakitler, _onbellek);


    private void KopyalaImsakiye_Click(object sender, RoutedEventArgs e) => Kopyala(ImsakiyeListesi);

    /// <summary>Şehir ya da gün değiştiyse imsakiyeyi yeniden hazırlar; sekme her açıldığında çağrılır.</summary>
    private async Task ImsakiyeYukle(bool zorla = false)
    {
        if (!SehirSecili || _imsakiyeYukleniyor) return;

        var bugun = DateOnly.FromDateTime(SehirSimdi);
        string anahtar = $"{SehirKodu}|{bugun:yyyyMMdd}";
        if (!zorla && anahtar == _imsakiyeAnahtar) return;

        _imsakiyeYukleniyor = true;
        string kod = SehirKodu;
        ImsakiyeBilgisi.Text = "İmsakiye hazırlanıyor…";
        try
        {
            var imsakiye = await ImsakiyeServisiGetir().HazirlaAsync(kod, SehirMetni, bugun);
            if (kod != SehirKodu) return; // hazırlanırken şehir değişti

            if (imsakiye is null)
            {
                ImsakiyeListesi.ItemsSource = null;
                MetinYaz(ImsakiyeNotlari, "turktakvim.com sitesinde imsakiye için hicrî tarih verisi şu an yok. Site yeni yılın verisini yayımlayınca imsakiye burada kendiliğinden görünür.");
                ImsakiyeBilgisi.Text = "İmsakiye şu an hazır değil.";
                return;
            }

            ImsakiyeyiDoldur(imsakiye, bugun);
            _imsakiyeAnahtar = anahtar;
        }
        catch (VeriHatasi)
        {
            ImsakiyeListesi.ItemsSource = null;
            MetinYaz(ImsakiyeNotlari, "İmsakiye için internet gerekiyor ve bağlantı kurulamadı. Bağlandığınızda bu sekmeyi yeniden açın; bir kez hazırlanınca çevrimdışı da görünür.");
            ImsakiyeBilgisi.Text = "İmsakiye hazırlanamadı.";
        }
        finally
        {
            _imsakiyeYukleniyor = false;
        }
    }

    private Imsakiye? _sonImsakiye;
    private List<string> _yazdirmaNotlari = new();

    private void ImsakiyeyiDoldur(Imsakiye im, DateOnly bugun)
    {
        var tr = new CultureInfo("tr-TR");
        var a = im.Aralik;
        string Gun(DateOnly d) => d.ToString("d MMMM yyyy dddd", tr);

        int kadirGunu = 26; // 26. günü 27. güne bağlayan gece
        var satirlar = im.Gunler
            .Select(g => new ImsakiyeSatiri(g, g.Tarih == bugun, g.Gun == kadirGunu && a.GunSayisi >= 27))
            .ToList();

        ImsakiyeBilgisi.Text = $"Ramezân-ı Şerîf İmsâkiyesi, Hicrî {a.HicriYil}, Milâdî {im.MiladiYil}, {im.SehirAdi}";
        ImsakiyeListesi.ItemsSource = satirlar;
        int bugunSirasi = satirlar.FindIndex(s => s.Bugun);
        ImsakiyeListesi.SelectedIndex = bugunSirasi >= 0 ? bugunSirasi : 0;
        if (bugunSirasi >= 0) ImsakiyeListesi.ScrollIntoView(satirlar[bugunSirasi]);

        var bilgi = new List<string>();
        string zaman = bugun < a.Baslangic ? "başlayacak" : "başladı";
        string son = bugun <= a.Bitis ? "sona erecek" : "sona erdi";
        bilgi.Add($"Ramazan {Gun(a.Baslangic)} günü {zaman}, {Gun(a.Bitis)} günü {son}. Ramazan {a.GunSayisi} gün sürüyor.");

        // İlk/son imsak ve iftar: Ramazan'ın ilk ve son gününün İmsak ve Akşam vakitleri.
        var ilk = im.Gunler[0];
        var sonGun = im.Gunler[^1];
        string Saat(ImsakiyeGunu g, int sutun) => VakitSaati.Ayristir(g.Saatler[sutun]).Okunan;
        string Kisa(ImsakiyeGunu g) => g.Tarih.ToString("d MMMM dddd", tr);
        bilgi.Add($"İlk imsak: {Kisa(ilk)}, {Saat(ilk, 0)}. Son imsak: {Kisa(sonGun)}, {Saat(sonGun, 0)}.");
        bilgi.Add($"İlk iftar: {Kisa(ilk)}, {Saat(ilk, 6)}. Son iftar: {Kisa(sonGun)}, {Saat(sonGun, 6)}.");

        if (a.GunSayisi >= 27)
        {
            var kadir = a.Baslangic.AddDays(kadirGunu - 1);
            bilgi.Add($"Kadir Gecesi: {Gun(kadir)} gününü, {Gun(kadir.AddDays(1))} gününe bağlayan gecedir.");
        }
        bilgi.Add($"Ramazan Bayramı: {Gun(a.BayramGunu)}" + (im.BayramGunuGunes is { } g ? $", Güneş {VakitSaati.Ayristir(g).Okunan}." : "."));
        bilgi.Add("İmsâk vakti, oruca başlama zamanı ve sabâh nemâzının ilk vaktidir. İmsakiyedeki Sabah vakti, sabâh nemâzının ihtiyatlı vaktidir.");

        var notlar = new List<string>();
        if (a.Bitis < bugun)
            notlar.Add("Bu imsakiye geçen Ramazan'a aittir. Türkiye Takvimi sitesi yeni yılın hicrî tarihlerini yayımlayınca yeni imsakiye burada kendiliğinden görünür.");
        notlar.AddRange(bilgi);
        MetinYaz(ImsakiyeNotlari, string.Join("\n", notlar));

        _sonImsakiye = im;
        _yazdirmaNotlari = bilgi;
    }

    // ---------- Yazdırma ----------

    private void ImsakiyeYazdir_Click(object sender, RoutedEventArgs e) => ImsakiyeYazdir();
    private void ImsakiyePdf_Click(object sender, RoutedEventArgs e) => ImsakiyeYazdir(pdf: true);

    private void ImsakiyeYazdir(bool pdf = false)
    {
        if (_sonImsakiye is not { } im)
        {
            Duyur("Yazdırılacak imsakiye yok. İmsakiye hazır olunca yeniden deneyin.");
            return;
        }
        BelgeyiYazdir(ImsakiyeBelgesi.Olustur(im, im.Aralik.HicriYil, _yazdirmaNotlari), "Türkiye Takvimi İmsakiye", "İmsakiye", pdf);
    }

    /// <summary>Yazdırılacak A4 imsakiye sayfasını PNG resmi olarak kaydeder (paylaşım için).</summary>
    private void ImsakiyeResmiKaydet(Imsakiye im, string dosya) =>
        BelgeResmiKaydet(ImsakiyeBelgesi.Olustur(im, im.Aralik.HicriYil, _yazdirmaNotlari), dosya);
}