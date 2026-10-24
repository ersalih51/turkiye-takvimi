# Türkiye Takvimi

Namaz vakitlerini, günün tarihini, sözünü ve arka yaprağını, haftalık ve aylık vakit çizelgelerini, imsakiyeyi ve mübarek gün ve geceleri bir arada gösteren, ücretsiz ve açık kaynaklı bir Windows masaüstü uygulamasıdır. Windows 11'in görünümüne uyar, açık ve koyu temayı destekler, yazı boyutu büyütülebilir ve tamamen klavyeyle kullanılabilir.

Bu uygulama Salih Eroğul tarafından, herkesin faydalanması amacıyla geliştirilmiştir.

> Uygulama turktakvim.com sitesinin sahipleri tarafından hazırlanmış ya da onaylanmış resmî bir uygulama değildir. Tüm vakitler ve içerikler turktakvim.com sitesinden alınır.

## Özellikler

- **Vakitler:** İmsak, Sabah, Güneş, Öğle, İkindi, Akşam ve Yatsı; sıradaki vakit ve kalan süre, canlı saat, hicrî tarih. Ramazan'da iftar ve sahur bilgisi öne çıkar.
- **Takvim:** Seçili günün tarihi (miladî, hicrî, rûmî), günün vakitleri, tarihte o gün, günün sözü, arka yaprak ve seçili yerden kıble yönü (gerçek kuzeye göre ve pusulayla manyetik kuzeye göre). İstenen güne gidilebilir.
- **Diğer Vakitler:** İşrak, Dahve-i Kübra, Kerahet, Asr-ı Sani, İsfirar, İştibak-i Nücum, İşa-i Sani, Gece Yarısı, Teheccüd, Seher ve Kıble Saati; isteğe bağlı Ezânî (alaturka) vakitler.
- **Çizelgeler:** Haftalık, aylık ve Ramazan imsakiyesi. Aylık ve imsakiye A4 olarak yazdırılabilir ya da PDF olarak kaydedilebilir. Ramazan'a bir ay kala sekme imsakiye ile açılır.
- **Mübarek gün ve geceler:** Kandiller, bayramlar, Ramazan'ın başlangıcı ve Kadir Gecesi; kaç gün kaldığıyla birlikte.
- **Bilgiler menüsü:** turktakvim.com'daki bilgi yazıları, düâlar ve mektûbât tercemesi kategoriler hâlinde; açıldıkça indirilir, sonra çevrimdışı da okunur.
- **Bildirimler:** Her vakit için ayrı ayrı açılıp kapatılır (başlangıçta kapalıdır); vakitten önce hatırlatma, sessiz saatler, mübarek gün bildirimi.
- **Yer seçimi:** Dünyadaki 120 binden fazla şehir, ilçe, kasaba ve köy; son kullanılan yerler arasında hızlı geçiş (Ctrl+K).
- **Çevrimdışı çalışma:** Vakitler yıllık olarak indirilir ve saklanır; günün içeriği önceden indirilir.
- **Paylaşma:** Vakitler, çizelgeler ve metinler Windows'un paylaşma penceresiyle paylaşılabilir.
- **Erişilebilirlik:** Uygulama erişilebilirlik ölçütleri dikkate alınarak tasarlanmıştır; görme engelli kullanıcılar da ekran okuyucu programlarla (NVDA ile denenmiştir) erişilebilir bir şekilde kullanabilir. Tüm bölümler ekran okuyucu (NVDA) ile denenerek tasarlandı; her şey klavyeyle kullanılabilir, kısayollar Yardım menüsündeki Klavye Kısayolları belgesinde listelenir.

## Gizlilik

- Bilgileriniz yalnızca kendi bilgisayarınızda saklanır.
- Uygulama yalnızca turktakvim.com'a vakit ve içerik istekleri gönderir. "Konumumu internet adresimden tahmin et" düğmesine basarsanız yaklaşık konum için ipwho.is hizmetine istek gider; yalnızca siz bastığınızda.
- Beklenmeyen hatalar bu bilgisayarda `%LocalAppData%\TurkiyeTakvimi\kayit\hata.log` dosyasına yazılır; hiçbir yere gönderilmez ve içinde yer ya da kişisel bilgi bulunmaz.

## Kullanım

Uygulamanın içinde Yardım menüsünde **Kullanım Kılavuzu** (F1) ve **Klavye Kısayolları** (Ctrl+F1) bulunur.

## Derleme (geliştiriciler için)

Gereken: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
src\TurkiyeTakvimi.Arayuz\bin\Debug\TurkiyeTakvimi.exe
```

Çözüm üç projeden oluşur:

| Proje | Görevi |
|---|---|
| `src\TurkiyeTakvimi.Cekirdek` | Siteyle konuşma, veri modelleri, önbellek, hesaplamalar (kıble, mübarek günler, imsakiye). Arayüz bilmez. |
| `src\TurkiyeTakvimi.Konsol` | Çekirdeği komut satırından sınama aracı (`bugun`, `ara`, `icerik`, `yil`, `imsakiye`, `mubarek`, `bilgiler`, `onbellek`). |
| `src\TurkiyeTakvimi.Arayuz` | WPF uygulaması (Fluent görünüm). |

| `src\TurkiyeTakvimi.Testler` | Otomatik testler (xUnit): ayrıştırıcılar ve metin temizliği. |

### Siteden gelen metinler ve kalite güvencesi

turktakvim.com metinlerinde satır bölmek için yumuşak tire gibi görünmez karakterler bulunabiliyor; ekran okuyucu bunları hece hece okur. Bunun için kalıcı düzen:

1. **Giriş kapısı:** İndirilen her sayfa ayrıştırılmadan önce `GorunmezKarakterler.HamHtml` ile temizlenir; önbellek dosyaları okunurken `OnbellekMetni` ile; ekrana yazılırken `At` ile (son güvence). Siteden metin gösteren her yeni yer bu yardımcılardan geçmelidir.
2. **Denetçi:** `dotnet run --project src\TurkiyeTakvimi.Konsol -- metinkontrol` taze içeriği, Bilgiler başlıklarını ve her kategoriden bir yazıyı indirip görünmez karakter, bozuk kodlama izi (Ã, �) ve denetim karakterlerini arar; sorun varsa çıkış kodu 1 olur.
3. **Testler:** `dotnet test` bu davranışı güvence altına alır. Yeni bir metin kaynağı eklenince buraya test eklenir.

Veri kaynakları ve istek biçimleri: [docs/VERI-KAYNAKLARI.md](docs/VERI-KAYNAKLARI.md).

Sınama için ortam değişkenleri: `TT_SANAL_ZAMAN="yyyy-MM-dd HH:mm"` (şehir saatini taklit eder), `TT_CEVRIMDISI=1` (internetsiz mod), `TT_YAZDIR_RESIM=dosya.png` (yazdırılacak sayfayı resim olarak kaydeder).

## Üçüncü taraf veriler

- Pusula açısı için manyetik sapma, NOAA/NCEI Dünya Manyetik Modeli WMM2025 katsayılarıyla hesaplanır (`src/TurkiyeTakvimi.Cekirdek/Veri/WMM2025.COF`). Katsayılar ABD kamu kurumunun yayımladığı verilerdir; model 2025.0-2030.0 arası geçerlidir.

## Ses kaynakları

- **Dalga, Kristal, Damla:** Bu uygulama için sentezlenmiş sesler (`src/TurkiyeTakvimi.Arayuz/Varliklar/Sesler`), MIT lisansı kapsamındadır.
- **Bülbül 1-3:** Wikimedia Commons'taki "Common Nightingale's song 2" kaydından (Digweed1, [CC0](https://creativecommons.org/publicdomain/zero/1.0/)) kesilmiş kısa parçalardır. Atıf zorunlu değildir, nezaketen belirtilmiştir.

## Lisans

Kaynak kod [MIT lisansı](LICENSE) ile yayımlanır. turktakvim.com'dan alınan veriler ve yazılar sitenin ve yazarlarının hakkıdır, bu lisansa dahil değildir.
