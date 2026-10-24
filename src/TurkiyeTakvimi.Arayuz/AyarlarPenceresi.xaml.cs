using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Şehir ve çevrimdışı veri ayarları. Değişiklikler hemen uygulanır ve kaydedilir.</summary>
public partial class AyarlarPenceresi : Window
{
    private readonly MainWindow _sahip;
    private readonly double _olcek;
    private CancellationTokenSource? _yilIndirme;

    /// <param name="cevrimdisi">Doğruysa pencere Çevrimdışı veri bölümüne odaklanarak açılır.</param>
    public AyarlarPenceresi(MainWindow sahip, double olcek, bool cevrimdisi = false)
    {
        _sahip = sahip;
        _olcek = olcek;
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height; // büyük yazı boyutunda pencere ekrana sığar, içerik kaydırılır

        if (olcek > 1.0)
        {
            Kok.LayoutTransform = new ScaleTransform(olcek, olcek);
            Width = Math.Min(Width * olcek, SystemParameters.WorkArea.Width);
        }

        // Menüden hangi konu seçildiyse yalnızca o görünür.
        string baslik = cevrimdisi ? "Çevrimdışı veri" : "Genel ayarlar";
        Title = baslik;
        BaslikYazisi.Text = baslik;
        YerKarti.Visibility = GenelKarti.Visibility = SifirlaDugmesi.Visibility = cevrimdisi ? Visibility.Collapsed : Visibility.Visible;
        CevrimdisiKarti.Visibility = cevrimdisi ? Visibility.Visible : Visibility.Collapsed;

        YilListesiniYenile(DateTime.Today.Year);
        Closed += (_, _) => _yilIndirme?.Cancel();

        ArkaPlanKutusu.IsChecked = sahip.ArkaPlandaIndir;
        BildirimAyarlariniYukle();
        Yenile();
        Loaded += (_, _) =>
        {
            if (cevrimdisi) ArkaPlanKutusu.Focus();
            else SehirKutusu.Focus();
        };
    }

    // ---------- Bildirimler ve açılış ----------

    private bool _bildirimYukleniyor = true; // kutular kurulurken Checked olayları ayar kaydetmesin

    private void BildirimAyarlariniYukle()
    {
        _bildirimYukleniyor = true;
        var a = _sahip.AyarlarOku;
        TepsiKutusu.IsChecked = a.TepsideCalis;
        BaslatKutusu.IsChecked = OtomatikBaslat.Acik();
        RamazanKutusu.IsChecked = a.RamazanBilgisi;
        EzaniKutusu.IsChecked = a.EzaniGoster;
        KisayolKutusu.IsChecked = a.KisayolAcik;
        _bildirimYukleniyor = false;
    }

    private void Tepsi_Click(object sender, RoutedEventArgs e)
    {
        if (_bildirimYukleniyor) return;
        bool acik = TepsiKutusu.IsChecked == true;
        _sahip.AyarDegistir(a => a.TepsideCalis = acik);
        Bildir(acik
            ? "Pencere kapatılınca uygulama bildirim alanında çalışmaya devam edecek."
            : "Pencere kapatılınca uygulama tamamen kapanacak; bildirimler gelmez.");
    }

    private void Baslat_Click(object sender, RoutedEventArgs e)
    {
        if (_bildirimYukleniyor) return;
        bool acik = BaslatKutusu.IsChecked == true;
        if (!OtomatikBaslat.Ayarla(acik))
        {
            _bildirimYukleniyor = true;
            BaslatKutusu.IsChecked = OtomatikBaslat.Acik();
            _bildirimYukleniyor = false;
            Bildir("Windows ile başlatma ayarı değiştirilemedi.");
            return;
        }
        Bildir(acik ? "Uygulama Windows ile birlikte başlayacak." : "Uygulama Windows ile birlikte başlamayacak.");
    }

    private void Kisayol_Click(object sender, RoutedEventArgs e)
    {
        if (_bildirimYukleniyor) return;
        bool acik = KisayolKutusu.IsChecked == true;
        _sahip.AyarDegistir(a => a.KisayolAcik = acik);
        if (acik && !_sahip.KisayoluUygula())
        {
            // Başka bir uygulama Win+Alt+V'yi kullanıyor: ayarı geri al ve bildir.
            _bildirimYukleniyor = true;
            KisayolKutusu.IsChecked = false;
            _bildirimYukleniyor = false;
            _sahip.AyarDegistir(a => a.KisayolAcik = false);
            Bildir("Win+Alt+V başka bir uygulama tarafından kullanılıyor, bu yüzden açılamadı.");
            return;
        }
        _sahip.KisayoluUygula();
        Bildir(acik
            ? "Win+Alt+V açıldı: uygulama arka plandayken bile sıradaki vakti söyler."
            : "Win+Alt+V kapatıldı.");
    }

    private void Ezani_Click(object sender, RoutedEventArgs e)
    {
        if (_bildirimYukleniyor) return;
        bool acik = EzaniKutusu.IsChecked == true;
        _sahip.AyarDegistir(a => a.EzaniGoster = acik);
        _sahip.EzaniAyariniUygula();
        Bildir(acik
            ? "Ezânî vakitler Diğer Vakitler listesinin sonuna eklendi."
            : "Ezânî vakitler listeden kaldırıldı.");
    }

    private void Ramazan_Click(object sender, RoutedEventArgs e)
    {
        if (_bildirimYukleniyor) return;
        bool acik = RamazanKutusu.IsChecked == true;
        _sahip.AyarDegistir(a => a.RamazanBilgisi = acik);
        Bildir(acik ? "Ramazan bilgisi açıldı." : "Ramazan bilgisi kapatıldı.");
    }

    private void Yenile()
    {
        SehirKutusu.Text = _sahip.SehirMetni;

        double mb = _sahip.OnbellekBoyutu / 1024.0 / 1024.0;
        OnbellekBilgisi.Text = $"Önbellek boyutu: {mb.ToString("0.0", CultureInfo.GetCultureInfo("tr-TR"))} MB";
    }

    private async void SehirDegistir_Click(object sender, RoutedEventArgs e)
    {
        if (await _sahip.SehirSecAsync(this)) Bildir("Yer değiştirildi: " + _sahip.SehirMetni);
        Yenile();
        SehirDegistirDugmesi.Focus();
    }

    private void ArkaPlan_Click(object sender, RoutedEventArgs e)
    {
        if (_bildirimYukleniyor) return;
        bool acik = ArkaPlanKutusu.IsChecked == true;
        _sahip.ArkaPlanIndirmeAyarla(acik);
        Bildir(acik
            ? "Arka planda indirme açıldı. Yılın içeriği internet varken yavaş yavaş indirilecek."
            : "Arka planda indirme kapatıldı. Yalnızca baktığınız günler kaydedilir.");
    }

    private async void Temizle_Click(object sender, RoutedEventArgs e)
    {
        var yanit = MessageBox.Show(this,
            "Önbellekteki diğer yerlerin vakitleri ve tüm günlük içerikler (söz, olay, arka yaprak) ile indirilmiş Bilgiler yazıları silinecek. Seçili yerin vakitleri yeniden indirilir, bunun için internet gerekir. Ayarlarınız (yer, tema, yazı boyutu) silinmez.\n\nDevam edilsin mi?",
            "Önbelleği temizle", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (yanit != MessageBoxResult.Yes) return;

        TemizleDugmesi.IsEnabled = false;
        Bildir("Önbellek temizleniyor…");
        bool temizlendi = await _sahip.OnbellegiTemizleAsync();
        TemizleDugmesi.IsEnabled = true;
        Yenile();
        Bildir(temizlendi
            ? "Önbellek temizlendi, seçili yerin vakitleri yeniden indirildi."
            : "İnternet bağlantısı yok, bu yüzden önbellek silinmedi. Verileriniz korundu; internet gelince yeniden deneyin.");
        TemizleDugmesi.Focus();
    }

    // ---------- Yıl indir ----------

    /// <summary>Listedeki her yıl kendi durumunu söyler: "2026, tamamen hazır", "2025, 43 / 365 gün hazır" ya da "2027, indirilmemiş".</summary>
    private sealed record YilOgesi(int Yil, int Hazir)
    {
        public int Toplam => DateTime.IsLeapYear(Yil) ? 366 : 365;
        public bool Tamam => Hazir >= Toplam;

        public override string ToString() =>
            Tamam ? $"{Yil}, tamamen hazır"
            : Hazir == 0 ? $"{Yil}, indirilmemiş"
            : $"{Yil}, {Hazir} / {Toplam} gün hazır";
    }

    /// <summary>1982'den (Türkiye Takvimi'nin ilk yılı) gelecek yıla kadar yılları, diskteki durumlarıyla listeler.</summary>
    private void YilListesiniYenile(int secilecekYil)
    {
        var sayilar = _sahip.IcerikGunSayilari();
        var ogeler = Enumerable.Range(1982, DateTime.Today.Year - 1982 + 2).Reverse()
            .Select(y => new YilOgesi(y, sayilar.GetValueOrDefault(y)))
            .ToList();

        YilKutusu.ItemsSource = ogeler;
        YilKutusu.SelectedItem = ogeler.FirstOrDefault(o => o.Yil == secilecekYil) ?? ogeler[0];
        YilDurumunuGuncelle();
    }

    private void YilKutusu_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        YilDurumunuGuncelle();

    /// <summary>Seçili yıl zaten tamamen hazırsa İndir düğmesi "geçersiz" olur: odaklanılır ama ekran okuyucuya kullanılamıyor denir.</summary>
    private void YilDurumunuGuncelle() =>
        YilIndirDugmesi.Gecersiz = YilKutusu.SelectedItem is YilOgesi { Tamam: true };

    private void YilIptal_Click(object sender, RoutedEventArgs e) => _yilIndirme?.Cancel();

    /// <summary>Seçilen yılın tüm günlerinin içeriğini ve seçili yerin o yılki vakitlerini indirir; ilerlemeyi gösterir.</summary>
    private async void YilIndir_Click(object sender, RoutedEventArgs e)
    {
        if (YilKutusu.SelectedItem is not YilOgesi oge)
        {
            Bildir("Önce indirilecek yılı seçin.");
            return;
        }

        int yil = oge.Yil;
        if (YilIndirDugmesi.Gecersiz)
        {
            Bildir($"{yil} yılı zaten tamamen hazır, yeniden indirmeye gerek yok.");
            return;
        }

        int toplam = DateTime.IsLeapYear(yil) ? 366 : 365;
        var iptal = _yilIndirme = new CancellationTokenSource();
        (int Islenen, int Indirilen) son = (0, 0);
        int duyurulan = 0;

        YilKilitle(true);
        YilCubuguAyarla(0);
        YilBilgisi.Text = $"{yil} yılı indiriliyor…";
        Bildir($"{yil} yılı indiriliyor. İptal etmek için İptal düğmesine basın.");

        bool bitti = false;
        var ilerleme = new SiraliIlerleme(Dispatcher, p =>
        {
            if (bitti) return; // geç gelen bildirim son mesajı ezmesin
            son = p;
            YilCubuguAyarla((double)p.Islenen / toplam);
            YilBilgisi.Text = $"{p.Islenen} / {toplam} gün işlendi, {p.Indirilen} gün yeni indirildi.";

            // Ekran okuyucuyu yormamak için yalnızca her yüzde 25'te duyurulur.
            int basamak = p.Islenen * 4 / toplam;
            if (basamak > duyurulan && basamak < 4)
            {
                duyurulan = basamak;
                Bildir($"Yüzde {basamak * 25}.");
            }
        });

        string mesaj;
        try
        {
            var sonuc = await _sahip.YilIndirAsync(yil, ilerleme, iptal.Token);
            mesaj = sonuc.Neden switch
            {
                DurmaNedeni.InternetYok =>
                    $"İnternet bağlantısı kesildi. {sonuc.Islenen} / {toplam} gün hazır. İnternet gelince yeniden deneyebilirsiniz.",
                DurmaNedeni.IcerikYok when sonuc.Indirilen == 0 =>
                    $"turktakvim.com {yil} yılı için henüz içerik yayınlamamış.",
                DurmaNedeni.IcerikYok =>
                    $"Sitede bu tarihten sonrası için içerik yok. {sonuc.Indirilen} gün indirildi.",
                _ when sonuc.Indirilen == 0 =>
                    $"{yil} yılı zaten tamamen indirilmişti, {sonuc.Islenen} günün hepsi çevrimdışı hazır.",
                _ =>
                    $"{yil} yılı hazır: {sonuc.Islenen} günün hepsi çevrimdışı kullanıma uygun, {sonuc.Indirilen} gün yeni indirildi."
            };
            YilBilgisi.Text = mesaj;
        }
        catch (OperationCanceledException)
        {
            mesaj = $"İndirme iptal edildi. {son.Islenen} / {toplam} gün işlendi, {son.Indirilen} gün kaydedildi.";
            YilBilgisi.Text = mesaj;
        }

        bitti = true;
        YilKilitle(false);
        Yenile();
        YilListesiniYenile(yil);
        Bildir(mesaj);
        YilIndirDugmesi.Focus();
    }

    /// <summary>İndirme sürerken ilgili kontroller kilitlenir; yalnızca İptal etkin kalır.</summary>
    private void YilKilitle(bool indiriliyor)
    {
        YilKutusu.IsEnabled = !indiriliyor;
        YilIndirDugmesi.IsEnabled = !indiriliyor;
        YilIptalDugmesi.IsEnabled = indiriliyor;
        SehirDegistirDugmesi.IsEnabled = !indiriliyor;
        TemizleDugmesi.IsEnabled = !indiriliyor;
        SifirlaDugmesi.IsEnabled = !indiriliyor;
        YilCubuguKabi.Visibility = indiriliyor ? Visibility.Visible : Visibility.Collapsed;
        if (indiriliyor) YilIptalDugmesi.Focus();
    }

    private void YilCubuguAyarla(double oran)
    {
        oran = Math.Clamp(oran, 0, 1);
        YilDolu.Width = new GridLength(Math.Max(oran, 0.001), GridUnitType.Star);
        YilBos.Width = new GridLength(Math.Max(1 - oran, 0.001), GridUnitType.Star);
    }

    private void Klasor_Click(object sender, RoutedEventArgs e) => _sahip.OnbellekKlasoruAc();

    private void Sifirla_Click(object sender, RoutedEventArgs e)
    {
        var yanit = MessageBox.Show(this,
            "Yer, tema ve yazı boyutu gibi tüm ayarlarınız sıfırlanacak ve uygulama ilk kurulum ekranıyla (yer seçme) açılacak.\n\n" +
            "İndirilmiş vakitler ve günlük içerikler de silinsin mi?\n\n" +
            "Evet: ayarlar sıfırlanır, indirilen veriler de silinir.\n" +
            "Hayır: ayarlar sıfırlanır, indirilen veriler kalır (yer seçince hızlı açılır).\n" +
            "İptal: hiçbir şey yapılmaz.",
            "İlk kurulum haline dön", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
        if (yanit == MessageBoxResult.Cancel) return;

        _sahip.IlkKurulumaDon(onbellekSil: yanit == MessageBoxResult.Yes);
        Close(); // ana pencere şehir seçme ekranını açar
    }

    private void Kapat_Click(object sender, RoutedEventArgs e) => Close();

    private void Bildir(string metin)
    {
        Durum.Text = metin;
        var peer = UIElementAutomationPeer.FromElement(Durum) ?? UIElementAutomationPeer.CreatePeerForElement(Durum);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}

/// <summary>
/// İlerleme bildirimlerini arayüz iş parçacığında, geliş sırasıyla çalıştırır. Standart Progress sınıfı bildirimi
/// kuyruğa atar; iş çok hızlı biterse bildirimler son mesajdan sonra çalışıp onu ezebilirdi.
/// </summary>
internal sealed class SiraliIlerleme : IProgress<(int Islenen, int Indirilen)>
{
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private readonly Action<(int Islenen, int Indirilen)> _islem;

    public SiraliIlerleme(System.Windows.Threading.Dispatcher dispatcher, Action<(int Islenen, int Indirilen)> islem)
    {
        _dispatcher = dispatcher;
        _islem = islem;
    }

    public void Report((int Islenen, int Indirilen) deger)
    {
        if (_dispatcher.CheckAccess()) _islem(deger);
        else _dispatcher.BeginInvoke(_islem, deger);
    }
}