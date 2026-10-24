using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Uygulamanın adı, sürümü, veri kaynağı, gizlilik bilgisi ve kısayolları.</summary>
public partial class HakkindaPenceresi : Window
{
    private const string SiteAdresi = "https://www.turktakvim.com";
    private readonly string _veriKlasoru;

    public HakkindaPenceresi(double olcek, string veriKlasoru)
    {
        _veriKlasoru = veriKlasoru;
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;

        if (olcek > 1.0)
        {
            Kok.LayoutTransform = new ScaleTransform(olcek, olcek);
            Width = Math.Min(Width * olcek, SystemParameters.WorkArea.Width);
        }

        // Simgenin en büyük boyutu kullanılır (varsayılan çerçeve 16 piksel olduğundan bulanık görünürdü).
        var kaynak = Application.GetResourceStream(new Uri("pack://application:,,,/Varliklar/uygulama.ico"));
        if (kaynak is not null)
        {
            var kod = new IconBitmapDecoder(kaynak.Stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Simge.Source = kod.Frames.OrderByDescending(c => c.PixelWidth).First();
        }

        string surum = VeriYolu.SurumMetni(Assembly.GetExecutingAssembly().GetName().Version);
        SurumYazisi.Text = VeriYolu.Tasinabilir ? $"Sürüm {surum}  ·  taşınabilir" : $"Sürüm {surum}";

        var paragraflar = new List<string>
        {
            $"Türkiye Takvimi, sürüm {surum}. Namaz vakitlerini, günün tarihini, sözünü ve arka yaprağını, haftalık ve aylık çizelgeleri, imsakiyeyi ve mübarek gün ve geceleri bir arada gösteren, ücretsiz ve açık kaynaklı bir masaüstü uygulamasıdır.",
            "Geliştiren: Bu uygulama Salih Eroğul tarafından, herkesin faydalanması amacıyla geliştirilmiştir.",
            "Erişilebilirlik: Uygulama erişilebilirlik ölçütleri dikkate alınarak tasarlanmıştır. Görme engelli kullanıcılar da ekran okuyucu programlarla (NVDA ile denenmiştir) uygulamayı erişilebilir bir şekilde kullanabilir. Tüm bölümler yalnızca klavyeyle kullanılabilir; yazı boyutu büyütülebilir, açık ve koyu tema seçilebilir.",
            "Veri kaynağı: Namaz vakitleri, günün sözü, günün tarihi (tarihte bugün), arka yaprak, hicrî tarihler ve imsakiye bilgileri turktakvim.com sitesinden alınır. Bu uygulama sitenin sahipleri tarafından hazırlanmış ya da onaylanmış resmî bir uygulama değildir.",
            "Vakitler: Siteye uygun olarak Vasatî vakitler gösterilir. İsterseniz Ayarlar'dan Ezânî (alaturka) vakitler de eklenir.",
            "Pusula: Pusulayla kıble açısı, NOAA'nın Dünya Manyetik Modeli (WMM2025) ile hesaplanır. Doğruluğu yaklaşık yarım derecedir; model 2030'a kadar geçerlidir, sonrasında uygulama güncellenmelidir.",
            "Gizlilik: Bilgileriniz yalnızca bu bilgisayarda saklanır. Yer seçerken \"Konumumu tahmin et\"e siz basarsanız IP adresiniz konum tahmini için ipwho.is hizmetine gönderilir; başka hiçbir kişisel bilgi gönderilmez.",
            "Güncelleme ve geri bildirim: Uygulama kendiliğinden güncelleme aramaz. Yardım menüsünden Güncellemeleri denetle'yi seçerseniz en son sürüm bilgisi için github.com'a bağlanılır. Geri bildirim gönder, GitHub'daki proje sayfasında (github.com/ersalih51/turkiye-takvimi) herkese açık bir kayıt açar; sürüm ve Windows bilgisi dışında kişisel bilgi eklenmez.",
            "Kullanım: Uygulamanın nasıl kullanılacağı Yardım menüsündeki Kullanım Kılavuzu'nda (F1), tüm kısayol tuşları Klavye Kısayolları'nda (Ctrl+F1) anlatılır.",
            "Lisans: Uygulamanın kodu MIT lisansıyla yayımlanır; yani serbestçe kullanılabilir, kopyalanabilir ve değiştirilebilir. turktakvim.com'dan gelen veriler ve yazılar sitenin ve yazarlarının hakkıdır, bu lisansa dahil değildir.",
            "Sesler: Dalga, Kristal ve Damla bu uygulama için üretilmiştir. Bülbül sesleri, Wikimedia Commons'ta kamu malı (CC0) olarak paylaşılan, Digweed1'in bülbül kaydından kesilmiştir.",
            "Hata kaydı: Beklenmeyen bir hata olursa bu bilgisayarda küçük bir kayıt dosyasına yazılır; hiçbir yere gönderilmez. Kaydı Hata kaydını aç düğmesiyle görebilirsiniz.",
            "Bilgiler menüsündeki yazılar turktakvim.com sitesinden alınır. Bir yazı ilk açıldığında indirilir ve küçük bir önbellekte saklanır; sonra internetsiz de okunur. Önbellek bir sınırı aşarsa en eski yazılar kendiliğinden silinir.",
            "Verileriniz bu bilgisayarda saklanır; klasörü açmak için Veri klasörünü aç düğmesini kullanabilirsiniz."
        };
        Bilgiler.Text = string.Join("\n", paragraflar);

        Loaded += (_, _) => Bilgiler.Focus();
    }

    private void Klasor_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_veriKlasoru);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_veriKlasoru}\"") { UseShellExecute = true });
            Bildir("Veri klasörü açıldı.");
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            Bildir("Veri klasörü açılamadı.");
        }
    }

    private void Hata_Click(object sender, RoutedEventArgs e)
    {
        if (!HataKaydi.Var())
        {
            Bildir("Hata kaydı yok: şimdiye kadar beklenmeyen bir hata oluşmadı.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{HataKaydi.Dosya}\"") { UseShellExecute = true });
            Bildir("Hata kaydı açıldı.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Bildir("Hata kaydı açılamadı.");
        }
    }

    private void Site_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(SiteAdresi) { UseShellExecute = true });
            Bildir("turktakvim.com tarayıcıda açıldı.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Bildir("Tarayıcı açılamadı.");
        }
    }

    private void Kapat_Click(object sender, RoutedEventArgs e) => Close();

    private void Bildir(string metin)
    {
        Durum.Text = metin;
        var peer = UIElementAutomationPeer.FromElement(Durum) ?? UIElementAutomationPeer.CreatePeerForElement(Durum);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
