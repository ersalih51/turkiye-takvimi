namespace TurkiyeTakvimi.Cekirdek;

public sealed class SehirZamani
{
    /// <summary>Şehrin yerel saati ile UTC arasındaki fark (dakika). Yaz saatini içerir; 15 dakikaya yuvarlanır.</summary>
    public int FarkDakika { get; set; }

    public DateTime Guncelleme { get; set; }

    /// <summary>Şehrin koordinatları (kuzey/doğu pozitif); kıble yönü için. Eski kayıtlarda yoktur.</summary>
    public double? Enlem { get; set; }
    public double? Boylam { get; set; }
}

/// <summary>
/// Şehrin saat farkını öğrenir ve diske kaydeder. Vakitler şehrin yerel saatiyle yazılır; bilgisayarın saat dilimi
/// farklıysa (örn. Türkiye'deki bir bilgisayarda Berlin seçili) geri sayım şehir saatine göre yapılmalıdır.
/// </summary>
public sealed class SehirZamanServisi
{
    private readonly TurktakvimIstemci _istemci;
    private readonly DiskOnbellegi _onbellek;

    public SehirZamanServisi(TurktakvimIstemci istemci, DiskOnbellegi onbellek)
    {
        _istemci = istemci;
        _onbellek = onbellek;
    }

    public static string OnbellekAdi(string sehirKodu) => "sehirzaman-" + sehirKodu;

    /// <summary>Daha önce öğrenilmiş saat farkı; yoksa null (o zaman bilgisayar saati kullanılır).</summary>
    public TimeSpan? Kayitli(string sehirKodu)
    {
        var z = _onbellek.Oku<SehirZamani>(OnbellekAdi(sehirKodu));
        return z is null ? null : TimeSpan.FromMinutes(z.FarkDakika);
    }

    /// <summary>Daha önce öğrenilmiş koordinatlar; yoksa null.</summary>
    public (double Enlem, double Boylam)? KayitliKonum(string sehirKodu)
    {
        var z = _onbellek.Oku<SehirZamani>(OnbellekAdi(sehirKodu));
        return z is { Enlem: { } e, Boylam: { } b } ? (e, b) : null;
    }

    /// <summary>Siteden güncel farkı alıp kaydeder. İnternet yoksa ya da veri okunamazsa null döner.</summary>
    public async Task<TimeSpan?> GuncelleAsync(string sehirKodu, CancellationToken iptal = default)
    {
        try
        {
            var anlik = await _istemci.SehirAnligiAsync(sehirKodu, iptal);
            if (anlik is null) return null;

            double dakika = Math.Round((anlik.Yerel - DateTime.UtcNow).TotalMinutes / 15.0) * 15.0;
            _onbellek.Yaz(OnbellekAdi(sehirKodu), new SehirZamani
            {
                FarkDakika = (int)dakika, Guncelleme = DateTime.UtcNow, Enlem = anlik.Enlem, Boylam = anlik.Boylam
            });
            return TimeSpan.FromMinutes(dakika);
        }
        catch (VeriHatasi)
        {
            return null;
        }
    }
}
