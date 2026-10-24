using System.Globalization;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>Vakitleri önce diskteki yıllık önbellekten verir; yoksa yılı bir kerede indirip kaydeder.</summary>
public sealed class VakitServisi
{
    private readonly TurktakvimIstemci _istemci;
    private readonly DiskOnbellegi _onbellek;

    public VakitServisi(TurktakvimIstemci istemci, DiskOnbellegi onbellek)
    {
        _istemci = istemci;
        _onbellek = onbellek;
    }

    public async Task<VakitSonucu> GunuGetirAsync(string sehirKodu, DateOnly tarih, CancellationToken iptal = default)
    {
        var yil = _onbellek.Oku<YillikVakitler>(OnbellekAdi(sehirKodu, tarih.Year));
        var kaynak = VeriKaynagi.Onbellek;

        if (yil is null || Bul(yil, tarih) is null)
        {
            yil = await _istemci.YillikVakitleriIndirAsync(sehirKodu, tarih.Year, iptal);
            _onbellek.Yaz(OnbellekAdi(sehirKodu, tarih.Year), yil);
            kaynak = VeriKaynagi.Internet;
        }

        var gun = Bul(yil, tarih)
                  ?? throw new VeriHatasi($"{tarih:dd.MM.yyyy} için vakit bulunamadı.");
        return new VakitSonucu(yil.SehirAdi, gun, kaynak);
    }

    /// <summary>Bir yılı indirip diske yazar (zaten varsa atlar). Önbellek bir yıl ileriyi hazır tutmak için kullanılır.</summary>
    public async Task<bool> YiliHazirlaAsync(string sehirKodu, int yil, bool zorla = false, CancellationToken iptal = default)
    {
        string ad = OnbellekAdi(sehirKodu, yil);
        if (!zorla && _onbellek.Oku<YillikVakitler>(ad) is not null) return false;

        var veri = await _istemci.YillikVakitleriIndirAsync(sehirKodu, yil, iptal);
        _onbellek.Yaz(ad, veri);
        return true;
    }

    /// <summary>İçinde bulunulan yılı ve (Kasım'dan itibaren) gelecek yılı hazırlar. Hata verirse sessizce false döner.</summary>
    public async Task<bool> ArkaPlandaHazirlaAsync(string sehirKodu, DateOnly bugun, CancellationToken iptal = default)
    {
        try
        {
            bool yeni = await YiliHazirlaAsync(sehirKodu, bugun.Year, iptal: iptal);
            if (bugun.Month >= 11)
                yeni |= await YiliHazirlaAsync(sehirKodu, bugun.Year + 1, iptal: iptal);
            return yeni;
        }
        catch (VeriHatasi)
        {
            return false;
        }
    }

    public const int SaklananSehirSayisi = 4;

    /// <summary>
    /// Şehir kullanıma alınınca çağrılır: <paramref name="sehirKodu"/> son kullanılanların başına geçer, listede
    /// <see cref="SaklananSehirSayisi"/> şehirden fazlası kalırsa en eskilerin vakit dosyaları diskten silinir.
    /// Güncel liste döndürülür.
    /// </summary>
    public List<string> SehirleriDuzenle(IEnumerable<string> sonSehirler, string sehirKodu)
    {
        var liste = sonSehirler.Where(k => k != sehirKodu).Prepend(sehirKodu).Take(SaklananSehirSayisi).ToList();
        _onbellek.Sil(ad =>
            (ad.StartsWith("vakit-", StringComparison.Ordinal) &&
             !liste.Any(k => ad.StartsWith($"vakit-{k}-", StringComparison.Ordinal))) ||
            (ad.StartsWith("sehirzaman-", StringComparison.Ordinal) &&
             !liste.Contains(ad["sehirzaman-".Length..])));
        return liste;
    }

    private static GunlukVakit? Bul(YillikVakitler yil, DateOnly tarih) =>
        yil.Gunler.FirstOrDefault(g => g.Tarih == tarih);

    private static string OnbellekAdi(string sehirKodu, int yil) =>
        string.Create(CultureInfo.InvariantCulture, $"vakit-{sehirKodu}-{yil}");
}
