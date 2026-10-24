using System.Globalization;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>
/// Mübarek gün ve geceler listesinde bir satır. <see cref="ToString"/> ekran okuyucunun okuyacağı cümledir:
/// "Berât Kandili, 2 Şubat 2026 Pazartesi akşamı. 15 Şa'bân 1447 gecesi. 3 gün sonra."
/// </summary>
public sealed class MubarekSatiri : IKopyalanabilir
{
    private static readonly CultureInfo Tr = new("tr-TR");
    private readonly string _okunan;

    public MubarekSatiri(MubarekGun gun, DateOnly bugun)
    {
        Tarih = gun.Tarih;
        Ad = gun.Ad;
        HicriMetni = gun.HicriTarih;
        Gece = gun.Tur == MubarekTur.Gece;
        TurMetni = Gece ? "Gece" : "Gün";
        Gecti = gun.Son < bugun;

        if (gun.Bitis is { } son)
        {
            bool ayniAy = son.Month == gun.Tarih.Month;
            TarihMetni = ayniAy
                ? $"{gun.Tarih.Day}–{son.Day} {son.ToString("MMMM yyyy", Tr)}"
                : $"{gun.Tarih.ToString("d MMMM", Tr)} – {son.ToString("d MMMM yyyy", Tr)}";
            Okunan = $"{gun.Tarih.ToString("d MMMM", Tr)} ile {son.ToString("d MMMM yyyy", Tr)} arası";
        }
        else
        {
            string gunAdi = gun.Tarih.ToString("d MMMM yyyy dddd", Tr);
            TarihMetni = Gece ? $"{gunAdi} akşamı" : gunAdi;
            Okunan = TarihMetni;
        }

        int fark = gun.Tarih.DayNumber - bugun.DayNumber;
        DurumMetni = bugun >= gun.Tarih && bugun <= gun.Son
            ? (gun.Bitis is null && Gece ? "Bu akşam" : gun.Bitis is null ? "Bugün" : "Sürüyor")
            : fark == 1 ? (Gece ? "Yarın akşam" : "Yarın")
            : fark > 1 ? $"{fark} gün sonra"
            : "";

        _okunan = $"{Ad}, {Okunan}. {HicriMetni}." + (DurumMetni.Length > 0 ? $" {DurumMetni}." : "");
    }

    public DateOnly Tarih { get; }
    public string Ad { get; }
    public string TarihMetni { get; }
    public string HicriMetni { get; }
    public string TurMetni { get; }
    public string DurumMetni { get; }
    public bool Gece { get; }
    public bool Gecti { get; }
    public bool Siradaki { get; set; }
    private string Okunan { get; }

    public string Kopya => $"{Ad}: {Okunan}, {HicriMetni}";

    public override string ToString() => _okunan;
}
