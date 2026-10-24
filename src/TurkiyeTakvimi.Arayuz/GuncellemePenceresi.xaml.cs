using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>
/// Yardım > Güncellemeleri denetle. Pencere açılınca en son sürüme bakar (yalnızca kullanıcı istediği için internete bağlanır).
/// Kurulu sürümde yeni sürümü indirip kurar; taşınabilir sürümde sürüm sayfasını açar.
/// </summary>
public partial class GuncellemePenceresi : Window
{
    private readonly MainWindow _sahip;
    private readonly GuncellemeServisi _servis;
    private readonly string _kuruluSurum;
    private readonly CancellationTokenSource _iptal = new();
    private SurumBilgisi? _yeni;

    public GuncellemePenceresi(MainWindow sahip, double olcek, GuncellemeServisi servis)
    {
        _sahip = sahip;
        _servis = servis;
        _kuruluSurum = Environment.GetEnvironmentVariable("TT_SURUM") is { Length: > 0 } sanal ? sanal : VeriYolu.UygulamaSurumu; // TT_SURUM: sınama için kurulu sürümü taklit eder
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;

        if (olcek > 1.0)
        {
            Kok.LayoutTransform = new ScaleTransform(olcek, olcek);
            Width = Math.Min(Width * olcek, SystemParameters.WorkArea.Width);
        }

        Metin.Text = $"Kurulu sürüm: {_kuruluSurum}";
        AltBaslik.Text = "Denetleniyor…";
        Loaded += async (_, _) =>
        {
            Metin.Focus();
            await DenetleAsync();
        };
        Closed += (_, _) => _iptal.Cancel();
    }

    private async Task DenetleAsync()
    {
        try
        {
            var surum = await _servis.SonSurumuAlAsync(_kuruluSurum, _iptal.Token);
            var kurulu = Version.TryParse(_kuruluSurum, out var v) ? v : new Version(0, 0);

            if (!GuncellemeServisi.YeniMi(kurulu, surum.Surum))
            {
                Bildir("Güncelsiniz.", $"Kurulu sürüm: {_kuruluSurum}\nEn son sürüm: {VeriYolu.SurumMetni(surum.Surum)}");
                return;
            }

            _yeni = surum;
            string yeniMetin = VeriYolu.SurumMetni(surum.Surum);
            string baslik = $"Yeni sürüm var: {yeniMetin}.";
            var satirlar = new List<string> { $"Kurulu sürüm: {_kuruluSurum}", $"Yeni sürüm: {yeniMetin}" };
            if (surum.Notlar.Length > 0) satirlar.Add(surum.Notlar);

            if (VeriYolu.Tasinabilir)
            {
                satirlar.Add("Taşınabilir sürümü kullanıyorsunuz: sürüm sayfasından yeni zip dosyasını indirip klasörün üzerine açın. Ayarlarınız Veri klasöründe kalır.");
            }
            else if (surum.KurulumAdresi is not null && surum.KurulumOzeti is not null)
            {
                satirlar.Add("İndir ve kur düğmesi yeni sürümü indirir, doğrular ve kurulumu başlatır; uygulama bu sırada kapanır.");
                IndirDugmesi.Visibility = Visibility.Visible;
            }
            SayfaDugmesi.Visibility = Visibility.Visible;
            Bildir(baslik, string.Join("\n", satirlar));
        }
        catch (OperationCanceledException)
        {
        }
        catch (VeriHatasi ex)
        {
            HataKaydi.Yaz("Güncelleme denetimi", ex);
            Bildir("Güncellemeler denetlenemedi.", $"Kurulu sürüm: {_kuruluSurum}\nİnternet bağlantınızı denetleyip yeniden deneyin.");
            SayfaDugmesi.Visibility = Visibility.Visible;
        }
    }

    private async void Indir_Click(object sender, RoutedEventArgs e)
    {
        if (_yeni is null) return;
        IndirDugmesi.IsEnabled = false;
        SayfaDugmesi.IsEnabled = false;
        Duyur("İndiriliyor…");

        try
        {
            int sonYuzde = 0;
            var ilerleme = new Progress<double>(oran =>
            {
                int yuzde = (int)(oran * 100) / 25 * 25; // yüzde 25'lik adımlarla; ekran okuyucu sürekli konuşmasın
                if (yuzde > sonYuzde && yuzde < 100)
                {
                    sonYuzde = yuzde;
                    Duyur($"İndiriliyor, yüzde {yuzde}.");
                }
            });
            string yol = await _servis.KurulumuIndirAsync(_yeni, ilerleme, _iptal.Token);
            Duyur("Kurulum başlatılıyor. Uygulama kapanacak.");
            await Task.Delay(1200);
            _sahip.GuncellemeKurulumunuBaslat(yol);
        }
        catch (OperationCanceledException)
        {
        }
        catch (VeriHatasi ex)
        {
            HataKaydi.Yaz("Güncelleme indirme", ex);
            Duyur("Güncelleme indirilemedi ya da doğrulanamadı. Sürüm sayfasından elle indirebilirsiniz.");
            IndirDugmesi.IsEnabled = true;
            SayfaDugmesi.IsEnabled = true;
        }
    }

    private void Sayfa_Click(object sender, RoutedEventArgs e)
    {
        string adres = _yeni?.SayfaAdresi ?? GuncellemeServisi.SurumSayfasi;
        if (!Uri.TryCreate(adres, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps || u.Host != "github.com")
            adres = GuncellemeServisi.SurumSayfasi;
        Process.Start(new ProcessStartInfo(adres) { UseShellExecute = true });
    }

    private void Kapat_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Başlıktaki kısa sonucu (ekran okuyucuya duyurulur) ve ayrıntı metnini yazar.</summary>
    private void Bildir(string baslik, string ayrinti)
    {
        Metin.Text = ayrinti;
        Duyur(baslik);
    }

    private void Duyur(string metin)
    {
        AltBaslik.Text = metin;
        var peer = UIElementAutomationPeer.FromElement(AltBaslik) ?? UIElementAutomationPeer.CreatePeerForElement(AltBaslik);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
