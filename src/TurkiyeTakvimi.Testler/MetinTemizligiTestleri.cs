using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Testler;

/// <summary>
/// Siteden gelen metinlerde görünmez karakter (yumuşak tire vb.) kalmadığını güvence altına alır. Bu testler kırılırsa, ekran okuyucu
/// kelimeleri hece hece okumaya başlar (örn. "Ârif­le­rin"). Yeni bir metin kaynağı eklenince buraya da bir test eklenir.
/// </summary>
public class MetinTemizligiTestleri
{
    private const string YumusakTire = "­";

    [Fact]
    public void At_YumusakTireVeSifirGenislikliBoslugu_Atar()
    {
        string kirli = "Âr" + YumusakTire + "if" + "​" + "ler" + "⁠" + "in﻿";
        Assert.Equal("Ârifler" + "in", GorunmezKarakterler.At(kirli));
    }

    [Fact]
    public void At_TemizMetne_Dokunmaz()
    {
        const string temiz = "Âriflerin ışığı, velîlerin önderi";
        Assert.Equal(temiz, GorunmezKarakterler.At(temiz));
        Assert.Equal("", GorunmezKarakterler.At(null));
    }

    [Theory]
    [InlineData("Âr&shy;if&shy;ler")]
    [InlineData("Âr&#173;if&#xAD;ler")]
    [InlineData("Âr­if­ler")]
    [InlineData("Âr&SHY;if&#8203;ler")]
    public void HamHtml_HtmlVarliklariniDaAtar(string html)
    {
        Assert.Equal("Ârifler", GorunmezKarakterler.HamHtml(html));
    }

    [Fact]
    public void OnbellekMetni_JsonKacisiniDaAtar()
    {
        // System.Text.Json görünmez karakteri ­ kaçış dizisi olarak yazar; eski önbellek dosyalarında böyle kalmıştır.
        string json = "{\"soz\":\"Âr\\u00ADif\\u00adler\\u200B\"}";
        Assert.Equal("{\"soz\":\"Ârifler\"}", GorunmezKarakterler.OnbellekMetni(json));
    }

    [Fact]
    public void Metin_HtmldenCikanMetindeYumusakTireKalmaz()
    {
        string html = "<p>Âri" + YumusakTire + "fler&shy;in ışı" + YumusakTire + "ğı</p>";
        string metin = TurktakvimIstemci.Metin(html);
        Assert.Equal("Âriflerin ışığı", metin);
    }

    [Fact]
    public void YaziyiAyristir_BilgiYazisindaGorunmezKarakterKalmaz()
    {
        string html = "<div id=\"Wrapper\"><p><strong>Başlık</strong></p><p>Bu bir yazı" + YumusakTire + "dır ve yeterince uzundur, ki kırıntı sayılmasın.</p></div>";
        var yazi = BilgilerServisi.YaziyiAyristir(html, "Başlık");
        Assert.NotEmpty(yazi.Paragraflar);
        Assert.All(yazi.Paragraflar, p => Assert.Empty(MetinDenetcisi.Incele(p)));
    }

    [Fact]
    public void Onbellek_EskiKayittakiYumusakTireOkunurkenTemizlenir()
    {
        string klasor = Path.Combine(Path.GetTempPath(), "tt-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var onbellek = new DiskOnbellegi(klasor);
            onbellek.Yaz("deneme", new BilgiYazisi("Başlık", new List<string> { "Âr" + YumusakTire + "ifler" }));
            Assert.Contains("\\u00AD", File.ReadAllText(Path.Combine(klasor, "deneme.json")), StringComparison.OrdinalIgnoreCase); // dosyada hâlâ kirli

            var okunan = onbellek.Oku<BilgiYazisi>("deneme");
            Assert.NotNull(okunan);
            Assert.Equal("Ârifler", okunan!.Paragraflar[0]);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, recursive: true);
        }
    }

    [Fact]
    public void Denetci_TemizMetindeSorunBulmaz()
    {
        Assert.Empty(MetinDenetcisi.Incele("Âriflerin ışığı, velîlerin önderi. Ç ğ ı İ ö ş ü â î û."));
        Assert.Empty(MetinDenetcisi.Incele(null));
    }

    [Theory]
    [InlineData("Âr­if", "görünmez")]
    [InlineData("Âri�f", "kodlama")]
    [InlineData("Ã¶rnek", "kodlama")]
    [InlineData("Åžehir Ä±", "kodlama")]
    [InlineData("a\u0007b", "denetim")]
    public void Denetci_BilinenSorunlariYakalar(string metin, string beklenenParca)
    {
        var sorunlar = MetinDenetcisi.Incele(metin);
        Assert.Contains(sorunlar, s => s.Contains(beklenenParca, StringComparison.OrdinalIgnoreCase));
    }
}
