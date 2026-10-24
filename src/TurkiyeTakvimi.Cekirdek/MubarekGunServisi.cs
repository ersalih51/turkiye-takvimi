using System.Globalization;
using System.Text.RegularExpressions;

namespace TurkiyeTakvimi.Cekirdek;

public enum MubarekTur
{
    /// <summary>Gece: Tarih, gecenin başladığı akşamın günüdür (örn. Berât Kandili için 14 Şa'bân).</summary>
    Gece,
    Gun
}

/// <param name="Tarih">Gün ya da gecenin (akşamın) miladî günü.</param>
/// <param name="Bitis">Birkaç gün süren bayramlarda son gün; yoksa null.</param>
/// <param name="HicriTarih">Tarih gününün hicrî karşılığı, örn. "14 Şa'bân 1447".</param>
public sealed record MubarekGun(string Ad, DateOnly Tarih, DateOnly? Bitis, MubarekTur Tur, string HicriTarih)
{
    public DateOnly Son => Bitis ?? Tarih;
    public bool Kandil => Ad.Contains("Kandil", StringComparison.Ordinal) || Ad.Contains("Kadir", StringComparison.Ordinal);
    public bool Bayram => Ad.Contains("Bayram", StringComparison.Ordinal) && !Ad.Contains("Arefe", StringComparison.Ordinal);
}

/// <summary>
/// Mübarek gün ve geceler (kandiller, bayramlar, Ramazan, arefe, hicrî yılbaşı…). Günlerin miladî tarihleri turktakvim.com'un
/// hicrî tarihlerine bakılarak bulunur; hangi hicrî günün ne olduğu bilinen kurallara göre belirlenir
/// (örn. Berât: 14 Şa'bân akşamı, Kadir Gecesi: 26 Ramazan akşamı, Mîrâc: 26 Receb akşamı).
/// </summary>
public sealed class MubarekGunServisi
{
    private static readonly string[] AyAdlari =
    {
        "Muharrem", "Safer", "Rebîülevvel", "Rebîülâhir", "Cemâzil-evvel", "Cemâzil-âhir",
        "Receb", "Şa'bân", "Ramezân", "Şevvâl", "Zilkade", "Zilhicce"
    };

    private readonly IcerikServisi _icerik;
    private readonly DiskOnbellegi _onbellek;

    public MubarekGunServisi(IcerikServisi icerik, DiskOnbellegi onbellek)
    {
        _icerik = icerik;
        _onbellek = onbellek;
    }

    public static string OnbellekAdi(int yil) => $"mubarek-{yil}";

    /// <summary>Yalnızca diskteki kayıt; internete gitmez.</summary>
    public List<MubarekGun>? Kayitli(int yil) => _onbellek.Oku<List<MubarekGun>>(OnbellekAdi(yil));

    /// <summary>
    /// Verilen miladî yılın mübarek gün ve geceleri, tarih sırasıyla. Sitede o yılın hicrî tarihleri henüz yayımlanmamışsa
    /// boş liste döner. İnternet yoksa ve kayıt da yoksa VeriHatasi fırlatır.
    /// </summary>
    public async Task<List<MubarekGun>> YiliHazirlaAsync(int yil, CancellationToken iptal = default)
    {
        if (Kayitli(yil) is { } kayitli) return kayitli;

        var sonuc = new List<MubarekGun>();
        bool eksik = false;
        var yilBasi = new DateOnly(yil, 1, 1);
        var yilSonu = new DateOnly(yil, 12, 31);

        for (int h = yil - 581; h <= yil - 577; h++)
        {
            // Hicrî ayın 1. gününün miladî tarihi; ay bu miladî yılın olaylarını etkilemiyorsa hiç araştırılmaz.
            async Task<DateOnly?> Ay(int ay)
            {
                var tahmin = Tahmin(h, ay);
                if (tahmin is null || tahmin.Value < yilBasi.AddDays(-14) || tahmin.Value > yilSonu.AddDays(2)) return null;
                var gercek = await AyBaslangiciAsync(h, ay, tahmin.Value, iptal);
                if (gercek is null) eksik = true;
                return gercek;
            }

            void Ekle(string ad, DateOnly? tarih, MubarekTur tur, string hicri, DateOnly? bitis = null)
            {
                if (tarih is null || tarih.Value.Year != yil) return;
                sonuc.Add(new MubarekGun(ad, tarih.Value, bitis, tur, hicri));
            }

            string Hicri(int gun, int ay, bool gece = false) => $"{gun} {AyAdi(ay)} {h}" + (gece ? " gecesi" : "");

            if (await Ay(1) is { } muharrem)
            {
                Ekle("Hicrî Yılbaşı Gecesi", muharrem.AddDays(-1), MubarekTur.Gece, $"1 Muharrem {h} gecesi");
                Ekle("Âşûrâ Günü", muharrem.AddDays(9), MubarekTur.Gun, Hicri(10, 1));
            }
            if (await Ay(3) is { } rebiulevvel)
                Ekle("Mevlid Kandili", rebiulevvel.AddDays(10), MubarekTur.Gece, Hicri(12, 3, gece: true));
            if (await Ay(7) is { } receb)
            {
                Ekle("Üç Ayların Başlangıcı", receb, MubarekTur.Gun, Hicri(1, 7));
                // Regaib: Receb'in ilk Cuma gecesi = o Cuma'dan önceki gün akşamı.
                var cuma = receb;
                while (cuma.DayOfWeek != DayOfWeek.Friday) cuma = cuma.AddDays(1);
                Ekle("Regaib Kandili", cuma.AddDays(-1), MubarekTur.Gece, $"Receb {h} ayının ilk Cuma gecesi");
                Ekle("Mîrâc Kandili", receb.AddDays(25), MubarekTur.Gece, Hicri(27, 7, gece: true));
            }
            if (await Ay(8) is { } saban)
                Ekle("Berât Kandili", saban.AddDays(13), MubarekTur.Gece, Hicri(15, 8, gece: true));
            if (await Ay(9) is { } ramazan)
            {
                Ekle("Ramazan'ın Başlangıcı", ramazan, MubarekTur.Gun, Hicri(1, 9));
                Ekle("Kadir Gecesi", ramazan.AddDays(25), MubarekTur.Gece, Hicri(27, 9, gece: true));
            }
            if (await Ay(10) is { } sevval)
            {
                Ekle("Ramazan Bayramı Arefesi", sevval.AddDays(-1), MubarekTur.Gun, $"Ramazan'ın son günü, {h}");
                Ekle("Ramazan Bayramı", sevval, MubarekTur.Gun, $"1-3 Şevvâl {h}", sevval.AddDays(2));
            }
            if (await Ay(12) is { } zilhicce)
            {
                Ekle("Kurban Bayramı Arefesi", zilhicce.AddDays(8), MubarekTur.Gun, Hicri(9, 12));
                Ekle("Kurban Bayramı", zilhicce.AddDays(9), MubarekTur.Gun, $"10-13 Zilhicce {h}", zilhicce.AddDays(12));
            }
        }

        var sirali = sonuc.GroupBy(g => (g.Ad, g.Tarih)).Select(g => g.First()).OrderBy(g => g.Tarih).ToList();

        // Hicrî tarihleri yayımlanmamış (ya da eksik) yıl kalıcı saklanmaz; sonra yeniden denenir.
        if (!eksik && sirali.Count > 0) _onbellek.Yaz(OnbellekAdi(yil), sirali);
        return sirali;
    }
    /// <summary>Hicrî ay adı: "Şa'bân", "Ramezân" gibi.</summary>
    public static string AyAdi(int ay) => AyAdlari[ay - 1];

    /// <summary>Hicrî (yıl, ay) çiftinin miladî başlangıcına yakın bir tahmin (Ümmü'l-Kurâ takvimi).</summary>
    private static DateOnly? Tahmin(int hicriYil, int ay)
    {
        try
        {
            return DateOnly.FromDateTime(new UmAlQuraCalendar().ToDateTime(hicriYil, ay, 1, 12, 0, 0, 0));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Hicrî ayın 1. gününü sitenin hicrî tarihlerine bakarak bulur; site o günler için tarih vermiyorsa null.</summary>
    private async Task<DateOnly?> AyBaslangiciAsync(int hicriYil, int ay, DateOnly tahmin, CancellationToken iptal)
    {
        int hedef = hicriYil * 12 + (ay - 1);
        var p = tahmin;
        for (int i = 0; i < 8; i++)
        {
            var d = await HicriAsync(p, iptal);
            if (d is null) return null;

            int mevcut = d.Value.Yil * 12 + (d.Value.Ay - 1);
            if (mevcut == hedef) return p.AddDays(-(d.Value.Gun - 1));
            p = mevcut < hedef
                ? p.AddDays(Math.Max(1, 30 - d.Value.Gun))
                : p.AddDays(-d.Value.Gun);
        }
        return null;
    }

    private async Task<(int Yil, int Ay, int Gun)?> HicriAsync(DateOnly tarih, CancellationToken iptal)
    {
        var icerik = await _icerik.GetirAsync(tarih, iptal);
        string? metin = icerik.Bilgi?.HicriTarih;
        if (string.IsNullOrWhiteSpace(metin)) return null;

        var m = Regex.Match(metin.Trim(), @"^(\d+)\s+(\S+)\s+(\d{4})$");
        if (!m.Success) return null;
        int ay = AyNumarasi(m.Groups[2].Value);
        if (ay == 0) return null;
        return (int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), ay, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>Sitenin yazdığı hicrî ay adından (örn. "Cemâzil-evvel", "RAMEZÂN") ay numarasını bulur; tanınmazsa 0.</summary>
    public static int AyNumarasi(string ad)
    {
        string a = ad.ToLower(new CultureInfo("tr-TR")).Replace("â", "a").Replace("î", "i").Replace("û", "u").Replace("ş", "s").Replace("'", "").Replace("’", "");
        if (a.Contains("muharrem")) return 1;
        if (a.Contains("safer")) return 2;
        if (a.StartsWith("rebi")) return a.Contains("evvel") ? 3 : 4;
        if (a.StartsWith("cemazil") || a.StartsWith("cemaziye")) return a.Contains("evvel") ? 5 : 6;
        if (a.Contains("receb")) return 7;
        if (a.StartsWith("sa") && a.Contains("ban")) return 8;
        if (a.Contains("ramez") || a.Contains("ramaz")) return 9;
        if (a.Contains("sevv")) return 10;
        if (a.StartsWith("zil") && (a.Contains("kade") || a.Contains("kaade") || a.Contains("ka"))) return a.Contains("hicce") ? 12 : 11;
        if (a.Contains("hicce")) return 12;
        return 0;
    }
}
