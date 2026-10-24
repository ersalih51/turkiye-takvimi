using System.Text.Json;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>Kullanıcının kalıcı ayarları. Yeni ayarlar eklenirken varsayılan değerleri burada verilir.</summary>
/// <summary>"Bu vakti bir kez bildir": belirli bir günün belirli vaktinde tek seferlik bildirim. Bildirim verilince silinir.</summary>
public sealed class TekBildirim
{
    /// <summary>Gün, "yyyy-MM-dd".</summary>
    public string Gun { get; set; } = "";

    /// <summary>Vakit numarası (VakitAdlari.Hepsi sırası).</summary>
    public int Vakit { get; set; }
}

public sealed class Ayarlar
{
    /// <summary>
    /// Sağ tık menüsünden kurulan tek seferlik vakit bildirimleri. Bildirim ayarlarından (hangi vakitler, kaç dakika önce, sessiz saatler)
    /// bağımsızdır: kullanıcı özellikle istediği için bildirimler kapalı olsa da, sessiz saatte de verilir.
    /// </summary>
    public List<TekBildirim> TekBildirimler { get; set; } = new();

    public const string TemaSistem = "sistem";
    public const string TemaAcik = "acik";
    public const string TemaKoyu = "koyu";

    public string Tema { get; set; } = TemaSistem;

    /// <summary>Arayüz yazı boyutu çarpanı (1,0 = normal, en çok 2,0). Az görenler için.</summary>
    public double Olcek { get; set; } = 1.0;
    /// <summary>Seçili yerin turktakvim.com kodu. Boşsa henüz şehir seçilmemiştir (ilk açılışta seçtirilir).</summary>
    public string SehirKodu { get; set; } = "";

    /// <summary>Seçili yerin görünen adı (örn. "Niğde-Niğde").</summary>
    public string SehirAdi { get; set; } = "";

    /// <summary>En son kullanılan şehir kodları, en yenisi başta. Diskte yalnızca bunların vakitleri saklanır.</summary>
    public List<string> SonSehirler { get; set; } = new();

    /// <summary>Son kullanılan şehirlerin görünen adları (kod → ad); hızlı şehir değiştirme menüsü için.</summary>
    public Dictionary<string, string> SehirAdlari { get; set; } = new();

    /// <summary>Açıksa içinde bulunulan yılın günlük içeriği internet varken arka planda indirilir.</summary>
    public bool ArkaPlandaIndir { get; set; } = true;

    /// <summary>
    /// Bildirim istenen vakitlerin numaraları (<c>VakitAdlari.Hepsi</c> sırası, 0-17). Varsayılan boş: hiçbir vakit için bildirim gelmez.
    /// </summary>
    public List<int> BildirimVakitleri { get; set; } = new();

    /// <summary>Bildirimle birlikte kısa bir uyarı sesi çalınır.</summary>
    public bool BildirimSesi { get; set; } = true;

    /// <summary>Bildirimle çalan sesin anahtarı ("windows" = Windows'un varsayılan sesi; diğerleri uygulamayla gelen sesler).</summary>
    public string BildirimSesAdi { get; set; } = "windows";

    /// <summary>Vakitten kaç dakika önce hatırlatılacağı; 0 = önceden hatırlatma yok.</summary>
    public int OncedenDakika { get; set; } = 0;

    /// <summary>Açıksa sessiz saatlerde bildirim gösterilmez.</summary>
    public bool SessizSaatler { get; set; }

    public string SessizBaslangic { get; set; } = "23:00";
    public string SessizBitis { get; set; } = "04:00";

    /// <summary>Pencere kapatılınca uygulama bildirim alanında çalışmaya devam eder (bildirimler için gerekir).</summary>
    public bool TepsideCalis { get; set; } = true;

    /// <summary>"Tepside çalışıyor" bilgisi bir kez gösterildi.</summary>
    public bool TepsiBilgisiGosterildi { get; set; }

    /// <summary>İlk açılışta gösterilen Hoş geldiniz penceresi bir kez gösterildi mi (kullanıcıya görünen bir ayar değildir).</summary>
    public bool KarsilamaGosterildi { get; set; }

    /// <summary>Ramazan ayında iftar ve sahur bilgisi öne çıkarılır.</summary>
    public bool RamazanBilgisi { get; set; } = true;

    /// <summary>Diğer Vakitler listesinde Ezânî (alaturka saat) vakitleri de gösterilir. Varsayılan kapalı.</summary>
    public bool EzaniGoster { get; set; }

    /// <summary>Win+Alt+V: uygulama arka plandayken bile sıradaki vakti ve kalan süreyi bildirim olarak söyler.</summary>
    public bool KisayolAcik { get; set; } = true;

    /// <summary>Mübarek gün ve gecelerde (kandil, bayram…) bir gün önce akşam ve o gün sabah bildirim verilir. Varsayılan kapalı.</summary>
    public bool MubarekGunBildirimi { get; set; }
}

/// <summary>Ayarları LocalAppData\TurkiyeTakvimi\ayarlar.json dosyasında saklar. Bozuk ya da eksik dosya varsayılanlara döner.</summary>
public sealed class AyarDeposu
{
    private static readonly JsonSerializerOptions Secenekler = new() { WriteIndented = true };

    private readonly string _yol;

    public AyarDeposu(string? klasor = null)
    {
        klasor ??= VeriYolu.Kok;
        Directory.CreateDirectory(klasor);
        _yol = Path.Combine(klasor, "ayarlar.json");
    }

    public Ayarlar Yukle()
    {
        try
        {
            if (!File.Exists(_yol)) return new Ayarlar();
            using var akis = File.OpenRead(_yol);
            return JsonSerializer.Deserialize<Ayarlar>(akis, Secenekler) ?? new Ayarlar();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Ayarlar();
        }
    }

    public void Kaydet(Ayarlar ayarlar)
    {
        string gecici = _yol + ".tmp";
        File.WriteAllText(gecici, JsonSerializer.Serialize(ayarlar, Secenekler));
        File.Move(gecici, _yol, overwrite: true);
    }
}
