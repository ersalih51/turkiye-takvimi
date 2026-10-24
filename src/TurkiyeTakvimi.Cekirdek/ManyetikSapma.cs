using System.Globalization;
using System.Reflection;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>
/// Manyetik sapma (deklinasyon): pusulanın gösterdiği manyetik kuzey ile gerçek kuzey arasındaki açı (doğuya doğru artı).
/// NOAA/NCEI Dünya Manyetik Modeli WMM2025 (2025.0-2030.0) kullanılır; internet gerekmez. Doğruluk yaklaşık yarım derecedir.
/// Model dosyasının geçerlilik süresi bittikten sonra da hesap yapılır, ama hata yıllar geçtikçe artar.
/// </summary>
public static class ManyetikSapma
{
    private const int Derece = 12;
    private const double Epoch = 2025.0;
    private const double YerYaricapiKm = 6371.2;   // modelin başvuru yarıçapı
    private const double A = 6378.137;              // WGS84 yarı büyük eksen (km)
    private const double F = 1 / 298.257223563;     // WGS84 basıklık
    private static readonly double E2 = F * (2 - F);

    private static readonly Lazy<double[,][]> Katsayi = new(Yukle);

    /// <summary>[n,m] -> {g, h, gDot, hDot}</summary>
    private static double[,][] Yukle()
    {
        var k = new double[Derece + 1, Derece + 1][];
        using var akis = Assembly.GetExecutingAssembly().GetManifestResourceStream("WMM2025.COF")
                         ?? throw new InvalidOperationException("WMM katsayı dosyası bulunamadı.");
        using var okuyucu = new StreamReader(akis);
        string? satir;
        while ((satir = okuyucu.ReadLine()) is not null)
        {
            string[] p = satir.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length != 6 || !int.TryParse(p[0], out int n) || !int.TryParse(p[1], out int m) || n > Derece || m > n) continue;
            k[n, m] = p.Skip(2).Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        }
        return k;
    }

    /// <summary>Manyetik sapma, derece (doğu artı). Enlem ve boylam derece, yükseklik kilometre (deniz seviyesi için 0).</summary>
    public static double Hesapla(double enlem, double boylam, DateTime tarih, double yuksekliKm = 0)
    {
        double yil = tarih.Year + (tarih.DayOfYear - 1) / (DateTime.IsLeapYear(tarih.Year) ? 366.0 : 365.0);
        return Hesapla(enlem, boylam, yil, yuksekliKm);
    }

    public static double Hesapla(double enlem, double boylam, double ondalikYil, double yuksekliKm = 0)
    {
        var (x, y, _) = Alan(enlem, boylam, ondalikYil, yuksekliKm);
        return Math.Atan2(y, x) * 180.0 / Math.PI;
    }

    /// <summary>Manyetik alanın kuzey (X), doğu (Y) ve aşağı (Z) bileşenleri, nanotesla. Doğrulama içindir.</summary>
    public static (double X, double Y, double Z) Alan(double enlem, double boylam, double ondalikYil, double yuksekliKm)
    {
        var k = Katsayi.Value;
        double dt = ondalikYil - Epoch;

        // Jeodezik enlemden jeosentrik küresel koordinatlara.
        enlem = Math.Clamp(enlem, -89.9, 89.9);
        double fi = enlem * Math.PI / 180.0, lam = boylam * Math.PI / 180.0;
        double sinFi = Math.Sin(fi), cosFi = Math.Cos(fi);
        double rc = A / Math.Sqrt(1 - E2 * sinFi * sinFi);
        double p = (rc + yuksekliKm) * cosFi;
        double z = (rc * (1 - E2) + yuksekliKm) * sinFi;
        double r = Math.Sqrt(p * p + z * z);
        double fiGeosentrik = Math.Asin(z / r);

        double ct = Math.Sin(fiGeosentrik), st = Math.Cos(fiGeosentrik); // kolatitüdün cos ve sin değerleri

        // Schmidt yarı-normalleştirilmiş Legendre fonksiyonları ve kolatitüde göre türevleri.
        var pnm = new double[Derece + 1, Derece + 1];
        var dpnm = new double[Derece + 1, Derece + 1];
        pnm[0, 0] = 1;
        for (int n = 1; n <= Derece; n++)
        {
            for (int m = 0; m <= n; m++)
            {
                if (n == m)
                {
                    double c = n == 1 ? 1.0 : Math.Sqrt((2.0 * n - 1) / (2.0 * n));
                    pnm[n, n] = c * st * pnm[n - 1, n - 1];
                    dpnm[n, n] = c * (ct * pnm[n - 1, n - 1] + st * dpnm[n - 1, n - 1]);
                }
                else
                {
                    double bolen = Math.Sqrt((double)n * n - (double)m * m);
                    double onceki2 = n >= 2 && m <= n - 2 ? pnm[n - 2, m] : 0;
                    double donceki2 = n >= 2 && m <= n - 2 ? dpnm[n - 2, m] : 0;
                    double kat = Math.Sqrt((double)(n - 1) * (n - 1) - (double)m * m);
                    pnm[n, m] = ((2.0 * n - 1) * ct * pnm[n - 1, m] - kat * onceki2) / bolen;
                    dpnm[n, m] = ((2.0 * n - 1) * (ct * dpnm[n - 1, m] - st * pnm[n - 1, m]) - kat * donceki2) / bolen;
                }
            }
        }

        double br = 0, bt = 0, bp = 0;
        double ra = YerYaricapiKm / r;
        double kat2 = ra * ra; // (a/r)^(n+2), n=0'dan başlayıp her adımda ra ile çarpılır
        for (int n = 1; n <= Derece; n++)
        {
            kat2 *= ra;
            for (int m = 0; m <= n; m++)
            {
                var c = k[n, m];
                if (c is null) continue;
                double g = c[0] + dt * c[2], h = c[1] + dt * c[3];
                double cm = Math.Cos(m * lam), sm = Math.Sin(m * lam);
                double gh = g * cm + h * sm;
                br += kat2 * (n + 1) * gh * pnm[n, m];
                bt -= kat2 * gh * dpnm[n, m];
                bp -= kat2 * m * (-g * sm + h * cm) * pnm[n, m] / st;
            }
        }

        // Jeosentrik bileşenlerden jeodezik kuzey-doğu-aşağı bileşenlerine.
        double xs = -bt, ys = bp, zs = -br;
        double psi = fiGeosentrik - fi;
        double xg = xs * Math.Cos(psi) - zs * Math.Sin(psi);
        double zg = xs * Math.Sin(psi) + zs * Math.Cos(psi);
        return (xg, ys, zg);
    }
}
