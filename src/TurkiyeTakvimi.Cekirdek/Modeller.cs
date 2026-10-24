namespace TurkiyeTakvimi.Cekirdek;

public sealed record Sehir(string Kod, string Ad, string Ulke)
{
    /// <summary>
    /// Sitenin "Ad-Bölge" yazımını okunur hâle getirir: "Niğde-Niğde" → "Niğde", "Bor-Niğde" → "Bor, Niğde".
    /// (Ekran okuyucu tireyi "tire" diye okurdu.)
    /// </summary>
    public string GorunenAd
    {
        get
        {
            int tire = Ad.LastIndexOf('-');
            if (tire <= 0 || tire == Ad.Length - 1) return Ad;

            string ad = Ad[..tire].Trim(), bolge = Ad[(tire + 1)..].Trim();
            return string.Equals(ad, bolge, StringComparison.CurrentCultureIgnoreCase) ? ad : $"{ad}, {bolge}";
        }
    }
}

/// <summary>Bir günün 18 vakti, <see cref="VakitAdlari.Hepsi"/> sırasında "SS:dd" metni olarak.</summary>
public sealed record GunlukVakit(DateOnly Tarih, string[] Saatler);

public sealed record YillikVakitler(string SehirKodu, string SehirAdi, int Yil, List<GunlukVakit> Gunler);

/// <summary>
/// Günün künye bilgileri: Hicrî tarih, Hicrî Şemsî / Rûmî / Hızır (veya Kasım) sayısı, yılın kaçıncı günü,
/// ay-gün-hafta ve gündüzün uzama/kısalma durumu. <see cref="Satirlar"/> ekranda gösterilen hazır satırlardır.
/// </summary>
public sealed record GunBilgisi(string HicriTarih, IReadOnlyList<string> Satirlar);

public sealed record GunlukIcerik(
    DateOnly Tarih,
    string? Soz,
    IReadOnlyList<string> Olaylar,
    string? ArkaYaprakBaslik,
    string? ArkaYaprakMetin,
    string? GununEki,
    GunBilgisi? Bilgi = null);

/// <summary>Toplu içerik indirmesi neden bitti.</summary>
public enum DurmaNedeni
{
    Tamamlandi,
    InternetYok,
    IcerikYok
}

/// <summary>Toplu içerik indirmesinin sonucu: kaç gün işlendi (kayıtlı olduğu için atlananlar dahil), kaçı yeni indirildi.</summary>
public sealed record HazirlamaSonucu(int Indirilen, int Islenen, DurmaNedeni Neden);

public enum VeriKaynagi
{
    Onbellek,
    Internet
}

/// <summary>Sitenin günlük servisinden alınan şehir bilgisi: şehrin yerel saati ve koordinatları (kuzey/doğu pozitif).</summary>
public sealed record SehirAnligi(DateTime Yerel, double? Enlem, double? Boylam);

/// <summary>Ezânî (alaturka saat) bir vakit: ad ve sitenin yazdığı saat (örn. "İmsâk", "11:14").</summary>
public sealed record EzaniSatiri(string Ad, string Saat);

public sealed record VakitSonucu(string SehirAdi, GunlukVakit Gun, VeriKaynagi Kaynak);

/// <summary>Veri alınamadığında veya beklenmeyen biçimde geldiğinde fırlatılır.</summary>
public class VeriHatasi : Exception
{
    public VeriHatasi(string mesaj, Exception? ic = null) : base(mesaj, ic) { }
}

/// <summary>Site bağlantı kurulduğu halde o yer için vakit üretemediğinde (örn. kutba yakın yerlerde sunucu hatası) fırlatılır.</summary>
public sealed class YerIcinVeriYok : VeriHatasi
{
    public YerIcinVeriYok(Exception? ic = null) : base("turktakvim.com bu yer için vakit veremiyor.", ic) { }
}
