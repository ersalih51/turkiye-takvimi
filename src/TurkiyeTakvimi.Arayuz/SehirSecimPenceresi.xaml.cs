using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>
/// Şehir, ilçe, kasaba ya da köy aranıp seçilir. "Seç"e basılınca yerin bir yıllık vakitleri hemen indirilir
/// (yaklaşık 2 saniye); başarılı olursa pencere kapanır ve <see cref="Secilen"/> doludur.
/// </summary>
public partial class SehirSecimPenceresi : Window
{
    private const int EnFazlaSonuc = 200;

    private readonly TurktakvimIstemci _istemci;
    private readonly VakitServisi _vakitler;
    private CancellationTokenSource? _arama;
    private CancellationTokenSource? _indirme;
    private bool _mesgul;

    public Sehir? Secilen { get; private set; }

    public SehirSecimPenceresi(TurktakvimIstemci istemci, VakitServisi vakitler, double olcek, string? baslangicMetni = null)
    {
        _istemci = istemci;
        _vakitler = vakitler;
        InitializeComponent();

        if (olcek > 1.0)
        {
            Kok.LayoutTransform = new ScaleTransform(olcek, olcek);
            var alan = SystemParameters.WorkArea;
            Width = Math.Min(Width * olcek, alan.Width);
            Height = Math.Min(Height * olcek, alan.Height);
        }

        if (!string.IsNullOrWhiteSpace(baslangicMetni)) AramaKutusu.Text = baslangicMetni;
        Loaded += (_, _) =>
        {
            AramaKutusu.Focus();
            AramaKutusu.SelectAll();
        };
        Closed += (_, _) =>
        {
            _arama?.Cancel();
            _indirme?.Cancel();
        };
    }

    private sealed record Oge(Sehir Sehir)
    {
        public string Ad => Sehir.GorunenAd;
        public string Ulke => Sehir.Ulke;
        public override string ToString() => $"{Sehir.GorunenAd}, {Sehir.Ulke}";
    }

    // ---------- Arama ----------

    private void Ara_Click(object sender, RoutedEventArgs e) => _ = AraAsync(AramaKutusu.Text);

    private void AramaKutusu_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        _ = AraAsync(AramaKutusu.Text);
    }

    private async Task<bool> AraAsync(string metin)
    {
        if (_mesgul) return false;
        if (string.IsNullOrWhiteSpace(metin))
        {
            Bildir("Aramak için bir yer adı yazın.");
            return false;
        }

        _arama?.Cancel();
        var iptal = _arama = new CancellationTokenSource();
        Bildir("Aranıyor…");
        Sonuclar.ItemsSource = null;

        List<Sehir> bulunan;
        try
        {
            bulunan = await _istemci.SehirAraEsnekAsync(metin, iptal.Token);
        }
        catch (VeriHatasi)
        {
            Bildir("Arama yapılamadı: internet bağlantısı kurulamadı. Yer aramak için internet gerekir.");
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        if (bulunan.Count == 0)
        {
            Bildir("Aradığınız yer bulunamadı. Yazılışını kontrol edip yeniden deneyin.");
            return false;
        }

        // Türkiye'dekiler başa gelir.
        var sirali = bulunan.OrderBy(s => s.Ulke == "Türkiye" ? 0 : 1).ToList();
        bool kesildi = sirali.Count > EnFazlaSonuc;
        Sonuclar.ItemsSource = sirali.Take(EnFazlaSonuc).Select(s => new Oge(s)).ToList();
        Sonuclar.SelectedIndex = 0;

        Bildir(kesildi
            ? $"{sirali.Count} yer bulundu, ilk {EnFazlaSonuc} tanesi gösteriliyor. Aramayı daraltmak için daha ayrıntılı yazın."
            : $"{sirali.Count} yer bulundu.");

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            Sonuclar.UpdateLayout();
            if (Sonuclar.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem ilk) ilk.Focus();
        });
        return true;
    }

    // ---------- Konum tahmini ----------

    private async void Konum_Click(object sender, RoutedEventArgs e)
    {
        if (_mesgul) return;
        Bildir("Konumunuz tahmin ediliyor…");

        KonumBulucu.Tahmin? tahmin;
        using (var http = TurktakvimIstemci.VarsayilanHttpIstemcisi())
        {
            tahmin = await KonumBulucu.TahminEtAsync(http);
        }

        if (tahmin is null)
        {
            Bildir("Konum tahmin edilemedi. Yer adını yazarak arayabilirsiniz.");
            return;
        }

        string? once = tahmin.Sehir ?? tahmin.Bolge;
        AramaKutusu.Text = once ?? "";
        bool bulundu = await AraAsync(once ?? "");
        if (!bulundu && !string.IsNullOrWhiteSpace(tahmin.Bolge) && tahmin.Bolge != once)
        {
            AramaKutusu.Text = tahmin.Bolge;
            await AraAsync(tahmin.Bolge);
        }
        else if (bulundu)
        {
            Bildir($"Tahmin edilen yer: {once}. {Durum.Text} Doğru yeri seçip Seç düğmesine basın; değilse yazarak arayın.");
        }
    }

    // ---------- Seçim ----------

    private void Sec_Click(object sender, RoutedEventArgs e) => _ = SecAsync();

    private void Sonuclar_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        _ = SecAsync();
    }

    private void Sonuclar_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Sonuclar.SelectedItem is not null) _ = SecAsync();
    }

    private async Task SecAsync()
    {
        if (_mesgul) return;
        if (Sonuclar.SelectedItem is not Oge oge)
        {
            Bildir("Önce listeden bir yer seçin. Liste boşsa yukarıdan arama yapın.");
            return;
        }

        var sehir = oge.Sehir;
        Mesgul(true);
        Bildir($"{sehir.GorunenAd} için vakitler indiriliyor…");
        var iptal = _indirme = new CancellationTokenSource();

        try
        {
            int yil = DateTime.Today.Year;
            await _vakitler.YiliHazirlaAsync(sehir.Kod, yil, zorla: false, iptal.Token);
            if (DateTime.Today.Month >= 11)
            {
                try
                {
                    await _vakitler.YiliHazirlaAsync(sehir.Kod, yil + 1, zorla: false, iptal.Token);
                }
                catch (VeriHatasi)
                {
                    // Gelecek yıl henüz yayınlanmamış olabilir; şehir seçimi bundan etkilenmez.
                }
            }
        }
        catch (YerIcinVeriYok)
        {
            Mesgul(false);
            Bildir($"{sehir.GorunenAd} için turktakvim.com vakit veremiyor (genellikle 60 derece kuzeyin üstündeki yerlerde olur). Lütfen yakındaki başka bir yer seçin.");
            FocusSonuclar();
            return;
        }
        catch (VeriHatasi)
        {
            Mesgul(false);
            Bildir($"{sehir.GorunenAd} için vakitler indirilemedi. İnternet bağlantınızı kontrol edip yeniden deneyin.");
            FocusSonuclar();
            return;
        }
        catch (OperationCanceledException)
        {
            Mesgul(false);
            Bildir("İndirme iptal edildi.");
            FocusSonuclar();
            return;
        }

        Secilen = sehir;
        DialogResult = true;
    }

    private void Vazgec_Click(object sender, RoutedEventArgs e)
    {
        if (_mesgul)
        {
            _indirme?.Cancel(); // indirme sürüyorsa önce o iptal edilir
            return;
        }
        DialogResult = false;
    }

    // ---------- Yardımcılar ----------

    private void Mesgul(bool mesgul)
    {
        _mesgul = mesgul;
        AramaKutusu.IsEnabled = !mesgul;
        AraDugmesi.IsEnabled = !mesgul;
        KonumDugmesi.IsEnabled = !mesgul;
        Sonuclar.IsEnabled = !mesgul;
        SecDugmesi.IsEnabled = !mesgul;
        if (mesgul) VazgecDugmesi.Focus();
    }

    private void FocusSonuclar()
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            Sonuclar.UpdateLayout();
            if (Sonuclar.SelectedIndex >= 0 &&
                Sonuclar.ItemContainerGenerator.ContainerFromIndex(Sonuclar.SelectedIndex) is ListBoxItem oge)
                oge.Focus();
            else
                AramaKutusu.Focus();
        });
    }

    /// <summary>Durum satırını günceller ve ekran okuyucuya (NVDA) haber verir.</summary>
    private void Bildir(string metin)
    {
        Durum.Text = metin;
        var peer = UIElementAutomationPeer.FromElement(Durum) ?? UIElementAutomationPeer.CreatePeerForElement(Durum);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
