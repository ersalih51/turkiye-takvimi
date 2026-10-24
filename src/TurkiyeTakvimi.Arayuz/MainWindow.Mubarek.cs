using System.Globalization;
using System.Windows;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Mübarek gün ve geceler: liste penceresi, Vakitler başlığındaki "Bu akşam Berât Kandili" bilgisi ve Takvim satırı.</summary>
public partial class MainWindow
{
    private MubarekGunServisi? _mubarekServisi;
    private readonly Dictionary<int, List<MubarekGun>> _mubarekYillar = new();

    private MubarekGunServisi MubarekServisi => _mubarekServisi ??= new MubarekGunServisi(_icerik, _onbellek);

    private void MubarekGunler_Click(object sender, RoutedEventArgs e) => _ = MubarekGunleriAc();

    private async Task<List<MubarekGun>> MubarekYiliGetir(int yil)
    {
        if (_mubarekYillar.TryGetValue(yil, out var bellekte)) return bellekte;
        var liste = await MubarekServisi.YiliHazirlaAsync(yil);
        if (liste.Count > 0) _mubarekYillar[yil] = liste;
        return liste;
    }

    private async Task MubarekGunleriAc()
    {
        var bugun = DateOnly.FromDateTime(SehirSimdi);
        var pencere = new MubarekGunlerPenceresi(MubarekYiliGetir, bugun, _ayarlar.Olcek) { Owner = this };
        if (pencere.ShowDialog() != true || pencere.SecilenTarih is not { } tarih)
        {
            return;
        }

        // Seçilen güne Takvim sekmesinde git: o günün bilgileri, olayı ve vakitleri görünür.
        Sekmeler.SelectedItem = TakvimSekme;
        await TariheGitAsync(tarih.ToDateTime(TimeOnly.MinValue), Duyur);
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () => TarihKutusu.Focus());
    }

    /// <summary>Bu yılın (Kasım'dan sonra gelecek yılın da) listesini arka planda hazırlar; başlık ve bildirimler bunu kullanır.</summary>
    private async Task MubarekGunleriniHazirla()
    {
        var bugun = DateOnly.FromDateTime(SehirSimdi);
        try
        {
            await MubarekYiliGetir(bugun.Year);
            if (bugun.Month >= 11) await MubarekYiliGetir(bugun.Year + 1);
        }
        catch (VeriHatasi)
        {
            return; // internet yok ve kayıt yok: bir sonraki açılışta yeniden denenir
        }

        if (_bugun is not null) BaslikTarihiniYaz();
    }

    private IEnumerable<MubarekGun> MubarekGunlerBellekte(int yil)
    {
        if (_mubarekYillar.TryGetValue(yil, out var l)) return l;
        if (MubarekServisi.Kayitli(yil) is { } diskte)
        {
            _mubarekYillar[yil] = diskte;
            return diskte;
        }
        return Array.Empty<MubarekGun>();
    }

    /// <summary>"Bu akşam Berât Kandili" gibi, bugün ya da yarın mübarek bir gün/gece varsa kısa bilgi; yoksa null.</summary>
    private string? MubarekOzeti(DateOnly bugun)
    {
        var yarin = bugun.AddDays(1);
        var parcalar = new List<string>();

        foreach (var g in MubarekGunlerBellekte(bugun.Year).Concat(MubarekGunlerBellekte(yarin.Year)).Distinct())
        {
            bool gece = g.Tur == MubarekTur.Gece;
            if (bugun >= g.Tarih && bugun <= g.Son)
                parcalar.Add(gece ? $"Bu akşam {g.Ad}" : $"Bugün {g.Ad}");
            else if (g.Tarih == yarin)
                parcalar.Add(gece ? $"Yarın akşam {g.Ad}" : $"Yarın {g.Ad}");
        }
        return parcalar.Count == 0 ? null : string.Join("  ·  ", parcalar);
    }

    /// <summary>Takvim sekmesindeki "Tarih ve Günün Bilgileri" için: seçili gün mübarek bir gün/geceyse tek satır.</summary>
    private string? MubarekSatiriMetni(DateOnly tarih)
    {
        var parcalar = MubarekGunlerBellekte(tarih.Year)
            .Where(g => tarih >= g.Tarih && tarih <= g.Son)
            .Select(g => g.Tur == MubarekTur.Gece ? $"{g.Ad} (akşamı başlar)" : g.Ad)
            .ToList();
        return parcalar.Count == 0 ? null : "Mübarek gün ve gece: " + string.Join(", ", parcalar);
    }
}
