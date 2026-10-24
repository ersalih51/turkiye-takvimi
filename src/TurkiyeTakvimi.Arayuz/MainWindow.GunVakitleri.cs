using System.Windows;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Takvim sekmesindeki "Günün Vakitleri": seçili tarihin (herhangi bir yıl) ana vakitleri.</summary>
public partial class MainWindow
{
    private int _gunVakitleriSurum;

    private void KopyalaGunVakitleri_Click(object sender, RoutedEventArgs e) => Kopyala(GunVakitleri);

    private void PaylasGunVakitleri_Click(object sender, RoutedEventArgs e)
    {
        if (GunVakitleri.SelectedItem is not GunSatiri g || g.Hucreler.Count == 0)
        {
            Duyur("Paylaşılacak vakit yok.");
            return;
        }
        PaylasimiBaslat($"{SehirMetni}, {g.Baslik} vakitleri", $"{SehirMetni}, {g.Kopya}");
    }

    /// <summary>Seçili tarihin vakitlerini gösterir. Site her yıl için vakit verdiğinden eski ve ileri yıllar da çalışır; yıl diskte yoksa indirilir.</summary>
    private async Task GunVakitleriniYukle(DateOnly tarih)
    {
        if (!SehirSecili)
        {
            GunVakitleri.ItemsSource = null;
            return;
        }

        int surum = ++_gunVakitleriSurum;
        string kod = SehirKodu;
        try
        {
            var sonuc = await _vakitler.GunuGetirAsync(kod, tarih);
            if (surum != _gunVakitleriSurum || kod != SehirKodu) return;

            var bugun = DateOnly.FromDateTime(SehirSimdi);
            GunVakitleri.ItemsSource = new List<GunSatiri>
            {
                new(tarih, tarih == bugun, VakitAdlari.Ana.Select(v => (VakitAdlari.Hepsi[v], sonuc.Gun.Saatler[v])).ToList())
            };
            GunVakitleri.SelectedIndex = 0;
        }
        catch (VeriHatasi)
        {
            if (surum != _gunVakitleriSurum) return;
            GunVakitleri.ItemsSource = new List<GunSatiri> { GunSatiri.Not("Bu günün vakitleri alınamadı. Başka bir yılın vakitleri için internet gerekiyor.") };
            GunVakitleri.SelectedIndex = 0;
        }
    }
}
