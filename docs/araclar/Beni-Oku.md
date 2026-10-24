# docs\araclar ve docs\kaynaklar: yardımcı araçlar ve kaynak dosyalar

Bunlar uygulamanın parçası değildir (çözüme dahil değil, paketlenmez); yalnızca ses ve pusula verisini yeniden üretebilmek için saklanır.

- `araclar\sesgen\`: bildirim seslerini üreten ve bülbül kaydından parça kesen küçük konsol programı (NAudio.Vorbis kullanır).
  - Sentez sesleri (Dalga, Kristal, Damla): `dotnet run --project docs\araclar\sesgen -- <hedef klasör>`
  - Bülbül parçası kesme: `dotnet run --project docs\araclar\sesgen -- kes <kaynak.ogg> <çıktı.wav> <başlangıç sn> <süre sn>` (bağımsız değişkenlerin tam sırası için `Program.cs`'e bak).
  - Üretilen ses dosyaları `src\TurkiyeTakvimi.Arayuz\Varliklar\Sesler\` içinde ve denemeler `docs\ses-denemeleri\` içindedir.
- `kaynaklar\bulbul\bulbul2.ogg`: Wikimedia Commons "Common Nightingale's song 2" (Digweed1, CC0). Bülbül sesleri bundan kesildi.
- `kaynaklar\WMM2025\`: NOAA/NCEI Dünya Manyetik Modeli WMM2025 katsayıları ve resmî test değerleri (pusula sapması). **2030 yaklaşırken yeni model (WMM2030) buradan örnek alınarak eklenir:** `src\TurkiyeTakvimi.Cekirdek\Veri\` içindeki `.COF` değiştirilir, `ManyetikSapma.cs` ve testleri güncellenir.
