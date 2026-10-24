using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Testler;

/// <summary>Güncelleme denetimi: GitHub sürüm yanıtının okunması, sürüm karşılaştırması ve indirme güvenliği.</summary>
public class GuncellemeTestleri
{
    private const string Yanit = """
        {
          "tag_name": "v2026.11",
          "html_url": "https://github.com/ersalih51/turkiye-takvimi/releases/tag/v2026.11",
          "body": "## Türkiye Takvimi 2026.11\r\n\r\n- **Yeni:** güncelleme denetimi\r\n- `Kurulum.exe` dosyası",
          "assets": [
            { "name": "TurkiyeTakvimi-2026.11-Ozet.txt", "browser_download_url": "https://github.com/x/y/releases/download/v2026.11/ozet.txt" },
            { "name": "TurkiyeTakvimi-2026.11-Kurulum.exe",
              "browser_download_url": "https://github.com/ersalih51/turkiye-takvimi/releases/download/v2026.11/TurkiyeTakvimi-2026.11-Kurulum.exe",
              "digest": "sha256:ABCDEF0123" }
          ]
        }
        """;

    [Fact]
    public void Ayristir_SurumuKurulumDosyasiniVeOzetiBulur()
    {
        var s = GuncellemeServisi.Ayristir(Yanit);
        Assert.Equal(new Version(2026, 11, 0), s.Surum);
        Assert.Equal("v2026.11", s.Etiket);
        Assert.Equal("TurkiyeTakvimi-2026.11-Kurulum.exe", s.KurulumAdi);
        Assert.Equal("abcdef0123", s.KurulumOzeti);
        Assert.EndsWith("TurkiyeTakvimi-2026.11-Kurulum.exe", s.KurulumAdresi);
    }

    [Fact]
    public void Ayristir_NotlardakiIsaretlemeyiTemizler()
    {
        var s = GuncellemeServisi.Ayristir(Yanit);
        Assert.Equal("Türkiye Takvimi 2026.11\n- Yeni: güncelleme denetimi\n- Kurulum.exe dosyası", s.Notlar);
    }

    [Fact]
    public void Ayristir_NotlardaIndirmeBolumunuKeser()
    {
        var s = GuncellemeServisi.Ayristir("""{ "tag_name": "v2026.10.1", "body": "### Yenilikler\n\n- Yeni madde\n\n### İndirme\n\n- dosya açıklaması", "assets": [] }""");
        Assert.Equal("Yenilikler\n- Yeni madde", s.Notlar);
    }

    [Fact]
    public void Ayristir_KurulumDosyasiYoksa_AdresVeOzetBos()
    {
        var s = GuncellemeServisi.Ayristir("""{ "tag_name": "2026.12", "assets": [ { "name": "baska.zip", "browser_download_url": "https://github.com/a" } ] }""");
        Assert.Null(s.KurulumAdresi);
        Assert.Null(s.KurulumOzeti);
        Assert.Equal(new Version(2026, 12, 0), s.Surum);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ }")]
    [InlineData("{ \"tag_name\": \"sürüm-x\" }")]
    [InlineData("bu json değil")]
    public void Ayristir_BozukYanit_VeriHatasiVerir(string json) =>
        Assert.Throws<VeriHatasi>(() => GuncellemeServisi.Ayristir(json));

    [Theory]
    [InlineData("2026.10.0.0", "2026.10", false)]
    [InlineData("2026.10.0", "2026.11", true)]
    [InlineData("2026.10.0", "2026.10.1", true)]
    [InlineData("2026.11.0", "2026.10", false)]
    [InlineData("2026.10.0", "2027.1", true)]
    public void YeniMi_SurumleriDogruKarsilastirir(string kurulu, string son, bool beklenen) =>
        Assert.Equal(beklenen, GuncellemeServisi.YeniMi(Version.Parse(kurulu), Version.Parse(son)));

    [Theory]
    [InlineData("https://github.com/ersalih51/turkiye-takvimi/releases/download/v1/a.exe", true)]
    [InlineData("https://objects.githubusercontent.com/abc", true)]
    [InlineData("http://github.com/a.exe", false)]
    [InlineData("https://github.com.kotu.example/a.exe", false)]
    [InlineData("https://example.com/githubusercontent.com", false)]
    [InlineData("dosya.exe", false)]
    public void GuvenilirAdresMi_YalnizcaGitHubHttps(string adres, bool beklenen) =>
        Assert.Equal(beklenen, GuncellemeServisi.GuvenilirAdresMi(adres));

    [Fact]
    public void GeriBildirimAdresi_SurumuVeWindowsuYazarKisiselBilgiYazmaz()
    {
        string adres = GuncellemeServisi.GeriBildirimAdresi("2026.10", "Microsoft Windows NT 10.0.26200.0", tasinabilir: false);
        Assert.StartsWith("https://github.com/ersalih51/turkiye-takvimi/issues/new?title=", adres);
        string cozulmus = Uri.UnescapeDataString(adres[(adres.IndexOf("&body=", StringComparison.Ordinal) + 6)..]);
        Assert.Contains("Sürüm: 2026.10 (kurulu)", cozulmus);
        Assert.Contains("Windows: Microsoft Windows NT 10.0.26200.0", cozulmus);
        Assert.DoesNotContain(Environment.UserName, cozulmus, StringComparison.OrdinalIgnoreCase);
    }
}
