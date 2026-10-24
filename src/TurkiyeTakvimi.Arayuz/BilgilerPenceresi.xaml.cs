using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Başlık listesindeki bir satır. Ekran okuyucu satırın adını ToString'den okur.</summary>
public sealed class BilgiBasligi
{
    public BilgiBasligi(BilgiOgesi oge) => Oge = oge;

    public BilgiOgesi Oge { get; }
    public string Baslik => GorunmezKarakterler.At(Oge.Baslik);
    public Visibility SiteEtiketi => Oge.Yazi ? Visibility.Collapsed : Visibility.Visible;

    public override string ToString() => Oge.Yazi ? Baslik : Baslik + ", sitede açılır";
}

/// <summary>
/// Bilgiler: bir kategorinin başlık listesi; bir başlık seçilince yazısı paragraf paragraf aynı pencerede açılır.
/// Esc, yazıdan başlıklara döner; başlıklardayken pencereyi kapatır.
/// </summary>
public partial class BilgilerPenceresi : Window
{
    private readonly BilgilerServisi _servis;
    private readonly string _anahtar;
    private readonly string _kategoriAdi;
    private List<BilgiBasligi> _tumBasliklar = new();
    private BilgiOgesi? _acik;
    private List<string> _acikParagraflar = new();
    private int _surum;

    public BilgilerPenceresi(BilgilerServisi servis, string anahtar, string kategoriAdi, double olcek)
    {
        _servis = servis;
        _anahtar = anahtar;
        _kategoriAdi = kategoriAdi;
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;

        if (olcek > 1.0)
        {
            Kok.LayoutTransform = new ScaleTransform(olcek, olcek);
            Width = Math.Min(Width * olcek, SystemParameters.WorkArea.Width);
            Height = Math.Min(Height * olcek, SystemParameters.WorkArea.Height);
        }

        Title = kategoriAdi;
        Baslik.Text = kategoriAdi;
        Loaded += async (_, _) => await BasliklariYukle();
    }

    private bool YaziAcik => YaziKutusu.Visibility == Visibility.Visible;

    private async Task BasliklariYukle()
    {
        AltBaslik.Text = "Başlıklar hazırlanıyor…";
        Bildir("Başlıklar hazırlanıyor…");
        try
        {
            var indeks = await _servis.IndeksiGetirAsync(DateTime.Now);
            var kategori = indeks.Kategoriler.FirstOrDefault(k => BilgilerServisi.Anahtar(k.Ad).StartsWith(_anahtar, StringComparison.Ordinal));
            if (kategori is null)
            {
                AltBaslik.Text = "Bu bölüm sitede bulunamadı.";
                Bildir("Bu bölüm turktakvim.com'da bulunamadı.");
                return;
            }

            _tumBasliklar = kategori.Ogeler.Select(o => new BilgiBasligi(o)).ToList();
            BasliklarListesi.SetValue(AutomationProperties.NameProperty, $"{_kategoriAdi} başlıkları");
            AltBaslik.Text = "";
            ListeyiSuz();
            Durum.Text = ""; // "hazırlanıyor" iletisi kalkar; hazır olunca bir şey söylenmez
            BasliklaraOdakla();
        }
        catch (VeriHatasi)
        {
            AltBaslik.Text = "Başlıklar alınamadı.";
            Bildir("Bilgiler için internet gerekiyor ve bağlantı kurulamadı. Daha önce açtığınız yazılar internetsiz de okunur.");
        }
    }

    private void ListeyiSuz()
    {
        string ara = BilgilerServisi.Anahtar(AramaKutusu.Text);
        var liste = ara.Length == 0
            ? _tumBasliklar
            : _tumBasliklar.Where(b => BilgilerServisi.Anahtar(b.Baslik).Contains(ara, StringComparison.Ordinal)).ToList();
        BasliklarListesi.ItemsSource = liste;
        if (liste.Count > 0) BasliklarListesi.SelectedIndex = 0;
        AltBaslik.Text = ara.Length == 0 ? "" : $"{liste.Count} başlık bulundu"; // arama sonucunun sayısı arama yapana geri bildirimdir
    }

    private void AramaKutusu_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ListeyiSuz();
        if (AramaKutusu.Text.Length > 0) Bildir($"{BasliklarListesi.Items.Count} başlık bulundu.");
    }

    /// <summary>Arama kutusunda aşağı ok ve Enter, listeye geçirir.</summary>
    private void AramaKutusu_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Enter)
        {
            e.Handled = true;
            BasliklaraOdakla();
        }
    }

    private void BasliklaraOdakla()
    {
        BasliklarListesi.UpdateLayout();
        if (BasliklarListesi.SelectedItem is not null)
        {
            BasliklarListesi.ScrollIntoView(BasliklarListesi.SelectedItem);
            BasliklarListesi.UpdateLayout();
            if (BasliklarListesi.ItemContainerGenerator.ContainerFromItem(BasliklarListesi.SelectedItem) is ListBoxItem oge) oge.Focus();
            else BasliklarListesi.Focus();
        }
        else
        {
            AramaKutusu.Focus();
        }
    }

    private void BasliklarListesi_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && BasliklarListesi.SelectedItem is BilgiBasligi)
        {
            e.Handled = true;
            _ = Ac((BasliklarListesi.SelectedItem as BilgiBasligi)!.Oge);
        }
    }

    private void BasliklarListesi_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (BasliklarListesi.SelectedItem is BilgiBasligi b) _ = Ac(b.Oge);
    }

    /// <summary>Yazıyı indirir/okur ve yazı sayfasını gösterir. Düz yazıya çevrilemeyen başlıklar sitede açılır.</summary>
    private async Task Ac(BilgiOgesi oge)
    {
        if (!oge.Yazi)
        {
            SiteyiAc(oge);
            return;
        }

        int surum = ++_surum;
        Bildir($"{oge.Baslik} hazırlanıyor…");
        try
        {
            var yazi = await _servis.YaziGetirAsync(oge);
            if (surum != _surum) return;

            if (yazi.Paragraflar.Count == 0)
            {
                Bildir("Bu başlık sitede tablo ya da resim olarak duruyor. Tarayıcıda açılıyor.");
                SiteyiAc(oge);
                return;
            }

            _acik = oge;
            _acikParagraflar = yazi.Paragraflar;
            Title = $"{yazi.Baslik} - {_kategoriAdi}";
            Baslik.Text = yazi.Baslik;
            AltBaslik.Text = _kategoriAdi;
            YaziKutusu.SetValue(AutomationProperties.NameProperty, yazi.Baslik);
            YaziKutusu.Text = GorunmezKarakterler.At(string.Join("\n", yazi.Paragraflar)); // önbellekteki eski yazılarda da temiz

            BasliklarSayfasi.Visibility = Visibility.Collapsed;
            YaziKutusu.Visibility = Visibility.Visible;
            YaziDugmeleri.Visibility = Visibility.Visible;
            Durum.Text = "";

            YaziKutusu.UpdateLayout();
            YaziKutusu.CaretIndex = 0;
            YaziKutusu.ScrollToHome();
            YaziKutusu.Focus();
        }
        catch (VeriHatasi)
        {
            if (surum != _surum) return;
            Bildir("Bu yazı daha önce açılmadığı için internet gerekiyor ve bağlantı kurulamadı.");
        }
    }

    private void Don_Click(object sender, RoutedEventArgs e) => BasliklaraDon();

    private void BasliklaraDon()
    {
        if (!YaziAcik) return;
        _surum++;
        YaziKutusu.Visibility = Visibility.Collapsed;
        YaziDugmeleri.Visibility = Visibility.Collapsed;
        BasliklarSayfasi.Visibility = Visibility.Visible;
        Title = _kategoriAdi;
        Baslik.Text = _kategoriAdi;
        ListeyiSuzYenidenBaslik();
        Durum.Text = "";
        BasliklaraOdakla();
    }

    private void ListeyiSuzYenidenBaslik() =>
        AltBaslik.Text = BasliklarListesi.Items.Count == _tumBasliklar.Count ? "" : $"{BasliklarListesi.Items.Count} başlık bulundu";

    private void Pencere_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        if (YaziAcik) BasliklaraDon();
        else Close();
    }

    private void YaziyiKopyala_Click(object sender, RoutedEventArgs e)
    {
        if (_acikParagraflar.Count == 0) return;
        Kopyala(Baslik.Text + Environment.NewLine + string.Join(Environment.NewLine, _acikParagraflar), "Yazının tamamı panoya kopyalandı.");
    }

    private void Site_Click(object sender, RoutedEventArgs e)
    {
        if (_acik is not null) SiteyiAc(_acik);
    }

    private void SiteyiAc(BilgiOgesi oge)
    {
        try
        {
            Process.Start(new ProcessStartInfo(oge.Adres) { UseShellExecute = true });
            Bildir($"{oge.Baslik} tarayıcıda açıldı.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Bildir("Tarayıcı açılamadı.");
        }
    }

    private void Kopyala(string metin, string bildirim)
    {
        try
        {
            Clipboard.SetText(metin);
            Bildir(bildirim);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            Bildir("Pano şu anda kullanılamıyor, tekrar deneyin.");
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
