using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>turktakvim.com ile konuşur; sayfaları ayrıştırır. Önbellek bilmez.</summary>
public sealed class TurktakvimIstemci
{
    private const string GadgetAdresi = "http://gadget.turktakvim.com/gadget.php";
    private const string SiteAdresi = "https://www.turktakvim.com";
    private const int VakitSayisi = 18;

    private static readonly CultureInfo Turkce = new("tr-TR");
    private readonly HttpClient _http;

    public TurktakvimIstemci(HttpClient http)
    {
        _http = http;
    }

    public static HttpClient VarsayilanHttpIstemcisi()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TurkiyeTakvimi/0.1");
        return http;
    }

    public async Task<List<Sehir>> SehirAraAsync(string ad, CancellationToken iptal = default)
    {
        string kodlu = string.Join(';', ad.Trim().ToLower(Turkce).Select(c => (int)c));
        string html = await MetinAlAsync($"{GadgetAdresi}?pg=2&q={kodlu}", iptal);

        var sonuc = new List<Sehir>();
        string ulke = "";
        foreach (Match m in Regex.Matches(html,
                     @"(?<ulke>[^<>]+)<br style=""clear:both;"" />|sehir_yap\((?<kod>\d+)\)""><img[^>]*>(?<ad>[^<]+)"))
        {
            if (m.Groups["ulke"].Success)
                ulke = WebUtility.HtmlDecode(m.Groups["ulke"].Value).Trim();
            else
                sonuc.Add(new Sehir(m.Groups["kod"].Value, WebUtility.HtmlDecode(m.Groups["ad"].Value).Trim(), ulke));
        }
        return sonuc;
    }

    /// <summary>
    /// <see cref="SehirAraAsync"/> gibi arar; hiç sonuç yoksa Türkçe harf varyantlarını (nigde → niğde, sanliurfa → şanlıurfa)
    /// en az değişiklikten başlayarak dener. Site arama yaparken harfleri birebir istediği için bu gerekir.
    /// </summary>
    public async Task<List<Sehir>> SehirAraEsnekAsync(string ad, CancellationToken iptal = default)
    {
        foreach (string aday in TurkceYazimlari(ad).Take(14))
        {
            var sonuc = await SehirAraAsync(aday, iptal);
            if (sonuc.Count > 0) return sonuc;
        }
        return new List<Sehir>();
    }

    private static readonly Dictionary<char, char> HarfCiftleri = new()
    {
        ['g'] = 'ğ', ['ğ'] = 'g', ['s'] = 'ş', ['ş'] = 's', ['c'] = 'ç', ['ç'] = 'c',
        ['o'] = 'ö', ['ö'] = 'o', ['u'] = 'ü', ['ü'] = 'u', ['i'] = 'ı', ['ı'] = 'i'
    };

    /// <summary>Girilen yazı, sonra 1, 2, 3 harfi değişmiş halleri (ğ/ş/ç/ö/ü harfleri ı'dan önce denenir).</summary>
    internal static IEnumerable<string> TurkceYazimlari(string ad)
    {
        string temiz = ad.Trim().ToLower(Turkce);
        yield return temiz;

        // Değiştirilebilir konumlar; noktalı/noktasız i en sona bırakılır.
        var konumlar = Enumerable.Range(0, temiz.Length)
            .Where(i => HarfCiftleri.ContainsKey(temiz[i]))
            .OrderBy(i => temiz[i] is 'i' or 'ı' ? 1 : 0)
            .ThenBy(i => i)
            .ToList();

        var goruldu = new HashSet<string> { temiz };
        for (int adet = 1; adet <= Math.Min(3, konumlar.Count); adet++)
        {
            foreach (var secim in Kombinasyonlar(konumlar, adet))
            {
                var harfler = temiz.ToCharArray();
                foreach (int i in secim) harfler[i] = HarfCiftleri[harfler[i]];
                string yeni = new(harfler);
                if (goruldu.Add(yeni)) yield return yeni;
            }
        }
    }

    private static IEnumerable<List<int>> Kombinasyonlar(List<int> kaynak, int adet, int baslangic = 0)
    {
        if (adet == 0)
        {
            yield return new List<int>();
            yield break;
        }
        for (int i = baslangic; i <= kaynak.Count - adet; i++)
            foreach (var devam in Kombinasyonlar(kaynak, adet - 1, i + 1))
            {
                devam.Insert(0, kaynak[i]);
                yield return devam;
            }
    }

    /// <summary>
    /// Sitenin günlük servisinden şehrin şu anki YEREL saatini alır (yaz saati dahil). Vakitler şehrin yerel saatiyle
    /// yazıldığı için, şehir bilgisayarın saat diliminden farklıysa geri sayım bu saate göre yapılmalıdır.
    /// </summary>
    public async Task<SehirAnligi?> SehirAnligiAsync(string sehirKodu, CancellationToken iptal = default)
    {
        string ham = await MetinAlAsync($"{GadgetAdresi}?pg=1&sid={Uri.EscapeDataString(sehirKodu)}&cityID={Uri.EscapeDataString(sehirKodu)}", iptal);
        string[] alanlar = ham.Split(';');
        if (alanlar.Length <= 25 ||
            !DateTime.TryParseExact(alanlar[25].Trim(), "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var yerel))
            return null;

        return new SehirAnligi(yerel, Derece(alanlar[1], alanlar[2], alanlar[3], "S"), Derece(alanlar[4], alanlar[5], alanlar[6], "W"));
    }

    private static double? Derece(string derece, string dakika, string yon, string eksiYon)
    {
        if (!double.TryParse(derece.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ||
            !double.TryParse(dakika.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double m))
            return null;
        double deger = d + m / 60.0;
        return yon.Trim().Equals(eksiYon, StringComparison.OrdinalIgnoreCase) ? -deger : deger;
    }

    /// <summary>
    /// Sitenin "Ezânî Vakitler" sayfasından (alaturka saat: akşam 12:00) o günün vakitlerini alır. Şehir, sitenin
    /// cityID çerezi ile seçilir. Ezânî saatler yıllık tabloda yoktur; yalnızca günlük sayfada verilir.
    /// </summary>
    public async Task<List<EzaniSatiri>> EzaniVakitleriAsync(string sehirKodu, DateOnly tarih, CancellationToken iptal = default)
    {
        string adres = $"{SiteAdresi}/index.php?vakit=ezani&tarih={tarih:yyyy-MM-dd}";
        string html;
        try
        {
            using var istek = new HttpRequestMessage(HttpMethod.Get, adres);
            istek.Headers.TryAddWithoutValidation("Cookie", $"cityID={Uri.EscapeDataString(sehirKodu)}");
            using var yanit = await _http.SendAsync(istek, iptal);
            yanit.EnsureSuccessStatusCode();
            html = Encoding.UTF8.GetString(await yanit.Content.ReadAsByteArrayAsync(iptal));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !iptal.IsCancellationRequested)
        {
            throw new VeriHatasi("İnternetten veri alınamadı.", ex);
        }

        int bas = html.IndexOf("Ezânî Vakitler</p>", StringComparison.Ordinal);
        if (bas < 0) throw new VeriHatasi("Ezânî vakitler sayfada bulunamadı.");
        int son = html.IndexOf("</table>", bas, StringComparison.Ordinal);
        string tablo = html.Substring(bas, (son < 0 ? html.Length : son) - bas);

        var hucreler = Regex.Matches(tablo, @"<td[^>]*>\s*([^<]*?)\s*</td>", RegexOptions.Singleline)
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Replace('\u00a0', ' ').Trim())
            .Where(s => s.Length > 0 && !s.StartsWith("&nbsp", StringComparison.Ordinal))
            .ToList();

        var sonuc = new List<EzaniSatiri>();
        for (int i = 0; i + 1 < hucreler.Count; i += 2)
        {
            if (!Regex.IsMatch(hucreler[i + 1], @"^\d{1,2}:\d{2}$")) throw new VeriHatasi("Ezânî vakit tablosu beklenen biçimde gelmedi.");
            sonuc.Add(new EzaniSatiri(hucreler[i], hucreler[i + 1]));
        }
        if (sonuc.Count < 5) throw new VeriHatasi("Ezânî vakit tablosu beklenen biçimde gelmedi.");
        return sonuc;
    }
    public async Task<YillikVakitler> YillikVakitleriIndirAsync(string sehirKodu, int yil, CancellationToken iptal = default)
    {
        var alanlar = new List<KeyValuePair<string, string>>
        {
            new("input1", $"{yil}-01-01"),
            new("input2", $"{yil}-12-31"),
            new("git", "Indir")
        };
        foreach (string ad in new[]
                 {
                     "imsak", "sabah", "gunes", "israk", "dahve", "kerahet", "ogle", "ikindi", "asrisani",
                     "isfirar", "aksam", "istibak", "yatsi", "isaisani", "geceyarisi", "teheccud", "seher", "kible"
                 })
            alanlar.Add(new($"checkbox-{ad}", "1"));

        // https şart: http adresi yönlendirirken form verisi kaybolur.
        string html;
        try
        {
            using var yanit = await _http.PostAsync(
                $"{SiteAdresi}/excel.php?cityID={Uri.EscapeDataString(sehirKodu)}",
                new FormUrlEncodedContent(alanlar), iptal);
            yanit.EnsureSuccessStatusCode();
            html = Encoding.UTF8.GetString(await yanit.Content.ReadAsByteArrayAsync(iptal));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.InternalServerError)
        {
            throw new YerIcinVeriYok(ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !iptal.IsCancellationRequested)
        {
            throw new VeriHatasi("Vakit verisi indirilemedi.", ex);
        }

        return VakitTablosunuAyristir(html, sehirKodu, yil);
    }

    public async Task<GunlukIcerik> GunlukIcerikIndirAsync(DateOnly tarih, CancellationToken iptal = default)
    {
        string t = tarih.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string on = await MetinAlAsync($"{SiteAdresi}/index.php?tarih={t}&page=onyuz", iptal);
        string arka = await MetinAlAsync($"{SiteAdresi}/index.php?tarih={t}&page=arkayuz", iptal);
        return IcerigiAyristir(tarih, on, arka);
    }

    internal static YillikVakitler VakitTablosunuAyristir(string html, string sehirKodu, int yil)
    {
        const int Sutun = VakitSayisi + 1; // tarih + vakitler

        string sehirAdi = Metin(Regex.Match(html, "<b>(.*?)</b>", RegexOptions.Singleline).Groups[1].Value);
        var hucreler = Regex.Matches(html, @"<td[^>]*>(.*?)</td>", RegexOptions.Singleline)
            .Select(m => Metin(m.Groups[1].Value))
            .ToList();

        if (hucreler.Count < Sutun || hucreler.Count % Sutun != 0 || hucreler[0] != "Tarih")
            throw new VeriHatasi("Vakit tablosu beklenen biçimde gelmedi.");
        if (hucreler.Count == Sutun)
            throw new VeriHatasi($"{yil} yılı için vakit verisi bulunamadı.");

        var gunler = new List<GunlukVakit>();
        for (int i = Sutun; i < hucreler.Count; i += Sutun)
        {
            if (!DateOnly.TryParseExact(hucreler[i], "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tarih))
                throw new VeriHatasi($"Tarih okunamadı: {hucreler[i]}");

            string[] saatler = hucreler.GetRange(i + 1, VakitSayisi).ToArray();
            // "*HH:mm" = sitenin "yaklaşık" işareti, "-" = o gün için vakit yok (yüksek enlemli yerler).
            saatler = saatler.Select(s => Regex.IsMatch(s, @"^-+$") ? "-" : s).ToArray();
            if (saatler.Any(s => !VakitSaati.Gecerli(s)))
                throw new VeriHatasi($"{hucreler[i]} tarihli satırda saat okunamadı.");

            gunler.Add(new GunlukVakit(tarih, saatler));
        }
        return new YillikVakitler(sehirKodu, sehirAdi, yil, gunler);
    }

    internal static GunlukIcerik IcerigiAyristir(DateOnly tarih, string onYuzHtml, string arkaYuzHtml)
    {
        string? soz = SpanMetni(onYuzHtml, "gununsozu");
        if (soz is not null) soz = Regex.Replace(soz, @"\s+", " ");
        string? olay = SpanMetni(onYuzHtml, "gununolayi");
        var olaylar = olay is null
            ? new List<string>()
            : olay.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        string? baslik = null, metin = null, ek = null;
        var h1 = Regex.Match(arkaYuzHtml, @"<h1 style='font-weight:bold[^>]*>(.*?)</h1>", RegexOptions.Singleline);
        if (h1.Success)
        {
            baslik = Bos(Metin(h1.Groups[1].Value));
            int basla = h1.Index + h1.Length;
            int bitis = arkaYuzHtml.IndexOf(@"<div align=""center"" style=""border-bottom:4px", basla, StringComparison.Ordinal);
            if (bitis < 0) bitis = arkaYuzHtml.IndexOf("<nav", basla, StringComparison.Ordinal);
            if (bitis > basla)
                metin = Bos(Metin(arkaYuzHtml[basla..bitis], paragrafAyir: true));

            var alt = Regex.Match(arkaYuzHtml[(bitis > 0 ? bitis : basla)..], @"<div class=""alt"">(.*?)</div>", RegexOptions.Singleline);
            if (alt.Success) ek = Bos(Metin(alt.Groups[1].Value));
        }
        return new GunlukIcerik(tarih, soz, olaylar, baslik, metin, ek, BilgiAyristir(onYuzHtml));
    }

    /// <summary>Ön yüzdeki iki künye bloğunu (takvimon_part1 ve takvimon_part2) hazır satırlara çevirir.</summary>
    internal static GunBilgisi? BilgiAyristir(string html)
    {
        int a = html.IndexOf("<div id=\"takvimon_part1\">", StringComparison.Ordinal);
        int b = html.IndexOf("<div id=\"takvimon_part2\">", StringComparison.Ordinal);
        int e = html.IndexOf("id=\"gununsozu\"", StringComparison.Ordinal);
        if (a < 0 || b <= a || e <= b) return null;

        string kisim1 = html[a..b];
        string kisim2 = html[b..e];

        string hicri = HicriDuzelt(Metin(Regex.Match(kisim1, @"class=""hicri"">(.*?)(?:<!--|</div>)",
            RegexOptions.Singleline).Groups[1].Value));

        // Birinci satır: "Hicrî Şemsî: 1405   Rûmî: ...   Hızır: 170" (üçüncü etiket güne göre Hızır ya da Kasım olur).
        int ilkSatirSonu = kisim1.IndexOf("</div>", StringComparison.Ordinal);
        string ilkSatir = ilkSatirSonu > 0 ? kisim1[..ilkSatirSonu] : kisim1;
        var ciftler = Regex.Matches(ilkSatir, @"<b>\s*([^<:]+?)\s*:\s*</b>\s*(.*?)\s*(?=&nbsp;|<b>|<br|$)", RegexOptions.Singleline)
            .Select(m => $"{Metin(m.Groups[1].Value)}: {Metin(m.Groups[2].Value)}")
            .ToList();

        var satirlar = new List<string>();
        if (hicri.Length > 0) satirlar.Add($"Hicrî tarih: {hicri}");
        if (ciftler.Count > 0) satirlar.Add(string.Join("  ·  ", ciftler));

        // İkinci blok: yılın günü, ay-gün-hafta, gündüzün uzama/kısalma durumu (kendi kimliği olmayan iç div'ler).
        foreach (Match m in Regex.Matches(kisim2, @"<div (?!id=)[^>]*>(.*?)</div>", RegexOptions.Singleline))
        {
            string satir = Metin(m.Groups[1].Value);
            if (satir.Length > 0) satirlar.Add(satir);
        }

        return satirlar.Count == 0 ? null : new GunBilgisi(hicri, satirlar);
    }

    /// <summary>"11 CEMÂZİL-EVVEL 1448" biçimini "11 Cemâzil-evvel 1448" yapar (aya göre büyük/küçük harf, Türkçe kurallarıyla).</summary>
    internal static string HicriDuzelt(string ham)
    {
        var m = Regex.Match(ham.Trim(), @"^(\d+)\s+(.+?)\s+(\d+)$");
        if (!m.Success) return ham.Trim();

        string ay = m.Groups[2].Value.ToLower(Turkce);
        ay = char.ToUpper(ay[0], Turkce) + ay[1..];
        return $"{m.Groups[1].Value} {ay} {m.Groups[3].Value}";
    }

    private static string? SpanMetni(string html, string id)
    {
        var m = Regex.Match(html, $@"id=""{id}"">(.*?)</span>", RegexOptions.Singleline);
        return m.Success ? Bos(Metin(m.Groups[1].Value)) : null;
    }

    private static string? Bos(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>HTML'i düz metne çevirir. Satır sonları korunur, etiketler ve fazla boşluklar atılır.</summary>
    internal static string Metin(string html, bool paragrafAyir = false)
    {
        string s = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, paragrafAyir ? @"</p>" : @"(?!)", "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "<[^>]+>", "");
        s = WebUtility.HtmlDecode(s).Replace(' ', ' ');
        s = Regex.Replace(s, @"[ \t]+", " ");
        s = Regex.Replace(s, @" ?\n ?", "\n");
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        return GorunmezKarakterler.At(s).Trim();
    }

    /// <summary>Sitenin herhangi bir sayfasını (tam adres) metin olarak alır.</summary>
    public Task<string> SayfaAlAsync(string adres, CancellationToken iptal = default) => MetinAlAsync(adres, iptal);

    public const string Site = SiteAdresi;

    private async Task<string> MetinAlAsync(string adres, CancellationToken iptal)
    {
        try
        {
            byte[] bayt = await _http.GetByteArrayAsync(adres, iptal);
            // Giriş kapısı: indirilen her sayfadaki görünmez karakterler (yumuşak tire vb.) ayrıştırmadan önce atılır.
            return GorunmezKarakterler.HamHtml(Encoding.UTF8.GetString(bayt));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !iptal.IsCancellationRequested)
        {
            throw new VeriHatasi("İnternetten veri alınamadı.", ex);
        }
    }
}
