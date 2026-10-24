using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>
/// İmsakiyeyi A4 dikey, tek sayfalık, yazıcı dostu bir tablo olarak hazırlar (beyaz zemin, siyah yazı, açık gri şeritler).
/// Kullanıcıya ayar sorulmaz; sayfa düzeni sabittir.
/// </summary>
public static class ImsakiyeBelgesi
{
    internal const double SayfaGenislik = 793.7;   // A4 = 210 mm
    internal const double SayfaYukseklik = 1122.5; // A4 = 297 mm
    internal const double Kenar = 48;

    private static readonly CultureInfo Tr = new("tr-TR");
    private static readonly string[] Basliklar = { "İmsak", "Sabah", "Güneş", "İşrak", "Öğle", "İkindi", "Akşam", "Yatsı" };

    internal static readonly Brush Siyah = Brushes.Black;
    internal static readonly Brush Gri = Frozen(0x55, 0x55, 0x55);
    internal static readonly Brush BaslikZemini = Frozen(0xD9, 0xD9, 0xD9);
    internal static readonly Brush SeritZemini = Frozen(0xF2, 0xF2, 0xF2);
    internal static readonly Brush VurguZemini = Frozen(0xE4, 0xE4, 0xE4);
    internal static readonly Brush Cizgi = Frozen(0xB0, 0xB0, 0xB0);

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var f = new SolidColorBrush(Color.FromRgb(r, g, b));
        f.Freeze();
        return f;
    }

    /// <param name="notlar">Tablonun altına yazılacak bilgi notları.</param>
    public static FixedDocument Olustur(Imsakiye im, int hicriYil, IReadOnlyList<string> notlar)
    {
        var kok = new Grid { Width = SayfaGenislik, Height = SayfaYukseklik, Background = Brushes.White };
        var icerik = new StackPanel { Margin = new Thickness(Kenar, Kenar, Kenar, Kenar) };
        kok.Children.Add(icerik);

        icerik.Children.Add(Yazi("RAMEZÂN-I ŞERÎF İMSÂKİYESİ", 24, FontWeights.Bold, Siyah, TextAlignment.Center, new Thickness(0, 0, 0, 4)));
        icerik.Children.Add(Yazi($"Hicrî {hicriYil}  ·  Milâdî {im.MiladiYil}  ·  {im.SehirAdi}", 14, FontWeights.SemiBold, Gri,
            TextAlignment.Center, new Thickness(0, 0, 0, 14)));

        icerik.Children.Add(Tablo(im));

        icerik.Children.Add(Yazi("★ işaretli günün akşamı Kadir Gecesi'dir.", 10, FontWeights.Normal, Gri, TextAlignment.Left,
            new Thickness(2, 8, 0, 6)));
        foreach (string not in notlar)
            icerik.Children.Add(Yazi(not, 11.5, FontWeights.Normal, Siyah, TextAlignment.Left, new Thickness(2, 0, 0, 5)));

        icerik.Children.Add(Yazi("Vakitler turktakvim.com sitesinden alınmış, Türkiye Takvimi uygulamasıyla hazırlanmıştır.", 9, FontWeights.Normal, Gri,
            TextAlignment.Left, new Thickness(2, 10, 0, 0)));

        return Belgele(kok);
    }

    /// <summary>Hazırlanan sayfa içeriğini tek sayfalık A4 belgesine çevirir.</summary>
    internal static FixedDocument Belgele(Grid kok)
    {
        var sayfa = new FixedPage { Width = SayfaGenislik, Height = SayfaYukseklik };
        sayfa.Children.Add(kok);
        sayfa.Measure(new Size(SayfaGenislik, SayfaYukseklik));
        sayfa.Arrange(new Rect(0, 0, SayfaGenislik, SayfaYukseklik));
        sayfa.UpdateLayout();

        var belge = new FixedDocument();
        belge.DocumentPaginator.PageSize = new Size(SayfaGenislik, SayfaYukseklik);
        var icerikSayfasi = new PageContent();
        ((IAddChild)icerikSayfasi).AddChild(sayfa);
        belge.Pages.Add(icerikSayfasi);
        return belge;
    }
    private static UIElement Tablo(Imsakiye im)
    {
        var tablo = new Grid();
        tablo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        tablo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
        tablo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        for (int i = 0; i < 8; i++) tablo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int satir = 0;
        tablo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Hucre(tablo, satir, 0, "Gün", 11, FontWeights.Bold, TextAlignment.Left, BaslikZemini);
        Hucre(tablo, satir, 1, "Tarih", 11, FontWeights.Bold, TextAlignment.Left, BaslikZemini);
        Hucre(tablo, satir, 2, "Günler", 11, FontWeights.Bold, TextAlignment.Left, BaslikZemini);
        for (int i = 0; i < 8; i++)
            Hucre(tablo, satir, 3 + i, Basliklar[i], 11, FontWeights.Bold, TextAlignment.Center, BaslikZemini);

        foreach (var gun in im.Gunler)
        {
            satir++;
            tablo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Brush zemin = gun.Gun % 2 == 0 ? SeritZemini : Brushes.White;
            bool kadir = gun.Gun == 26 && im.Aralik.GunSayisi >= 27;

            Hucre(tablo, satir, 0, gun.Gun.ToString(CultureInfo.InvariantCulture), 12, FontWeights.Bold, TextAlignment.Left, zemin);
            Hucre(tablo, satir, 1, gun.Tarih.ToString("d MMMM", Tr), 12.5, FontWeights.SemiBold, TextAlignment.Left, zemin);
            Hucre(tablo, satir, 2, gun.Tarih.ToString("dddd", Tr) + (kadir ? " ★" : ""), 12, FontWeights.Normal, TextAlignment.Left, zemin);
            for (int i = 0; i < 8; i++)
            {
                bool vurgulu = i is 0 or 6; // İmsak ve Akşam (iftar)
                Hucre(tablo, satir, 3 + i, VakitSaati.Ayristir(gun.Saatler[i]).Gorunen, 12.5,
                    vurgulu ? FontWeights.Bold : FontWeights.Normal, TextAlignment.Center, vurgulu ? VurguZemini : zemin);
            }
        }

        return new Border { BorderBrush = Cizgi, BorderThickness = new Thickness(1), Child = tablo };
    }

    internal static void Hucre(Grid tablo, int satir, int sutun, string metin, double boyut, FontWeight kalinlik,
        TextAlignment hiza, Brush zemin)
    {
        var kutu = new Border
        {
            Background = zemin,
            BorderBrush = Cizgi,
            BorderThickness = new Thickness(0, 0, 0, 0.5),
            Padding = new Thickness(6, 3.5, 6, 3.5),
            Child = Yazi(metin, boyut, kalinlik, Siyah, hiza, new Thickness(0))
        };
        Grid.SetRow(kutu, satir);
        Grid.SetColumn(kutu, sutun);
        tablo.Children.Add(kutu);
    }

    internal static TextBlock Yazi(string metin, double boyut, FontWeight kalinlik, Brush renk, TextAlignment hiza, Thickness bosluk) =>
        new()
        {
            Text = metin,
            FontFamily = new FontFamily("Segoe UI, Segoe UI Symbol"),
            FontSize = boyut,
            FontWeight = kalinlik,
            Foreground = renk,
            TextAlignment = hiza,
            TextWrapping = TextWrapping.Wrap,
            Margin = bosluk
        };
}
