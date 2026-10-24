using System.Text.Json;

namespace TurkiyeTakvimi.Cekirdek;

/// <summary>
/// İnternet adresinden (IP) kabaca bulunduğunuz yeri tahmin eder. Kullanıcı isteyince çalışır; adres, tahmin servisine
/// (ipwho.is) gönderilir. Tahmin çoğu zaman yalnızca ili verir, ilçeyi/köyü kullanıcı elle seçer.
/// </summary>
public static class KonumBulucu
{
    private const string Adres = "https://ipwho.is/?lang=tr";

    public sealed record Tahmin(string? Sehir, string? Bolge);

    public static async Task<Tahmin?> TahminEtAsync(HttpClient http, CancellationToken iptal = default)
    {
        try
        {
            string json = await http.GetStringAsync(Adres, iptal);
            using var belge = JsonDocument.Parse(json);
            var kok = belge.RootElement;
            if (kok.TryGetProperty("success", out var basari) && basari.ValueKind == JsonValueKind.False) return null;

            string? sehir = kok.TryGetProperty("city", out var s) ? s.GetString() : null;
            string? bolge = kok.TryGetProperty("region", out var b) ? b.GetString() : null;
            return string.IsNullOrWhiteSpace(sehir) && string.IsNullOrWhiteSpace(bolge) ? null : new Tahmin(sehir, bolge);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (iptal.IsCancellationRequested) throw;
            return null;
        }
    }
}
