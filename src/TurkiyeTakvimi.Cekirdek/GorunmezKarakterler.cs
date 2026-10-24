using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>
/// turktakvim.com metinlerine satır bölmek için yumuşak tire (U+00AD) gibi görünmez karakterler konulmuş ("Ârif­le­rin").
/// Ekranda görünmezler, ama ekran okuyucu kelimeyi hece hece tireyle okur. Siteden gelen metinler bu sınıftan geçer:
/// <list type="bullet">
/// <item><see cref="HamHtml"/>: indirilen sayfa daha ayrıştırılmadan (giriş kapısı),</item>
/// <item><see cref="OnbellekMetni"/>: önbellekteki JSON okunurken (eski kayıtlar da temizlenir),</item>
/// <item><see cref="At"/>: ekrana yazılırken (son güvence).</item>
/// </list>
/// Yeni bir yerde site metni gösteriliyorsa mutlaka bunlardan biri kullanılır. <see cref="MetinDenetcisi"/> sorunları ölçer.
/// </summary>
public static class GorunmezKarakterler
{
    /// <summary>Görünmez ve okunuşu bozan karakterler: yumuşak tire, sıfır genişlikli boşluklar, kelime birleştirici, BOM.</summary>
    public static readonly char[] Atilacaklar = { '­', '​', '‌', '‍', '⁠', '﻿' };

    private static readonly Regex HtmlVarligi = new(@"&(?:shy|#173|#xAD|#xad|#8203|#x200B|#x200b|#8204|#8205|zwnj|zwj);", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex JsonKacisi = new(@"\\u(?:00AD|200B|200C|200D|2060|FEFF)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Düz metinden görünmez karakterleri atar.</summary>
    public static string At(string? metin) =>
        string.IsNullOrEmpty(metin) || metin.IndexOfAny(Atilacaklar) < 0
            ? metin ?? ""
            : string.Concat(metin.Split(Atilacaklar));

    /// <summary>İndirilen ham sayfa: hem karakter hem HTML varlığı olarak (&amp;shy; gibi) görünmez karakterleri atar.</summary>
    public static string HamHtml(string html) => At(HtmlVarligi.Replace(html, ""));

    /// <summary>Önbellek dosyasının JSON metni: kaçış dizileri (­) ve düz karakterler atılır.</summary>
    public static string OnbellekMetni(string json) => At(JsonKacisi.Replace(json, ""));
}

/// <summary>Bir metindeki ekran okuyucuyu ya da okuyanı yanıltan yapısal sorunları bulur (test ve "metinkontrol" aracı için).</summary>
public static class MetinDenetcisi
{
    // UTF-8 baytlarının yanlış kodlamayla okunmasından kalan tipik izler: "Ã¶", "Ä±", "Å\u009f".
    private static readonly string[] BozukKodlamaIzleri = { "Ã", "Ä±", "Ä°", "Å\u009f", "Å\u009e", "Ã§", "Ã¼", "â€" };

    public static List<string> Incele(string? metin)
    {
        var sorunlar = new List<string>();
        if (string.IsNullOrEmpty(metin)) return sorunlar;

        int gorunmez = metin.Count(c => GorunmezKarakterler.Atilacaklar.Contains(c));
        if (gorunmez > 0) sorunlar.Add($"{gorunmez} görünmez karakter (yumuşak tire vb.)");

        int kontrol = metin.Count(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'));
        if (kontrol > 0) sorunlar.Add($"{kontrol} denetim karakteri");

        if (metin.Contains('�')) sorunlar.Add("değiştirme karakteri (�): kodlama hatası");
        if (BozukKodlamaIzleri.Any(metin.Contains)) sorunlar.Add("bozuk kodlama izi (Ã, Ä± gibi)");

        // Bellekteki karakterlerin Unicode kategorisi: eşlenmemiş ya da özel kullanım alanı.
        int garip = metin.Count(c => CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.OtherNotAssigned or UnicodeCategory.PrivateUse);
        if (garip > 0) sorunlar.Add($"{garip} tanımsız ya da özel karakter");

        if (metin.Contains(' ')) sorunlar.Add("kesilmez boşluk (U+00A0)");
        return sorunlar;
    }
}
