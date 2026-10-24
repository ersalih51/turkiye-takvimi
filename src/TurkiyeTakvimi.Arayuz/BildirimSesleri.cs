using System.IO;
using System.Media;
using System.Windows;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Bildirimle çalabilen sesler: Windows'un varsayılan sesi ve uygulamayla gelen kısa sesler (Varliklar/Sesler).</summary>
public static class BildirimSesleri
{
    public sealed record Secenek(string Anahtar, string Ad, string? Dosya)
    {
        /// <summary>Ekran okuyucu liste satırının adını ToString'den okur.</summary>
        public override string ToString() => Ad;
    }

    public static IReadOnlyList<Secenek> Liste { get; } = new List<Secenek>
    {
        new("windows", "Windows varsayılanı", null),
        new("dalga", "Dalga", "dalga.wav"),
        new("kristal", "Kristal", "kristal.wav"),
        new("damla", "Damla", "damla.wav"),
        new("bulbul1", "Bülbül 1", "bulbul1.wav"),
        new("bulbul2", "Bülbül 2", "bulbul2.wav"),
        new("bulbul3", "Bülbül 3", "bulbul3.wav")
    };

    public static Secenek Bul(string? anahtar) =>
        Liste.FirstOrDefault(s => s.Anahtar == anahtar) ?? Liste[0];

    private static SoundPlayer? _calar; // çalarken toplanmasın; yeni ses önceki sesi keser

    public static void Cal(string? anahtar)
    {
        try
        {
            _calar?.Stop();
            _calar = null;

            var secenek = Bul(anahtar);
            if (secenek.Dosya is null)
            {
                SystemSounds.Asterisk.Play();
                return;
            }

            var kaynak = Application.GetResourceStream(new Uri($"pack://application:,,,/Varliklar/Sesler/{secenek.Dosya}"));
            if (kaynak is null)
            {
                SystemSounds.Asterisk.Play();
                return;
            }

            var bellek = new MemoryStream();
            kaynak.Stream.CopyTo(bellek);
            bellek.Position = 0;
            _calar = new SoundPlayer(bellek);
            _calar.Play();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UriFormatException)
        {
            SystemSounds.Asterisk.Play();
        }
    }
}
