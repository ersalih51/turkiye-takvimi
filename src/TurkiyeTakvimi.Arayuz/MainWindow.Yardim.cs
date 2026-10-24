using System.Windows;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Yardım menüsü: Kullanım Kılavuzu (F1) ve Klavye Kısayolları (Ctrl+F1).</summary>
public partial class MainWindow
{
    private void Kilavuz_Click(object sender, RoutedEventArgs e) => KilavuzuAc();
    private void Kisayollar_Click(object sender, RoutedEventArgs e) => KisayollariAc();

    // Her ikisi de başlık seviyeli birer belgedir (gezinti kipinde H, Shift+H ve rakam tuşlarıyla başlıklar arasında dolaşılır).
    private void KilavuzuAc() =>
        new BelgePenceresi("Kullanım Kılavuzu", "Türkiye Takvimi'nin nasıl kullanılacağı", YardimIcerigi.Kilavuz(), _ayarlar.Olcek) { Owner = this }.ShowDialog();

    private void KisayollariAc() =>
        new BelgePenceresi("Klavye Kısayolları", "Tüm kısayol tuşlarının listesi", YardimIcerigi.Kisayollar(), _ayarlar.Olcek, kalinAnahtar: true) { Owner = this }.ShowDialog();
}
