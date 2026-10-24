using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Şehir menüsü: son kullanılan şehirler arasında tek adımda geçiş; Ctrl+K menüyü açar.</summary>
public partial class MainWindow
{
    /// <summary>Menü öğelerini ayarlardaki son şehirlerle yeniler. Şehir ya da ayar değişince çağrılır.</summary>
    private void SehirMenusunuGuncelle()
    {
        SehirMenusu.Items.Clear();

        var kodlar = _ayarlar.SonSehirler.ToList();
        if (SehirSecili && !kodlar.Contains(_ayarlar.SehirKodu)) kodlar.Insert(0, _ayarlar.SehirKodu);

        foreach (string kod in kodlar.Take(VakitServisi.SaklananSehirSayisi))
        {
            string ad = SehirAdiBul(kod);
            bool simdiki = SehirSecili && kod == _ayarlar.SehirKodu;
            var oge = new MenuItem
            {
                Header = ad.Replace("_", "__"), // "_" erişim tuşu sayılmasın
                IsCheckable = true,
                IsChecked = simdiki,
                Tag = kod
            };
            oge.Click += HizliSehir_Click;
            SehirMenusu.Items.Add(oge);
        }

        if (SehirMenusu.Items.Count > 0) SehirMenusu.Items.Add(new Separator());
        var baska = new MenuItem { Header = "_Başka yer seç…" };
        baska.Click += SehirSec_Click;
        SehirMenusu.Items.Add(baska);
    }

    /// <summary>Şehrin görünen adı: önce kayıtlı ad, yoksa diskteki vakit kaydından.</summary>
    private string SehirAdiBul(string kod)
    {
        if (kod == _ayarlar.SehirKodu && !string.IsNullOrWhiteSpace(_ayarlar.SehirAdi)) return _ayarlar.SehirAdi;
        if (_ayarlar.SehirAdlari.TryGetValue(kod, out var kayitli) && !string.IsNullOrWhiteSpace(kayitli)) return kayitli;

        foreach (string dosya in _onbellek.DosyaAdlari($"vakit-{kod}-"))
            if (_onbellek.Oku<YillikVakitler>(dosya) is { } yil && !string.IsNullOrWhiteSpace(yil.SehirAdi))
                return new Sehir(kod, yil.SehirAdi, "").GorunenAd;

        return "Yer " + kod;
    }

    private async void HizliSehir_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string kod }) return;
        string ad = SehirAdiBul(kod);

        if (SehirSecili && kod == _ayarlar.SehirKodu)
        {
            Duyur($"{ad} zaten seçili.");
            SehirMenusunuGuncelle(); // tıklamayla kalkan işaret geri gelsin
            return;
        }

        // Yeni şehrin vakitleri hazır değilse (örn. internet yok) şehir değiştirilmez.
        try
        {
            await _vakitler.GunuGetirAsync(kod, DateOnly.FromDateTime(DateTime.Now));
        }
        catch (VeriHatasi)
        {
            Duyur($"{ad} için vakitler alınamadı, internet gerekiyor. Yer değiştirilmedi.");
            return;
        }

        await SehriUygula(new Sehir(kod, ad, ""));
        _imsakiyeAnahtar = "";
        _aylikAnahtar = "";
        if (ImsakiyeAktif) _ = ImsakiyeYukle(zorla: true);
        if (AylikAktif) _ = AylikYukle(zorla: true);

        Sekmeler.SelectedItem = AnaSekme;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => OdakVer(AnaListe));
    }

    /// <summary>Ctrl+K: Şehir menüsünü açar ve ilk öğeye odaklanır (menü çubuğunda Enter'a basmakla aynı).</summary>
    private void SehirMenusunuAc()
    {
        if (!SehirSecili) return;
        SehirMenusu.Focus();
        var kaynak = PresentationSource.FromVisual(SehirMenusu);
        if (kaynak is null) return;
        SehirMenusu.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, kaynak, Environment.TickCount, Key.Enter)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        });
    }
}
