// Bildirim seslerini sentezler: deniz dalgası (stereo), çan ve damla (mono). 22.05 kHz, 16 bit WAV.
using System.Text;

const int SR = 22050;

// Kullanım: sesgen kes <kaynak.ogg> <çıktı klasörü> <ad> <süre sn> <aday sayısı>
if (args.Length >= 5 && args[0] == "kes")
{
    Kes(args[1], args[2], args[3], double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture), args.Length > 5 ? int.Parse(args[5]) : 3);
    return;
}

string hedef = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(hedef);

DenizDalgasi(Path.Combine(hedef, "dalga.wav"));
Can(Path.Combine(hedef, "kristal.wav"));
Damla(Path.Combine(hedef, "damla.wav"));
Console.WriteLine("tamam");

// Uzun bir kaydı tek kanala indirir, 22.05 kHz'e çevirir, en yoğun ötüşlü pencereleri aday olarak keser.
static void Kes(string kaynak, string klasor, string ad, double sure, int adet)
{
    Directory.CreateDirectory(klasor);
    using var okuyucu = new NAudio.Vorbis.VorbisWaveReader(kaynak);
    int sr0 = okuyucu.WaveFormat.SampleRate, ch = okuyucu.WaveFormat.Channels;
    var tum = new List<float>();
    var tampon = new float[sr0 * ch];
    int oku;
    while ((oku = okuyucu.Read(tampon, 0, tampon.Length)) > 0)
        for (int i = 0; i + ch <= oku; i += ch)
        {
            float s = 0; for (int c = 0; c < ch; c++) s += tampon[i + c];
            tum.Add(s / ch);
        }
    Console.WriteLine($"kaynak: {sr0} Hz, {ch} kanal, {tum.Count / (double)sr0:F1} sn");

    // 22.05 kHz'e: basit alçak geçiren ortalama + doğrusal aradeğer.
    double oran = (double)sr0 / SR;
    int pencere = Math.Max(1, (int)Math.Round(oran));
    var mono = new float[(int)(tum.Count / oran)];
    for (int i = 0; i < mono.Length; i++)
    {
        double konum = i * oran;
        int a = (int)konum;
        float topla = 0; int say = 0;
        for (int j = -pencere / 2; j <= pencere / 2; j++) { int k = a + j; if (k >= 0 && k < tum.Count) { topla += tum[k]; say++; } }
        mono[i] = topla / Math.Max(1, say);
    }

    // Düşük frekanstaki rüzgâr ve uğultuyu azaltmak için hafif yüksek geçiren (~250 Hz).
    float onceki = 0, onceki2 = 0; double alfa = 1 / (1 + 2 * Math.PI * 250 / SR);
    for (int i = 0; i < mono.Length; i++) { float x = mono[i]; float y = (float)(alfa * (onceki2 + x - onceki)); onceki = x; onceki2 = y; mono[i] = y; }

    int cerceve = SR / 20; // 50 ms
    int nc = mono.Length / cerceve;
    var rms = new double[nc];
    for (int c = 0; c < nc; c++) { double t = 0; for (int i = 0; i < cerceve; i++) { double v = mono[c * cerceve + i]; t += v * v; } rms[c] = Math.Sqrt(t / cerceve); }

    int pen = (int)(sure * 20);
    var adaylar = new List<(int Bas, double Puan)>();
    for (int s = 0; s + pen < nc; s += 2)
    {
        double ort = 0, kare = 0; for (int i = 0; i < pen; i++) { ort += rms[s + i]; kare += rms[s + i] * rms[s + i]; }
        ort /= pen; double std = Math.Sqrt(Math.Max(0, kare / pen - ort * ort));
        // Yoğun ve değişken (ötüş içeren) pencereler; başlangıç sessizlikten sonra gelen bir girişe yakınsa artı puan.
        double giris = s > 4 ? (rms[s + 1] > 1.5 * rms[s - 4] ? 1.15 : 1.0) : 1.0;
        adaylar.Add((s, (ort + 0.5 * std) * giris));
    }
    var secilen = new List<int>();
    foreach (var a in adaylar.OrderByDescending(x => x.Puan))
    {
        if (secilen.All(b => Math.Abs(b - a.Bas) > pen + 20)) secilen.Add(a.Bas);
        if (secilen.Count >= adet) break;
    }

    int n = 1;
    foreach (int bas in secilen.OrderBy(x => x))
    {
        int basOrnek = bas * cerceve, uz = (int)(sure * SR);
        var parca = new float[uz];
        Array.Copy(mono, basOrnek, parca, 0, Math.Min(uz, mono.Length - basOrnek));
        Soldur(parca, 0.08, 0.5);
        var k = new[] { parca };
        Normalle(k, 0.5f);
        string yol = Path.Combine(klasor, $"{ad}-{n}.wav");
        Yaz(yol, k);
        Console.WriteLine($"{yol}  kaynakta {bas / 20.0:F1}. saniyeden başlar, {new FileInfo(yol).Length / 1024} KB");
        n++;
    }
}

static void Yaz(string yol, float[][] kanallar)
{
    int n = kanallar[0].Length, ch = kanallar.Length;
    using var f = File.Create(yol);
    using var w = new BinaryWriter(f);
    w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * ch * 2);
    w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)ch);
    w.Write(SR); w.Write(SR * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
    w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * ch * 2);
    for (int i = 0; i < n; i++)
        for (int c = 0; c < ch; c++)
            w.Write((short)Math.Clamp(kanallar[c][i] * 32767f, -32768f, 32767f));
}

static void Normalle(float[][] kanallar, float tepe)
{
    float en = kanallar.SelectMany(k => k).Max(Math.Abs);
    if (en <= 0) return;
    float k2 = tepe / en;
    foreach (var k in kanallar) for (int i = 0; i < k.Length; i++) k[i] *= k2;
}

static void Soldur(float[] x, double bas, double son)
{
    int n = x.Length;
    int a = (int)(bas * SR), b = (int)(son * SR);
    for (int i = 0; i < Math.Min(a, n); i++) x[i] *= (float)(0.5 - 0.5 * Math.Cos(Math.PI * i / a));
    for (int i = 0; i < Math.Min(b, n); i++) x[n - 1 - i] *= (float)(0.5 - 0.5 * Math.Cos(Math.PI * i / b));
}

// Soldan sağa, sonra sağdan sola vuran iki dalga.
static void DenizDalgasi(string yol)
{
    double sure = 5.4;
    int n = (int)(sure * SR);
    var rnd = new Random(7);
    var solGurultu = new float[n]; var sagGurultu = new float[n];
    double lpS1 = 0, lpS2 = 0, lpS3 = 0, lpR1 = 0, lpR2 = 0, lpR3 = 0; // sol/sağ için üç süzgeç (düşük, orta, tiz)
    var sol = new float[n]; var sag = new float[n];

    for (int i = 0; i < n; i++)
    {
        double t = (double)i / SR;
        // İki dalga: 0-2.7 sn ve 2.7-5.4 sn. Her dalga yükselip köpürerek çekilir.
        double u = (t % 2.7) / 2.7;
        double zarf = u < 0.38 ? Math.Pow(Math.Sin(Math.PI / 2 * u / 0.38), 2.0) : Math.Exp(-(u - 0.38) * 3.6);
        // Kıyıya vururken tiz (köpük) artar: kesim frekansı zarfı izler.
        double kesim = 450 + 3200 * Math.Pow(zarf, 1.6);
        double a2 = 1 - Math.Exp(-2 * Math.PI * kesim / SR);
        double a1 = 1 - Math.Exp(-2 * Math.PI * 220 / SR);
        double a3 = 1 - Math.Exp(-2 * Math.PI * 5200 / SR);

        double ws = rnd.NextDouble() * 2 - 1, wr = rnd.NextDouble() * 2 - 1;
        lpS1 += a1 * (ws - lpS1); lpS2 += a2 * (ws - lpS2); lpS3 += a3 * (ws - lpS3);
        lpR1 += a1 * (wr - lpR1); lpR2 += a2 * (wr - lpR2); lpR3 += a3 * (wr - lpR3);

        double mixS = 1.7 * lpS1 + 1.1 * lpS2 + 0.35 * zarf * lpS3;
        double mixR = 1.7 * lpR1 + 1.1 * lpR2 + 0.35 * zarf * lpR3;

        // Konum: ilk dalgada -1'den +1'e, ikincide +1'den -1'e (sabit güç paylaşımı).
        double dalga = t < 2.7 ? t / 2.7 : 1 - (t - 2.7) / 2.7;
        double p = -1 + 2 * dalga;
        double ang = (p + 1) * Math.PI / 4;
        double sesli = 0.12 + 0.88 * zarf;
        sol[i] = (float)(mixS * sesli * Math.Cos(ang) * 1.2 + mixR * sesli * Math.Cos(ang) * 0.0);
        sag[i] = (float)(mixR * sesli * Math.Sin(ang) * 1.2);
        // Her kulakta biraz da karşı kanal (daha doğal, kulaklıkta rahatsız etmesin).
        sol[i] += (float)(mixR * sesli * 0.12);
        sag[i] += (float)(mixS * sesli * 0.12);
    }
    var k = new[] { sol, sag };
    foreach (var x in k) Soldur(x, 0.25, 0.7);
    Normalle(k, 0.5f);
    Yaz(yol, k);
}

static void Nota(float[] y, double baslangic, double f0, double[] oran, double[] genlik, double[] omur, double ata = 0.004)
{
    int bas = (int)(baslangic * SR);
    double enUzun = omur.Max() * 5;
    int uz = (int)(enUzun * SR);
    for (int i = 0; i < uz && bas + i < y.Length; i++)
    {
        double t = (double)i / SR;
        double v = 0;
        for (int p = 0; p < oran.Length; p++)
            v += genlik[p] * Math.Exp(-t / omur[p]) * Math.Sin(2 * Math.PI * f0 * oran[p] * t);
        double giris = t < ata ? t / ata : 1;
        y[bas + i] += (float)(v * giris);
    }
}

// Yumuşak çan: iki yükselen not.
static void Can(string yol)
{
    var y = new float[(int)(2.0 * SR)];
    double[] oran = { 1, 2.0, 2.76, 5.4 }, gen = { 1, 0.42, 0.22, 0.08 }, omur = { 0.55, 0.32, 0.2, 0.1 };
    Nota(y, 0.00, 659.25, oran, gen, omur);   // Mi5
    Nota(y, 0.26, 987.77, oran, gen, omur);   // Si5
    Soldur(y, 0.002, 0.35);
    var k = new[] { y };
    Normalle(k, 0.5f);
    Yaz(yol, k);
}

// Marimba benzeri kısa damla: iki hızlı not.
static void Damla(string yol)
{
    var y = new float[(int)(1.4 * SR)];
    double[] oran = { 1, 4.0, 9.9 }, gen = { 1, 0.35, 0.08 }, omur = { 0.18, 0.05, 0.02 };
    Nota(y, 0.00, 1046.5, oran, gen, omur, 0.002);  // Do6
    Nota(y, 0.15, 1568.0, oran, gen, omur, 0.002);  // Sol6
    Nota(y, 0.30, 2093.0, oran, gen, omur, 0.002);  // Do7
    Soldur(y, 0.002, 0.25);
    var k = new[] { y };
    Normalle(k, 0.45f);
    Yaz(yol, k);
}
