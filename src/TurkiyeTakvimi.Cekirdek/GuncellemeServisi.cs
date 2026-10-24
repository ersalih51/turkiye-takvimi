using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>GitHub'daki en son sürüm: sürüm numarası, sayfası, notları ve (varsa) kurulum dosyası.</summary>
public sealed record SurumBilgisi(
    Version Surum,
    string Etiket,
    string SayfaAdresi,
    string Notlar,
    string? KurulumAdi,
    string? KurulumAdresi,
    string? KurulumOzeti);

/// <summary>
/// Yardım > Güncellemeleri denetle. Yalnızca kullanıcı isteyince çalışır; hiçbir zaman kendiliğinden internete bağlanmaz.
/// Herkese açık GitHub sürüm bilgisine (hesap ya da anahtar gerektirmeden) bakar; kurulum dosyasını indirir ve özetini doğrular.
/// </summary>
public sealed class GuncellemeServisi
{
    public const string Depo = "ersalih51/turkiye-takvimi";
    public static string SurumSayfasi => $"https://github.com/{Depo}/releases/latest";
    public static string YeniSorunAdresi => $"https://github.com/{Depo}/issues/new";

    private static readonly Regex KurulumDosyaAdi = new(@"^TurkiyeTakvimi-[0-9.]+-Kurulum\.exe$", RegexOptions.Compiled);

    private readonly HttpClient _http;

    public GuncellemeServisi(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<SurumBilgisi> SonSurumuAlAsync(string kuruluSurum, CancellationToken iptal = default)
    {
        using var istek = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Depo}/releases/latest");
        istek.Headers.UserAgent.ParseAdd($"TurkiyeTakvimi/{kuruluSurum}");
        istek.Headers.Accept.ParseAdd("application/vnd.github+json");
        try
        {
            using var yanit = await _http.SendAsync(istek, iptal);
            if (!yanit.IsSuccessStatusCode)
                throw new VeriHatasi($"GitHub sürüm bilgisini vermedi ({(int)yanit.StatusCode}).");
            return Ayristir(await yanit.Content.ReadAsStringAsync(iptal));
        }
        catch (HttpRequestException ex)
        {
            throw new VeriHatasi("GitHub'a bağlanılamadı.", ex);
        }
        catch (TaskCanceledException ex) when (!iptal.IsCancellationRequested)
        {
            throw new VeriHatasi("GitHub zamanında yanıt vermedi.", ex);
        }
    }

    /// <summary>GitHub "en son sürüm" yanıtını ayrıştırır.</summary>
    internal static SurumBilgisi Ayristir(string json)
    {
        try
        {
            using var belge = JsonDocument.Parse(json);
            var kok = belge.RootElement;
            string etiket = kok.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(etiket.TrimStart('v', 'V'), out var surum))
                throw new VeriHatasi($"Sürüm numarası anlaşılamadı: {etiket}");

            string sayfa = kok.TryGetProperty("html_url", out var h) ? h.GetString() ?? SurumSayfasi : SurumSayfasi;
            string notlar = kok.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

            string? ad = null, adres = null, ozet = null;
            if (kok.TryGetProperty("assets", out var dosyalar))
            {
                foreach (var d in dosyalar.EnumerateArray())
                {
                    string dosyaAdi = d.GetProperty("name").GetString() ?? "";
                    if (!KurulumDosyaAdi.IsMatch(dosyaAdi)) continue;
                    ad = dosyaAdi;
                    adres = d.GetProperty("browser_download_url").GetString();
                    if (d.TryGetProperty("digest", out var o) && o.GetString() is { } s && s.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                        ozet = s["sha256:".Length..].ToLowerInvariant();
                    break;
                }
            }

            return new SurumBilgisi(Normallestir(surum), etiket, sayfa, DuzMetin(notlar), ad, adres, ozet);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new VeriHatasi("Sürüm bilgisi okunamadı.", ex);
        }
    }

    /// <summary>"2026.10" ile "2026.10.0.0" aynı sayılsın diye üç bölüme getirir.</summary>
    public static Version Normallestir(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    public static bool YeniMi(Version kurulu, Version son) => Normallestir(son) > Normallestir(kurulu);

    /// <summary>Sürüm notlarındaki işaretleme (başlık #, kalın **, kod `) ekran okuyucuda gürültü yapmasın diye atılır.</summary>
    internal static string DuzMetin(string metin)
    {
        var satirlar = GorunmezKarakterler.At(metin).Replace("\r\n", "\n").Split('\n')
            .Select(s => Regex.Replace(s.TrimEnd(), @"^#+\s*", "").Replace("**", "").Replace("`", ""))
            .Where(s => s.Length > 0);
        string sonuc = string.Join("\n", satirlar);
        return sonuc.Length > 3000 ? sonuc[..3000] + "…" : sonuc;
    }

    /// <summary>
    /// Kurulum dosyasını geçici klasöre indirir ve SHA-256 özetini GitHub'ın bildirdiği özetle karşılaştırır.
    /// Özet yoksa ya da uyuşmuyorsa dosya silinir ve hata verilir. İlerleme 0–1 arasındadır.
    /// </summary>
    public async Task<string> KurulumuIndirAsync(SurumBilgisi surum, IProgress<double>? ilerleme = null, CancellationToken iptal = default)
    {
        if (surum.KurulumAdresi is null || surum.KurulumAdi is null)
            throw new VeriHatasi("Bu sürümde kurulum dosyası bulunamadı.");
        if (surum.KurulumOzeti is null)
            throw new VeriHatasi("Kurulum dosyasının özeti bulunamadığı için doğrulanamadı.");
        if (!GuvenilirAdresMi(surum.KurulumAdresi))
            throw new VeriHatasi("Kurulum dosyasının adresi güvenilir görünmüyor.");

        string klasor = Path.Combine(Path.GetTempPath(), "TurkiyeTakvimi-guncelleme");
        Directory.CreateDirectory(klasor);
        string yol = Path.Combine(klasor, surum.KurulumAdi);

        try
        {
            using var istek = new HttpRequestMessage(HttpMethod.Get, surum.KurulumAdresi);
            istek.Headers.UserAgent.ParseAdd("TurkiyeTakvimi");
            using var yanit = await _http.SendAsync(istek, HttpCompletionOption.ResponseHeadersRead, iptal);
            if (!yanit.IsSuccessStatusCode)
                throw new VeriHatasi($"Kurulum dosyası indirilemedi ({(int)yanit.StatusCode}).");

            long? toplam = yanit.Content.Headers.ContentLength;
            await using (var kaynak = await yanit.Content.ReadAsStreamAsync(iptal))
            await using (var hedef = File.Create(yol))
            {
                var tampon = new byte[81920];
                long okunan = 0;
                int n;
                while ((n = await kaynak.ReadAsync(tampon, iptal)) > 0)
                {
                    await hedef.WriteAsync(tampon.AsMemory(0, n), iptal);
                    okunan += n;
                    if (toplam is > 0) ilerleme?.Report((double)okunan / toplam.Value);
                }
            }

            string ozet = await OzetAsync(yol, iptal);
            if (!string.Equals(ozet, surum.KurulumOzeti, StringComparison.OrdinalIgnoreCase))
                throw new VeriHatasi("İndirilen dosya doğrulanamadı (özet uyuşmuyor).");
            return yol;
        }
        catch
        {
            try { File.Delete(yol); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    internal static bool GuvenilirAdresMi(string adres) =>
        Uri.TryCreate(adres, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps &&
        (u.Host == "github.com" || u.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    private static async Task<string> OzetAsync(string yol, CancellationToken iptal)
    {
        await using var akis = File.OpenRead(yol);
        return Convert.ToHexString(await SHA256.HashDataAsync(akis, iptal)).ToLowerInvariant();
    }

    /// <summary>Geri bildirim sayfası: sürüm ve Windows bilgisi önceden yazılır, başka kişisel bilgi eklenmez.</summary>
    public static string GeriBildirimAdresi(string surum, string windows, bool tasinabilir)
    {
        var govde = new StringBuilder()
            .AppendLine($"Sürüm: {surum} ({(tasinabilir ? "taşınabilir" : "kurulu")})")
            .AppendLine($"Windows: {windows}")
            .AppendLine()
            .AppendLine("---")
            .AppendLine("Sorununuzu ya da önerinizi buraya yazın. Bu kayıt herkese açıktır; lütfen kişisel bilgi (adres, telefon, e-posta) yazmayın.")
            .ToString();
        return $"{YeniSorunAdresi}?title={Uri.EscapeDataString("Geri bildirim")}&body={Uri.EscapeDataString(govde)}";
    }
}
