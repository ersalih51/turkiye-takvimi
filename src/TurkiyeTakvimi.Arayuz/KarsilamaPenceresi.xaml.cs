using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>
/// Hoş geldiniz: ilk açılışta (yer seçiminden önce) ve Yardım menüsünden açılan kısa tanıtım.
/// Ekran okuyucu kullanıcıları için tüm bilgi tek bir imleçli metinde (tümünü oku çalışır).
/// </summary>
public partial class KarsilamaPenceresi : Window
{
    public KarsilamaPenceresi(double olcek, bool ilkAcilis)
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;

        if (olcek > 1.0)
        {
            Kok.LayoutTransform = new ScaleTransform(olcek, olcek);
            Width = Math.Min(Width * olcek, SystemParameters.WorkArea.Width);
        }

        var kaynak = Application.GetResourceStream(new Uri("pack://application:,,,/Varliklar/uygulama.ico"));
        if (kaynak is not null)
        {
            var kod = new IconBitmapDecoder(kaynak.Stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Simge.Source = kod.Frames.OrderByDescending(c => c.PixelWidth).First();
        }

        BaslaDugmesi.Content = ilkAcilis ? "Yer seçerek başla" : "Kapat";
        Metin.Text = string.Join("\n", new[]
        {
            "Türkiye Takvimi; namaz vakitlerini, günün tarihini, sözünü ve arka yaprağını, haftalık ve aylık çizelgeleri, imsakiyeyi ve mübarek gün ve geceleri bir arada gösterir.",
            "Bilgiler turktakvim.com sitesinden alınır. İnternet yokken en son indirilenler görünür; her şey bir kez indirilince çevrimdışı da çalışır.",
            ilkAcilis
                ? "Başlamak için bir yer seçeceksiniz: şehir, ilçe, kasaba ya da köy olabilir. Aşağıdaki düğmeye basınca yer seçme penceresi açılır."
                : "Yeri istediğiniz zaman Yer menüsünden ya da Ctrl+K ile değiştirebilirsiniz.",
            "Sekmeler: Vakitler, Takvim, Diğer Vakitler ve Çizelgeler. Ctrl+1, Ctrl+2 ve Ctrl+3 ilk üçüne, Ctrl+4, Ctrl+5 ve Ctrl+6 Çizelgeler sekmesinde haftalık, aylık ve imsakiyeye gider.",
            "Takvim sekmesindeki alanlara her yerden Alt+T (Günün Tarihi), Alt+S (Günün Sözü), Alt+A (Arka Yaprak), Alt+B (Tarih ve Günün Bilgileri) ve Alt+V (Günün Vakitleri) ile gidilir.",
            "Win+Alt+V, uygulama arka plandayken bile sıradaki vakti ve kalan süreyi söyler.",
            "Bilgiler menüsünde turktakvim.com'daki bilgi yazıları ve düâlar var. Bildirimler başlangıçta kapalıdır; Ayarlar'dan istediğiniz vakitler için açabilirsiniz.",
            "Ayrıntılı anlatım için Yardım menüsündeki Kullanım Kılavuzu'na (F1), tüm kısayollar için Klavye Kısayolları'na (Ctrl+F1) bakın. Menü çubuğuna Alt tuşuyla girilir. Bu pencereyi Yardım menüsündeki Hoş geldiniz maddesinden yeniden açabilirsiniz."
        });

        Loaded += (_, _) => Metin.Focus();
    }

    private void Basla_Click(object sender, RoutedEventArgs e) => Close();
}
