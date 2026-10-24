# Türkiye Takvimi — Veri Kaynakları (22.10.2026 tarihinde araştırıldı)

Hepsi turktakvim.com'a aittir; resmî bir API belgesi yoktur, sitenin kendi sayfaları kullanılır.
Önemli: `excel.php` gibi POST isteklerinde mutlaka **https** kullanılmalı. `http` adresi https'e yönlendirir ve yönlendirmede POST verisi kaybolur.

## 1. Şehir arama
`GET http://gadget.turktakvim.com/gadget.php?pg=2&q=<KOD>`
- `<KOD>`: aranan kelimenin her harfinin karakter kodu, `;` ile birleştirilmiş (örn. "izmir" → `105;122;109;105;114`).
- Kelime **küçük harfle** gönderilmeli ("istanbul" bulunur, "Istanbul" bulunmaz). Türkçe için `ToLower(tr-TR)`.
- Yanıt HTML: ülke adı ve `sehir_yap(KOD)` ile şehir kodları. Örnekler: İstanbul 16741, Ankara 16709, İzmir 16742, İstanbul Havalimanı 124096.
- Tüm dünyada 122 binden fazla yer var.

## 2. Günlük vakitler (anlık, 18 vakit)
`GET http://gadget.turktakvim.com/gadget.php?pg=1&sid=<ŞEHİR>&cityID=<ŞEHİR>`
- Yanıt `;` ile ayrılmış tek satır: 0 şehir, 1-3 enlem, 4-6 boylam, 7-24 on sekiz vakit, 25 sunucu saati, 26 hicri tarih, 27 bölge.
- Sıra: İmsak, Sabah, Güneş, İşrak, Dahve-i Kübra, Kerahet, Öğle, İkindi, Asr-ı Sani, İsfirar, Akşam, İştibak-i Nücum, Yatsı, İşa-i Sani, Gece Yarısı, Teheccüd, Seher, Kıble Saati.

## 3. Toplu vakitler (yıllık önbellek için ana kaynak)
`POST https://www.turktakvim.com/excel.php?cityID=<ŞEHİR>`
Form alanları: `input1=YYYY-AA-GG` (başlangıç), `input2=YYYY-AA-GG` (bitiş), `git=Indir`, ve istenen vakitler için `checkbox-<ad>=1`.
Vakit adları: imsak, sabah, gunes, israk, dahve, kerahet, ogle, ikindi, asrisani, isfirar, aksam, istibak, yatsi, isaisani, geceyarisi, teheccud, seher, kible.
- Yanıt HTML tablo (Excel olarak etiketli). İlk satır şehir, ikinci satır sütun başlıkları, sonra her gün için bir satır (`GG-AA-YYYY`).
- Denendi: 2027 yılının tamamı, 18 vakit, 365 gün, yaklaşık 169 KB, 1,4 saniye. Gelecek yıllar da veriliyor.

Ek: `GET https://www.turktakvim.com/yillikvakitler.php?cityID=<ŞEHİR>` yalnızca 8 vakitli yıllık tablo verir (içinde tüm yıl var). Yeterli değil, `excel.php` tercih edilir.

## 4. Günlük içerik (Günün Sözü, Günün Olayı, Arka Yaprak)
- `GET http://www.turktakvim.com/index.php?tarih=YYYY-AA-GG&page=onyuz`
  - `<span id="gununsozu">…</span>` Günün Sözü.
  - `<span id="gununolayi">…</span>` Günün Olayı / Tarihte Bugün.
  - Ayrıca: "Yılın N. günü", hicri tarih, Rûmî tarih, günün kısalma/uzama bilgisi.
- `GET http://www.turktakvim.com/index.php?tarih=YYYY-AA-GG&page=arkayuz`
  - Arka Yaprak: `<h1 style='font-weight:bold; …'>başlık</h1>` ve ardından `<div style='margin:5px;'><p>…</p>` paragrafları.
- Tarih parametresi geçmişe doğru çalışıyor (2015 denendi). Sitenin tarih seçicisi yaklaşık 44 yıl geriyi destekliyor. 1975 gibi eski tarihlerde arka yaprak boş geliyor.
- Metinler HTML varlıkları içeriyor (`&ccedil;` gibi), `WebUtility.HtmlDecode` ile çözülmeli.

## 5. Bilgilendirme yazıları
`GET http://www.turktakvim.com/index.php?link=<SAYFA>`
- `html/bilgiler.php`, `html/namaz_vakitleri_nasil_hesaplanir.php`, `html/imsak_vakti.html`, `html/muhim_tenbih.html`, `html/temkin_muddeti.html`, `html/ruyet.html`, `html/sual-cevab/sual_cevab.html`, `html/kible_yonu_nasil_hesaplanir.php`.

## Notlar
- Sitede "Vasatî Vakitler" ve "Ezânî Vakitler" seçeneği var; uygulama ilk sürümde vasatî vakitleri gösterir. Ezânî seçeneği sonraya bırakıldı.
- Örnek ID `10923` İstanbul değil (Filipinler'de bir yer). İstanbul'un doğru kodu 16741.
