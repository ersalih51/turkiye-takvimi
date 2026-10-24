using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Yardım menüsü: Güncellemeleri denetle ve Geri bildirim gönder (ikisi de yalnızca kullanıcı isteyince internete bağlanır).</summary>
public partial class MainWindow
{
    private void Guncelleme_Click(object sender, RoutedEventArgs e)
    {
        // TT_CEVRIMDISI=1 iken güncelleme denetimi de internetsiz davranır (hata yolunu denemek için).
        var http = Environment.GetEnvironmentVariable("TT_CEVRIMDISI") == "1" ? new HttpClient(new InternetYokHandler()) : null;
        new GuncellemePenceresi(this, _ayarlar.Olcek, new GuncellemeServisi(http)) { Owner = this }.ShowDialog();
    }

    private void GeriBildirim_Click(object sender, RoutedEventArgs e)
    {
        var cevap = MessageBox.Show(this,
            "Geri bildirim, GitHub'daki proje sayfasında herkese açık bir kayıt olarak yazılır ve bunun için GitHub hesabınızın olması gerekir. " +
            "Sürüm ve Windows bilgisi kayda önceden yazılır; başka kişisel bilgi eklenmez.\n\nTarayıcıda GitHub sayfası açılsın mı?",
            "Geri bildirim gönder", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (cevap != MessageBoxResult.Yes) return;

        string adres = GuncellemeServisi.GeriBildirimAdresi(VeriYolu.UygulamaSurumu, Environment.OSVersion.VersionString, VeriYolu.Tasinabilir);
        Process.Start(new ProcessStartInfo(adres) { UseShellExecute = true });
    }

    /// <summary>
    /// İndirilip doğrulanan kurulum dosyasını başlatır. Kurulum, uygulama kapandıktan sonra açılsın diye iki saniye gecikmeli başlatılır
    /// (çalışan uygulama kurulumun "uygulama açık" denetimine takılırdı); sonra uygulama tamamen kapanır.
    /// </summary>
    internal void GuncellemeKurulumunuBaslat(string kurulumYolu)
    {
        var bilgi = new ProcessStartInfo("cmd.exe", $"/c ping -n 3 127.0.0.1 >nul & start \"\" \"{kurulumYolu}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(bilgi);
        Cik();
    }
}
