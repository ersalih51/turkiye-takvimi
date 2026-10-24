using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Listeden panoya kopyalanabilen öğe.</summary>
public interface IKopyalanabilir
{
    string Kopya { get; }
}

/// <summary>Listede gösterilen bir vakit. <see cref="ToString"/> ekran okuyucunun okuyacağı metindir.</summary>
public sealed class VakitSatiri : IKopyalanabilir
{
    private readonly VakitSaati _vakit;

    /// <param name="hucre">Sitedeki hücre metni: "05:41", "*23:55" veya "-".</param>
    private readonly string? _gorunen;
    private readonly string? _okunan;

    /// <param name="yarin">Yarının vakti (gün bittikten sonra sıradaki imsak): satırda "Yarın" rozeti görünür, "yarın" okunan cümlenin sonunda söylenir.</param>
    /// <param name="vakitNo">Vakit numarası (tek seferlik bildirim için); Ezânî ve bilgi satırlarında -1.</param>
    /// <param name="bildirilecek">Bu vakit için "bir kez bildir" kurulu: satırda "Bildirim" rozeti görünür.</param>
    public VakitSatiri(string ad, string hucre, bool siradaki, bool gecti = false, bool yarin = false, int vakitNo = -1, bool bildirilecek = false)
    {
        VakitNo = vakitNo;
        Bildirilecek = bildirilecek;
        Yarin = yarin;
        Ad = ad;
        _vakit = VakitSaati.Ayristir(hucre);
        Siradaki = siradaki;
        Gecti = gecti;
    }

    private VakitSatiri(string ad, string gorunen, string okunan)
    {
        Ad = ad;
        _gorunen = gorunen;
        _okunan = okunan;
    }

    /// <summary>Saat olmayan bilgi satırı (örn. kıble yönü): ekranda kısa metin, ekran okuyucuda uzun metin.</summary>
    public static VakitSatiri Bilgi(string ad, string gorunen, string okunan) => new(ad, gorunen, okunan);

    public string Ad { get; }
    public string Saat => _gorunen ?? _vakit.Gorunen;
    public bool Siradaki { get; }
    public bool Gecti { get; }
    public bool Yarin { get; }
    public int VakitNo { get; }
    public bool Bildirilecek { get; }

    private string Okunan => _okunan ?? _vakit.Okunan;

    public string Kopya => $"{Ad}: {Okunan}";

    public override string ToString() =>
        $"{Ad}, {Okunan}" + (Siradaki ? ", sıradaki vakit" : "") + (Bildirilecek ? ", bir kez bildirilecek" : "") + (Yarin ? ", yarın" : "");
}

/// <summary>Haftalık görünümdeki bir gün: ana vakitler yan yana kartta, ekran okuyucuya tek cümle olarak okunur.</summary>
public sealed class GunSatiri : IKopyalanabilir
{
    public sealed record Hucre(string Ad, string Saat);

    private static readonly System.Globalization.CultureInfo Tr = new("tr-TR");
    private readonly List<(string Ad, VakitSaati Vakit)> _vakitler;

    private GunSatiri(string baslik)
    {
        Baslik = baslik;
        _vakitler = new();
        Hucreler = new();
    }

    /// <summary>Vakit yerine kısa bir bilgi/hata metni gösteren satır.</summary>
    public static GunSatiri Not(string metin) => new(metin);

    public GunSatiri(DateOnly gun, bool bugun, List<(string Ad, string Hucre)> vakitler)
    {
        Bugun = bugun;
        // Başka bir yıl seçildiyse yıl da söylenir (örn. "23 Ekim 1990 Salı").
        Baslik = gun.ToString(gun.Year == DateTime.Today.Year ? "d MMMM dddd" : "d MMMM yyyy dddd", Tr);
        _vakitler = vakitler.Select(v => (v.Ad, VakitSaati.Ayristir(v.Hucre))).ToList();
        Hucreler = _vakitler.Select(v => new Hucre(v.Ad, v.Vakit.Gorunen)).ToList();
    }

    public string Baslik { get; }
    public bool Bugun { get; }
    public List<Hucre> Hucreler { get; }

    public string Kopya => _vakitler.Count == 0 ? Baslik : $"{Baslik}: " + string.Join(", ", _vakitler.Select(v => $"{v.Ad} {v.Vakit.Okunan}"));

    public override string ToString() => Bugun ? Kopya.Replace(": ", ", bugün: ") : Kopya;
}

/// <summary>
/// İmsakiye satırı: Ramazan'ın günü, tarih ve sekiz vakit. Ekran okuyucuya tek cümle olarak okunur.
/// </summary>
public sealed class ImsakiyeSatiri : IKopyalanabilir
{
    public sealed record Hucre(string Saat, bool Vurgulu);

    private static readonly System.Globalization.CultureInfo Tr = new("tr-TR");
    private static readonly string[] Adlar = { "İmsak", "Sabah", "Güneş", "İşrak", "Öğle", "İkindi", "Akşam iftar", "Yatsı" };
    private readonly List<VakitSaati> _vakitler;
    private readonly string _ek;

    /// <param name="kadirGecesi">Bu günün akşamı Kadir Gecesi'dir (26. günü 27. güne bağlayan gece).</param>
    public ImsakiyeSatiri(ImsakiyeGunu gun, bool bugun, bool kadirGecesi)
    {
        Gun = gun.Gun;
        Bugun = bugun;
        KadirGecesi = kadirGecesi;
        TarihMetni = gun.Tarih.ToString("d MMMM", Tr);
        HaftaGunu = gun.Tarih.ToString("dddd", Tr);
        _vakitler = gun.Saatler.Select(VakitSaati.Ayristir).ToList();
        // İmsak (oruç başlar) ve Akşam (iftar) sütunları vurgulanır.
        Hucreler = _vakitler.Select((v, i) => new Hucre(v.Gorunen, i is 0 or 6)).ToList();
        _ek = (bugun ? ", bugün" : "") + (kadirGecesi ? ", akşam Kadir Gecesi" : "");
    }

    public int Gun { get; }
    public bool Bugun { get; }
    public bool KadirGecesi { get; }
    public string TarihMetni { get; }
    public string HaftaGunu { get; }
    public List<Hucre> Hucreler { get; }

    public string Kopya =>
        $"Ramazan {Gun}. gün, {TarihMetni} {HaftaGunu}: " +
        string.Join(", ", _vakitler.Select((v, i) => $"{Adlar[i]} {v.Okunan}"));

    public override string ToString() =>
        $"Ramazan {Gun}. gün, {TarihMetni} {HaftaGunu}{_ek}: " +
        string.Join(", ", _vakitler.Select((v, i) => $"{Adlar[i]} {v.Okunan}"));
}
/// <summary>
/// Aylık çizelgenin bir günü: ayın günü, tarih, hafta günü ve yedi ana vakit. Ekran okuyucuya tek cümle olarak okunur.
/// </summary>
public sealed class AylikSatir : IKopyalanabilir
{
    public sealed record Hucre(string Saat);

    public static readonly string[] Adlar = { "İmsak", "Sabah", "Güneş", "Öğle", "İkindi", "Akşam", "Yatsı" };
    private static readonly System.Globalization.CultureInfo Tr = new("tr-TR");
    private readonly List<VakitSaati> _vakitler;

    public AylikSatir(DateOnly tarih, string[] saatler, bool bugun)
    {
        Tarih = tarih;
        Bugun = bugun;
        Gun = tarih.Day;
        TarihMetni = tarih.ToString("d MMMM", Tr);
        HaftaGunu = tarih.ToString("dddd", Tr);
        Cuma = tarih.DayOfWeek == DayOfWeek.Friday;
        _vakitler = saatler.Select(VakitSaati.Ayristir).ToList();
        Hucreler = _vakitler.Select(v => new Hucre(v.Gorunen)).ToList();
    }

    public DateOnly Tarih { get; }
    public int Gun { get; }
    public bool Bugun { get; }
    public bool Cuma { get; }
    public string TarihMetni { get; }
    public string HaftaGunu { get; }
    public List<Hucre> Hucreler { get; }
    public string[] Gorunenler => _vakitler.Select(v => v.Gorunen).ToArray();

    public string Kopya =>
        $"{TarihMetni} {HaftaGunu}: " + string.Join(", ", _vakitler.Select((v, i) => $"{Adlar[i]} {v.Okunan}"));

    public override string ToString() => Bugun ? Kopya.Replace(": ", ", bugün: ") : Kopya;
}