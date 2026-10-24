using System.Globalization;
using System.Text.RegularExpressions;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>
/// Sitedeki bir vakit hücresi: "05:41", "*23:55" (site bu saatin yaklaşık olduğunu belirtir) veya "-" (o gün için vakit yok).
/// </summary>
public readonly record struct VakitSaati(TimeSpan? Zaman, bool Yaklasik)
{
    public bool Var => Zaman is not null;

    public static bool Gecerli(string hucre) => Regex.IsMatch(hucre, @"^\*?\d{2}:\d{2}$|^-$");

    public static VakitSaati Ayristir(string hucre)
    {
        bool yaklasik = hucre.StartsWith('*');
        string metin = hucre.TrimStart('*');
        return TimeSpan.TryParseExact(metin, @"hh\:mm", CultureInfo.InvariantCulture, out var zaman)
            ? new VakitSaati(zaman, yaklasik)
            : new VakitSaati(null, false);
    }

    /// <summary>Ekranda görünen metin: "05:41", "23:55*" ya da "—".</summary>
    public string Gorunen => Zaman is { } z ? z.ToString(@"hh\:mm", CultureInfo.InvariantCulture) + (Yaklasik ? "*" : "") : "—";

    /// <summary>Ekran okuyucunun okuyacağı metin: "05:41", "23:55, yaklaşık" ya da "yok".</summary>
    public string Okunan => Zaman is { } z
        ? z.ToString(@"hh\:mm", CultureInfo.InvariantCulture) + (Yaklasik ? ", yaklaşık" : "")
        : "yok";
}
