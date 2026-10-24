using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Bir yılın mübarek gün ve gecelerini listeler. Enter, seçili günü Takvim sekmesinde açtırır.</summary>
public partial class MubarekGunlerPenceresi : Window
{
    private readonly Func<int, Task<List<MubarekGun>>> _yukle;
    private readonly DateOnly _bugun;
    private int _yil;
    private int _surum;

    /// <summary>Enter ile seçilen gün; pencere kapanınca ana pencere Takvim'de bu güne gider.</summary>
    public DateOnly? SecilenTarih { get; private set; }

    public MubarekGunlerPenceresi(Func<int, Task<List<MubarekGun>>> yukle, DateOnly bugun, double olcek)
    {
        _yukle = yukle;
        _bugun = bugun;
        _yil = bugun.Year;
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;

        if (olcek > 1.0)
        {
            Kok.LayoutTransform = new ScaleTransform(olcek, olcek);
            Width = Math.Min(Width * olcek, SystemParameters.WorkArea.Width);
            Height = Math.Min(Height * olcek, SystemParameters.WorkArea.Height);
        }

        Loaded += async (_, _) => await YiliYukle(ilkAcilis: true);
    }

    private void OncekiYil_Click(object sender, RoutedEventArgs e) { _yil--; _ = YiliYukle(); }
    private void SonrakiYil_Click(object sender, RoutedEventArgs e) { _yil++; _ = YiliYukle(); }
    private void BuYil_Click(object sender, RoutedEventArgs e)
    {
        if (BuYilDugmesi.Gecersiz)
        {
            Bildir("Zaten bu yılı görüyorsunuz.");
            return;
        }
        _yil = _bugun.Year;
        _ = YiliYukle();
    }
    private void Kapat_Click(object sender, RoutedEventArgs e) => Close();

    private async Task YiliYukle(bool ilkAcilis = false)
    {
        int surum = ++_surum, yil = _yil;
        YilYazisi.Text = $"{yil} yılı";
        Liste.SetValue(AutomationProperties.NameProperty, $"Mübarek gün ve geceler, {yil}");
        BuYilDugmesi.Gecersiz = yil == _bugun.Year; // odaklanılır ama ekran okuyucu 'kullanılamıyor' der (diğer Bugün/Bu ay düğmeleri gibi)
        Bildir($"{yil} yılı hazırlanıyor…");

        try
        {
            var gunler = await _yukle(yil);
            if (surum != _surum) return;

            if (gunler.Count == 0)
            {
                Liste.ItemsSource = null;
                Bildir($"{yil} yılı için hicrî tarihler turktakvim.com'da henüz yayımlanmamış. Yayımlanınca burada görünür.");
                return;
            }

            var satirlar = gunler.Select(g => new MubarekSatiri(g, _bugun)).ToList();
            // Sıradaki: bugün ya da sonrasındaki ilk gün; geçmiş bir yıl gösteriliyorsa ilk satır.
            var siradaki = satirlar.FirstOrDefault(s => !s.Gecti);
            if (siradaki is not null && yil == _bugun.Year) siradaki.Siradaki = true;

            Liste.ItemsSource = satirlar;
            Liste.SelectedItem = (yil == _bugun.Year ? siradaki : null) ?? satirlar[0];
            Liste.UpdateLayout();
            Liste.ScrollIntoView(Liste.SelectedItem);
            Durum.Text = ""; // "hazırlanıyor" iletisi kalkar; hazır olunca bir şey söylenmez

            // Pencere açılınca ya da yıl değişince odak listede, sıradaki günde olur.
            Liste.UpdateLayout();
            if (Liste.ItemContainerGenerator.ContainerFromItem(Liste.SelectedItem) is ListBoxItem oge && (ilkAcilis || Liste.IsKeyboardFocusWithin)) oge.Focus();
        }
        catch (VeriHatasi)
        {
            if (surum != _surum) return;
            Liste.ItemsSource = null;
            Bildir($"{yil} yılı hazırlanamadı: internet gerekiyor ve bağlantı kurulamadı.");
        }
    }

    private void Liste_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Liste.SelectedItem is MubarekSatiri)
        {
            e.Handled = true;
            Git();
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            Kopyala();
        }
    }

    private void Liste_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Liste.SelectedItem is MubarekSatiri) Git();
    }

    private void Git_Click(object sender, RoutedEventArgs e) => Git();
    private void Kopyala_Click(object sender, RoutedEventArgs e) => Kopyala();

    private void Git()
    {
        if (Liste.SelectedItem is not MubarekSatiri s) return;
        SecilenTarih = s.Tarih;
        DialogResult = true;
    }

    private void Kopyala()
    {
        if (Liste.SelectedItem is not MubarekSatiri s) return;
        try
        {
            Clipboard.SetText(s.Kopya);
            Bildir($"Panoya kopyalandı: {s.Kopya}");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            Bildir("Pano şu anda kullanılamıyor, tekrar deneyin.");
        }
    }

    private void Bildir(string metin)
    {
        Durum.Text = metin;
        var peer = UIElementAutomationPeer.FromElement(Durum) ?? UIElementAutomationPeer.CreatePeerForElement(Durum);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
