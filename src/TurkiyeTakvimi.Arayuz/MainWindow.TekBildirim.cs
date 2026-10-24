using System.Windows;
using System.Windows.Controls;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>
/// "Bu vakti bir kez bildir": Vakitler ve Diğer Vakitler satırlarının sağ tık menüsünden kurulur. Vakit girince tek bildirim verilir ve
/// kendiliğinden silinir; ertesi gün yinelenmez. Bildirim ayarlarından (açık/kapalı vakitler, hatırlatma süresi, sessiz saatler)
/// bağımsızdır: bildirimler kapalıyken ve sessiz saatte de verilir.
/// </summary>
public partial class MainWindow
{
    private const int TekBildirimPenceresiDakika = 15; // uykudan çok geç dönüldüyse eski vakit bildirilmez
    private int _tekBildirimSurumu;                   // Diğer Vakitler listesi rozet değişince yeniden kurulsun

    private static string GunAnahtari(DateOnly gun) => gun.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>"Bir kez bildir" hangi güne kurulur: vakit bugün hâlâ gelecekteyse bugün, geçtiyse (ya da yarının satırıysa) yarın.</summary>
    private DateOnly TekBildirimGunu(int vakit, bool yarinSatiri)
    {
        var simdi = SehirSimdi;
        var bugun = DateOnly.FromDateTime(simdi);
        if (yarinSatiri) return bugun.AddDays(1);
        return VakitZamani(simdi, vakit) is { } z && z > simdi ? bugun : bugun.AddDays(1);
    }

    private bool TekBildirimVar(DateOnly gun, int vakit) =>
        _ayarlar.TekBildirimler.Any(t => t.Gun == GunAnahtari(gun) && t.Vakit == vakit);

    /// <summary>Satırın rozeti için: bu vakit, bir sonraki gelişi için "bir kez bildir" kurulu mu.</summary>
    private bool TekBildirimKurulu(int vakit, bool yarinSatiri) =>
        vakit >= 0 && TekBildirimVar(TekBildirimGunu(vakit, yarinSatiri), vakit);

    /// <summary>Sağ tık menüsü açılırken madde adı seçili satıra göre ayarlanır ("bir kez bildir" ya da "bildirimi kaldır").</summary>
    private void VakitListesi_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ListBox { ContextMenu: { } menu } liste) return;
        var madde = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Tag as string == "TekBildirim");
        if (madde is null) return;

        var satir = liste.SelectedItem as VakitSatiri;
        madde.IsEnabled = satir is { VakitNo: >= 0 };
        madde.Header = satir is { Bildirilecek: true } ? "Bildirimi _kaldır" : "Bu vakti bir _kez bildir";
    }

    private void TekBildirim_Click(object sender, RoutedEventArgs e)
    {
        var liste = ((sender as MenuItem)?.Parent as ContextMenu)?.PlacementTarget as ListBox;
        if (liste?.SelectedItem is not VakitSatiri { VakitNo: >= 0 } satir) return;

        var gun = TekBildirimGunu(satir.VakitNo, satir.Yarin);
        string ad = GosterAd(satir.VakitNo);
        string gunMetni = gun == DateOnly.FromDateTime(SehirSimdi) ? "bugün" : "yarın";

        _tekBildirimSurumu++; // Diğer Vakitler listesi de yeniden kurulsun (rozet)
        if (TekBildirimVar(gun, satir.VakitNo))
        {
            AyarDegistir(a => a.TekBildirimler.RemoveAll(t => t.Gun == GunAnahtari(gun) && t.Vakit == satir.VakitNo));
            Duyur($"{ad}, {gunMetni} için bir kez bildirim kaldırıldı.");
        }
        else
        {
            AyarDegistir(a => a.TekBildirimler.Add(new TekBildirim { Gun = GunAnahtari(gun), Vakit = satir.VakitNo }));
            Duyur($"{ad}, {gunMetni} vaktinde bir kez bildirilecek.");
        }
    }

    /// <summary>Her saniye: vakti gelen tek seferlik bildirimleri verir ve siler; geçmişte kalanları da temizler.</summary>
    private void TekBildirimleriDenetle(DateTime simdi)
    {
        if (_ayarlar.TekBildirimler.Count == 0 || _bugun is null) return;

        var bugun = DateOnly.FromDateTime(simdi);
        string bugunAnahtar = GunAnahtari(bugun);
        bool degisti = false;

        foreach (var t in _ayarlar.TekBildirimler.ToList())
        {
            bool gecmisGun = string.CompareOrdinal(t.Gun, bugunAnahtar) < 0;
            bool bugunMu = t.Gun == bugunAnahtar;
            if (!gecmisGun && !bugunMu) continue; // ileri bir gün: vakti gelince

            bool bildir = false;
            if (bugunMu && t.Vakit >= 0 && t.Vakit < VakitAdlari.Hepsi.Length && VakitZamani(simdi, t.Vakit) is { } zaman)
            {
                if (simdi < zaman) continue; // vakit daha gelmedi
                bildir = simdi - zaman < TimeSpan.FromMinutes(TekBildirimPenceresiDakika);
            }

            if (bildir && _bugun is not null)
                BildirimGoster(GirisBasligi(t.Vakit), $"{SehirMetni}, {VakitSaati.Ayristir(_bugun.Gun.Saatler[t.Vakit]).Okunan}.");

            _ayarlar.TekBildirimler.Remove(t);
            degisti = true;
        }

        if (!degisti) return;
        AyarlariKaydet();
        _tekBildirimSurumu++;
        ListeleriDoldur(odakla: false);
    }
}
