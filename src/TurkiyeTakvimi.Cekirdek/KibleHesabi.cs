using System.Globalization;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>Bir yerden Kâbe'ye yön (kuzeyden saat yönünde derece) ve uzaklık.</summary>
public static class KibleHesabi
{
    private const double KabeEnlem = 21.4225;
    private const double KabeBoylam = 39.8262;
    private const double DunyaYaricapiKm = 6371.0;

    public static double Yon(double enlem, double boylam)
    {
        double f1 = Rad(enlem), f2 = Rad(KabeEnlem), dl = Rad(KabeBoylam - boylam);
        double y = Math.Sin(dl) * Math.Cos(f2);
        double x = Math.Cos(f1) * Math.Sin(f2) - Math.Sin(f1) * Math.Cos(f2) * Math.Cos(dl);
        return (Deg(Math.Atan2(y, x)) + 360.0) % 360.0;
    }

    public static double UzaklikKm(double enlem, double boylam)
    {
        double f1 = Rad(enlem), f2 = Rad(KabeEnlem), dl = Rad(KabeBoylam - boylam), df = f2 - f1;
        double a = Math.Sin(df / 2) * Math.Sin(df / 2) + Math.Cos(f1) * Math.Cos(f2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
        return 2 * DunyaYaricapiKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static readonly string[] Yonler =
        { "kuzey", "kuzeydoğu", "doğu", "güneydoğu", "güney", "güneybatı", "batı", "kuzeybatı" };

    public static string YonAdi(double derece) => Yonler[(int)Math.Round(derece / 45.0) % 8];

    /// <summary>Örn. "Kuzeyden saat yönünde 150 derece, güneydoğu. Kâbe'ye uzaklık yaklaşık 2.100 kilometre."</summary>
    public static string Metin(double enlem, double boylam)
    {
        double yon = Yon(enlem, boylam);
        double km = Math.Round(UzaklikKm(enlem, boylam) / 10.0) * 10.0;
        var tr = new CultureInfo("tr-TR");
        return $"Kuzeyden saat yönünde {Math.Round(yon).ToString("0", tr)} derece, {YonAdi(yon)}. Kâbe'ye uzaklık yaklaşık {km.ToString("N0", tr)} kilometre.";
    }

    /// <summary>Pusulayla kıble yönü: manyetik kuzeyden saat yönünde derece (gerçek yön eksi manyetik sapma).</summary>
    public static double ManyetikYon(double enlem, double boylam, DateTime tarih) =>
        ((Yon(enlem, boylam) - ManyetikSapma.Hesapla(enlem, boylam, tarih)) % 360.0 + 360.0) % 360.0;

    /// <summary>
    /// Örn. "Pusulayla kıble: manyetik kuzeyden saat yönünde yaklaşık 146 derece. Pusula bu yerde gerçek kuzeyin yaklaşık 6 derece doğusunu gösterir."
    /// </summary>
    public static string PusulaMetni(double enlem, double boylam, DateTime tarih)
    {
        double sapma = ManyetikSapma.Hesapla(enlem, boylam, tarih);
        double manyetik = ManyetikYon(enlem, boylam, tarih);
        var tr = new CultureInfo("tr-TR");
        string sapmaMetni = Math.Round(Math.Abs(sapma)) < 1
            ? "Bu yerde pusula neredeyse gerçek kuzeyi gösterir."
            : $"Pusula bu yerde gerçek kuzeyin yaklaşık {Math.Round(Math.Abs(sapma)).ToString("0", tr)} derece {(sapma > 0 ? "doğusunu" : "batısını")} gösterir.";
        return $"Pusulayla kıble: manyetik kuzeyden saat yönünde yaklaşık {Math.Round(manyetik).ToString("0", tr)} derece. {sapmaMetni}";
    }

    private static double Rad(double d) => d * Math.PI / 180.0;
    private static double Deg(double r) => r * 180.0 / Math.PI;
}
