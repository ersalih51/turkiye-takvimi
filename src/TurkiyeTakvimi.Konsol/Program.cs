using System.Globalization;
using System.Text;
using TurkiyeTakvimi.Cekirdek;

Console.OutputEncoding = Encoding.UTF8;

const string VarsayilanSehir = "16741"; // İstanbul

// TT_CEVRIMDISI=1 ayarlanırsa internet yokmuş gibi davranır (önbelleği sınamak için).
bool cevrimdisi = Environment.GetEnvironmentVariable("TT_CEVRIMDISI") == "1";
HttpClient http = cevrimdisi
    ? new HttpClient(new InternetYokHandler())
    : TurktakvimIstemci.VarsayilanHttpIstemcisi();

var onbellek = new DiskOnbellegi();
var istemci = new TurktakvimIstemci(http);
var vakitler = new VakitServisi(istemci, onbellek);
var icerik = new IcerikServisi(istemci, onbellek);

string komut = args.Length > 0 ? args[0].ToLowerInvariant() : "bugun";

try
{
    switch (komut)
    {
        case "bugun":
            await BugunuGoster(args.Length > 1 ? args[1] : VarsayilanSehir);
            return 0;
        case "ara" when args.Length > 1:
            await Ara(string.Join(' ', args.Skip(1)));
            return 0;
        case "icerik":
            await Icerik(args.Length > 1 ? DateOnly.ParseExact(args[1], "yyyy-MM-dd", CultureInfo.InvariantCulture) : BugunTarihi());
            return 0;
        case "yil":
            await Yil(args.Length > 1 ? args[1] : VarsayilanSehir,
                args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : DateTime.Now.Year);
            return 0;
        case "icerikyil":
        {
            int yil = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : DateTime.Now.Year;
            var gunler = Enumerable.Range(0, new DateOnly(yil, 12, 31).DayOfYear)
                .Select(i => new DateOnly(yil, 1, 1).AddDays(i));
            var sure = System.Diagnostics.Stopwatch.StartNew();
            int sayi = (await icerik.GunleriHazirlaAsync(gunler, TimeSpan.FromMilliseconds(args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 300))).Indirilen;
            Console.WriteLine($"{yil}: {sayi} günün içeriği indirildi ({sure.Elapsed.TotalSeconds:F0} sn).");
            return 0;
        }
        case "imsakiye":
        {
            var servis = new ImsakiyeServisi(istemci, icerik, vakitler, onbellek);
            var im = await servis.HazirlaAsync(args.Length > 1 ? args[1] : VarsayilanSehir, "Şehir", BugunTarihi());
            if (im is null) { Console.WriteLine("Sitede imsakiye için hicrî tarih verisi bulunamadı."); return 1; }
            Console.WriteLine($"Hicrî {im.Aralik.HicriYil}, Milâdî {im.MiladiYil}: {im.Aralik.Baslangic:dd.MM.yyyy} - {im.Aralik.Bitis:dd.MM.yyyy} ({im.Aralik.GunSayisi} gün), bayram {im.Aralik.BayramGunu:dd.MM.yyyy} güneş {im.BayramGunuGunes}");
            foreach (var g in im.Gunler) Console.WriteLine($"{g.Gun,2} {g.Tarih:dd.MM ddd}  {string.Join("  ", g.Saatler)}");
            return 0;
        }
        case "mubarek":
        {
            int yil = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : BugunTarihi().Year;
            var servis = new MubarekGunServisi(icerik, onbellek);
            var liste = await servis.YiliHazirlaAsync(yil);
            Console.WriteLine(liste.Count == 0 ? "Bu yıl için hicrî tarih verisi yok." : $"{yil}: {liste.Count} mübarek gün/gece");
            foreach (var g in liste)
                Console.WriteLine($"{g.Tarih:dd.MM.yyyy ddd}{(g.Bitis is { } b ? $" - {b:dd.MM}" : "")}  {g.Ad} ({g.Tur}) — {g.HicriTarih}");
            return 0;
        }
        case "metinkontrol":
        {
            // Siteden gelen metinlerde yapısal sorun (görünmez karakter, bozuk kodlama vb.) var mı? Uygulamaya her metin kaynağı eklendiğinde çalıştırılır.
            int toplam = 0, sorunlu = 0;
            void Bak(string ad, string? metin)
            {
                toplam++;
                var sorunlar = MetinDenetcisi.Incele(metin);
                if (sorunlar.Count == 0) return;
                sorunlu++;
                Console.WriteLine($"SORUN  {ad}: {string.Join("; ", sorunlar)}");
            }

            // 1) Ekrana gidecek taze metinler: bugünün içeriği, Bilgiler listesi ve her kategoriden bir yazı.
            var bugunTarih = BugunTarihi();
            var ic = await istemci.GunlukIcerikIndirAsync(bugunTarih);
            Bak("Günün sözü", ic.Soz);
            foreach (var (olay, i) in ic.Olaylar.Select((o, i) => (o, i))) Bak($"Günün olayı {i + 1}", olay);
            Bak("Arka yaprak başlığı", ic.ArkaYaprakBaslik);
            Bak("Arka yaprak metni", ic.ArkaYaprakMetin);
            Bak("Günün eki", ic.GununEki);
            if (ic.Bilgi is { } bilgi) foreach (var (satir, i) in bilgi.Satirlar.Select((s, i) => (s, i))) Bak($"Künye satırı {i + 1}", satir);

            var bilgiler = new BilgilerServisi(istemci, onbellek);
            var indeks = await bilgiler.IndeksiGetirAsync(DateTime.Now);
            foreach (var kat in indeks.Kategoriler)
            {
                Bak($"Kategori adı: {kat.Ad}", kat.Ad);
                foreach (var oge in kat.Ogeler) Bak($"Başlık: {oge.Baslik}", oge.Baslik);
                if (kat.Ogeler.FirstOrDefault(o => o.Yazi) is { } ilk)
                {
                    var yazi = await bilgiler.YaziGetirAsync(ilk);
                    foreach (var (p, i) in yazi.Paragraflar.Select((p, i) => (p, i))) Bak($"Yazı '{ilk.Baslik}' paragraf {i + 1}", p);
                }
            }

            // 2) Önbellekteki eski kayıtlar: okunurken zaten temizlenir; kaç dosyada izi kaldığı bilgi içindir.
            int izli = 0;
            foreach (var dosya in Directory.EnumerateFiles(onbellek.Klasor, "*.json", SearchOption.AllDirectories))
            {
                string ham = File.ReadAllText(dosya);
                if (GorunmezKarakterler.OnbellekMetni(ham).Length != ham.Length) izli++;
            }

            Console.WriteLine($"{toplam} metin denetlendi, {sorunlu} sorunlu. Önbellekte görünmez karakter izi olan dosya: {izli} (okunurken temizlenir).");
            return sorunlu == 0 ? 0 : 1;
        }
        case "sapma" when args.Length > 2:
        {
            // sapma <enlem> <boylam> [ondalık yıl] [yükseklik km]: manyetik sapma ve alan bileşenleri (NOAA test değerleriyle karşılaştırmak için).
            double enlem = double.Parse(args[1], CultureInfo.InvariantCulture), boylam = double.Parse(args[2], CultureInfo.InvariantCulture);
            double yil = args.Length > 3 ? double.Parse(args[3], CultureInfo.InvariantCulture) : DateTime.Now.Year + (DateTime.Now.DayOfYear - 1) / 365.0;
            double km = args.Length > 4 ? double.Parse(args[4], CultureInfo.InvariantCulture) : 0;
            var (x, y, z) = ManyetikSapma.Alan(enlem, boylam, yil, km);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"D={ManyetikSapma.Hesapla(enlem, boylam, yil, km):0.00} X={x:0.0} Y={y:0.0} Z={z:0.0}"));
            return 0;
        }
        case "bilgiler":
        {
            var servis = new BilgilerServisi(istemci, onbellek);
            var indeks = await servis.IndeksiGetirAsync(DateTime.Now);
            if (args.Length < 2)
            {
                foreach (var k in indeks.Kategoriler)
                    Console.WriteLine($"{k.Ad}: {k.Ogeler.Count} başlık, {k.Ogeler.Count(o => o.Yazi)} yazı");
                return 0;
            }
            // bilgiler <kategori sırası> [başlık sırası]
            var kat = indeks.Kategoriler[int.Parse(args[1], CultureInfo.InvariantCulture)];
            if (args.Length < 3)
            {
                for (int i = 0; i < kat.Ogeler.Count; i++) Console.WriteLine($"{i}  {(kat.Ogeler[i].Yazi ? "" : "[site] ")}{kat.Ogeler[i].Baslik}");
                return 0;
            }
            var oge = kat.Ogeler[int.Parse(args[2], CultureInfo.InvariantCulture)];
            var yazi = await servis.YaziGetirAsync(oge);
            Console.WriteLine($"# {yazi.Baslik} ({yazi.Paragraflar.Count} paragraf)");
            foreach (string p in yazi.Paragraflar.Take(15)) Console.WriteLine(p.Length > 300 ? p[..300] + "…" : p);
            return 0;
        }
        case "menu":
            await Menu();
            return 0;
        case "onbellek":
            Console.WriteLine($"Önbellek klasörü: {onbellek.Klasor}");
            return 0;
        default:
            Yardim();
            return komut is "yardim" or "-h" or "--help" ? 0 : 1;
    }
}
catch (VeriHatasi ex)
{
    Console.WriteLine($"Hata: {ex.Message}");
    return 1;
}
catch (FormatException)
{
    Console.WriteLine("Tarih veya yıl okunamadı. Tarih biçimi: 2026-10-22");
    return 1;
}

// TT_BUGUN=yyyy-MM-dd ile sınama için başka bir gün taklit edilebilir.
static DateOnly BugunTarihi() =>
    DateOnly.TryParseExact(Environment.GetEnvironmentVariable("TT_BUGUN"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var taklit)
        ? taklit : DateOnly.FromDateTime(DateTime.Now);

async Task Menu()
{
    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("===== Türkiye Takvimi =====");
        Console.WriteLine("1 - Bugünün vakitleri ve günlük içerik (İstanbul)");
        Console.WriteLine("2 - Şehir ara");
        Console.WriteLine("3 - Başka bir şehrin vakitleri (şehir kodu ile)");
        Console.WriteLine("4 - Belirli bir günün içeriği");
        Console.WriteLine("0 - Çıkış");
        string? secim = Sor("Seçiminiz (0 ile 4 arası), sonra Enter: ");
        if (secim is null || secim == "0") return;

        try
        {
            switch (secim)
            {
                case "1":
                    await BugunuGoster(VarsayilanSehir);
                    break;
                case "2":
                    string? ad = Sor("Aranacak şehir adı: ");
                    if (!string.IsNullOrWhiteSpace(ad)) await Ara(ad);
                    break;
                case "3":
                    string? kod = Sor("Şehir kodu (örnek: 16742): ");
                    if (!string.IsNullOrWhiteSpace(kod)) await BugunuGoster(kod.Trim());
                    break;
                case "4":
                    string? t = Sor("Tarih (örnek: 2026-10-23, boş bırakırsanız bugün): ");
                    DateOnly tarih = string.IsNullOrWhiteSpace(t)
                        ? BugunTarihi()
                        : DateOnly.ParseExact(t.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    await Icerik(tarih);
                    break;
                default:
                    Console.WriteLine("Geçersiz seçim.");
                    break;
            }
        }
        catch (VeriHatasi ex)
        {
            Console.WriteLine($"Hata: {ex.Message}");
        }
        catch (FormatException)
        {
            Console.WriteLine("Tarih okunamadı. Tarih biçimi: 2026-10-22");
        }
    }
}

static string? Sor(string soru)
{
    Console.Write(soru);
    return Console.ReadLine();
}

void Yardim()
{
    Console.WriteLine("Kullanım:");
    Console.WriteLine("  bugun [sehirKodu]          Bugünün vakitleri ve içerikleri (varsayılan: İstanbul)");
    Console.WriteLine("  ara <sehir adı>            Şehir kodunu bulur");
    Console.WriteLine("  icerik [yyyy-AA-gg]        Günün Sözü, Olayı ve Arka Yaprak");
    Console.WriteLine("  yil [sehirKodu] [yıl]      Bir yılın vakitlerini indirip önbelleğe alır");
    Console.WriteLine("  onbellek                   Önbellek klasörünü gösterir");
}

async Task BugunuGoster(string sehirKodu)
{
    var bugun = BugunTarihi();
    VakitSonucu sonuc;
    try
    {
        sonuc = await vakitler.GunuGetirAsync(sehirKodu, bugun);
    }
    catch (VeriHatasi)
    {
        Console.WriteLine("Vakitler alınamadı: önbellekte kayıt yok ve internete bağlanılamadı.");
        return;
    }

    Console.WriteLine($"Şehir: {sonuc.SehirAdi}   Tarih: {bugun:dd MMMM yyyy dddd}   ({KaynakAdi(sonuc.Kaynak)})");
    Console.WriteLine();
    Console.WriteLine("Ana vakitler:");
    foreach (int i in VakitAdlari.Ana)
        Console.WriteLine($"  {VakitAdlari.Hepsi[i]}: {VakitSaati.Ayristir(sonuc.Gun.Saatler[i]).Okunan}");
    Console.WriteLine();
    Console.WriteLine("Diğer vakitler:");
    for (int i = 0; i < VakitAdlari.Hepsi.Length; i++)
        if (!VakitAdlari.AnaMi(i))
            Console.WriteLine($"  {VakitAdlari.Hepsi[i]}: {VakitSaati.Ayristir(sonuc.Gun.Saatler[i]).Okunan}");
    Console.WriteLine();
    SiradakiVakit(sonuc.Gun);

    Console.WriteLine();
    await Icerik(bugun);
    await vakitler.ArkaPlandaHazirlaAsync(sehirKodu, bugun);
}

void SiradakiVakit(GunlukVakit gun)
{
    DateTime simdi = DateTime.Now;
    foreach (int i in VakitAdlari.Ana)
    {
        var saat = VakitSaati.Ayristir(gun.Saatler[i]);
        if (saat.Zaman is not { } z) continue;
        DateTime vakit = simdi.Date + z;
        if (vakit > simdi)
        {
            TimeSpan kalan = vakit - simdi;
            Console.WriteLine($"Sıradaki vakit: {VakitAdlari.Hepsi[i]} ({saat.Okunan}) — kalan süre: {(int)kalan.TotalHours} saat {kalan.Minutes} dakika");
            return;
        }
    }
    Console.WriteLine("Bugünün ana vakitleri tamamlandı. Sıradaki vakit yarın imsak.");
}

async Task Icerik(DateOnly tarih)
{
    GunlukIcerik ic;
    try
    {
        ic = await icerik.GetirAsync(tarih);
    }
    catch (VeriHatasi)
    {
        Console.WriteLine("Günlük içerik alınamadı: önbellekte kayıt yok ve internete bağlanılamadı.");
        return;
    }

    Console.WriteLine($"— {tarih:dd MMMM yyyy} —");
    Console.WriteLine();
    Console.WriteLine("Günün Bilgileri:");
    if (ic.Bilgi is null) Console.WriteLine("  (yok)");
    else foreach (string satir in ic.Bilgi.Satirlar) Console.WriteLine("  " + satir);
    Console.WriteLine();
    Console.WriteLine("Günün Sözü:");
    Console.WriteLine(ic.Soz is null ? "  (yok)" : "  " + ic.Soz.Replace("\n", "\n  "));
    Console.WriteLine();
    Console.WriteLine("Günün Olayı:");
    if (ic.Olaylar.Count == 0) Console.WriteLine("  (yok)");
    foreach (string olay in ic.Olaylar) Console.WriteLine("  " + olay);
    Console.WriteLine();
    Console.WriteLine("Arka Yaprak:");
    if (ic.ArkaYaprakBaslik is null)
    {
        Console.WriteLine("  (yok)");
        return;
    }
    Console.WriteLine("  " + ic.ArkaYaprakBaslik);
    Console.WriteLine();
    Console.WriteLine(ic.ArkaYaprakMetin is null ? "  (metin yok)" : ic.ArkaYaprakMetin);
    if (ic.GununEki is not null)
    {
        Console.WriteLine();
        Console.WriteLine(ic.GununEki);
    }
}

async Task Ara(string ad)
{
    var sonuclar = await istemci.SehirAraAsync(ad);
    if (sonuclar.Count == 0)
    {
        Console.WriteLine("Aradığınız şehir bulunamadı.");
        return;
    }
    foreach (var grup in sonuclar.GroupBy(s => s.Ulke))
    {
        Console.WriteLine($"{grup.Key}:");
        foreach (var s in grup) Console.WriteLine($"  {s.Kod} = {s.Ad}");
    }
    Console.WriteLine();
    Console.WriteLine("Vakitleri görmek için: bugun <kod>");
}

async Task Yil(string sehirKodu, int yil)
{
    bool yeni = await vakitler.YiliHazirlaAsync(sehirKodu, yil);
    Console.WriteLine(yeni
        ? $"{yil} yılı indirildi ve önbelleğe kaydedildi."
        : $"{yil} yılı zaten önbellekte.");
}

static string KaynakAdi(VeriKaynagi k) => k == VeriKaynagi.Onbellek ? "diskten" : "internetten indirildi";

sealed class InternetYokHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("İnternet yok (sınama modu).");
}
