using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

public partial class MainWindow : Window
{
    private readonly AyarDeposu _ayarDeposu = new();
    private readonly Ayarlar _ayarlar;

    private string SehirKodu => _ayarlar.SehirKodu;
    private static readonly CultureInfo Tr = new("tr-TR");

    private readonly DiskOnbellegi _onbellek = new();
    private readonly TurktakvimIstemci _istemci;
    private readonly VakitServisi _vakitler;
    private readonly IcerikServisi _icerik;
    private readonly SehirZamanServisi _sehirZaman;
    private readonly EzaniServisi _ezaniServisi;
    private TimeSpan? _sehirFarki; // seçili şehrin yerel saatinin UTC'den farkı; bilinmiyorsa bilgisayar saati kullanılır
    private readonly DispatcherTimer _zamanlayici = new() { Interval = TimeSpan.FromSeconds(1) };

    private VakitSonucu? _bugun;
    private DateOnly _yuklenenGun;
    private DateTime? _yarinImsak;
    private int _siradakiSira = -2; // -2: henüz hesaplanmadı, -1: bugün kalmadı
    private string _sonKalanMetin = "";
    private bool _yukleniyor;
    private bool _hazir;
    private bool _icerigeOdakla;
    private string? _hicriBugun;
    private bool _ramazanGosterildi;
    private string _digerAnahtar = "";
    private readonly CancellationTokenSource _arkaPlanIptal = new();
    private DateTime _seciliTarih = DateTime.Today;
    private DateTime _tarihBaslangic;
    private DateTime _tarihSonu;
    private bool _takvimSessiz;
    private CancellationTokenSource? _icerikIptal;

    public MainWindow(bool gizliBaslat = false)
    {
        CultureInfo.CurrentCulture = Tr;
        CultureInfo.CurrentUICulture = Tr;
        _ayarlar = _ayarDeposu.Yukle();
        InitializeComponent();
        OzellikleriKur();
        SehirMenusunuGuncelle();
        if (gizliBaslat && _ayarlar.TepsideCalis && SehirSecili) GizliBaslangicHazirla();
        TemaUygula(_ayarlar.Tema, kaydet: false);
        OlcekAyarla(_ayarlar.Olcek, kaydet: false);

        // TT_CEVRIMDISI=1 ayarlıysa uygulama internet yokmuş gibi davranır (çevrimdışı çalışmayı denemek için).
        var http = Environment.GetEnvironmentVariable("TT_CEVRIMDISI") == "1"
            ? new HttpClient(new InternetYokHandler())
            : TurktakvimIstemci.VarsayilanHttpIstemcisi();
        _istemci = new TurktakvimIstemci(http);
        _vakitler = new VakitServisi(_istemci, _onbellek);
        _icerik = new IcerikServisi(_istemci, _onbellek);
        _sehirZaman = new SehirZamanServisi(_istemci, _onbellek);
        _ezaniServisi = new EzaniServisi(_istemci, _onbellek);

        var bugun = DateTime.Today;
        _tarihBaslangic = bugun.AddYears(-44);
        _tarihSonu = YilSonu(bugun);
        _seciliTarih = bugun;
        GunDugmeleriniGuncelle();

        _zamanlayici.Tick += (_, _) => Tik();
        Loaded += async (_, _) =>
        {
            GizliBaslangiciBitir();
            if (SehirSecili)
                await GunuYukle(zorla: false);
            else
                IlkSehirSecimi();

            _hazir = true;
            _ = CizelgeVarsayilaniniBelirle();
            await IcerigiYukle(DateOnly.FromDateTime(bugun));
            _zamanlayici.Start();
            IcerikIndirmesiniBaslat();
        };
        Closed += (_, _) =>
        {
            _zamanlayici.Stop();
            _arkaPlanIptal.Cancel();
        };
    }

    // ---------- Vakitler ----------

    private async Task GunuYukle(bool zorla)
    {
        if (_yukleniyor) return;
        _yukleniyor = true;
        try
        {
            if (SehirSecili) _sehirFarki = _sehirZaman.Kayitli(SehirKodu);
            var bugun = DateOnly.FromDateTime(SehirSimdi);
            Duyur(zorla ? "Vakitler internetten yenileniyor." : "Vakitler yükleniyor.");

            string? uyari = null;
            if (zorla)
            {
                try
                {
                    await _vakitler.YiliHazirlaAsync(SehirKodu, bugun.Year, zorla: true);
                }
                catch (VeriHatasi)
                {
                    uyari = "İnternete bağlanılamadı, kayıtlı vakitler gösteriliyor.";
                }
            }

            var sonuc = await _vakitler.GunuGetirAsync(SehirKodu, bugun);

            // Gün değiştiyse ve tarih seçici hâlâ eski güne bakıyorsa, yeni güne taşı.
            var eskiGun = _yuklenenGun;
            _bugun = sonuc;
            _yuklenenGun = bugun;
            if (_hazir && eskiGun != default && eskiGun != bugun)
            {
                if (YilSonu(DateTime.Today) > _tarihSonu) _tarihSonu = YilSonu(DateTime.Today);
                if (_seciliTarih == eskiGun.ToDateTime(TimeOnly.MinValue))
                    SeciliTarihiAyarla(DateTime.Today);
            }

            _yarinImsak = await YarinImsakiniBul(bugun);
            SehirBasligi.Text = sonuc.SehirAdi;
            if (eskiGun != bugun) _hicriBugun = null;
            BaslikTarihiniYaz();
            _siradakiSira = -2;
            KibleyiGuncelle();
            if (_ezaniGun != bugun || _ezaniSehir != SehirKodu) _ezani = new List<EzaniSatiri>(); // başka şehir/gün: eski Ezânî satırları görünmesin
            ListeleriDoldur(odakla: !_hazir);
            Tik();

            string durum = sonuc.Kaynak == VeriKaynagi.Internet
                ? "Vakitler internetten indirildi."
                : zorla ? "Vakitler yenilendi." : "Vakitler hazır.";
            if (sonuc.Gun.Saatler.Any(s => s.StartsWith('*')))
                durum += " Yıldızlı saatler yaklaşıktır.";
            if (sonuc.Gun.Saatler.Any(s => s == "-"))
                durum += " Bazı vakitler bu gün için hesaplanamıyor.";
            Duyur(uyari ?? durum);

            _ = _vakitler.ArkaPlandaHazirlaAsync(SehirKodu, bugun);
            _ = HicriBugunuYukle(bugun);
            BugunUyarisiniGuncelle();
            _ = MubarekGunleriniHazirla();
            _ = SehirFarkiniTazele();
            _ = HaftayiYukle();
            _ = EzaniYukle();
        }
        catch (VeriHatasi)
        {
            KalanSure.Text = "";
            Duyur("Vakitler alınamadı: kayıtlı veri yok ve internete bağlanılamadı. Yenilemek için F5 tuşuna basın.");
        }
        finally
        {
            _yukleniyor = false;
        }
    }

    private async Task<DateTime?> YarinImsakiniBul(DateOnly bugun)
    {
        try
        {
            var yarin = bugun.AddDays(1);
            var sonuc = await _vakitler.GunuGetirAsync(SehirKodu, yarin);
            _yarinSaatler = sonuc.Gun.Saatler;
            _yarinSaatlerAnahtar = $"{SehirKodu}|{yarin:yyyyMMdd}";
            return VakitSaati.Ayristir(sonuc.Gun.Saatler[VakitAdlari.Ana[0]]).Zaman is { } z ? yarin.ToDateTime(TimeOnly.FromTimeSpan(z)) : null;
        }
        catch (VeriHatasi)
        {
            return null;
        }
    }

    private IReadOnlyList<string>? _yarinSaatler;      // yarının 18 vakti (gün bitince sıradaki "diğer vakit" için)
    private string _yarinSaatlerAnahtar = "";
    private string _siradakiDigerAnahtar = "";           // "vakit|yarın": liste yalnızca bu değişince yeniden kurulur

    /// <summary>
    /// Diğer Vakitler'de sıradaki vakit: bugün şimdiden sonra gelen en yakın vakit; bugün kalmadıysa yarının en erken vakti
    /// (Yarın = true). Ezânî satırları dahil değildir. Sıradaki yoksa Vakit = -1.
    /// </summary>
    private (int Vakit, bool Yarin) SiradakiDiger(DateTime simdi)
    {
        if (_bugun is null) return (-1, false);

        int sec = -1;
        DateTime? enYakin = null;
        for (int i = 0; i < VakitAdlari.Hepsi.Length; i++)
        {
            if (VakitAdlari.AnaMi(i) || VakitZamani(simdi, i) is not { } z || z <= simdi) continue;
            if (enYakin is null || z < enYakin) { enYakin = z; sec = i; }
        }
        if (sec >= 0) return (sec, false);

        var yarin = DateOnly.FromDateTime(simdi).AddDays(1);
        if (_yarinSaatler is null || _yarinSaatlerAnahtar != $"{SehirKodu}|{yarin:yyyyMMdd}") return (-1, false);
        for (int i = 0; i < VakitAdlari.Hepsi.Length; i++)
        {
            if (VakitAdlari.AnaMi(i) || VakitSaati.Ayristir(_yarinSaatler[i]).Zaman is not { } t) continue;
            var z = yarin.ToDateTime(TimeOnly.FromTimeSpan(t));
            if (enYakin is null || z < enYakin) { enYakin = z; sec = i; }
        }
        return (sec, sec >= 0);
    }

    /// <summary>Bugünün ana vakitleri içinde (0-4) sıradakinin yeri; hepsi geçtiyse -1.</summary>
    private int SiradakiAna(DateTime simdi)
    {
        if (_bugun is null) return -1;
        for (int i = 0; i < VakitAdlari.Ana.Length; i++)
        {
            if (VakitZamani(simdi, VakitAdlari.Ana[i]) is { } zaman && zaman > simdi) return i;
        }
        return -1;
    }

    /// <summary>Bugünün <paramref name="vakit"/> numaralı vaktinin tam zamanı; o gün için vakit yoksa null.</summary>
    private DateTime? VakitZamani(DateTime simdi, int vakit)
    {
        if (_bugun is null || VakitSaati.Ayristir(_bugun.Gun.Saatler[vakit]).Zaman is not { } z) return null;

        // Kuzeydeki yerlerde site yatsıyı gece yarısından sonrası (örn. *00:01) olarak verir: o, ertesi günün ilk dakikalarıdır.
        int yatsi = VakitAdlari.Ana[^1], aksam = VakitAdlari.Ana[^2];
        if (vakit == yatsi && VakitSaati.Ayristir(_bugun.Gun.Saatler[aksam]).Zaman is { } a && z < a) z += TimeSpan.FromDays(1);
        return simdi.Date + z;
    }

    private void Tik()
    {
        if (_bugun is null) return;

        var simdi = SehirSimdi;
        if (DateOnly.FromDateTime(simdi) != _yuklenenGun)
        {
            _ = GunuYukle(zorla: false);
            _ = SehirFarkiniTazele(); // uygulama günlerce açık kalırsa yaz/kış saati geçişi de yakalanır
            return;
        }

        int siradaki = SiradakiAna(simdi);
        var (digerVakit, digerYarin) = SiradakiDiger(simdi);
        if (siradaki != _siradakiSira || $"{digerVakit}|{digerYarin}" != _siradakiDigerAnahtar) ListeleriDoldur(odakla: false);

        string saatMetni = simdi.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (SaatYazisi.Text != saatMetni) SaatYazisi.Text = saatMetni;

        string metin;
        DateTime? onceki = null, sonraki = null;
        if (siradaki >= 0)
        {
            int v = VakitAdlari.Ana[siradaki];
            sonraki = VakitZamani(simdi, v);
            onceki = Enumerable.Range(0, siradaki).Reverse()
                .Select(j => VakitZamani(simdi, VakitAdlari.Ana[j])).FirstOrDefault(z => z is not null) ?? simdi.Date;
            metin = KalanMetni(GosterAd(v), VakitSaati.Ayristir(_bugun.Gun.Saatler[v]), sonraki!.Value - simdi, KalanEtiketi(v));
        }
        else if (_yarinImsak is { } yarin)
        {
            sonraki = yarin;
            onceki = VakitAdlari.Ana.Reverse().Select(v => VakitZamani(simdi, v)).FirstOrDefault(z => z is not null) ?? simdi.Date;
            metin = KalanMetni(GosterAd(VakitAdlari.Ana[0]),
                VakitSaati.Ayristir(yarin.ToString("HH:mm", CultureInfo.InvariantCulture)), yarin - simdi, KalanEtiketi(VakitAdlari.Ana[0]), ", yarın");
        }
        else
        {
            metin = "Bugünün ana vakitleri tamamlandı.";
        }

        IlerlemeyiGuncelle(simdi, onceki, sonraki);
        BildirimleriDenetle(simdi);
        TekBildirimleriDenetle(simdi);
        MubarekBildirimleriniDenetle(simdi);

        if (metin != _sonKalanMetin)
        {
            _sonKalanMetin = metin;
            KalanSure.Text = metin;
            TepsiMetniniGuncelle(siradaki, sonraki - simdi);
        }
    }

    /// <summary>Önceki vakitten sıradaki vakte geçen süreyi geri sayım kartındaki çubukta gösterir (yalnızca görsel).</summary>
    private void IlerlemeyiGuncelle(DateTime simdi, DateTime? onceki, DateTime? sonraki)
    {
        double oran = 0;
        if (onceki is { } o && sonraki is { } s && s > o)
            oran = Math.Clamp((simdi - o).TotalSeconds / (s - o).TotalSeconds, 0, 1);

        IlerlemeDolu.Width = new GridLength(Math.Max(oran, 0.001), GridUnitType.Star);
        IlerlemeBos.Width = new GridLength(Math.Max(1 - oran, 0.001), GridUnitType.Star);
    }

    private static string KalanMetni(string ad, VakitSaati vakit, TimeSpan kalan, string etiket = "Kalan süre", string sonEk = "")
    {
        if (kalan < TimeSpan.Zero) kalan = TimeSpan.Zero;
        string sure = (int)kalan.TotalHours > 0
            ? $"{(int)kalan.TotalHours} saat {kalan.Minutes} dakika"
            : kalan.Minutes > 0 ? $"{kalan.Minutes} dakika" : "1 dakikadan az";
        // Her yerde aynı düzen: ad, (Ramazan'da iftar / sahur sonu), saat, (yarın) virgülle ayrılır; parantez kullanılmaz.
        return $"Sıradaki vakit: {ad}, {vakit.Okunan}{sonEk}. {etiket}: {sure}.";
    }

    private void ListeleriDoldur(bool odakla)
    {
        if (_bugun is null) return;

        int siradaki = SiradakiAna(SehirSimdi);
        _siradakiSira = siradaki;
        var gun = _bugun.Gun;

        bool listedeOdak = AnaListe.IsKeyboardFocusWithin;
        int eskiSecim = AnaListe.SelectedIndex;

        var anaSatirlar = VakitAdlari.Ana
            .Select((v, i) => new VakitSatiri(GosterAd(v), gun.Saatler[v], i == siradaki, gecti: siradaki < 0 || i < siradaki,
                vakitNo: v, bildirilecek: TekBildirimKurulu(v, yarinSatiri: false)))
            .ToList();

        // Gün bittiyse ve yarının imsağı biliniyorsa, listenin sonunda "Yarın İmsak" sıradaki vakit olarak görünür.
        if (siradaki < 0 && _yarinImsak is { } yarin)
            anaSatirlar.Add(new VakitSatiri(GosterAd(VakitAdlari.Ana[0]),
                yarin.ToString("HH:mm", CultureInfo.InvariantCulture), siradaki: true, yarin: true,
                vakitNo: VakitAdlari.Ana[0], bildirilecek: TekBildirimKurulu(VakitAdlari.Ana[0], yarinSatiri: true)));

        AnaListe.ItemsSource = anaSatirlar;

        var simdi = SehirSimdi;
        var (siradakiDiger, digerYarin) = SiradakiDiger(simdi);
        _siradakiDigerAnahtar = $"{siradakiDiger}|{digerYarin}";
        string digerAnahtar = $"{SehirKodu}|{gun.Tarih:yyyyMMdd}|{EzaniSatirlari.Count}|{_siradakiDigerAnahtar}|{_tekBildirimSurumu}";
        if (DigerListe.Items.Count == 0 || odakla || digerAnahtar != _digerAnahtar)
        {
            _digerAnahtar = digerAnahtar;
            bool digerdeOdak = DigerListe.IsKeyboardFocusWithin;
            int eskiDiger = DigerListe.SelectedIndex;
            // Vakitler sekmesiyle aynı mantık: sıradaki vakit rozetli ve vurgulu; geçmiş vakitler soluk. Gün bitince yarının ilk vakti "Yarın" rozetiyle.
            var diger = Enumerable.Range(0, VakitAdlari.Hepsi.Length)
                .Where(i => !VakitAdlari.AnaMi(i))
                .Select(i => i == siradakiDiger && digerYarin
                    ? new VakitSatiri(VakitAdlari.Hepsi[i], _yarinSaatler![i], siradaki: true, yarin: true,
                        vakitNo: i, bildirilecek: TekBildirimKurulu(i, yarinSatiri: true))
                    : new VakitSatiri(VakitAdlari.Hepsi[i], gun.Saatler[i], siradaki: i == siradakiDiger,
                        gecti: i != siradakiDiger && VakitZamani(simdi, i) is { } z && z <= simdi,
                        vakitNo: i, bildirilecek: TekBildirimKurulu(i, yarinSatiri: false)))
                .ToList();
            int siradakiYeriDiger = diger.FindIndex(s => s.Siradaki);
            diger.AddRange(EzaniSatirlari.Select(e => VakitSatiri.Bilgi("Ezânî " + e.Ad, e.Saat, e.Saat + ", alaturka saat")));
            DigerListe.ItemsSource = diger;
            DigerListe.SelectedIndex = digerdeOdak && eskiDiger >= 0 && eskiDiger < diger.Count ? eskiDiger
                : siradakiYeriDiger >= 0 ? siradakiYeriDiger
                : eskiDiger >= 0 && eskiDiger < diger.Count ? eskiDiger : 0;
        }

        int siradakiYeri = siradaki >= 0 ? siradaki : anaSatirlar.Count - 1;
        AnaListe.SelectedIndex = odakla
            ? siradakiYeri
            : eskiSecim >= 0 && eskiSecim < anaSatirlar.Count ? eskiSecim : siradakiYeri;

        if (odakla || listedeOdak) OdakVer(AnaListe);
    }

    private static void OdakVer(ListBox liste)
    {
        liste.UpdateLayout();
        if (liste.SelectedIndex < 0) return;
        liste.ScrollIntoView(liste.SelectedItem);
        liste.UpdateLayout();
        if (liste.ItemContainerGenerator.ContainerFromIndex(liste.SelectedIndex) is ListBoxItem oge)
            oge.Focus();
        else
            liste.Focus();
    }

    /// <summary>
    /// İnternet varken, içinde bulunulan yılın tüm günlerinin içeriğini (söz, olay, arka yaprak, günün bilgileri)
    /// arka planda yavaş yavaş indirip diske kaydeder; internet kesilince de okunabilsin. Önce bugünden yıl sonuna,
    /// sonra yılın geçmiş günleri. Kasım'dan sonra site yayınladıysa gelecek yıl da alınır.
    /// Siteyi yormamak için indirmeler arasında bekler; eski yılları indirmez (onlar bakılınca tek tek kaydolur).
    /// </summary>
    private async Task IcerikleriArkaPlandaHazirla(CancellationToken iptal)
    {
        try
        {
            // Açılış hızı önceliklidir: indirme, pencere açıldıktan ve her şey yerleştikten sonra başlar.
            await Task.Delay(TimeSpan.FromSeconds(8), iptal);

            var bugun = DateOnly.FromDateTime(DateTime.Today);
            var gunler = TarihAraligi(bugun, new DateOnly(bugun.Year, 12, 31))
                .Concat(TarihAraligi(new DateOnly(bugun.Year, 1, 1), bugun.AddDays(-1)));
            if (bugun.Month >= 11)
                gunler = gunler.Concat(TarihAraligi(new DateOnly(bugun.Year + 1, 1, 1), new DateOnly(bugun.Year + 1, 12, 31)));

            int indirilen = (await Task.Run(() => _icerik.GunleriHazirlaAsync(gunler, TimeSpan.FromMilliseconds(600), iptal))).Indirilen;
            if (indirilen > 0)
                Duyur($"{indirilen} günün içeriği çevrimdışı kullanım için indirildi.");
        }
        catch (OperationCanceledException)
        {
            // Uygulama kapanıyor; kalan günler bir sonraki açılışta indirilir.
        }
    }

    private CancellationTokenSource? _icerikIndirme;

    /// <summary>Arka plan içerik indirmesini (ayar açıksa) başlatır; çalışan varsa önce onu durdurur.</summary>
    private void IcerikIndirmesiniBaslat()
    {
        _icerikIndirme?.Cancel();
        if (!_ayarlar.ArkaPlandaIndir) return;

        var iptal = _icerikIndirme = CancellationTokenSource.CreateLinkedTokenSource(_arkaPlanIptal.Token);
        _ = IcerikleriArkaPlandaHazirla(iptal.Token);
    }

    private static IEnumerable<DateOnly> TarihAraligi(DateOnly ilk, DateOnly son)
    {
        for (var gun = ilk; gun <= son; gun = gun.AddDays(1))
            yield return gun;
    }

    /// <summary>
    /// Şehrin saati: bilgisayarın saat diliminden farklı bir yer seçiliyse (örn. Türkiye'deki bilgisayarda Berlin)
    /// vakitler şehrin yerel saatiyle verildiği için saat, tarih ve geri sayım şehir saatine göre gösterilir.
    /// </summary>
    private DateTime SehirSimdi =>
        _sanalHedef is { } hedef ? hedef + (DateTime.UtcNow - _sanalBasi) :
        _sehirFarki is { } fark ? DateTime.SpecifyKind(DateTime.UtcNow + fark, DateTimeKind.Unspecified) : DateTime.Now;

    /// <summary>Şehrin saati bilgisayardan farklıysa bunu söyleyen kısa cümle; aynıysa null.</summary>
    private string? SaatFarkiMetni()
    {
        if (_sehirFarki is not { } sehir) return null;

        var fark = sehir - TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
        if (Math.Abs(fark.TotalMinutes) < 1) return null;

        int dakika = (int)Math.Round(Math.Abs(fark.TotalMinutes));
        string sure = dakika % 60 == 0 ? $"{dakika / 60} saat"
            : dakika < 60 ? $"{dakika} dakika"
            : $"{dakika / 60} saat {dakika % 60} dakika";
        return $"Şehrin saati bilgisayarınızdan {sure} {(fark > TimeSpan.Zero ? "ileride" : "geride")}.";
    }

    /// <summary>Ana ekran başlığı: şehrin tarihi, biliniyorsa hicrî tarih ve (varsa) saat farkı bilgisi.</summary>
    private void BaslikTarihiniYaz()
    {
        string miladi = SehirSimdi.ToString("d MMMM yyyy dddd", Tr);
        string metin = _hicriBugun is null ? miladi : $"{miladi}  ·  {_hicriBugun}";
        string? mubarek = MubarekOzeti(DateOnly.FromDateTime(SehirSimdi));
        if (mubarek is not null) metin = $"{metin}  ·  {mubarek}";
        string? fark = SaatFarkiMetni();
        TarihYazisi.Text = fark is null ? metin : $"{metin}  ·  {fark}";
    }

    /// <summary>Yeni seçilen şehrin saat farkını belirler: kayıtlıysa onu kullanır, yoksa siteden öğrenir (en çok 4 sn bekler).</summary>
    private async Task SehirFarkiniBelirle()
    {
        _sehirFarki = _sehirZaman.Kayitli(SehirKodu);
        if (_sehirFarki is not null) return;

        using var iptal = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        try
        {
            _sehirFarki = await _sehirZaman.GuncelleAsync(SehirKodu, iptal.Token);
        }
        catch (OperationCanceledException)
        {
            // Öğrenilemezse bilgisayar saati kullanılır; sonraki açılışta yeniden denenir.
        }
    }

    /// <summary>Arka planda güncel farkı alır (yaz saati değişmiş olabilir); değişirse ekran yeni saate göre yenilenir.</summary>
    private async Task SehirFarkiniTazele()
    {
        string kod = _ayarlar.SehirKodu;
        if (string.IsNullOrWhiteSpace(kod)) return;

        var taze = await _sehirZaman.GuncelleAsync(kod);
        if (taze is null || kod != _ayarlar.SehirKodu) return;

        // Koordinatlar yeni öğrenilmiş olabilir: kıble yönü listeye girer.
        KibleyiGuncelle();
        if (taze == _sehirFarki) return;

        _sehirFarki = taze;
        BaslikTarihiniYaz();
        Tik();
    }
    /// <summary>Bugünün hicrî tarihini içerik servisinden alıp (önbellekte varsa internetsiz) başlığa ekler.</summary>
    private async Task HicriBugunuYukle(DateOnly bugun)
    {
        try
        {
            var ic = await _icerik.GetirAsync(bugun);
            _hicriBugun = string.IsNullOrWhiteSpace(ic.Bilgi?.HicriTarih) ? null : ic.Bilgi!.HicriTarih;
        }
        catch (VeriHatasi)
        {
            return; // Başlıkta yalnızca miladi tarih kalır.
        }

        if (_yuklenenGun != bugun) return;
        BaslikTarihiniYaz();
        if (RamazanMi != _ramazanGosterildi)
        {
            _ramazanGosterildi = RamazanMi;
            _sonKalanMetin = "";
            ListeleriDoldur(odakla: false);
            Tik();
        }
    }

    // ---------- Takvim sekmesi ----------

    private async Task IcerigiYukle(DateOnly tarih)
    {
        _ = GunVakitleriniYukle(tarih);
        _icerikIptal?.Cancel();
        var iptal = _icerikIptal = new CancellationTokenSource();

        MetinleriDoldur("Yükleniyor…");
        try
        {
            var ic = await _icerik.GetirAsync(tarih, iptal.Token);
            if (iptal.IsCancellationRequested) return;

            const string yok = "Bu tarih için kayıt yok.";
            TarihKutusunuYaz(tarih, ic.Bilgi is { Satirlar.Count: > 0 } ? ic.Bilgi.Satirlar : new[] { yok });

            MetinYaz(OlayKutusu, string.Join("\n", ic.Olaylar.Count > 0 ? ic.Olaylar : new[] { yok }));

            MetinYaz(SozKutusu, ic.Soz ?? yok);

            // Başlık metnin ilk satırı olur; Tab'de alan adı, aşağı okla ilk satır olarak okunur.
            var parcalar = new[] { ic.ArkaYaprakBaslik, ic.ArkaYaprakMetin, ic.GununEki }
                .Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            MetinYaz(ArkaYaprakKutusu, parcalar.Count > 0 ? string.Join("\n", parcalar) : yok);
        }
        catch (VeriHatasi)
        {
            if (iptal.IsCancellationRequested) return;
            MetinleriDoldur("İçerik alınamadı: kayıtlı veri yok ve internete bağlanılamadı.");
        }
        catch (OperationCanceledException)
        {
            // Başka bir tarih seçildi; yeni yükleme devam ediyor.
        }
    }

    /// <summary>İlk satır miladi tarih, devamı günün bilgileridir (boş satır yok: NVDA satır satır okurken tekrar etmesin).</summary>
    private DateOnly _tarihKutusuTarih;
    private List<string> _tarihKutusuSatirlari = new();

    /// <summary>
    /// Takvim sekmesindeki "Tarih ve Günün Bilgileri": tarih, mübarek gün satırı, sitenin günlük bilgileri ve (biliniyorsa)
    /// seçili yerin kıble yönü. Kıble yönü tarihe bağlı değildir, her günde aynıdır.
    /// </summary>
    private void TarihKutusunuYaz(DateOnly tarih, IEnumerable<string> satirlar)
    {
        _tarihKutusuTarih = tarih;
        _tarihKutusuSatirlari = satirlar.ToList();

        string? mubarek = MubarekSatiriMetni(tarih);
        string? kible = _kibleMetni.Length > 0 ? "Kıble yönü: " + _kibleMetni : null;
        MetinYaz(TarihKutusu, tarih.ToString("d MMMM yyyy dddd", Tr) + "\n"
            + (mubarek is null ? "" : mubarek + "\n")
            + string.Join("\n", _tarihKutusuSatirlari)
            + (kible is null ? "" : "\n" + kible)
            + (kible is null || _pusulaMetni.Length == 0 ? "" : "\n" + _pusulaMetni));
    }

    /// <summary>Kıble yönü ya da başka bir ek bilgi sonradan öğrenilince kutuyu aynı gün için yeniden yazar.</summary>
    private void TarihKutusunuYenile()
    {
        if (_tarihKutusuSatirlari.Count > 0) TarihKutusunuYaz(_tarihKutusuTarih, _tarihKutusuSatirlari);
    }

    private void MetinleriDoldur(string mesaj)
    {
        TarihKutusunuYaz(DateOnly.FromDateTime(_seciliTarih), new[] { mesaj });
        MetinYaz(OlayKutusu, mesaj);
        MetinYaz(SozKutusu, mesaj);
        MetinYaz(ArkaYaprakKutusu, mesaj);
    }

    /// <summary>
    /// Boş satırlar bırakılmaz: WPF'in UI Automation'ı boş satır için bir önceki satırın metnini döndürür,
    /// bu yüzden NVDA aşağı okla ilerlerken o satırı iki kez okurdu. Paragraflar tek satır sonuyla ayrılır.
    /// </summary>
    private static void MetinYaz(TextBox kutu, string metin)
    {
        // Önbellekteki eski kayıtlarda da yumuşak tire gibi görünmez karakterler olabilir: ekran okuyucu heceleyerek okumasın.
        string temiz = Regex.Replace(GorunmezKarakterler.At(metin).Replace("\r", ""), @"\n\s*\n+", "\n").Trim();
        kutu.Text = temiz.Replace("\n", Environment.NewLine);
        kutu.CaretIndex = 0;
        kutu.ScrollToHome();
    }

    /// <summary>Seçili tarihi değiştirir: düğmeleri günceller ve o günün içeriğini yükler.</summary>
    private void SeciliTarihiAyarla(DateTime tarih)
    {
        _seciliTarih = tarih.Date;
        GunDugmeleriniGuncelle();
        BugunUyarisiniGuncelle();
        if (_hazir) _ = IcerigiYukle(DateOnly.FromDateTime(_seciliTarih));
    }

    private void OncekiGun_Click(object sender, RoutedEventArgs e) => GunDegistir(-1);
    private void SonrakiGun_Click(object sender, RoutedEventArgs e) => GunDegistir(1);

    private void BugunTarihi_Click(object sender, RoutedEventArgs e)
    {
        if (BugunDugmesi.Gecersiz)
        {
            Duyur("Zaten bugünün tarihindesiniz.");
            return;
        }
        SeciliTarihiAyarla(DateTime.Today);
    }

    /// <summary>turktakvim.com içerik yayınını bulunulan yılın sonuna kadar tutuyor (31 Aralık'tan sonrası boş geliyor).</summary>
    private static DateTime YilSonu(DateTime tarih) => new(tarih.Year, 12, 31);

    private async void GunDegistir(int fark) =>
        await TariheGitAsync(_seciliTarih.AddDays(fark), Duyur);

    /// <summary>
    /// Verilen tarihe gider; sınırlar dışındaysa nedenini <paramref name="hataBildir"/> ile söyler ve false döner.
    /// Bulunulan yılın sonundan ötesi için siteyi yoklar: yeni yılın içeriği yayınlanmışsa sınır genişler.
    /// </summary>
    private async Task<bool> TariheGitAsync(DateTime hedef, Action<string> hataBildir)
    {
        hedef = hedef.Date;
        if (hedef < _tarihBaslangic)
        {
            hataBildir("Daha eski bir tarih için içerik yok.");
            return false;
        }

        if (hedef > _tarihSonu)
        {
            if (!await SiteIcerigiVarMi(hedef))
            {
                hataBildir($"{_tarihSonu:d MMMM yyyy} tarihinden sonrası için henüz içerik yok.");
                return false;
            }
            _tarihSonu = YilSonu(hedef);
        }

        if (hedef != _seciliTarih) SeciliTarihiAyarla(hedef);
        return true;
    }
    private async Task<bool> SiteIcerigiVarMi(DateTime tarih)
    {
        try
        {
            var ic = await _icerik.GetirAsync(DateOnly.FromDateTime(tarih));
            return ic.Soz is not null || ic.ArkaYaprakBaslik is not null;
        }
        catch (VeriHatasi)
        {
            return false;
        }
    }

    /// <summary>
    /// Gün düğmeleri her zaman odaklanabilir kalır. Uygulanamayan düğme "geçersiz" olur: soluk görünür ve
    /// ekran okuyucuya kullanılamıyor olarak bildirilir.
    /// </summary>
    private void GunDugmeleriniGuncelle()
    {
        OncekiGunDugmesi.Gecersiz = _seciliTarih.AddDays(-1) < _tarihBaslangic;
        SonrakiGunDugmesi.Gecersiz = _seciliTarih.AddDays(1) > _tarihSonu;
        BugunDugmesi.Gecersiz = _seciliTarih.Date == DateTime.Today;
    }

    // ---------- Açılır takvim ----------
    // Pencerede önce tarih yazma alanı gelir (gün.ay.yıl yazıp Enter); Tab ile takvime geçilir
    // (sağ/sol ok: gün, yukarı/aşağı ok: hafta, Enter: seç). Esc kapatır.

    private static readonly string[] TarihBicimleri =
    {
        "d.M.yyyy", "d/M/yyyy", "d-M-yyyy", "d M yyyy", "d MMMM yyyy", "d MMMM yyyy dddd"
    };

    private static bool TarihOku(string metin, out DateTime tarih) =>
        DateTime.TryParseExact(metin.Trim(), TarihBicimleri, Tr, DateTimeStyles.AllowWhiteSpaces, out tarih);

    private void TakvimiGoster_Click(object sender, RoutedEventArgs e)
    {
        _takvimSessiz = true;
        TakvimKontrolu.DisplayMode = CalendarMode.Month;
        TakvimKontrolu.DisplayDateStart = _tarihBaslangic;
        TakvimKontrolu.DisplayDateEnd = _tarihSonu;
        TakvimKontrolu.SelectedDate = _seciliTarih;
        TakvimKontrolu.DisplayDate = _seciliTarih;
        _takvimSessiz = false;

        TarihGirisKutusu.Text = _seciliTarih.ToString("dd.MM.yyyy", Tr);
        TarihHatasiniTemizle();

        TakvimPopup.IsOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            TakvimAdlariniGuncelle();
            TakvimBilgisiniYaz(duyur: false);
            TarihGirisKutusu.Focus();
            TarihGirisKutusu.SelectAll();
        });
    }

    // Takvimin kendi düğmeleri (üstteki ay/yıl başlığı, önceki, sonraki) İngilizce ad taşır; Türkçe adlar burada verilir.

    private void TakvimKontrolu_Loaded(object sender, RoutedEventArgs e) => TakvimAdlariniGuncelle();

    private void TakvimKontrolu_DisplayModeChanged(object? sender, CalendarModeChangedEventArgs e) => TakvimAdlariniGuncelle();

    private void TakvimKontrolu_DisplayDateChanged(object? sender, CalendarDateChangedEventArgs e)
    {
        // Windows, ay düğmelerinden sonra odağı yeni ayın bir gününe taşır; seçili gün odağı izlemeli ki Tamam duyulan günü seçsin.
        if (!_takvimSessiz) Dispatcher.BeginInvoke(DispatcherPriority.Input, TakvimSeciminiOdagaUydur);
        TakvimAdlariniGuncelle();
        TakvimBilgisiniYaz(duyur: !_takvimSessiz);
    }

    private void TakvimAdlariniGuncelle()
    {
        var mod = TakvimKontrolu.DisplayMode;
        var gosterilen = TakvimKontrolu.DisplayDate;
        string birim = mod switch { CalendarMode.Month => "ay", CalendarMode.Year => "yıl", _ => "on yıl" };
        string baslik = mod switch
        {
            CalendarMode.Month => $"{gosterilen.ToString("MMMM yyyy", Tr)}, ay ve yıl seçimini aç",
            CalendarMode.Year => $"{gosterilen.Year}, yıl seçimini aç",
            _ => $"{gosterilen.Year / 10 * 10} - {gosterilen.Year / 10 * 10 + 9}, on yıllık dönem"
        };

        TakvimParcaAdi("PART_PreviousButton", $"Önceki {birim}");
        TakvimParcaAdi("PART_NextButton", $"Sonraki {birim}");
        TakvimParcaAdi("PART_HeaderButton", baslik);
    }

    private void TakvimParcaAdi(string parca, string ad)
    {
        // Düğmeler Calendar'ın değil, içindeki CalendarItem'ın şablonunda durur.
        if (TakvimKontrolu.Template?.FindName("PART_CalendarItem", TakvimKontrolu) is not Control oge) return;
        if (oge.Template?.FindName(parca, oge) is Button dugme)
            AutomationProperties.SetName(dugme, ad);
    }

    /// <summary>Takvimin altındaki satır gösterilen ayı söyler; değişince ekran okuyucuya duyurulur.</summary>
    private void TakvimBilgisiniYaz(bool duyur)
    {
        TakvimBilgisi.Text = $"Gösterilen: {TakvimKontrolu.DisplayDate.ToString("MMMM yyyy", Tr)}";
        if (duyur) TakvimBilgisiniDuyur();
    }

    private void TakvimBilgisiniDuyur()
    {
        var peer = UIElementAutomationPeer.FromElement(TakvimBilgisi) ?? UIElementAutomationPeer.CreatePeerForElement(TakvimBilgisi);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void TakvimDuyur(string mesaj)
    {
        TakvimBilgisi.Text = mesaj;
        TakvimBilgisiniDuyur();
    }

    private void TakvimSeciminiOdagaUydur()
    {
        if (!TakvimPopup.IsOpen || TakvimKontrolu.DisplayMode != CalendarMode.Month) return;

        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.CalendarDayButton { DataContext: DateTime gun } &&
            gun.Date >= _tarihBaslangic && gun.Date <= _tarihSonu)
        {
            if (TakvimKontrolu.SelectedDate != gun.Date)
            {
                _takvimSessiz = true;
                TakvimKontrolu.SelectedDate = gun.Date;
                _takvimSessiz = false;
            }
            return;
        }

        TakvimSeciminiGosterileneUydur();
    }

    /// <summary>Odak bir gün düğmesinde değilken ay değişirse seçili gün aynı gün numarasıyla o aya taşınır; böylece Tamam görünen ayı seçer.</summary>
    private void TakvimSeciminiGosterileneUydur()
    {
        if (TakvimKontrolu.DisplayMode != CalendarMode.Month || TakvimKontrolu.SelectedDate is not { } secili) return;

        var gosterilen = TakvimKontrolu.DisplayDate;
        if (secili.Year == gosterilen.Year && secili.Month == gosterilen.Month) return;

        int gun = Math.Min(secili.Day, DateTime.DaysInMonth(gosterilen.Year, gosterilen.Month));
        var yeni = new DateTime(gosterilen.Year, gosterilen.Month, gun);
        if (yeni < _tarihBaslangic) yeni = _tarihBaslangic;
        if (yeni > _tarihSonu) yeni = _tarihSonu;

        _takvimSessiz = true;
        TakvimKontrolu.SelectedDate = yeni;
        _takvimSessiz = false;
    }

    /// <summary>Üst düğmelerde (ay/yıl başlığı, önceki, sonraki) ok tuşları yıl yıl atlar; seçili gün yeni yıla taşınır.</summary>
    private void TakvimYilAtla(int yon)
    {
        var simdiki = TakvimKontrolu.SelectedDate ?? TakvimKontrolu.DisplayDate;
        int yil = simdiki.Year + yon;
        if (yil < _tarihBaslangic.Year)
        {
            TakvimDuyur("Daha eski bir yıl için içerik yok.");
            return;
        }
        if (yil > _tarihSonu.Year)
        {
            TakvimDuyur($"{_tarihSonu.Year} yılından sonrası için henüz içerik yok.");
            return;
        }

        int gun = Math.Min(simdiki.Day, DateTime.DaysInMonth(yil, simdiki.Month));
        var hedef = new DateTime(yil, simdiki.Month, gun);
        if (hedef < _tarihBaslangic) hedef = _tarihBaslangic;
        if (hedef > _tarihSonu) hedef = _tarihSonu;

        TakvimKontrolu.SelectedDate = hedef;
        TakvimKontrolu.DisplayDate = hedef; // DisplayDateChanged gösterilen ayı duyurur
    }

    private void TakvimTamam_Click(object sender, RoutedEventArgs e) => TakvimSecimiTamamla();

    private void TakvimVazgec_Click(object sender, RoutedEventArgs e)
    {
        TakvimPopup.IsOpen = false;
        TakvimGosterDugmesi.Focus();
    }

    /// <summary>Esc, pencerenin her yerinde seçim yapmadan kapatır ve odağı Takvimi göster düğmesine verir.</summary>
    private void TakvimPopup_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        TakvimPopup.IsOpen = false;
        TakvimGosterDugmesi.Focus();
        e.Handled = true;
    }

    /// <summary>Yazılan tarih geçerliyse arkadaki takvim de oraya kayar (yalnızca görsel; gitmek için Enter gerekir).</summary>
    private void TarihGirisKutusu_TextChanged(object sender, TextChangedEventArgs e)
    {
        TarihHatasiniTemizle();
        if (TarihOku(TarihGirisKutusu.Text, out var t) && t >= _tarihBaslangic && t <= _tarihSonu)
        {
            _takvimSessiz = true; // yazarken takvim sessizce kayar, duyuru yapılmaz
            TakvimKontrolu.SelectedDate = t;
            TakvimKontrolu.DisplayDate = t;
            _takvimSessiz = false;
        }
    }

    private async void TarihGirisKutusu_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;

        if (!TarihOku(TarihGirisKutusu.Text, out var hedef))
        {
            TarihHatasiGoster("Tarih anlaşılamadı. Gün, ay ve yılı şöyle yazın: 23.10.2026");
            TarihGirisKutusu.SelectAll();
            return;
        }

        if (!await TariheGitAsync(hedef, TarihHatasiGoster))
        {
            TarihGirisKutusu.SelectAll();
            return;
        }

        TakvimPopup.IsOpen = false;
        TarihKutusu.Focus();
    }

    private void TarihHatasiGoster(string mesaj)
    {
        TarihHatasi.Text = mesaj;
        TarihHatasi.Visibility = Visibility.Visible;
        var peer = UIElementAutomationPeer.FromElement(TarihHatasi) ?? UIElementAutomationPeer.CreatePeerForElement(TarihHatasi);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void TarihHatasiniTemizle()
    {
        TarihHatasi.Text = "";
        TarihHatasi.Visibility = Visibility.Collapsed;
    }

    private void TakvimKontrolu_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var odak = Keyboard.FocusedElement;
        bool gunDugmesi = odak is System.Windows.Controls.Primitives.CalendarDayButton;
        bool ustDugme = odak is Button and not System.Windows.Controls.Primitives.CalendarDayButton
                                       and not System.Windows.Controls.Primitives.CalendarButton;

        // Ay/yıl başlığı, önceki ve sonraki düğmelerinde ok tuşları gün gezmek yerine yıl yıl atlar.
        if (ustDugme && TakvimKontrolu.DisplayMode == CalendarMode.Month &&
            e.Key is Key.Left or Key.Up or Key.Right or Key.Down)
        {
            TakvimYilAtla(e.Key is Key.Left or Key.Up ? -1 : 1);
            e.Handled = true;
            return;
        }

        // Yalnızca bir gün düğmesinde Enter/Boşluk günü seçer; başlık ve ay düğmelerindeki Enter pencereyi kapatmamalı.
        if (gunDugmesi && e.Key is Key.Enter or Key.Space)
        {
            // Seçili günde Enter, tarih değiştirmeyeceği için seçim olayı gelmez; yine de pencere kapanmalı.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, TakvimSecimiTamamla);
        }
    }

    private void TakvimKontrolu_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject kaynak && FindAncestor<System.Windows.Controls.Primitives.CalendarDayButton>(kaynak) is not null)
            Dispatcher.BeginInvoke(DispatcherPriority.Input, TakvimSecimiTamamla);
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null)
        {
            if (d is T hedef) return hedef;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    /// <summary>Takvimde Enter, Boşluk ya da fare tıklamasıyla gün seçilince pencere kapanır, o güne gidilir ve odak tarih alanına döner.</summary>
    private void TakvimSecimiTamamla()
    {
        if (!TakvimPopup.IsOpen) return;

        var secilen = TakvimKontrolu.SelectedDate ?? _seciliTarih;
        TakvimPopup.IsOpen = false;
        if (secilen.Date != _seciliTarih) SeciliTarihiAyarla(secilen);
        TarihKutusu.Focus();
    }
    // ---------- Klavye ve menüler ----------

    private void Pencere_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Yeni bir Alt basışı (tek başına Alt menü çubuğunu açar); önceki kısayolun izi kalmasın.
        if (e.Key == Key.System && !e.IsRepeat && e.SystemKey is Key.LeftAlt or Key.RightAlt) _altKisayolu = false;

        // Alt+T (Günün Tarihi) / Alt+B (Bilgiler) / Alt+S / Alt+A / Alt+V: hangi sekmede olunursa olunsun Takvim sekmesindeki alana gider.
        if (e.Key == Key.System && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            FrameworkElement? hedef = e.SystemKey switch
            {
                Key.T => OlayKutusu,
                Key.S => SozKutusu,
                Key.A => ArkaYaprakKutusu,
                Key.B => TarihKutusu,
                Key.V => GunVakitleri,
                _ => null
            };
            if (hedef is not null)
            {
                e.Handled = true;
                _altKisayolu = true;
                TakvimAlaninaGit(hedef);
                return;
            }
        }

        if (e.Key == Key.F1)
        {
            e.Handled = true;
            if (Keyboard.Modifiers == ModifierKeys.Control) KisayollariAc();
            else KilavuzuAc();
            return;
        }

        if (e.Key == Key.F5)
        {
            e.Handled = true;
            Yenile();
            return;
        }

        // Yazı boyutu: Ctrl+Artı, Ctrl+Eksi, Ctrl+0.
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && (Keyboard.Modifiers & ModifierKeys.Alt) == 0)
        {
            switch (e.Key)
            {
                case Key.OemPlus or Key.Add: OlcekAdimi(+1); e.Handled = true; return;
                case Key.OemMinus or Key.Subtract: OlcekAdimi(-1); e.Handled = true; return;
                case Key.D0 or Key.NumPad0: OlcekAyarla(1.0, kaydet: true); e.Handled = true; return;
            }
        }

        // Ctrl+Tab / Ctrl+Shift+Tab sekmeyi WPF değiştirir; biz yalnızca sonrasında odağı sekmenin ilk öğesine veririz.
        if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            _icerigeOdakla = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () => _icerigeOdakla = false);
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.D1 or Key.NumPad1: SekmeyeGec(AnaSekme); e.Handled = true; break;
                case Key.D2 or Key.NumPad2: SekmeyeGec(TakvimSekme); e.Handled = true; break;
                case Key.D3 or Key.NumPad3: SekmeyeGec(DigerSekme); e.Handled = true; break;
                case Key.D4 or Key.NumPad4: CizelgeyeGec(CizelgeTuru.Hafta); e.Handled = true; break;
                case Key.D5 or Key.NumPad5: CizelgeyeGec(CizelgeTuru.Aylik); e.Handled = true; break;
                case Key.D6 or Key.NumPad6: CizelgeyeGec(CizelgeTuru.Imsakiye); e.Handled = true; break;
                case Key.P when ImsakiyeAktif: ImsakiyeYazdir(); e.Handled = true; break;
                case Key.P when AylikAktif: AylikYazdir(false); e.Handled = true; break;
                case Key.Q: Cik(); e.Handled = true; break;
                case Key.K: SehirMenusunuAc(); e.Handled = true; break;
                case Key.M: _ = MubarekGunleriAc(); e.Handled = true; break;
                case Key.OemComma: AyarlarAc(); e.Handled = true; break;
            }
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.P)
        {
            if (ImsakiyeAktif) { ImsakiyeYazdir(pdf: true); e.Handled = true; }
            else if (AylikAktif) { AylikYazdir(true); e.Handled = true; }
        }
    }

    // Alt+harf kısayolu kullanıldıysa, Alt tuşu bırakılırken Windows menü çubuğunu etkinleştirmesin.
    // (Alt, harften önce bırakılırsa menü çubuğu açılıyordu.) Alt tek başına basılırsa menü yine açılır.
    // WPF olayları bunu engellemeye yetmediği için Alt'ın bırakılma mesajı pencere düzeyinde yutulur.
    private bool _altKisayolu;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;
    private const int VK_MENU = 0x12;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        System.Windows.Interop.ComponentDispatcher.ThreadFilterMessage += AltBirakmaSuzgeci;
        KisayoluUygula();
        Closed += (_, _) =>
        {
            System.Windows.Interop.ComponentDispatcher.ThreadFilterMessage -= AltBirakmaSuzgeci;
            KisayoluKaldir();
        };
    }

    private void AltBirakmaSuzgeci(ref System.Windows.Interop.MSG mesaj, ref bool islendi)
    {
        // Harf basılıyken bırakılan Alt, WM_KEYUP olarak gelir; tek başına bırakılan Alt WM_SYSKEYUP olarak.
        if (mesaj.message == WM_HOTKEY && mesaj.wParam.ToInt32() == KisayolNo)
        {
            islendi = true;
            SiradakiVakitiSoyle();
            return;
        }

        if (mesaj.message is WM_SYSKEYUP or WM_KEYUP && mesaj.wParam.ToInt32() == VK_MENU && _altKisayolu)
        {
            _altKisayolu = false;
            islendi = true;
        }
    }
    private void TakvimAlaninaGit(FrameworkElement hedef)
    {
        TakvimPopup.IsOpen = false;
        if (Sekmeler.SelectedItem != TakvimSekme) Sekmeler.SelectedItem = TakvimSekme;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (hedef is ListBox liste) OdakVer(liste);
            else hedef.Focus();
        });
    }

    /// <summary>Sekme klavye ya da menüyle değişince odak, o sekmenin ilk öğesine (Ana/Diğer: liste, Takvim: Günün Tarihi) gider.</summary>
    private void Sekmeler_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource == Sekmeler) BugunUyarisiniSoyle();
        if (e.OriginalSource == Sekmeler && Sekmeler.SelectedItem == CizelgeSekme)
        {
            CizelgeYukle();
            if (!_cizelgeSecildi) _ = CizelgeVarsayilaniniBelirle();
        }

        if (e.OriginalSource != Sekmeler || !_icerigeOdakla) return; // listelerin kendi seçim olayları buraya da yükselir
        _icerigeOdakla = false;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, IlkOgeyeOdakla);
    }

    private void SekmeyeGec(TabItem sekme)
    {
        if (Sekmeler.SelectedItem == sekme)
        {
            IlkOgeyeOdakla();
            return;
        }
        _icerigeOdakla = true;
        Sekmeler.SelectedItem = sekme;
    }

    private void IlkOgeyeOdakla()
    {
        if (Sekmeler.SelectedItem == CizelgeSekme)
        {
            CizelgeIlkOgeyeOdakla();
            return;
        }

        if (Sekmeler.SelectedItem == TakvimSekme)
            OlayKutusu.Focus();
        else
            OdakVer(Sekmeler.SelectedItem == DigerSekme ? DigerListe : AnaListe);
    }

    private void Liste_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || sender is not ListBox liste) return;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            Kopyala(liste);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            TumunuKopyala(liste);
            e.Handled = true;
        }
    }

    private void Kopyala(ListBox liste)
    {
        string? metin = liste.SelectedItem switch
        {
            IKopyalanabilir k => k.Kopya,
            string m => m,
            _ => null
        };
        if (metin is null) return;
        PanoyaKoy(metin, liste.SelectedItem is IKopyalanabilir ? $"Panoya kopyalandı: {metin}" : "Seçili paragraf panoya kopyalandı.");
    }

    private void TumunuKopyala(ListBox liste)
    {
        var parcalar = liste.Items.Cast<object>()
            .Select(o => o is IKopyalanabilir k ? k.Kopya : o.ToString())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
        if (parcalar.Count == 0) return;
        string ayirac = liste.Items[0] is IKopyalanabilir ? Environment.NewLine : Environment.NewLine + Environment.NewLine;
        PanoyaKoy(string.Join(ayirac, parcalar), "Listenin tamamı panoya kopyalandı.");
    }

    private void PanoyaKoy(string metin, string basariMesaji)
    {
        try
        {
            Clipboard.SetText(metin);
            Duyur(basariMesaji);
        }
        catch (ExternalException)
        {
            Duyur("Pano şu anda kullanılamıyor, tekrar deneyin.");
        }
    }

    private static ListBox? MenuListesi(object sender) =>
        ((sender as MenuItem)?.Parent as ContextMenu)?.PlacementTarget as ListBox;

    private void ParagrafKopyala_Click(object sender, RoutedEventArgs e)
    {
        if (MenuListesi(sender) is { } liste) Kopyala(liste);
    }

    private void TumunuKopyala_Click(object sender, RoutedEventArgs e)
    {
        if (MenuListesi(sender) is { } liste) TumunuKopyala(liste);
    }
    private void KopyalaAna_Click(object sender, RoutedEventArgs e) => Kopyala(AnaListe);
    private void KopyalaDiger_Click(object sender, RoutedEventArgs e) => Kopyala(DigerListe);
    private void KopyalaHafta_Click(object sender, RoutedEventArgs e) => Kopyala(HaftaListesi);

    private void AnaSekme_Click(object sender, RoutedEventArgs e) => SekmeyeGec(AnaSekme);
    private void DigerSekme_Click(object sender, RoutedEventArgs e) => SekmeyeGec(DigerSekme);
    private void TakvimSekme_Click(object sender, RoutedEventArgs e) => SekmeyeGec(TakvimSekme);

    private void Yenile_Click(object sender, RoutedEventArgs e) => Yenile();

    private async void Yenile()
    {
        await GunuYukle(zorla: true);
        await IcerigiYukle(DateOnly.FromDateTime(_seciliTarih));
    }

    private void OnbellekAc_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_onbellek.Klasor}\"") { UseShellExecute = true });
    }

    // ---------- Şehir ve ayarlar ----------

    private bool SehirSecili => !string.IsNullOrWhiteSpace(_ayarlar.SehirKodu);

    internal bool ArkaPlandaIndir => _ayarlar.ArkaPlandaIndir;

    internal string SehirMetni => SehirSecili
        ? (string.IsNullOrWhiteSpace(_ayarlar.SehirAdi) ? (_bugun?.SehirAdi ?? "Kod " + _ayarlar.SehirKodu) : _ayarlar.SehirAdi)
        : "Henüz yer seçilmedi";

    internal long OnbellekBoyutu => _onbellek.Boyut();

    /// <summary>Diskte kayıtlı günlük içerik sayısı, yıla göre ("icerik-2026-10-23" adlarından). Yıl indir listesi için.</summary>
    internal Dictionary<int, int> IcerikGunSayilari() =>
        _onbellek.DosyaAdlari("icerik-")
            .Select(ad => int.TryParse(ad.AsSpan(7, 4), out int yil) ? yil : 0)
            .Where(yil => yil > 0)
            .GroupBy(yil => yil)
            .ToDictionary(g => g.Key, g => g.Count());

    private void SehirSec_Click(object sender, RoutedEventArgs e) => _ = SehirSecAsync(odaklaAna: true);

    private void AyarlarAc_Click(object sender, RoutedEventArgs e) => AyarlarAc();

    // Ayarlar menüsü: Genel ayarlar, Bildirim ayarları ve Çevrimdışı veri (Ctrl+Virgül genel ayarları açar).
    private void AyarlarGenel_Click(object sender, RoutedEventArgs e) => AyarlarAc();
    private void AyarlarCevrimdisi_Click(object sender, RoutedEventArgs e) => AyarlarAc(cevrimdisi: true);
    private void AyarlarBildirim_Click(object sender, RoutedEventArgs e)
    {
        new BildirimPenceresi(this, _ayarlar.Olcek) { Owner = this }.ShowDialog();
        AyarPenceresindenDon();
    }

    private void AyarlarAc(bool cevrimdisi = false)
    {
        var pencere = new AyarlarPenceresi(this, _ayarlar.Olcek, cevrimdisi) { Owner = this };
        pencere.ShowDialog();

        if (_ilkKurulumBekliyor)
        {
            _ilkKurulumBekliyor = false;
            IlkSehirSecimi();
        }
        else
        {
            AyarPenceresindenDon();
        }
    }

    /// <summary>
    /// Ayar pencereleri kapanınca odak, açık olan sekmenin içeriğine döner (Tab sırasında olmayan bir öğeye odak verilmez).
    /// </summary>
    private void AyarPenceresindenDon() =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, IlkOgeyeOdakla);

    /// <summary>Şehir seçme penceresini açar; seçilirse vakitler indirilmiş olarak yeni şehre geçilir.</summary>
    internal async Task<bool> SehirSecAsync(Window? sahip = null, bool odaklaAna = false)
    {
        var pencere = new SehirSecimPenceresi(_istemci, _vakitler, _ayarlar.Olcek) { Owner = sahip ?? this };
        if (pencere.ShowDialog() != true || pencere.Secilen is not { } sehir) return false;

        await SehriUygula(sehir);
        _imsakiyeAnahtar = "";
        _aylikAnahtar = "";
        if (ImsakiyeAktif) _ = ImsakiyeYukle(zorla: true);
        if (AylikAktif) _ = AylikYukle(zorla: true);

        // Ana pencereden seçildiyse odak sıradaki vakte gelir; Ayarlar penceresinden seçildiyse Ayarlar kendi odağını korur.
        if (odaklaAna)
        {
            Sekmeler.SelectedItem = AnaSekme;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => OdakVer(AnaListe));
        }
        return true;
    }

    private async Task SehriUygula(Sehir sehir)
    {
        // Önceki şehir de "son kullanılanlar"a girer; hemen silinmez, geri dönülünce anında açılır.
        var sonlar = _ayarlar.SonSehirler.ToList();
        if (SehirSecili && !sonlar.Contains(_ayarlar.SehirKodu)) sonlar.Add(_ayarlar.SehirKodu);

        _ayarlar.SehirKodu = sehir.Kod;
        _ayarlar.SehirAdi = sehir.GorunenAd;
        _ayarlar.SonSehirler = _vakitler.SehirleriDuzenle(sonlar, sehir.Kod);
        _ayarlar.SehirAdlari[sehir.Kod] = sehir.GorunenAd;
        foreach (string eski in _ayarlar.SehirAdlari.Keys.Where(k => !_ayarlar.SonSehirler.Contains(k)).ToList())
            _ayarlar.SehirAdlari.Remove(eski);
        AyarlariKaydet();
        SehirMenusunuGuncelle();

        // Eski şehrin verisi ekranda kalmasın; yeni şehir diskten (yeni indirilmişti) anında yüklenir.
        _bildirilen.Clear();
        _bugun = null;
        _yarinImsak = null;
        _siradakiSira = -2;
        _sonKalanMetin = "";
        _sehirFarki = null;
        await SehirFarkiniBelirle();
        await GunuYukle(zorla: false);

        Duyur($"{sehir.GorunenAd} seçildi, vakitler hazır.");
        _ = GunVakitleriniYukle(DateOnly.FromDateTime(_seciliTarih));
        IcerikIndirmesiniBaslat();
    }

    /// <summary>İlk açılış: şehir seçilene kadar vakit gösterilmez; pencere açılıp şehir seçtirilir.</summary>
    private void IlkSehirSecimi()
    {
        SehirYokDurumu();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, async () =>
        {
            if (!_ayarlar.KarsilamaGosterildi)
            {
                _ayarlar.KarsilamaGosterildi = true;
                AyarlariKaydet();
                new KarsilamaPenceresi(_ayarlar.Olcek, ilkAcilis: true) { Owner = this }.ShowDialog();
            }
            Duyur("Hoş geldiniz. Vakitleri görmek için önce bir yer seçin.");
            if (!await SehirSecAsync(odaklaAna: true)) KalanSure.Focus(); // yer seçilmedi bilgisi ve ne yapılacağı burada yazar
        });
    }

    private void SehirYokDurumu()
    {
        SehirBasligi.Text = "Yer seçilmedi";
        BaslikTarihiniYaz();
        KalanSure.Text = "Vakitleri görmek için bir yer seçin: Yer menüsünden Başka yer seç'i seçin (Ctrl+K).";
        AnaListe.ItemsSource = null;
        DigerListe.ItemsSource = null;
    }

    internal void ArkaPlanIndirmeAyarla(bool acik)
    {
        _ayarlar.ArkaPlandaIndir = acik;
        AyarlariKaydet();
        IcerikIndirmesiniBaslat();
    }

    /// <summary>
    /// Önbelleği temizler, ama önce seçili şehrin güncel vakitlerini indirmeyi dener: internet yoksa hiçbir şey silinmez
    /// (false döner), böylece kullanıcı vakitsiz kalmaz. Başarılıysa seçili şehrin vakitleri dışındaki her şey silinir
    /// (diğer şehirler, tüm günlük içerikler) ve bugünün içeriği yeniden indirilir. Ayarlar (şehir, tema, boyut) korunur.
    /// </summary>
    internal async Task<bool> OnbellegiTemizleAsync()
    {
        string kod = _ayarlar.SehirKodu;
        if (SehirSecili)
        {
            try
            {
                await _vakitler.YiliHazirlaAsync(kod, DateTime.Today.Year, zorla: true);
            }
            catch (VeriHatasi)
            {
                return false;
            }
        }

        _icerikIndirme?.Cancel();
        _onbellek.Sil(ad => !(SehirSecili &&
            (ad.StartsWith($"vakit-{kod}-", StringComparison.Ordinal) || ad == SehirZamanServisi.OnbellekAdi(kod))));
        _onbellek.AltKlasorleriSil(); // indirilmiş Bilgiler yazıları
        _ayarlar.SonSehirler = SehirSecili ? new List<string> { kod } : new List<string>();
        _ayarlar.SehirAdlari = SehirSecili ? _ayarlar.SehirAdlari.Where(k => k.Key == kod).ToDictionary(k => k.Key, k => k.Value) : new();
        AyarlariKaydet();
        SehirMenusunuGuncelle();

        if (SehirSecili)
        {
            _bugun = null;
            _siradakiSira = -2;
            await GunuYukle(zorla: false);
        }
        await IcerigiYukle(DateOnly.FromDateTime(_seciliTarih));
        IcerikIndirmesiniBaslat();
        return true;
    }
    private bool _ilkKurulumBekliyor;

    /// <summary>
    /// Ayarları ilk kurulumdaki hâle getirir (şehir, tema, yazı boyutu, arka plan indirme); isteğe bağlı olarak
    /// indirilmiş tüm verileri de siler. Ayarlar penceresi kapanınca şehir seçme ekranı açılır.
    /// </summary>
    internal void IlkKurulumaDon(bool onbellekSil)
    {
        _icerikIndirme?.Cancel();

        var varsayilan = new Ayarlar();
        _ayarlar.Tema = varsayilan.Tema;
        _ayarlar.Olcek = varsayilan.Olcek;
        _ayarlar.SehirKodu = varsayilan.SehirKodu;
        _ayarlar.SehirAdi = varsayilan.SehirAdi;
        _ayarlar.SonSehirler = new List<string>();
        _ayarlar.SehirAdlari = new Dictionary<string, string>();
        _ayarlar.ArkaPlandaIndir = varsayilan.ArkaPlandaIndir;
        AyarlariKaydet();
        SehirMenusunuGuncelle();

        if (onbellekSil) _onbellek.Sil(_ => true);

        TemaUygula(_ayarlar.Tema, kaydet: false);
        OlcekAyarla(_ayarlar.Olcek, kaydet: false);

        _bugun = null;
        _yarinImsak = null;
        _siradakiSira = -2;
        _sonKalanMetin = "";
        _hicriBugun = null;
        SehirYokDurumu();
        _ilkKurulumBekliyor = true;

        if (onbellekSil) _ = IcerigiYukle(DateOnly.FromDateTime(_seciliTarih));
    }

    /// <summary>
    /// Seçilen yılın tüm günlerinin içeriğini (ve seçili yerin o yılki vakitlerini) indirir. Çalışan arka plan indirmesi
    /// durdurulur, bitince (ayar açıksa) yeniden başlar. İlerleme her işlenen günde bildirilir.
    /// </summary>
    internal async Task<HazirlamaSonucu> YilIndirAsync(int yil, IProgress<(int Islenen, int Indirilen)> ilerleme, CancellationToken iptal)
    {
        _icerikIndirme?.Cancel();
        try
        {
            if (SehirSecili)
            {
                try
                {
                    await _vakitler.YiliHazirlaAsync(SehirKodu, yil, zorla: false, iptal);
                }
                catch (VeriHatasi)
                {
                    // O yıl için vakit yayınlanmamış olabilir; içerik yine de indirilir.
                }
            }

            var gunler = TarihAraligi(new DateOnly(yil, 1, 1), new DateOnly(yil, 12, 31));
            return await Task.Run(() => _icerik.GunleriHazirlaAsync(gunler, TimeSpan.FromMilliseconds(350), iptal, ilerleme));
        }
        finally
        {
            IcerikIndirmesiniBaslat();
        }
    }

    internal void OnbellekKlasoruAc() =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_onbellek.Klasor}\"") { UseShellExecute = true });

    private void AyarlariKaydet()
    {
        try
        {
            _ayarDeposu.Kaydet(_ayarlar);
        }
        catch (IOException)
        {
            // Kaydedilemese de bu oturumda ayar geçerli kalır.
        }
    }

    // ---------- Yazı boyutu (az görenler için) ----------

    private static readonly double[] Olcekler = { 1.0, 1.15, 1.3, 1.5, 1.75, 2.0 };
    private const double TemelGenislik = 660;
    private const double TemelYukseklik = 860;

    private void Buyut_Click(object sender, RoutedEventArgs e) => OlcekAdimi(+1);
    private void Kucult_Click(object sender, RoutedEventArgs e) => OlcekAdimi(-1);
    private void NormalBoyut_Click(object sender, RoutedEventArgs e) => OlcekAyarla(1.0, kaydet: true);

    private void Pencere_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        OlcekAdimi(e.Delta > 0 ? +1 : -1);
        e.Handled = true;
    }

    private void OlcekAdimi(int yon)
    {
        int simdiki = Array.FindIndex(Olcekler, o => Math.Abs(o - _ayarlar.Olcek) < 0.01);
        if (simdiki < 0) simdiki = 0;

        int yeni = Math.Clamp(simdiki + yon, 0, Olcekler.Length - 1);
        if (yeni == simdiki)
        {
            Duyur(yon > 0 ? "Yazı boyutu zaten en büyük." : "Yazı boyutu zaten en küçük.");
            return;
        }
        OlcekAyarla(Olcekler[yeni], kaydet: true);
    }

    /// <summary>Sayfanın tamamını (menü dışında), açılır takvimi ve menü yazılarını verilen çarpanla büyütür.</summary>
    private void OlcekAyarla(double olcek, bool kaydet)
    {
        olcek = Math.Clamp(olcek, 1.0, 2.0);
        _ayarlar.Olcek = olcek;

        Kok.LayoutTransform = new ScaleTransform(olcek, olcek);
        // Açılır pencere, içinde durduğu sayfanın büyütmesini zaten devralır. Uzun olduğu için toplam büyüklüğü
        // en çok %150'de tutulur (1080p ekrana sığsın): %150'den büyük boyutlarda içeriden küçültülür.
        double popupOrani = Math.Min(olcek, 1.5) / olcek;
        TakvimPopupKok.LayoutTransform = new ScaleTransform(popupOrani, popupOrani);
        Resources["MenuYaziBoyutu"] = 16.0 * olcek;
        if (olcek > 1.0) PenceryiSigdir(olcek);

        if (!kaydet) return;
        try
        {
            _ayarDeposu.Kaydet(_ayarlar);
        }
        catch (IOException)
        {
            // Ayar kaydedilemese de boyut bu oturumda geçerli kalır.
        }
        Duyur($"Yazı boyutu yüzde {Math.Round(olcek * 100)}.");
    }

    /// <summary>Büyütülen içerik sıkışmasın diye pencere ekrana sığdığı kadar genişler (hiç küçülmez).</summary>
    private void PenceryiSigdir(double olcek)
    {
        if (WindowState != WindowState.Normal) return;

        var alan = SystemParameters.WorkArea;
        Width = Math.Max(Width, Math.Min(TemelGenislik * olcek, alan.Width));
        Height = Math.Max(Height, Math.Min(TemelYukseklik * olcek, alan.Height));
        Left = Math.Max(alan.Left, Math.Min(double.IsNaN(Left) ? alan.Left : Left, alan.Right - Width));
        Top = Math.Max(alan.Top, Math.Min(double.IsNaN(Top) ? alan.Top : Top, alan.Bottom - Height));
    }

    // ---------- Tema ----------

    private void TemaSistem_Click(object sender, RoutedEventArgs e) => TemaUygula(Ayarlar.TemaSistem, kaydet: true);
    private void TemaAcik_Click(object sender, RoutedEventArgs e) => TemaUygula(Ayarlar.TemaAcik, kaydet: true);
    private void TemaKoyu_Click(object sender, RoutedEventArgs e) => TemaUygula(Ayarlar.TemaKoyu, kaydet: true);

    private void TemaUygula(string tema, bool kaydet)
    {
        Application.Current.ThemeMode = tema switch
        {
            Ayarlar.TemaAcik => ThemeMode.Light,
            Ayarlar.TemaKoyu => ThemeMode.Dark,
            _ => ThemeMode.System
        };
        ThemeMode = Application.Current.ThemeMode;

        TemaSistem.IsChecked = tema is not (Ayarlar.TemaAcik or Ayarlar.TemaKoyu);
        TemaAcik.IsChecked = tema == Ayarlar.TemaAcik;
        TemaKoyu.IsChecked = tema == Ayarlar.TemaKoyu;

        _ayarlar.Tema = tema;
        if (!kaydet) return;

        try
        {
            _ayarDeposu.Kaydet(_ayarlar);
            Duyur(tema switch
            {
                Ayarlar.TemaAcik => "Açık tema seçildi.",
                Ayarlar.TemaKoyu => "Koyu tema seçildi.",
                _ => "Sistem teması kullanılıyor."
            });
        }
        catch (IOException)
        {
            Duyur("Tema değişti ama ayar kaydedilemedi.");
        }
    }
    private void Karsilama_Click(object sender, RoutedEventArgs e)
    {
        new KarsilamaPenceresi(_ayarlar.Olcek, ilkAcilis: false) { Owner = this }.ShowDialog();
    }

    private void Hakkinda_Click(object sender, RoutedEventArgs e)
    {
        new HakkindaPenceresi(_ayarlar.Olcek, _onbellek.Klasor) { Owner = this }.ShowDialog();
    }

    /// <summary>Durum satırını günceller ve ekran okuyucuya (NVDA) haber verir.</summary>
    private void Duyur(string metin)
    {
        Durum.Text = metin;
        var peer = UIElementAutomationPeer.FromElement(Durum) ?? UIElementAutomationPeer.CreatePeerForElement(Durum);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}

/// <summary>Sınama için: her isteği "internet yok" hatasıyla bitirir.</summary>
internal sealed class InternetYokHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("İnternet yok (sınama modu).");
}