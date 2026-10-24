using System.Windows;
using System.Windows.Controls;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Bilgiler menüsü: turktakvim.com'un Bilgiler bölümünün kategorileri.</summary>
public partial class MainWindow
{
    private BilgilerServisi? _bilgilerServisi;

    private void BilgiKategorisi_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string etiket }) return;
        string[] parcalar = etiket.Split('|');
        _bilgilerServisi ??= new BilgilerServisi(_istemci, _onbellek);
        new BilgilerPenceresi(_bilgilerServisi, parcalar[0], parcalar[1], _ayarlar.Olcek) { Owner = this }.ShowDialog();
    }
}
