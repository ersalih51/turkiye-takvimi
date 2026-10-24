using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Bildirimler, bildirim alanı (tepsi), Ramazan bilgisi, haftalık görünüm, kıble yönü ve uyku sonrası yenileme.</summary>
public partial class MainWindow
{
    private BildirimMerkezi? _bildirim;
    private const int KibleSaatiNo = 17;
    private bool _cikiyor;
    private bool _gizliBaslat;
    private readonly HashSet<string> _bildirilen = new();
    private string _kibleMetni = "";
    private string _kibleKisa = "";
    private string _pusulaMetni = "";
    private List<EzaniSatiri> _ezani = new();
    private DateOnly _ezaniGun;
    private string _ezaniSehir = "";

    // Sınama için: TT_SANAL_ZAMAN="2026-03-01 17:30" şehrin saatini o ana alır ve oradan akıtır.
    private readonly DateTime? _sanalHedef = Environment.GetEnvironmentVariable("TT_SANAL_ZAMAN") is { } s &&
        DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    private readonly DateTime _sanalBasi = DateTime.UtcNow;

    // Site hicrî ayı "Ramezân" yazıyor; "Ramazan" yazımına da dayanıklı olsun.
    private bool RamazanMi => _ayarlar.RamazanBilgisi && _hicriBugun is { } h &&
        (h.Contains("Ramez", StringComparison.OrdinalIgnoreCase) || h.Contains("Ramaz", StringComparison.OrdinalIgnoreCase));

    // ---------- Başlatma ve kapatma ----------

    private void OzellikleriKur()
    {
        _bildirim = new BildirimMerkezi();
        _bildirim.AcIstendi += PenceriyiGoster;
        _bildirim.AyarlarIstendi += () => { PenceriyiGoster(); AyarlarAc_Click(this, new RoutedEventArgs()); };
        _bildirim.CikisIstendi += Cik;
        _bildirim.SiradakiIstendi += SiradakiVakitiSoyle;

        SystemEvents.PowerModeChanged += UykudanDonunce;
        Closed += (_, _) =>
        {
            SystemEvents.PowerModeChanged -= UykudanDonunce;
            _bildirim?.Dispose();
        };
    }

    /// <summary>Otomatik başlatmada pencere hiç görünmeden (yalnızca bildirim alanı simgesiyle) açılır.</summary>
    private void GizliBaslangicHazirla()
    {
        _gizliBaslat = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -32000;
        Top = -32000;
    }

    private void GizliBaslangiciBitir()
    {
        if (!_gizliBaslat) return;
        Hide();
        // Pencere sonradan gösterilecekse ekranın ortasında açılır.
        Left = Math.Max(0, (SystemParameters.WorkArea.Width - Width) / 2);
        Top = Math.Max(0, (SystemParameters.WorkArea.Height - Height) / 2);
        ShowInTaskbar = true;
        ShowActivated = true;
        _gizliBaslat = false;
    }

    public void PenceriyiGoster()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true; // öndeki pencereleri aşmak için kısa süre; hemen kapatılır
        Topmost = false;
        Focus();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_cikiyor && _ayarlar.TepsideCalis && SehirSecili)
        {
            e.Cancel = true;
            Hide();
            if (!_ayarlar.TepsiBilgisiGosterildi)
            {
                _ayarlar.TepsiBilgisiGosterildi = true;
                AyarlariKaydet();
                _bildirim?.Goster("Türkiye Takvimi çalışmaya devam ediyor",
                    "Vakit bildirimleri için uygulama arka planda açık kalır. Tamamen kapatmak için bildirim alanındaki simgeye sağ tıklayıp Çıkış'ı seçin veya uygulamada Ctrl+Q tuşlarına basın.",
                    ses: false);
            }
            return;
        }
        base.OnClosing(e);
    }

    private void Cik()
    {
        _cikiyor = true;
        Application.Current.Shutdown();
    }

    private void Cikis_Click(object sender, RoutedEventArgs e) => Cik();

    private void UykudanDonunce(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume) return;
        // Bilgisayar uykudayken gün ya da yaz saati değişmiş olabilir.
        _ = Dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5)); // ağ bağlantısının dönmesine zaman tanı
            _ = SehirFarkiniTazele();
            Tik();
        });
    }

    // ---------- Her yerden çalışan kısayol: Win+Alt+V ----------
    // Ctrl+Alt+harf seçilmedi: Türkçe klavyede AltGr (= Ctrl+Alt) ile @, €, ₺ gibi karakterler yazıldığı için
    // böyle bir kısayol başka uygulamalarda yazmayı bozardı.

    private const int WM_HOTKEY = 0x0312;
    private const int KisayolNo = 0x7451;
    private const uint MOD_ALT = 0x1, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;
    private const uint VK_V = 0x56;
    private bool _kisayolKayitli;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Ayara göre kısayolu kaydeder ya da kaldırır. Kısayol başka bir uygulamada kullanılıyorsa false döner.</summary>
    internal bool KisayoluUygula()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (_ayarlar.KisayolAcik && !_kisayolKayitli)
            _kisayolKayitli = RegisterHotKey(hwnd, KisayolNo, MOD_WIN | MOD_ALT | MOD_NOREPEAT, VK_V);
        else if (!_ayarlar.KisayolAcik)
            KisayoluKaldir();
        return !_ayarlar.KisayolAcik || _kisayolKayitli;
    }

    private void KisayoluKaldir()
    {
        if (!_kisayolKayitli) return;
        UnregisterHotKey(new System.Windows.Interop.WindowInteropHelper(this).Handle, KisayolNo);
        _kisayolKayitli = false;
    }

    /// <summary>Sıradaki vakti ve kalan süreyi Windows bildirimi olarak gösterir (NVDA bildirimleri okur); pencere açık değilse de çalışır.</summary>
    private void SiradakiVakitiSoyle()
    {
        string metin = !SehirSecili ? "Henüz yer seçilmedi. Uygulamayı açıp bir yer seçin."
            : string.IsNullOrWhiteSpace(KalanSure.Text) ? "Vakitler henüz yüklenmedi."
            : KalanSure.Text;

        _bildirim?.Goster(SehirSecili ? $"Türkiye Takvimi, {SehirMetni}" : "Türkiye Takvimi", metin, ses: false);
        Duyur(metin);
    }

    // ---------- Ayarlar penceresi için ----------

    internal Ayarlar AyarlarOku => _ayarlar;

    /// <summary>Bir ayarı değiştirir, kaydeder ve ekranı yeni ayara göre yeniler.</summary>
    internal void AyarDegistir(Action<Ayarlar> degistir)
    {
        degistir(_ayarlar);
        AyarlariKaydet();
        _sonKalanMetin = "";
        ListeleriDoldur(odakla: false);
        Tik();
    }

    // ---------- Ramazan ----------

    /// <summary>Ramazan'da İmsak "sahur sonu", Akşam "iftar" olarak da adlandırılır.</summary>
    private string GosterAd(int vakit)
    {
        string ad = VakitAdlari.Hepsi[vakit];
        if (!RamazanMi) return ad;
        return vakit switch { 10 => "Akşam, iftar", 0 => "İmsak, sahur sonu", _ => ad };
    }

    private string KalanEtiketi(int vakit) =>
        !RamazanMi ? "Kalan süre"
        : vakit == 10 ? "İftara kalan süre"
        : vakit == 0 ? "Sahurun bitmesine kalan süre"
        : "Kalan süre";

    // ---------- Bildirimler ----------

    private bool SessizSaatteMi(DateTime simdi)
    {
        if (!_ayarlar.SessizSaatler) return false;
        if (!TimeSpan.TryParse(_ayarlar.SessizBaslangic, CultureInfo.InvariantCulture, out var bas) ||
            !TimeSpan.TryParse(_ayarlar.SessizBitis, CultureInfo.InvariantCulture, out var bit) || bas == bit)
            return false;

        var an = simdi.TimeOfDay;
        return bas < bit ? an >= bas && an < bit : an >= bas || an < bit; // gece yarısını aşan aralık
    }

    /// <summary>Her saniye çağrılır: vakit girince ve (isteniyorsa) vakitten önce bildirim verir. Her vakit bir kez bildirilir.</summary>
    private void BildirimleriDenetle(DateTime simdi)
    {
        if (_ayarlar.BildirimVakitleri.Count == 0 || _bugun is null || _bildirim is null) return;
        if (SessizSaatteMi(simdi)) return;

        var pencere = TimeSpan.FromMinutes(2); // uykudan çok geç dönüldüyse eski vakitler bildirilmez
        foreach (int v in _ayarlar.BildirimVakitleri)
        {
            if (v < 0 || v >= VakitAdlari.Hepsi.Length || VakitZamani(simdi, v) is not { } zaman) continue;

            if (simdi >= zaman && simdi - zaman < pencere && _bildirilen.Add($"{_yuklenenGun:yyyyMMdd}-{v}-giris"))
            {
                BildirimGoster(GirisBasligi(v), $"{SehirMetni}, {VakitSaati.Ayristir(_bugun.Gun.Saatler[v]).Okunan}.");
                return;
            }

            int once = _ayarlar.OncedenDakika;
            if (once <= 0) continue;
            var hatirlatma = zaman - TimeSpan.FromMinutes(once);
            if (simdi >= hatirlatma && simdi < zaman && simdi - hatirlatma < pencere &&
                _bildirilen.Add($"{_yuklenenGun:yyyyMMdd}-{v}-once"))
            {
                BildirimGoster(OnceBasligi(v, (int)Math.Ceiling((zaman - simdi).TotalMinutes)),
                    $"{SehirMetni}, {VakitSaati.Ayristir(_bugun.Gun.Saatler[v]).Okunan}.");
                return;
            }
        }
    }

    /// <summary>
    /// Mübarek gün ve gece bildirimi: bir gün önce akşam (20:00'den sonra) "Yarın …", o gün sabah (08:00'den sonra) "Bugün …"
    /// haberi. Her biri bir kez verilir; sessiz saatlerde verilmez.
    /// </summary>
    private void MubarekBildirimleriniDenetle(DateTime simdi)
    {
        if (!_ayarlar.MubarekGunBildirimi || _bildirim is null || SessizSaatteMi(simdi)) return;

        var bugun = DateOnly.FromDateTime(simdi);
        var yarin = bugun.AddDays(1);

        foreach (var g in MubarekGunlerBellekte(bugun.Year).Concat(MubarekGunlerBellekte(yarin.Year)).Distinct())
        {
            bool gece = g.Tur == MubarekTur.Gece;
            string selam = g.Kandil ? "Kandiliniz mübarek olsun."
                : g.Bayram ? "Bayramınız mübarek olsun."
                : g.Ad.StartsWith("Ramazan'ın", StringComparison.Ordinal) ? "Hayırlı Ramazanlar."
                : "";

            if (g.Tarih == yarin && simdi.Hour >= 20 && _bildirilen.Add($"{bugun:yyyyMMdd}-mubarek-yarin-{g.Ad}"))
            {
                BildirimGoster(gece ? $"Yarın akşam {g.Ad}" : $"Yarın {g.Ad}", $"{g.HicriTarih}.");
                return;
            }
            if (g.Tarih == bugun && simdi.Hour >= 8 && _bildirilen.Add($"{bugun:yyyyMMdd}-mubarek-bugun-{g.Ad}"))
            {
                BildirimGoster(gece ? $"Bu akşam {g.Ad}" : $"Bugün {g.Ad}", $"{g.HicriTarih}. {selam}".Trim());
                return;
            }
        }
    }

    private string GirisBasligi(int v) => RamazanMi && v == 10 ? "İftar vakti girdi"
        : RamazanMi && v == 0 ? "İmsak vakti girdi, sahur sona erdi"
        : v == KibleSaatiNo ? "Kıble saati geldi" : $"{VakitAdlari.Hepsi[v]} vakti girdi";

    private string OnceBasligi(int v, int dakika) => RamazanMi && v == 10 ? $"İftara {dakika} dakika kaldı"
        : RamazanMi && v == 0 ? $"Sahurun bitmesine {dakika} dakika kaldı"
        : v == KibleSaatiNo ? $"Kıble saatine {dakika} dakika kaldı" : $"{VakitAdlari.Hepsi[v]} vaktine {dakika} dakika kaldı";

    private void BildirimGoster(string baslik, string metin)
    {
        _bildirim?.Goster(baslik, metin, _ayarlar.BildirimSesi, _ayarlar.BildirimSesAdi);
        Duyur($"{baslik}. {metin}");
    }

    /// <summary>Bildirim alanı simgesinin üstüne gelince ve menüde görünen sıradaki vakit bilgisi.</summary>
    private void TepsiMetniniGuncelle(int siradaki, TimeSpan? kalan)
    {
        if (_bildirim is null) return;
        if (siradaki < 0 || kalan is null)
        {
            _bildirim.SiradakiniYaz("Türkiye Takvimi");
            return;
        }

        var k = kalan.Value < TimeSpan.Zero ? TimeSpan.Zero : kalan.Value;
        string sure = (int)k.TotalHours > 0 ? $"{(int)k.TotalHours} sa {k.Minutes} dk" : $"{k.Minutes} dk";
        int v = VakitAdlari.Ana[siradaki];
        _bildirim.SiradakiniYaz($"{GosterAd(v)}, {VakitSaati.Ayristir(_bugun!.Gun.Saatler[v]).Gorunen}, {sure} kaldı");
    }

    // ---------- Kıble yönü ----------

    private void KibleyiGuncelle()
    {
        string eski = _kibleMetni + _pusulaMetni;
        KibleyiHesapla();
        if (eski != _kibleMetni + _pusulaMetni) TarihKutusunuYenile();
    }

    private void KibleyiHesapla()
    {
        if (SehirSecili && _sehirZaman.KayitliKonum(SehirKodu) is { } k)
        {
            double yon = KibleHesabi.Yon(k.Enlem, k.Boylam);
            _kibleMetni = KibleHesabi.Metin(k.Enlem, k.Boylam);
            _kibleKisa = $"{Math.Round(yon):0}° {KibleHesabi.YonAdi(yon)}";
            _pusulaMetni = KibleHesabi.PusulaMetni(k.Enlem, k.Boylam, DateTime.Today);
        }
        else
        {
            _kibleMetni = _kibleKisa = _pusulaMetni = "";
        }
    }

    // ---------- Haftalık görünüm ----------

    private int _haftaSurumu;

    /// <summary>Bugünden başlayarak 7 günün ana vakitlerini listeler (yıllık önbellekten).</summary>
    private async Task HaftayiYukle()
    {
        if (!SehirSecili) return;
        int surum = ++_haftaSurumu;
        string kod = SehirKodu;
        var bugun = DateOnly.FromDateTime(SehirSimdi);
        var satirlar = new List<GunSatiri>();

        for (int i = 0; i < 7; i++)
        {
            var gun = bugun.AddDays(i);
            try
            {
                var sonuc = await _vakitler.GunuGetirAsync(kod, gun);
                satirlar.Add(new GunSatiri(gun, i == 0,
                    VakitAdlari.Ana.Select(v => (VakitAdlari.Hepsi[v], sonuc.Gun.Saatler[v])).ToList()));
            }
            catch (VeriHatasi)
            {
                break; // yıl sonu: gelecek yılın verisi henüz yoksa kalan günler gösterilmez
            }
        }

        if (surum != _haftaSurumu || kod != SehirKodu) return;

        int eski = HaftaListesi.SelectedIndex;
        HaftaListesi.ItemsSource = satirlar;
        HaftaListesi.SelectedIndex = satirlar.Count == 0 ? -1 : Math.Clamp(eski, 0, satirlar.Count - 1);
        HaftaBilgisi.Visibility = satirlar.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- Ezânî (alaturka) vakitler ----------

    private List<EzaniSatiri> EzaniSatirlari => _ayarlar.EzaniGoster ? _ezani : new List<EzaniSatiri>();

    /// <summary>Ayar değişince: açıldıysa bugünün Ezânî vakitlerini getirir, kapandıysa listeden kaldırır.</summary>
    internal void EzaniAyariniUygula()
    {
        if (_ayarlar.EzaniGoster) _ = EzaniYukle();
        else ListeleriDoldur(odakla: false);
    }

    /// <summary>Bugünün Ezânî vakitlerini önce diskten, yoksa internetten alır; gelince Diğer Vakitler listesine eklenir.</summary>
    private async Task EzaniYukle()
    {
        if (!SehirSecili || !_ayarlar.EzaniGoster) return;
        string kod = SehirKodu;
        var gun = _yuklenenGun;

        List<EzaniSatiri>? liste = _ezaniServisi.Kayitli(kod, gun);
        if (liste is null)
        {
            try
            {
                liste = await _ezaniServisi.GetirAsync(kod, gun);
            }
            catch (VeriHatasi)
            {
                return; // internet yoksa Ezânî satırları görünmez; vasatî vakitler etkilenmez
            }
        }

        if (kod != SehirKodu || gun != _yuklenenGun) return;
        _ezani = liste;
        _ezaniGun = gun;
        _ezaniSehir = kod;
        ListeleriDoldur(odakla: false);
    }}
