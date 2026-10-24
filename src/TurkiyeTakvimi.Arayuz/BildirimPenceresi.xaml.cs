using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Hangi vakitlerde bildirim geleceği ve bildirimin ayrıntıları. Değişiklikler hemen uygulanır ve kaydedilir.</summary>
public partial class BildirimPenceresi : Window
{
    private static readonly int[] OncedenSecenekleri = { 0, 5, 10, 15, 30 };

    private readonly MainWindow _sahip;
    private readonly Dictionary<int, CheckBox> _kutular = new();
    private bool _yukleniyor = true; // kutular kurulurken ve toplu işaretlenirken olaylar ayar kaydetmesin

    public BildirimPenceresi(MainWindow sahip, double olcek)
    {
        _sahip = sahip;
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;

        if (olcek > 1.0)
        {
            Kok.LayoutTransform = new ScaleTransform(olcek, olcek);
            Width = Math.Min(Width * olcek, SystemParameters.WorkArea.Width);
        }

        var a = sahip.AyarlarOku;
        var secili = a.BildirimVakitleri.ToHashSet();

        foreach (int v in Enumerable.Range(0, VakitAdlari.Hepsi.Length))
        {
            var kutu = new CheckBox
            {
                Content = VakitAdlari.Hepsi[v],
                Margin = new Thickness(0, 0, 8, 5),
                IsChecked = secili.Contains(v),
                Tag = v
            };
            kutu.Checked += Vakit_Click;
            kutu.Unchecked += Vakit_Click;
            _kutular[v] = kutu;
            (VakitAdlari.AnaMi(v) ? AnaKutular : DigerKutular).Children.Add(kutu);
        }

        SesKutusu.IsChecked = a.BildirimSesi;
        SesSecimKutusu.ItemsSource = BildirimSesleri.Liste;
        SesSecimKutusu.SelectedItem = BildirimSesleri.Bul(a.BildirimSesAdi);
        MubarekKutusu.IsChecked = a.MubarekGunBildirimi;
        OncedenKutusu.ItemsSource = OncedenSecenekleri.Select(OncedenMetni).ToList();
        OncedenKutusu.SelectedIndex = Math.Max(0, Array.IndexOf(OncedenSecenekleri, a.OncedenDakika));
        SessizKutusu.IsChecked = a.SessizSaatler;

        var saatler = Enumerable.Range(0, 48).Select(i => $"{i / 2:00}:{(i % 2) * 30:00}").ToList();
        SessizBasKutusu.ItemsSource = saatler;
        SessizBitKutusu.ItemsSource = saatler;
        SessizBasKutusu.SelectedItem = saatler.Contains(a.SessizBaslangic) ? a.SessizBaslangic : "23:00";
        SessizBitKutusu.SelectedItem = saatler.Contains(a.SessizBitis) ? a.SessizBitis : "04:00";

        TumuKutulariniGuncelle();
        DurumuYaz();
        _yukleniyor = false;
        Loaded += (_, _) => DurumOzeti.Focus(); // açılınca önce mevcut durum okunur, Tab ile ayarlara geçilir
    }

    /// <summary>Üstteki bildirim durumu satırı; vakitler ya da mübarek gün bildirimi değişince güncellenir.</summary>
    private void DurumuYaz()
    {
        var ayar = _sahip.AyarlarOku;
        int n = ayar.BildirimVakitleri.Count;
        string vakitler = n == 0 ? "Vakit bildirimi kapalı" : $"{n} vakit için bildirim açık";
        DurumOzeti.Text = ayar.MubarekGunBildirimi
            ? $"{vakitler}. Mübarek gün ve gece bildirimi açık."
            : n == 0 ? "Bildirimler kapalı: hiçbir bildirim gelmiyor." : $"{vakitler}.";

        // "Vakitten önce hatırlat" işaretli vakitlerin bildirimine bağlıdır: hiç vakit işaretli değilken kullanılamaz.
        OncedenKutusu.IsEnabled = n > 0;
        if (n == 0) DurumOzeti.Text += " Vakitten önce hatırlatma, vakit işaretlenince kullanılabilir.";
    }

    private static string OncedenMetni(int dakika) => dakika == 0 ? "Hatırlatma yok" : $"{dakika} dakika önce";

    private IEnumerable<int> Grup(bool ana) => _kutular.Keys.Where(v => VakitAdlari.AnaMi(v) == ana);

    /// <summary>"Tüm ..." kutusu yalnızca grubun hepsi işaretliyken işaretlidir.</summary>
    private void TumuKutulariniGuncelle()
    {
        bool onceki = _yukleniyor;
        _yukleniyor = true;
        AnaTumuKutusu.IsChecked = Grup(true).All(v => _kutular[v].IsChecked == true);
        DigerTumuKutusu.IsChecked = Grup(false).All(v => _kutular[v].IsChecked == true);
        _yukleniyor = onceki;
    }

    private void VakitleriKaydet()
    {
        var liste = _kutular.Where(k => k.Value.IsChecked == true).Select(k => k.Key).OrderBy(v => v).ToList();
        _sahip.AyarDegistir(a => a.BildirimVakitleri = liste);
        DurumuYaz();
    }

    private void Vakit_Click(object sender, RoutedEventArgs e)
    {
        if (_yukleniyor || sender is not CheckBox kutu) return;
        VakitleriKaydet();
        TumuKutulariniGuncelle();
        Bildir($"{kutu.Content} bildirimi {(kutu.IsChecked == true ? "açıldı" : "kapatıldı")}.");
    }

    private void AnaTumu_Click(object sender, RoutedEventArgs e) => TopluIsaretle(true, AnaTumuKutusu.IsChecked == true, "Tüm ana vakitlerin");

    private void DigerTumu_Click(object sender, RoutedEventArgs e) => TopluIsaretle(false, DigerTumuKutusu.IsChecked == true, "Tüm diğer vakitlerin");

    private void TopluIsaretle(bool ana, bool isaretli, string ad)
    {
        if (_yukleniyor) return;
        _yukleniyor = true;
        foreach (int v in Grup(ana)) _kutular[v].IsChecked = isaretli;
        _yukleniyor = false;
        VakitleriKaydet();
        Bildir($"{ad} bildirimi {(isaretli ? "açıldı" : "kapatıldı")}.");
    }

    private void Mubarek_Click(object sender, RoutedEventArgs e)
    {
        if (_yukleniyor) return;
        bool acik = MubarekKutusu.IsChecked == true;
        _sahip.AyarDegistir(a => a.MubarekGunBildirimi = acik);
        DurumuYaz();
        Bildir(acik
            ? "Mübarek gün ve gecelerde haber verilecek: bir gün önce akşam ve o gün sabah."
            : "Mübarek gün ve gece bildirimi kapatıldı.");
    }

    private void Ses_Click(object sender, RoutedEventArgs e)
    {
        if (_yukleniyor) return;
        bool acik = SesKutusu.IsChecked == true;
        _sahip.AyarDegistir(a => a.BildirimSesi = acik);
        Bildir(acik ? "Bildirim sesi açıldı." : "Bildirim sesi kapatıldı.");
    }

    /// <summary>Ses seçilince hemen çalar (ok tuşlarıyla gezerken her sesi duyarsınız) ve kaydedilir.</summary>
    private void SesSecimi_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_yukleniyor || SesSecimKutusu.SelectedItem is not BildirimSesleri.Secenek secenek) return;
        _sahip.AyarDegistir(a => a.BildirimSesAdi = secenek.Anahtar);
        BildirimSesleri.Cal(secenek.Anahtar);
        Bildir($"Bildirim sesi: {secenek.Ad}.");
    }

    private void SesDinle_Click(object sender, RoutedEventArgs e)
    {
        if (SesSecimKutusu.SelectedItem is BildirimSesleri.Secenek secenek) BildirimSesleri.Cal(secenek.Anahtar);
    }

    private void Onceden_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_yukleniyor || OncedenKutusu.SelectedIndex < 0) return;
        int dakika = OncedenSecenekleri[OncedenKutusu.SelectedIndex];
        _sahip.AyarDegistir(a => a.OncedenDakika = dakika);
        Bildir(dakika == 0 ? "Önceden hatırlatma kapatıldı." : $"Vakitten {dakika} dakika önce hatırlatılacak.");
    }

    private void Sessiz_Click(object sender, RoutedEventArgs e)
    {
        if (_yukleniyor) return;
        bool acik = SessizKutusu.IsChecked == true;
        _sahip.AyarDegistir(a => a.SessizSaatler = acik);
        Bildir(acik
            ? $"Sessiz saatler açıldı: {SessizBasKutusu.SelectedItem} ile {SessizBitKutusu.SelectedItem} arasında bildirim gösterilmez."
            : "Sessiz saatler kapatıldı.");
    }

    private void SessizSaat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_yukleniyor || SessizBasKutusu.SelectedItem is not string bas || SessizBitKutusu.SelectedItem is not string bit) return;
        _sahip.AyarDegistir(a => { a.SessizBaslangic = bas; a.SessizBitis = bit; });
        Bildir($"Sessiz saatler: {bas} ile {bit} arası.");
    }

    private void Kapat_Click(object sender, RoutedEventArgs e) => Close();

    private void Bildir(string metin)
    {
        Durum.Text = metin;
        var peer = UIElementAutomationPeer.FromElement(Durum) ?? UIElementAutomationPeer.CreatePeerForElement(Durum);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
