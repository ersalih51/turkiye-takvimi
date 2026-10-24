using System.Globalization;
using System.Text.RegularExpressions;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>Bir Ramazan'ın başlangıcı ve uzunluğu (29 ya da 30 gün); turktakvim.com'un hicrî tarihlerinden bulunur.</summary>
public sealed record RamazanAraligi(DateOnly Baslangic, int GunSayisi, int HicriYil)
{
    public DateOnly Bitis => Baslangic.AddDays(GunSayisi - 1);
    public DateOnly BayramGunu => Baslangic.AddDays(GunSayisi);

    /// <summary>Başlangıç ve bitişin siteden en son ne zaman yeniden doğrulandığı (Ramazan bitene dek günde bir kez).</summary>
    public DateOnly? SonDogrulama { get; init; }
}

/// <summary>İmsakiyenin bir günü: Ramazan'ın kaçıncı günü, miladî tarih ve sekiz vakit (sitedeki hücre metinleriyle).</summary>
public sealed record ImsakiyeGunu(int Gun, DateOnly Tarih, string[] Saatler);

public sealed record Imsakiye(
    string SehirAdi, int MiladiYil, RamazanAraligi Aralik, List<ImsakiyeGunu> Gunler, string? BayramGunuGunes);

/// <summary>
/// İmsakiye: Ramazan günlerini ve her günün İmsak, Sabah, Güneş, İşrak, Öğle, İkindi, Akşam, Yatsı vakitlerini verir.
/// Ramazan günleri sitenin hicrî tarihinden, vakitler sitenin yıllık vakit tablosundan alınır. Site yeni yılın hicrî
/// tarihlerini yıl başında yayımladığı için, hangi yılın Ramazan'ı sitede varsa o gösterilir.
/// </summary>
public sealed class ImsakiyeServisi
{
    /// <summary>İmsakiyede gösterilen vakitler (<see cref="VakitAdlari.Hepsi"/> sırası): İmsak, Sabah, Güneş, İşrak, Öğle, İkindi, Akşam, Yatsı.</summary>
    public static readonly int[] Vakitler = { 0, 1, 2, 3, 6, 7, 10, 12 };

    private enum HicriAy { Once, Ramazan, Sonra }

    private readonly TurktakvimIstemci _istemci;
    private readonly IcerikServisi _icerik;
    private readonly VakitServisi _vakitler;
    private readonly DiskOnbellegi _onbellek;

    public ImsakiyeServisi(TurktakvimIstemci istemci, IcerikServisi icerik, VakitServisi vakitler, DiskOnbellegi onbellek)
    {
        _istemci = istemci;
        _icerik = icerik;
        _vakitler = vakitler;
        _onbellek = onbellek;
    }

    /// <summary>
    /// Gösterilecek Ramazan'ın imsakiyesi: bu yılki Ramazan sürüyor ya da gelecekse o; bittiyse ve sitede gelecek yılın
    /// hicrî tarihleri yayımlanmışsa gelecek yılınki; değilse bu yılınki (geçmiş). Site verisi yoksa null.
    /// </summary>
    public async Task<Imsakiye?> HazirlaAsync(string sehirKodu, string sehirAdi, DateOnly bugun, CancellationToken iptal = default)
    {
        var bu = await AraligiBulAsync(bugun.Year, bugun, iptal);
        RamazanAraligi? secilen = bu;
        if (bu is null || bu.Bitis < bugun)
        {
            RamazanAraligi? sonraki = null;
            try
            {
                sonraki = await AraligiBulAsync(bugun.Year + 1, bugun, iptal);
            }
            catch (VeriHatasi)
            {
                // Gelecek yılın verisi henüz yayımlanmamış (ya da alınamadı): bu yılınki gösterilir.
            }
            secilen = sonraki ?? bu;
        }
        if (secilen is null) return null;

        var gunler = new List<ImsakiyeGunu>();
        for (int i = 0; i < secilen.GunSayisi; i++)
        {
            var tarih = secilen.Baslangic.AddDays(i);
            var sonuc = await _vakitler.GunuGetirAsync(sehirKodu, tarih, iptal);
            gunler.Add(new ImsakiyeGunu(i + 1, tarih, Vakitler.Select(v => sonuc.Gun.Saatler[v]).ToArray()));
        }

        string? bayramGunes = null;
        try
        {
            var bayram = await _vakitler.GunuGetirAsync(sehirKodu, secilen.BayramGunu, iptal);
            bayramGunes = bayram.Gun.Saatler[2];
        }
        catch (VeriHatasi)
        {
            // Bayram günü başka bir yılın tablosundaysa ve indirilemediyse yalnızca tarih gösterilir.
        }

        return new Imsakiye(sehirAdi, secilen.Baslangic.Year, secilen, gunler, bayramGunes);
    }

    /// <summary>Verilen miladî yıldaki Ramazan'ı bulur; sitede o yılın hicrî tarihleri yoksa null. Bulunan aralık kalıcı saklanır.</summary>
    public async Task<RamazanAraligi?> AraligiBulAsync(int yil, DateOnly bugun, CancellationToken iptal = default)
    {
        string ad = $"ramazan-{yil}";
        bool taze = false;
        if (_onbellek.Oku<RamazanAraligi>(ad) is { } kayitli)
        {
            // Biten Ramazan'ın günleri değişmez; bugün zaten doğrulandıysa da yeniden bakılmaz.
            if (kayitli.Bitis < bugun || kayitli.SonDogrulama == bugun) return kayitli;

            // Ramazan henüz bitmedi: site hicrî tarihi düzeltmiş olabilir; günde bir kez siteden yeniden doğrula.
            try
            {
                if (await DogrulaAsync(kayitli, iptal))
                {
                    var yeni = kayitli with { SonDogrulama = bugun };
                    _onbellek.Yaz(ad, yeni);
                    return yeni;
                }
            }
            catch (VeriHatasi)
            {
                return kayitli; // internet yok: kayıtlı tarihlerle devam
            }

            // Değişmiş: kaydı sil, siteden (önbelleği atlayarak) yeniden hesapla.
            _onbellek.Sil(a => a == ad);
            taze = true;
        }

        DateOnly? tahmin = Tahmin(yil);
        if (tahmin is null) return null;

        // Tahmin birkaç gün şaşabilir; sitenin hicrî tarihine bakarak doğru başlangıca yaklaş.
        var p = tahmin.Value;
        DateOnly? baslangic = null;
        for (int i = 0; i < 8 && baslangic is null; i++)
        {
            var d = await HicriDurumAsync(p, iptal, taze);
            if (d is null) return null;
            switch (d.Value.Ay)
            {
                case HicriAy.Ramazan:
                    baslangic = p.AddDays(-(d.Value.Gun - 1));
                    break;
                case HicriAy.Once:
                    p = p.AddDays(Math.Max(1, 29 - d.Value.Gun));
                    break;
                default:
                    p = p.AddDays(-d.Value.Gun - 10);
                    break;
            }
        }
        if (baslangic is null) return null;

        // Ramazan 29 ya da 30 gün sürer: 30. günde hâlâ Ramazan ise 30, Şevvâl başlamışsa 29.
        var son = await HicriDurumAsync(baslangic.Value.AddDays(29), iptal, taze);
        if (son is null) return null;
        int gunSayisi = son.Value.Ay == HicriAy.Ramazan ? 30 : 29;

        var hicriYil = (await HicriDurumAsync(baslangic.Value, iptal, taze))?.Yil ?? 0;
        var aralik = new RamazanAraligi(baslangic.Value, gunSayisi, hicriYil) { SonDogrulama = bugun };
        _onbellek.Yaz(ad, aralik);
        return aralik;
    }

    /// <summary>
    /// Kayıtlı Ramazan aralığını siteden (önbelleği atlayarak) yeniden doğrular: Şa'bân'ın son günü, Ramazan'ın 1. günü ve
    /// Şevvâl'in 1. günü kayıtla uyuşmalı. Site o günler için hicrî tarih vermiyorsa doğrulanamadığı için kayıt geçerli sayılır.
    /// </summary>
    private async Task<bool> DogrulaAsync(RamazanAraligi k, CancellationToken iptal)
    {
        var onceki = await HicriDurumAsync(k.Baslangic.AddDays(-1), iptal, taze: true);
        var ilk = await HicriDurumAsync(k.Baslangic, iptal, taze: true);
        var bayram = await HicriDurumAsync(k.BayramGunu, iptal, taze: true);

        if (onceki is { Ay: not HicriAy.Once }) return false;
        if (ilk is { } i && (i.Ay != HicriAy.Ramazan || i.Gun != 1)) return false;
        if (bayram is { } b && (b.Ay != HicriAy.Sonra || b.Gun != 1)) return false;
        return true;
    }

    /// <summary>Hicrî takvime göre (Ümmü'l-Kurâ) Ramazan'ın yaklaşık başlangıcı; yalnızca arama başlangıcı olarak kullanılır.</summary>
    private static DateOnly? Tahmin(int miladiYil)
    {
        var takvim = new UmAlQuraCalendar();
        for (int h = miladiYil - 581; h <= miladiYil - 577; h++)
        {
            try
            {
                var d = takvim.ToDateTime(h, 9, 1, 12, 0, 0, 0);
                if (d.Year == miladiYil) return DateOnly.FromDateTime(d);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
        return null;
    }

    /// <param name="taze">true ise önbelleği atlayıp siteden indirir.</param>
    private async Task<(HicriAy Ay, int Gun, int Yil)?> HicriDurumAsync(DateOnly tarih, CancellationToken iptal, bool taze = false)
    {
        var icerik = taze ? await _istemci.GunlukIcerikIndirAsync(tarih, iptal) : await _icerik.GetirAsync(tarih, iptal);
        string? hicri = icerik.Bilgi?.HicriTarih;
        if (string.IsNullOrWhiteSpace(hicri)) return null;

        var m = Regex.Match(hicri.Trim(), @"^(\d+)\s+(\S+)\s+(\d{4})$");
        if (!m.Success) return null;

        string ay = m.Groups[2].Value.ToLower(new CultureInfo("tr-TR"));
        HicriAy durum =
            ay.Contains("ramez") || ay.Contains("ramaz") ? HicriAy.Ramazan
            : ay.Contains("şa") && ay.Contains("bân") ? HicriAy.Once
            : ay.Contains("şevv") ? HicriAy.Sonra
            : HicriAy.Once;
        return (durum, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
    }
}
