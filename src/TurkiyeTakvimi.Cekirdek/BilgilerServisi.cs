using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>Bilgiler sayfasındaki bir başlık. Yazi=false ise sayfa PDF/tablo gibi düz yazıya çevrilemez, sitede açılır.</summary>
public sealed record BilgiOgesi(string Baslik, string Adres, bool Yazi);

public sealed record BilgiKategorisi(string Ad, List<BilgiOgesi> Ogeler);

public sealed record BilgiIndeksi(DateTime Tarih, List<BilgiKategorisi> Kategoriler);

/// <summary>Düz yazıya çevrilmiş bilgi yazısı; her paragraf ayrı bir satırdır.</summary>
public sealed record BilgiYazisi(string Baslik, List<string> Paragraflar);

/// <summary>
/// turktakvim.com "Bilgiler" bölümü: kategori ve başlık listesi yılda bir, yazılar açıldıkça indirilir.
/// Yazılar düz metin olarak küçük bir önbellekte tutulur; toplam boyut sınırı aşılırsa en eskiler silinir.
/// </summary>
public sealed class BilgilerServisi
{
    private const string IndeksAdi = "bilgiler-indeks";
    private const string IndeksSayfasi = "/index.php?link=html/bilgiler.php";
    private const long YaziOnbellekSiniri = 5L * 1024 * 1024;
    private const int EnFazlaParagraf = 3000;

    private static readonly CultureInfo Turkce = new("tr-TR");
    private readonly TurktakvimIstemci _istemci;
    private readonly DiskOnbellegi _indeksOnbellegi;
    private readonly DiskOnbellegi _yaziOnbellegi;

    public BilgilerServisi(TurktakvimIstemci istemci, DiskOnbellegi onbellek)
    {
        _istemci = istemci;
        _indeksOnbellegi = onbellek;
        _yaziOnbellegi = new DiskOnbellegi(Path.Combine(onbellek.Klasor, "bilgiler"));
    }

    public async Task<BilgiIndeksi> IndeksiGetirAsync(DateTime simdi, CancellationToken iptal = default)
    {
        var kayit = _indeksOnbellegi.Oku<BilgiIndeksi>(IndeksAdi);
        if (kayit is not null && (simdi - kayit.Tarih).TotalDays < 365) return kayit;

        try
        {
            string html = await _istemci.SayfaAlAsync(TurktakvimIstemci.Site + IndeksSayfasi, iptal);
            var yeni = new BilgiIndeksi(simdi, IndeksiAyristir(html));
            if (yeni.Kategoriler.Count == 0) throw new VeriHatasi("Bilgiler listesi okunamadı.");
            _indeksOnbellegi.Yaz(IndeksAdi, yeni);
            return yeni;
        }
        catch (VeriHatasi) when (kayit is not null)
        {
            return kayit; // yenilenemedi: eski liste kullanılır
        }
    }

    public async Task<BilgiYazisi> YaziGetirAsync(BilgiOgesi oge, CancellationToken iptal = default)
    {
        string ad = DosyaAdi(oge.Adres);
        var kayit = _yaziOnbellegi.Oku<BilgiYazisi>(ad);
        if (kayit is not null) return kayit;

        string html = await _istemci.SayfaAlAsync(oge.Adres, iptal);
        var yazi = YaziyiAyristir(html, oge.Baslik);
        if (yazi.Paragraflar.Count == 0) return yazi; // düz yazı yok: kaydedilmez, arayüz "sitede aç" önerir

        _yaziOnbellegi.Yaz(ad, yazi);
        _yaziOnbellegi.EskileriSil(YaziOnbellekSiniri);
        return yazi;
    }

    private static string DosyaAdi(string adres)
    {
        int i = adres.IndexOf("link=", StringComparison.Ordinal);
        string ham = i >= 0 ? adres[(i + 5)..] : adres;
        return "y-" + Regex.Replace(ham, @"[^A-Za-z0-9]+", "-").Trim('-');
    }

    // ---------- Ayrıştırma ----------

    /// <summary>Bilgiler sayfasından kategorileri ve başlıkları çıkarır (sayfadaki sıraya göre).</summary>
    public static List<BilgiKategorisi> IndeksiAyristir(string html)
    {
        int bas = html.IndexOf("class=\"style3\"", StringComparison.Ordinal);
        if (bas < 0) return new List<BilgiKategorisi>();
        bas = html.LastIndexOf("<div", bas, StringComparison.Ordinal);
        int son = html.IndexOf("<!-- Div Wrapper Element ends", bas, StringComparison.Ordinal);
        string govde = son > bas ? html[bas..son] : html[bas..];

        var kategoriler = new List<BilgiKategorisi>();
        BilgiKategorisi? gecerli = null;
        var temel = new Uri(TurktakvimIstemci.Site + "/index.php");

        foreach (Match m in Regex.Matches(govde,
                     @"<div[^>]*class=""style3""[^>]*>(?<kat>.*?)</div>|<div[^>]*class=""style1""[^>]*>(?<mek>.*?)</div>|<strong>(?<alt>[^<]*)</strong>|<a\s[^>]*href=['""](?<href>[^'""]+)['""][^>]*>(?<ad>.*?)</a>",
                     RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            if (m.Groups["kat"].Success)
            {
                gecerli = new BilgiKategorisi(Temizle(m.Groups["kat"].Value), new List<BilgiOgesi>());
                kategoriler.Add(gecerli);
            }
            else if (m.Groups["mek"].Success)
            {
                gecerli = new BilgiKategorisi("Mektûbât Tercemesi", new List<BilgiOgesi>());
                kategoriler.Add(gecerli);
            }
            else if (m.Groups["alt"].Success)
            {
                string alt = Temizle(m.Groups["alt"].Value);
                if (Anahtar(alt).StartsWith("diger kiymetli", StringComparison.Ordinal))
                {
                    gecerli = new BilgiKategorisi("Diğer Kıymetli Düâlar", new List<BilgiOgesi>());
                    kategoriler.Add(gecerli);
                }
            }
            else if (gecerli is not null)
            {
                // Listeden sonra sayfa altı başlar (kongre duyuruları, dil bağlantıları): burada bitirilir.
                if (m.Value.Contains("class=\"kongre\"", StringComparison.OrdinalIgnoreCase) ||
                    m.Value.Contains("class=\"hilal\"", StringComparison.OrdinalIgnoreCase) ||
                    m.Groups["href"].Value.Contains("HilalKongre", StringComparison.OrdinalIgnoreCase))
                    break;

                string baslik = Temizle(m.Groups["ad"].Value);
                if (baslik.Length == 0) continue;
                if (baslik.Any(char.IsLetter) && baslik == baslik.ToUpper(Turkce)) baslik = BuyukHarftenCevir(baslik);

                string href = WebUtility.HtmlDecode(m.Groups["href"].Value);
                if (!Uri.TryCreate(temel, href, out var adres)) continue;
                bool yazi = adres.AbsolutePath.EndsWith("index.php", StringComparison.OrdinalIgnoreCase) &&
                            adres.Query.Contains("link=html/", StringComparison.Ordinal) &&
                            !adres.Query.Contains(".pdf", StringComparison.OrdinalIgnoreCase);
                if (gecerli.Ogeler.Any(o => o.Adres == adres.AbsoluteUri)) continue;
                gecerli.Ogeler.Add(new BilgiOgesi(baslik, adres.AbsoluteUri, yazi));
            }
        }
        return kategoriler.Where(k => k.Ogeler.Count > 0).ToList();
    }

    /// <summary>Menü eşleştirmesi için: küçük harf, aksansız, ı→i.</summary>
    public static string Anahtar(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c == 'ı' ? 'i' : char.ToLowerInvariant(c));
        }
        return sb.ToString().Trim();
    }

    private static string BuyukHarftenCevir(string s) =>
        string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.ToLower(Turkce)).Select(k => k == "ve" ? k : char.ToUpper(k[0], Turkce) + k[1..]));

    private static string Temizle(string html) =>
        GorunmezKarakterler.At(Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", "")).Replace(' ', ' '), @"\s+", " ")).Trim();

    /// <summary>Yazı sayfasının gövdesini paragraf paragraf düz metne çevirir.</summary>
    public static BilgiYazisi YaziyiAyristir(string html, string varsayilanBaslik)
    {
        int bas = html.IndexOf("<div id=\"Wrapper\">", StringComparison.Ordinal);
        string icerik = bas >= 0 ? html[bas..] : html;
        int ic = icerik.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (ic >= 0) icerik = icerik[ic..];
        // Yazı, sitenin sayfa altı (kongre duyuruları, dil bağlantıları) başlamadan biter.
        foreach (string bitis in new[] { "<div id=\"dokumanlar_responsive\"", "<!-- Div Wrapper Element ends", "</body>" })
        {
            int son = icerik.IndexOf(bitis, StringComparison.OrdinalIgnoreCase);
            if (son >= 0) icerik = icerik[..son];
        }

        icerik = Regex.Replace(icerik, @"<!--.*?-->|<script.*?</script>|<style.*?</style>|<audio.*?</audio>|<video.*?</video>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        icerik = Regex.Replace(icerik, @"<a[^>]*class=""(hilal|kongre)""[^>]*>.*?</a>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        icerik = Regex.Replace(icerik, @"\s+", " ");
        icerik = Regex.Replace(icerik, @"<li[^>]*>", "\n• ", RegexOptions.IgnoreCase);
        icerik = Regex.Replace(icerik, @"<br\s*/?>|</p>|</li>|</tr>|</h\d>|</div>|</ol>|</ul>|</table>|<p[ >]|<tr[ >]|<h\d[ >]", "\n$0", RegexOptions.IgnoreCase);
        icerik = Regex.Replace(icerik, @"</t[dh]>", "; ", RegexOptions.IgnoreCase);
        string metin = GorunmezKarakterler.At(WebUtility.HtmlDecode(Regex.Replace(icerik, "<[^>]+>", "")).Replace(' ', ' '));

        var paragraflar = new List<string>();
        foreach (string satir in metin.Split('\n'))
        {
            string p = Regex.Replace(satir, @"\s+", " ").Trim().Trim(';').Trim();
            if (p.Length == 0 || p == "•") continue;
            paragraflar.Add(p);
            if (paragraflar.Count >= EnFazlaParagraf) break;
        }

        // Başlık olarak listedeki yazım kullanılır (sayfadakinde şapkalar eksik olabiliyor).
        string baslik = varsayilanBaslik;
        if (paragraflar.Count > 0 && Anahtar(paragraflar[0]) == Anahtar(baslik)) paragraflar.RemoveAt(0);

        // Bir iki kelimelik kırıntı (resim/tablo sayfası) yazı sayılmaz.
        if (paragraflar.Sum(p => p.Length) < 40) paragraflar.Clear();
        // Çoğu satırı birkaç karakterlik hücre olan sayfalar tablodur; düz yazı olarak okunmaz.
        else if (paragraflar.Count(p => p.Length <= 4) * 100 > paragraflar.Count * 35) paragraflar.Clear();
        return new BilgiYazisi(baslik, paragraflar);
    }
}
