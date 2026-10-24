using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>
/// Vakitler ve Diğer Vakitler sekmeleri her zaman bugünü gösterir. Takvim sekmesinde başka bir gün seçiliyse bu iki sekmede
/// "Bugünün vakitleri gösteriliyor" uyarısı çıkar; sekmeye geçince ekran okuyucu da bunu söyler.
/// </summary>
public partial class MainWindow
{
    private string? _bugunUyarisi;

    /// <summary>Takvim'de seçili gün bugünden farklıysa uyarıyı gösterir, değilse gizler.</summary>
    private void BugunUyarisiniGuncelle()
    {
        var bugun = DateOnly.FromDateTime(SehirSimdi);
        var secili = DateOnly.FromDateTime(_seciliTarih);

        if (secili == bugun || !SehirSecili)
        {
            _bugunUyarisi = null;
            VakitlerUyari.Visibility = Visibility.Collapsed;
            DigerUyari.Visibility = Visibility.Collapsed;
            return;
        }

        var tr = new CultureInfo("tr-TR");
        // Ekrandaki bant kısa (tek satıra sığsın); ekran okuyucuya tam cümle söylenir.
        string kisa = $"Bugünün vakitleri gösteriliyor. Takvim'de seçili gün: {secili.ToString(secili.Year == bugun.Year ? "d MMMM" : "d MMMM yyyy", tr)}";
        string metin = $"Bu sekme bugünün vakitlerini gösterir ({bugun.ToString("d MMMM yyyy dddd", tr)}). " +
                       $"Takvim sekmesinde {secili.ToString("d MMMM yyyy dddd", tr)} seçili.";
        _bugunUyarisi = metin;
        VakitlerUyariMetni.Text = kisa;
        DigerUyariMetni.Text = kisa;
        AutomationProperties.SetName(VakitlerUyariMetni, metin);
        AutomationProperties.SetName(DigerUyariMetni, metin);
        VakitlerUyari.Visibility = Visibility.Visible;
        DigerUyari.Visibility = Visibility.Visible;
    }

    /// <summary>Sayfa alanı değişince ızgaranın en küçük yüksekliği görünür alana eşitlenir: sığıyorsa düğmeler altta durur, sığmıyorsa sayfa kaydırılır.</summary>
    private void AnaKaydirma_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ViewportHeightChange == 0) return;
        double hedef = Math.Max(0, AnaKaydirma.ViewportHeight - AnaIzgara.Margin.Top - AnaIzgara.Margin.Bottom);
        if (Math.Abs(AnaIzgara.MinHeight - hedef) > 0.5) AnaIzgara.MinHeight = hedef;
    }

    /// <summary>Vakitler ya da Diğer Vakitler sekmesine geçilince uyarı varsa ekran okuyucuya söylenir.</summary>
    private void BugunUyarisiniSoyle()
    {
        if (_bugunUyarisi is { } metin && (Sekmeler.SelectedItem == AnaSekme || Sekmeler.SelectedItem == DigerSekme))
            Duyur(metin);
    }
}
