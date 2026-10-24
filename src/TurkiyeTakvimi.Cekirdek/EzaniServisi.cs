namespace TurkiyeTakvimi.Cekirdek;

/// <summary>Günün Ezânî (alaturka) vakitlerini diskten, yoksa siteden verir. Yalnızca seçili şehrin bugünkü günü tutulur.</summary>
public sealed class EzaniServisi
{
    private readonly TurktakvimIstemci _istemci;
    private readonly DiskOnbellegi _onbellek;

    public EzaniServisi(TurktakvimIstemci istemci, DiskOnbellegi onbellek)
    {
        _istemci = istemci;
        _onbellek = onbellek;
    }

    private static string Ad(string sehirKodu, DateOnly gun) => $"ezani-{sehirKodu}-{gun:yyyy-MM-dd}";

    /// <summary>Önbellekte yoksa siteden alır ve kaydeder (eski günlerin ve başka şehirlerin kayıtları silinir). İnternet yoksa VeriHatasi.</summary>
    public async Task<List<EzaniSatiri>> GetirAsync(string sehirKodu, DateOnly gun, CancellationToken iptal = default)
    {
        var kayitli = _onbellek.Oku<List<EzaniSatiri>>(Ad(sehirKodu, gun));
        if (kayitli is { Count: > 0 }) return kayitli;

        var yeni = await _istemci.EzaniVakitleriAsync(sehirKodu, gun, iptal);
        string simdiki = Ad(sehirKodu, gun);
        _onbellek.Sil(ad => ad.StartsWith("ezani-", StringComparison.Ordinal) && ad != simdiki);
        _onbellek.Yaz(simdiki, yeni);
        return yeni;
    }

    /// <summary>Yalnızca diskteki kayıt; internete gitmez.</summary>
    public List<EzaniSatiri>? Kayitli(string sehirKodu, DateOnly gun) =>
        _onbellek.Oku<List<EzaniSatiri>>(Ad(sehirKodu, gun)) is { Count: > 0 } k ? k : null;
}
