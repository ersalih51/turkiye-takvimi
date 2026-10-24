using System.Text.Json;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>JSON dosyalarını tek bir klasörde saklar. Okuma hatası "yok" sayılır, yazma güvenli (geçici dosya + taşıma) yapılır.</summary>
public sealed class DiskOnbellegi
{
    private static readonly JsonSerializerOptions Secenekler = new() { WriteIndented = false };

    public string Klasor { get; }

    public DiskOnbellegi(string? klasor = null)
    {
        Klasor = klasor ?? Path.Combine(VeriYolu.Kok, "onbellek");
        Directory.CreateDirectory(Klasor);
    }

    public T? Oku<T>(string ad) where T : class
    {
        string yol = Path.Combine(Klasor, ad + ".json");
        try
        {
            if (!File.Exists(yol)) return null;
            // Eski kayıtlarda kalmış görünmez karakterler (yumuşak tire vb.) okunurken atılır.
            return JsonSerializer.Deserialize<T>(GorunmezKarakterler.OnbellekMetni(File.ReadAllText(yol)), Secenekler);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Verilen önekle başlayan kayıtların adları (uzantısız).</summary>
    public List<string> DosyaAdlari(string onEk) =>
        new DirectoryInfo(Klasor).EnumerateFiles(onEk + "*.json")
            .Select(d => Path.GetFileNameWithoutExtension(d.Name)).ToList();

    /// <summary>Önbellekteki tüm dosyaların toplam boyutu (bayt); alt klasörler (Bilgiler yazıları) dahil.</summary>
    public long Boyut() =>
        new DirectoryInfo(Klasor).EnumerateFiles("*.json", SearchOption.AllDirectories).Sum(d => d.Length);

    /// <summary>Alt klasörlerdeki kayıtları (Bilgiler yazıları) siler; ana klasördekilere dokunmaz.</summary>
    public void AltKlasorleriSil()
    {
        foreach (var dosya in new DirectoryInfo(Klasor).EnumerateDirectories().SelectMany(k => k.EnumerateFiles("*.json", SearchOption.AllDirectories)).ToList())
        {
            try
            {
                dosya.Delete();
            }
            catch (IOException)
            {
                // Başka bir işlem kullanıyorsa atlanır.
            }
        }
    }

    /// <summary>Adı (uzantısız) filtreye uyan dosyaları siler; silinen dosya sayısını döndürür.</summary>
    public int Sil(Func<string, bool> filtre)
    {
        int silinen = 0;
        foreach (var dosya in new DirectoryInfo(Klasor).EnumerateFiles("*.json").ToList())
        {
            if (!filtre(Path.GetFileNameWithoutExtension(dosya.Name))) continue;
            try
            {
                dosya.Delete();
                silinen++;
            }
            catch (IOException)
            {
                // Başka bir işlem kullanıyorsa bu dosya atlanır.
            }
        }
        return silinen;
    }

    /// <summary>Toplam boyut sınırı aşılırsa en eski yazılan dosyalar silinir (disk büyümesin).</summary>
    public void EskileriSil(long sinirBayt)
    {
        var dosyalar = new DirectoryInfo(Klasor).EnumerateFiles("*.json").OrderBy(d => d.LastWriteTimeUtc).ToList();
        long toplam = dosyalar.Sum(d => d.Length);
        foreach (var dosya in dosyalar)
        {
            if (toplam <= sinirBayt) break;
            try
            {
                toplam -= dosya.Length;
                dosya.Delete();
            }
            catch (IOException)
            {
                // Başka bir işlem kullanıyorsa atlanır.
            }
        }
    }

    public void Yaz<T>(string ad, T deger)
    {
        string yol = Path.Combine(Klasor, ad + ".json");
        string gecici = yol + ".tmp";
        File.WriteAllText(gecici, JsonSerializer.Serialize(deger, Secenekler));
        File.Move(gecici, yol, overwrite: true);
    }
}
