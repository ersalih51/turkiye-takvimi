namespace TurkiyeTakvimi.Arayuz;

/// <summary>Yardım penceresindeki bir konu: başlık ve düz metin (satırlar tek satır sonuyla ayrılır, boş satır yok).</summary>
public sealed record YardimKonusu(string Baslik, string Metin)
{
    /// <summary>Ekran okuyucu liste satırının adını ToString'den okur.</summary>
    public override string ToString() => Baslik;
}

/// <summary>Yardım menüsündeki Kullanım Kılavuzu ve Klavye Kısayolları metinleri.</summary>
public static class YardimIcerigi
{
    private static string Satirlar(params string[] satirlar) => string.Join("\n", satirlar);

    public static List<YardimKonusu> Kilavuz() => new()
    {
        new("Başlarken", Satirlar(
            "Türkiye Takvimi; namaz vakitlerini, günün tarihini, sözünü ve arka yaprağını, haftalık ve aylık vakit çizelgelerini, imsakiyeyi ve mübarek gün ve geceleri gösterir. Bilgiler turktakvim.com sitesinden alınır.",
            "İlk açılışta bir yer seçersiniz. Yer; şehir, ilçe, kasaba ya da köy olabilir, yurt dışındaki yerler de seçilebilir. Seçtiğiniz yerin vakitleri bir kez indirilir, sonra internet olmadan da görünür.",
            "Uygulamada dört sekme vardır: Vakitler, Takvim, Diğer Vakitler ve Çizelgeler. Üstte Yer, Görünüm, Bilgiler, Ayarlar ve Yardım menüleri bulunur. Menü çubuğuna Alt tuşuyla girilir.",
            "Ana pencerede bu kılavuzu F1 ile, tüm kısayolların listesini Ctrl+F1 ile açabilirsiniz; ikisi de Yardım menüsünde de vardır.")),

        new("Yer seçme ve değiştirme", Satirlar(
            "Yer menüsünde son kullandığınız dört yer listelenir. Birini seçmek vakitleri hemen o yere geçirir. Menüyü Ctrl+K ile de açabilirsiniz.",
            "Başka bir yer için Yer menüsünde Başka yer seç'i seçin. Açılan pencerede yer adını yazıp Enter'a basın ya da Ara düğmesini kullanın, sonra sonuçlardan birini seçip Enter'a basın.",
            "Konumumu internet adresimden tahmin et düğmesi yalnızca siz bastığınızda çalışır; internet adresiniz yaklaşık konumu bulmak için ipwho.is hizmetine gönderilir, başka bir şey gönderilmez.",
            "Kuzey ya da güney kutba çok yakın yerlerde (yaklaşık 60 derece enlemin ötesi) siteden vakit alınamayabilir. Bu durumda uygulama bunu size bildirir.")),

        new("Vakitler sekmesi", Satirlar(
            "Sekmenin üstünde yerin adı, bugünün miladî ve hicrî tarihi ve o yerin anlık saati görünür.",
            "Mavi kartta sıradaki vakit ve kalan süre yazar; altındaki çubuk, önceki vakitten sıradaki vakte ne kadar gelindiğini gösterir. Ramazan'da iftar ve sahur bilgisi öne çıkarılır.",
            "Altta yedi ana vakit listelenir: İmsak, Sabah, Güneş, Öğle, İkindi, Akşam ve Yatsı. Sıradaki vakit işaretlidir, geçmiş vakitler soluk görünür.",
            "Görünüm menüsündeki Yenile (F5) bugünün verisini siteden yeniden alır; uygulama gün değişince de kendiliğinden yeniler. Takvim sekmesinde başka bir gün seçiliyse bu sekmede bunu bildiren kısa bir uyarı çıkar; bu sekme her zaman bugünü gösterir.",
            "Bir vakti Ctrl+C ile kopyalayabilir, sağ tıklayarak paylaşabilirsiniz.")),

        new("Takvim sekmesi", Satirlar(
            "Takvim sekmesi seçili günün bilgilerini gösterir. Alanlar yukarıdan aşağıya şöyledir: Tarih ve Günün Bilgileri, Günün Vakitleri, Günün Tarihi, Günün Sözü ve Arka Yaprak.",
            "Tarih ve Günün Bilgileri alanında miladî ve hicrî tarih, rûmî ve hicrî şemsî tarihler, yılın kaçıncı günü olduğu, varsa mübarek gün bilgisi ve seçili yerden kıble yönü bulunur. Kıble yönü hem gerçek kuzeye göre hem de pusulayla (manyetik kuzeye göre) verilir.",
            "Günün Vakitleri, seçili günün yedi ana vaktini gösterir. Günün Tarihi, tarihte o gün olanları; Günün Sözü ve Arka Yaprak sitenin o güne ait yazılarını verir.",
            "Gün değiştirmek için Önceki gün, Sonraki gün ve Bugün düğmelerini kullanın. Takvimi göster düğmesi bir takvim açar: oklarla gün seçip Enter'a basın, Esc vazgeçer. Tarihi doğrudan yazmak için gün.ay.yıl biçiminde, örneğin 23.10.2026 yazıp Enter'a basın.",
            "Mübarek gün ve geceler düğmesi, yılın kandil, bayram ve benzeri günlerinin listesini açar.",
            "Alanlara her yerden gitmek için Alt+T, Alt+S, Alt+A, Alt+B ve Alt+V kısayolları vardır. Yazı alanlarında sağ tık menüsünde kopyalama ve paylaşma vardır.")),

        new("Diğer Vakitler sekmesi", Satirlar(
            "Bu sekme bugünün diğer vakitlerini listeler: İşrak, Dahve-i Kübra, Kerahet, Asr-ı Sani, İsfirar, İştibak-i Nücum, İşa-i Sani, Gece Yarısı, Teheccüd, Seher ve Kıble Saati.",
            "Sıradaki vakit, Vakitler sekmesindeki gibi \"Sıradaki\" rozetiyle işaretlenir ve geçmiş vakitler soluk görünür. Liste saat sırasında olmadığı için rozet satırlar arasında sıçrayabilir. Bugün vakit kalmadıysa yarının ilk vakti \"Yarın\" rozetiyle gösterilir.",
            "Ayarlar'dan açarsanız Ezânî (alaturka) vakitler de bu listeye eklenir.",
            "Vakitler sekmesindeki gibi Ctrl+C ile bir vakti kopyalayabilir, sağ tık menüsünden bütün listeyi paylaşabilirsiniz.")),

        new("Çizelgeler sekmesi", Satirlar(
            "Çizelgeler sekmesinde üç çizelge vardır: Haftalık, Aylık ve İmsakiye. En üstteki üç seçenekten birini seçersiniz; sol ve sağ ok tuşlarıyla aralarında geçilir. Ctrl+4, Ctrl+5 ve Ctrl+6 doğrudan Haftalık, Aylık ve İmsakiye'yi açar.",
            "Haftalık, bugünden başlayarak yedi günün vakitlerini gösterir.",
            "Aylık, seçili ayın her günü için yedi ana vakti gösterir. Önceki ay, Bu ay ve Sonraki ay düğmeleriyle ay değiştirilir. Cuma günleri ayrıca işaretlenir.",
            "İmsakiye, Ramazan'ın her günü için vakitleri gösterir; altında ilk ve son imsak, ilk ve son iftar, Kadir Gecesi ve Ramazan Bayramı bilgisi yer alır. Ramazan'a bir ay ya da daha az kaldığında ve Ramazan sürerken, siz bir çizelge seçmediyseniz bu sekme İmsakiye ile açılır.",
            "Aylık ve İmsakiye çizelgeleri yazdırılabilir (Ctrl+P) ya da PDF olarak kaydedilebilir (Ctrl+Shift+P). Sayfa A4'tür ve ayrıca bir ayar gerektirmez. Çizelgeleri sağ tık menüsünden paylaşabilirsiniz.")),

        new("Mübarek gün ve geceler", Satirlar(
            "Bilgiler menüsünden (ilk madde) ya da Ctrl+M ile açılır. Seçili yılın kandillerini, Ramazan'ın başlangıcını, Kadir Gecesini, bayramları ve benzeri mübarek günleri tarihleri ve kaç gün kaldığıyla listeler.",
            "Önceki yıl, Bu yıl ve Sonraki yıl düğmeleriyle yıl değiştirilir. Bir güne Enter ile basarsanız Takvim sekmesinde o günün bilgilerine gidersiniz. Ctrl+C seçili satırı kopyalar.",
            "Hicrî tarihler siteden alındığı için, site gelecek yılın hicrî takvimini yayımlayana kadar o yıl için liste boş kalabilir.")),

        new("Bilgiler menüsü", Satirlar(
            "Bilgiler menüsü turktakvim.com sitesindeki bilgi yazılarını kategoriler hâlinde sunar: takvim ve vakit bilgileri, namaz vakti bilgileri, dinî bilgiler, düâlar ve mektûbât tercemesi.",
            "Bir kategoriyi seçince başlıkları listelenir. Başlık sayısı çoksa üstteki Başlıklarda ara kutusuna yazarak daraltabilirsiniz; aşağı ok sizi listeye geçirir.",
            "Bir başlığa Enter basınca yazı aynı pencerede açılır. Esc başlıklara döner; başlıklardayken Esc pencereyi kapatır. Yazıyı kopyala düğmesi yazının tamamını kopyalar.",
            "Tablo ya da PDF olarak duran bilgiler uygulama içinde okunamaz; bunlar sitede açılır etiketini taşır ve Enter ile tarayıcıda açılır.",
            "Açtığınız yazılar bilgisayarınızda küçük bir önbellekte saklanır ve sonra internet olmadan da okunur.")),

        new("Bildirimler", Satirlar(
            "Bildirimler başlangıçta kapalıdır. Açmak için Ayarlar menüsünden Bildirim ayarları'nı seçin.",
            "Orada Ana vakitler ve Diğer vakitler başlıkları altında bildirim almak istediğiniz vakitleri işaretlersiniz; Tüm ana vakitler ve Tüm diğer vakitler seçenekleri hepsini birden işaretler.",
            "İsterseniz vakitten kaç dakika önce hatırlatılacağını (bu, yalnızca işaretlediğiniz vakitler için çalışır; hiç vakit işaretli değilken seçilemez), bildirimle birlikte kısa bir uyarı sesi çalınıp çalınmayacağını (ses olarak Windows'un varsayılan sesi, Dalga, Kristal, Damla ya da üç bülbül sesinden birini seçebilirsiniz; seçerken ses çalar), mübarek gün ve gecelerde haber verilip verilmeyeceğini ve sessiz saatleri (bildirim gösterilmeyecek saat aralığı) seçebilirsiniz.",
            "Bildirimlerin gelmesi için uygulamanın açık olması gerekir. Pencereyi kapatsanız bile uygulama bildirim alanında çalışmaya devam eder (Genel ayarlardan kapatılabilir).",
            "Bir vakti yalnızca bir kez bildirmek için Vakitler ya da Diğer Vakitler listesinde o satırın sağ tık menüsünden Bu vakti bir kez bildir'i seçin. Vakit gelince tek bildirim verilir ve kendiliğinden silinir; ertesi gün yinelenmez. Bildirimler kapalı olsa da, sessiz saatlerde de verilir. Vakit bugün geçtiyse yarınki vakit için kurulur. Aynı maddeyle (Bildirimi kaldır) vazgeçilir. Bunun için uygulamanın açık olması gerekir.",
            "Win+Alt+V, uygulama arka plandayken bile sıradaki vakti ve kalan süreyi bildirim olarak gösterir.")),

        new("Ayarlar", Satirlar(
            "Ayarlar menüsünde üç madde vardır ve her biri yalnızca kendi konusunu gösteren ayrı bir pencere açar: Genel ayarlar (Ctrl+Virgül), Bildirim ayarları ve Çevrimdışı veri.",
            "Genel ayarlarda Seçili yer alanı mevcut yeri gösterir; yeri değiştirmek için Yeri değiştir düğmesi kullanılır. Altında: pencere kapanınca bildirim alanında çalışmaya devam etme, Windows ile birlikte başlama, Ramazan'da iftar ve sahur bilgisini öne çıkarma, Win+Alt+V kısayolu ve Diğer Vakitler'de Ezânî vakitleri gösterme seçenekleri bulunur.",
            "Bildirim ayarları penceresinin en üstünde bildirim durumu yazar; vakitler işaretlendikçe güncellenir.",
            "Çevrimdışı veri penceresinde: yılın içeriğini arka planda indirme, önbellek boyutunu görme, Temizle, Klasörü aç ve istediğiniz bir yılı çevrimdışı kullanım için indirme bulunur.",
            "Genel ayarlardaki İlk kurulum haline dön düğmesi yer, tema ve yazı boyutu gibi bütün ayarlarınızı sıfırlar ve uygulamayı yer seçme ekranıyla yeniden açar. İndirilmiş verilerin de silinip silinmeyeceğini önce size sorar.")),

        new("Görünüm: tema ve yazı boyutu", Satirlar(
            "Görünüm menüsünde Tema altından sistem temasını, açık temayı ya da koyu temayı seçebilirsiniz.",
            "Yazı boyutunu Ctrl+Artı ile büyütebilir, Ctrl+Eksi ile küçültebilir, Ctrl+0 ile normale döndürebilirsiniz. Boyut bir sonraki açılışta da hatırlanır.")),

        new("Yazdırma, PDF ve paylaşma", Satirlar(
            "Aylık ve İmsakiye çizelgeleri için Ctrl+P varsayılan yazıcıya A4 olarak yazdırır; Ctrl+Shift+P ise Microsoft Print to PDF ile PDF dosyası oluşturur ve Windows'un kaydetme penceresini açar. Aynı komutlar çizelgenin altındaki düğmelerde ve sağ tık menüsünde de vardır.",
            "Paylaş komutları, ilgili metni (ve çizelgelerde sayfanın resmini) Windows'un paylaşma penceresine gönderir; oradan e-posta, mesajlaşma uygulaması ya da başka bir hedef seçersiniz.")),

        new("Kopyalama", Satirlar(
            "Listelerde Ctrl+C seçili satırı, Ctrl+Shift+C listenin tamamını panoya kopyalar.",
            "Yazı alanlarında Ctrl+C seçili metni kopyalar.")),

        new("İnternet olmadan kullanım", Satirlar(
            "Seçtiğiniz yerin vakitleri yıllık olarak indirilir ve saklanır. Günün içeriği (söz, arka yaprak, tarihte bugün) arka planda önceden indirilir. Böylece internet yokken de çoğu şey çalışır.",
            "Bir yılın içeriğini önceden indirmek için Ayarlar menüsündeki Çevrimdışı veri'de Çevrimdışı için yıl indir bölümünü kullanın.",
            "İmsakiye için hicrî tarih bilgisi gerekir; bir kez hazırlanınca çevrimdışı da görünür. Bilgiler yazıları ise açıldıkça indirilir.")),

        new("Güncelleme ve geri bildirim", Satirlar(
            "Güncellemeleri denetle, Yardım menüsündedir. Uygulama kendiliğinden güncelleme aramaz; yalnızca siz seçince, en son sürümü öğrenmek için GitHub'a bağlanır.",
            "Yeni sürüm varsa kurulu sürümünüzde İndir ve kur düğmesi görünür: yeni sürümü indirir, bozulmadığını doğrular, uygulamayı kapatır ve kurulumu başlatır. Taşınabilir sürümde ise Sürüm sayfasını aç düğmesiyle yeni zip dosyasını kendiniz indirirsiniz.",
            "Geri bildirim gönder, hata ya da önerinizi GitHub'daki proje sayfasına yazmanız için tarayıcıda yeni kayıt sayfasını açar. Sürüm ve Windows bilgisi önceden yazılır. Kayıt herkese açıktır ve GitHub hesabı gerektirir; bu yüzden adres, telefon ya da e-posta gibi kişisel bilgi yazmayın.",
            "Sorun bir hata kaydıyla ilgiliyse Hakkında penceresindeki Hata kaydını aç düğmesiyle kaydı görebilir, gerekirse ilgili kısmı geri bildirime ekleyebilirsiniz.")),

        new("Sorun giderme", Satirlar(
            "Vakitler görünmüyorsa: internet bağlantınızı denetleyin ve F5'e basın. Seçili yerin yıllık verisi hiç indirilmediyse ilk gösterim için internet gerekir.",
            "İmsakiye ya da bir mübarek gün listesi boş görünüyorsa: turktakvim.com, o yılın hicrî tarihlerini henüz yayımlamamış olabilir. Site yayımlayınca kendiliğinden görünür.",
            "Bilgiler'de bir yazı açılmıyorsa: bağlantınızı denetleyin; tablo ve PDF olarak duran bilgiler için tarayıcı açılır.",
            "Önbellekteki eski veriden şüphe ediyorsanız Ayarlar menüsündeki Çevrimdışı veri'de Temizle düğmesini kullanın. Bu, seçili yerin vakitlerini yeniden indirir.",
            "Beklenmeyen bir hata olursa uygulama bunu bu bilgisayardaki küçük bir hata kaydına yazar ve çalışmaya devam etmeye çalışır; kayıt hiçbir yere gönderilmez. Hakkında penceresindeki Hata kaydını aç düğmesiyle görebilirsiniz.",
            "Bir şey ters giderse Genel ayarlardaki İlk kurulum haline dön düğmesi ayarları sıfırlar ve uygulamayı yer seçme ekranından yeniden başlatır."))
    };

    public static List<YardimKonusu> Kisayollar()
    {
        var gruplar = new List<YardimKonusu>
        {
            new("Genel", Satirlar(
                "F1: Kullanım Kılavuzu'nu açar.",
                "Ctrl+F1: Klavye Kısayolları'nı açar.",
                "F5: Bugünün verisini yeniler.",
                "Ctrl+K: Yer menüsünü açar, son yerler arasında geçiş yaptırır.",
                "Ctrl+M: Mübarek gün ve geceleri listeler.",
                "Ctrl+Virgül: Genel ayarlar penceresini açar.",
                "Ctrl+Q: Uygulamadan tamamen çıkar.",
                "Win+Alt+V: Uygulama arka plandayken bile sıradaki vakti ve kalan süreyi bildirim olarak gösterir.",
                "Alt: Menü çubuğuna girer. Ok tuşlarıyla gezilir, Enter seçer, Esc çıkar.")),

            new("Menüler", Satirlar(
                "Alt+R: Yer menüsü.",
                "Alt+G: Görünüm menüsü.",
                "Alt+L: Bilgiler menüsü.",
                "Alt+Y: Ayarlar menüsü (Genel ayarlar, Bildirim ayarları, Çevrimdışı veri).",
                "Alt+D: Yardım menüsü.")),

            new("Sekmeler", Satirlar(
                "Ctrl+1: Vakitler sekmesi.",
                "Ctrl+2: Takvim sekmesi.",
                "Ctrl+3: Diğer Vakitler sekmesi.",
                "Ctrl+4: Çizelgeler sekmesinde Haftalık.",
                "Ctrl+5: Çizelgeler sekmesinde Aylık.",
                "Ctrl+6: Çizelgeler sekmesinde İmsakiye.",
                "Ctrl+Tab: Sonraki sekme. Ctrl+Shift+Tab: önceki sekme.",
                "Çizelgeler sekmesinde Sol ok ve Sağ ok: Haftalık, Aylık ve İmsakiye arasında geçer.")),

            new("Takvim sekmesi", Satirlar(
                "Alt+T: Günün Tarihi alanına gider.",
                "Alt+S: Günün Sözü alanına gider.",
                "Alt+A: Arka Yaprak alanına gider.",
                "Alt+B: Tarih ve Günün Bilgileri alanına gider.",
                "Alt+V: Günün Vakitleri alanına gider.",
                "Bu beş kısayol hangi sekmede olursanız olun çalışır ve sizi Takvim sekmesindeki alana götürür.",
                "Takvim açıkken Ok tuşları: gün seçer. Enter: günü seçer ve takvimi kapatır. Esc: seçim yapmadan kapatır.",
                "Takvim açıkken ay ve yıl düğmelerinde Ok tuşları: yıl yıl atlar.",
                "Tarih yazma kutusunda gün.ay.yıl yazıp Enter: o tarihe gider. Örnek: 23.10.2026.")),

            new("Kopyalama", Satirlar(
                "Ctrl+C: Listede seçili satırı, yazı alanında seçili metni panoya kopyalar.",
                "Ctrl+Shift+C: Listenin tamamını panoya kopyalar.")),

            new("Çizelgeler ve yazdırma", Satirlar(
                "Ctrl+P: Aylık ya da İmsakiye çizelgesini varsayılan yazıcıya yazdırır.",
                "Ctrl+Shift+P: Aylık ya da İmsakiye çizelgesini PDF olarak kaydeder.",
                "Bu iki kısayol yalnızca ilgili çizelge açıkken çalışır.")),

            new("Yazı boyutu", Satirlar(
                "Ctrl+Artı: Yazıyı büyütür.",
                "Ctrl+Eksi: Yazıyı küçültür.",
                "Ctrl+0: Yazı boyutunu normale döndürür.")),

            new("Diğer pencereler", Satirlar(
                "Esc: Hakkında, Hoş geldiniz, Kullanım Kılavuzu, Klavye Kısayolları ve benzeri pencereleri kapatır.",
                "Bilgiler penceresinde Enter: seçili başlığı açar. Esc: başlıklara döner; başlıklardayken pencereyi kapatır.",
                "Bilgiler'de arama kutusunda Aşağı ok: sonuç listesine geçer.",
                "Mübarek Gün ve Geceler penceresinde Enter: seçili günü Takvim sekmesinde gösterir. Ctrl+C: satırı kopyalar.",
                "Yer seçme penceresinde Enter: arama kutusunda ara, sonuçlarda seç."))
        };

        return gruplar;
    }
}
