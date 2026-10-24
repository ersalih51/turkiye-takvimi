using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

public enum CizelgeTuru
{
    Hafta,
    Aylik,
    Imsakiye
}

/// <summary>
/// Çizelgeler sekmesi: Haftalık, Aylık ve İmsakiye tek sekmede. Üstteki üç seçenek hangisinin görüneceğini belirler;
/// Ctrl+4, Ctrl+5 ve Ctrl+6 doğrudan ilgili çizelgeyi açar. Ramazan'a bir ay kala sekme İmsakiye ile açılır.
/// </summary>
public partial class MainWindow
{
    private CizelgeTuru _cizelge = CizelgeTuru.Hafta;
    private bool _cizelgeSecildi;      // kullanıcı kendisi seçtiyse otomatik varsayılan onu değiştirmez
    private bool _cizelgeAyarlaniyor;  // kodla seçenek değiştirilirken Checked olayı tekrar çalışmasın

    private bool CizelgeAktif(CizelgeTuru tur) => Sekmeler.SelectedItem == CizelgeSekme && _cizelge == tur;
    private bool HaftaAktif => CizelgeAktif(CizelgeTuru.Hafta);
    private bool AylikAktif => CizelgeAktif(CizelgeTuru.Aylik);
    private bool ImsakiyeAktif => CizelgeAktif(CizelgeTuru.Imsakiye);

    private void HaftaSekme_Click(object sender, RoutedEventArgs e) => CizelgeyeGec(CizelgeTuru.Hafta);
    private void AylikSekme_Click(object sender, RoutedEventArgs e) => CizelgeyeGec(CizelgeTuru.Aylik);
    private void ImsakiyeSekme_Click(object sender, RoutedEventArgs e) => CizelgeyeGec(CizelgeTuru.Imsakiye);

    /// <summary>Seçeneklerden biri seçilince (fare, ok tuşları): içerik değişir, odak seçenekte kalır.</summary>
    private void CizelgeSecenegi_Checked(object sender, RoutedEventArgs e)
    {
        if (_cizelgeAyarlaniyor || !IsLoaded) return; // XAML yüklenirken varsayılan işaret olay doğurmasın
        _cizelgeSecildi = true;
        var tur = sender == AylikSecenegi ? CizelgeTuru.Aylik : sender == ImsakiyeSecenegi ? CizelgeTuru.Imsakiye : CizelgeTuru.Hafta;
        CizelgeGoster(tur);
        // NVDA, ok tuşuyla değişen seçeneğin durumunu kendiliğinden söylemediği için açıkça duyurulur.
        Duyur((tur == CizelgeTuru.Aylik ? "Aylık" : tur == CizelgeTuru.Imsakiye ? "İmsakiye" : "Haftalık") + ", seçili");
    }

    /// <summary>Ctrl+4/5/6 ve menü: Çizelgeler sekmesine geçer, çizelgeyi gösterir ve odağı içeriğin ilk öğesine verir.</summary>
    internal void CizelgeyeGec(CizelgeTuru tur)
    {
        _cizelgeSecildi = true;
        CizelgeGoster(tur);
        SekmeyeGec(CizelgeSekme);
    }

    /// <summary>İstenen çizelgeyi görünür yapar, seçeneği işaretler; sekme açıksa verisini yükler.</summary>
    private void CizelgeGoster(CizelgeTuru tur)
    {
        _cizelge = tur;
        HaftaIcerik.Visibility = tur == CizelgeTuru.Hafta ? Visibility.Visible : Visibility.Collapsed;
        AylikIcerik.Visibility = tur == CizelgeTuru.Aylik ? Visibility.Visible : Visibility.Collapsed;
        ImsakiyeIcerik.Visibility = tur == CizelgeTuru.Imsakiye ? Visibility.Visible : Visibility.Collapsed;

        _cizelgeAyarlaniyor = true;
        HaftaSecenegi.IsChecked = tur == CizelgeTuru.Hafta;
        AylikSecenegi.IsChecked = tur == CizelgeTuru.Aylik;
        ImsakiyeSecenegi.IsChecked = tur == CizelgeTuru.Imsakiye;
        _cizelgeAyarlaniyor = false;

        // Odak seçeneklerden birindeyse yeni işaretlenene taşınır; ok tuşları doğru yerden devam eder.
        if (Keyboard.FocusedElement is RadioButton odakli && (odakli == HaftaSecenegi || odakli == AylikSecenegi || odakli == ImsakiyeSecenegi))
            (tur == CizelgeTuru.Aylik ? AylikSecenegi : tur == CizelgeTuru.Imsakiye ? ImsakiyeSecenegi : HaftaSecenegi).Focus();

        if (Sekmeler.SelectedItem == CizelgeSekme) CizelgeYukle();
    }

    /// <summary>Seçili çizelgenin verisini (gerekirse) hazırlar. Haftalık veri vakit yüklenirken zaten hazırlanır.</summary>
    private void CizelgeYukle()
    {
        if (_cizelge == CizelgeTuru.Imsakiye) _ = ImsakiyeYukle();
        else if (_cizelge == CizelgeTuru.Aylik) _ = AylikYukle();
    }

    /// <summary>Çizelgeler sekmesine girince ya da Ctrl+4/5/6 ile gelince odak, seçili çizelgenin ilk öğesine gider.</summary>
    private void CizelgeIlkOgeyeOdakla()
    {
        switch (_cizelge)
        {
            case CizelgeTuru.Imsakiye: ImsakiyeBilgisi.Focus(); break;
            case CizelgeTuru.Aylik: AylikBilgisi.Focus(); break;
            default: OdakVer(HaftaListesi); break;
        }
    }

    /// <summary>
    /// Bugün Ramazan'a bir ay ya da daha az kaldıysa (ya da Ramazan sürüyorsa) ve kullanıcı henüz kendisi bir çizelge seçmediyse
    /// Çizelgeler sekmesi İmsakiye ile açılır.
    /// </summary>
    private async Task CizelgeVarsayilaniniBelirle()
    {
        try
        {
            var bugun = DateOnly.FromDateTime(SehirSimdi);
            foreach (int yil in new[] { bugun.Year, bugun.Year + 1 })
            {
                var aralik = await ImsakiyeServisiGetir().AraligiBulAsync(yil, bugun);
                if (aralik is null) continue;
                if (bugun >= aralik.Baslangic.AddDays(-30) && bugun <= aralik.Bitis)
                {
                    if (!_cizelgeSecildi) CizelgeGoster(CizelgeTuru.Imsakiye);
                    return;
                }
            }
        }
        catch (VeriHatasi)
        {
            // İnternet yok ve kayıt yok: varsayılan Haftalık kalır.
        }
    }
}
