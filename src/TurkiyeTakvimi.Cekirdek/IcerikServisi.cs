using System.Globalization;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>Günün Sözü, Günün Olayı ve Arka Yaprak. Bir kere indirilen gün diske kaydedilir.</summary>
public sealed class IcerikServisi
{
    private readonly TurktakvimIstemci _istemci;
    private readonly DiskOnbellegi _onbellek;

    public IcerikServisi(TurktakvimIstemci istemci, DiskOnbellegi onbellek)
    {
        _istemci = istemci;
        _onbellek = onbellek;
    }

    /// <summary>
    /// Verilen günlerin içeriğini sırayla indirip diske kaydeder (zaten kayıtlı olanları atlar). Siteyi yormamak için
    /// her indirmeden sonra <paramref name="bekleme"/> kadar bekler. Art arda 3 kez internet hatası alırsa ya da art arda
    /// 3 gün boş gelirse (site o tarihten sonrasını yayınlamamıştır) durur. Her gün işlendikçe <paramref name="ilerleme"/> bildirilir.
    /// </summary>
    public async Task<HazirlamaSonucu> GunleriHazirlaAsync(
        IEnumerable<DateOnly> gunler, TimeSpan bekleme, CancellationToken iptal = default,
        IProgress<(int Islenen, int Indirilen)>? ilerleme = null)
    {
        int indirilen = 0, islenen = 0, ardisikHata = 0, ardisikBos = 0;
        var neden = DurmaNedeni.Tamamlandi;

        foreach (var gun in gunler)
        {
            iptal.ThrowIfCancellationRequested();

            string ad = OnbellekAdi(gun);
            if (_onbellek.Oku<GunlukIcerik>(ad)?.Bilgi is not null)
            {
                ilerleme?.Report((++islenen, indirilen));
                continue;
            }

            try
            {
                var ic = await _istemci.GunlukIcerikIndirAsync(gun, iptal).ConfigureAwait(false);
                ardisikHata = 0;

                if (ic.Soz is null && ic.ArkaYaprakBaslik is null)
                {
                    if (++ardisikBos >= 3)
                    {
                        neden = DurmaNedeni.IcerikYok;
                        break;
                    }
                }
                else
                {
                    ardisikBos = 0;
                    _onbellek.Yaz(ad, ic);
                    indirilen++;
                }
            }
            catch (VeriHatasi)
            {
                if (++ardisikHata >= 3)
                {
                    neden = DurmaNedeni.InternetYok;
                    break;
                }
            }

            ilerleme?.Report((++islenen, indirilen));
            await Task.Delay(bekleme, iptal).ConfigureAwait(false);
        }
        return new HazirlamaSonucu(indirilen, islenen, neden);
    }
    private static string OnbellekAdi(DateOnly tarih) =>
        string.Create(CultureInfo.InvariantCulture, $"icerik-{tarih:yyyy-MM-dd}");

    /// <summary>İçerik yoksa ve internet de yoksa <see cref="VeriHatasi"/> fırlatır.</summary>
    public async Task<GunlukIcerik> GetirAsync(DateOnly tarih, CancellationToken iptal = default)
    {
        string ad = OnbellekAdi(tarih);

        // Künye bilgisi sonradan eklendi: onsuz kaydedilmiş eski günler bir kez daha indirilir.
        var kayitli = _onbellek.Oku<GunlukIcerik>(ad);
        if (kayitli?.Bilgi is not null) return kayitli;

        try
        {
            var yeni = await _istemci.GunlukIcerikIndirAsync(tarih, iptal);

            // Boş gelen günü kaydetme: site sonradan doldurabilir.
            if (yeni.Soz is not null || yeni.ArkaYaprakBaslik is not null)
                _onbellek.Yaz(ad, yeni);
            return yeni;
        }
        catch (VeriHatasi) when (kayitli is not null)
        {
            return kayitli; // İnternet yok: bilgisiz de olsa kayıtlı içerik gösterilir.
        }
    }
}
