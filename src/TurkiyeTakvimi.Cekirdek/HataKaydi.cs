using System.Reflection;
using System.Text;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>
/// Beklenmeyen hataları bu bilgisayardaki küçük bir metin dosyasına yazar. Kayıt hiçbir yere gönderilmez;
/// yalnızca tarih, sürüm, Windows sürümü ve hatanın kendisi yazılır (yer, ayar ya da kişisel bilgi yazılmaz).
/// Dosya büyürse eskisi "hata.eski.log" olarak bir kez saklanır, böylece disk dolmaz.
/// </summary>
public static class HataKaydi
{
    private const long EnFazlaBayt = 512 * 1024;
    private static readonly object Kilit = new();

    public static string Klasor { get; } = Path.Combine(VeriYolu.Kok, "kayit");

    public static string Dosya => Path.Combine(Klasor, "hata.log");

    /// <summary>Henüz bir hata yazılmış mı (kayıt dosyası var mı).</summary>
    public static bool Var() => File.Exists(Dosya);

    /// <summary>Bir hatayı kaydeder. Kayıt sırasında oluşan sorun yutulur: hata kaydı asla uygulamayı düşürmemeli.</summary>
    public static void Yaz(string baglam, Exception? hata)
    {
        try
        {
            lock (Kilit)
            {
                Directory.CreateDirectory(Klasor);
                var dosya = new FileInfo(Dosya);
                if (dosya.Exists && dosya.Length > EnFazlaBayt)
                    File.Move(Dosya, Path.Combine(Klasor, "hata.eski.log"), overwrite: true);

                var sb = new StringBuilder();
                sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ")
                  .Append("sürüm ").Append(VeriYolu.UygulamaSurumu)
                  .Append(" | ").Append(Environment.OSVersion.VersionString)
                  .Append(" | ").AppendLine(baglam);
                if (hata is not null) sb.AppendLine(hata.ToString());
                sb.AppendLine("----");
                File.AppendAllText(Dosya, sb.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Kayıt yazılamadıysa yapılacak bir şey yok.
        }
    }
}
