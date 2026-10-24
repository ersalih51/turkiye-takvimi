using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Aylık vakit çizelgesini A4 dikey, tek sayfalık, yazıcı dostu tablo olarak hazırlar (ayar sorulmaz).</summary>
public static class AylikBelgesi
{
    private static readonly string[] Basliklar = { "İmsak", "Sabah", "Güneş", "Öğle", "İkindi", "Akşam", "Yatsı" };

    public static FixedDocument Olustur(string ayAdi, string sehir, IReadOnlyList<AylikSatir> satirlar)
    {
        var kok = new Grid { Width = ImsakiyeBelgesi.SayfaGenislik, Height = ImsakiyeBelgesi.SayfaYukseklik, Background = System.Windows.Media.Brushes.White };
        var icerik = new StackPanel { Margin = new Thickness(ImsakiyeBelgesi.Kenar) };
        kok.Children.Add(icerik);

        icerik.Children.Add(ImsakiyeBelgesi.Yazi("AYLIK NAMAZ VAKİTLERİ", 24, FontWeights.Bold, ImsakiyeBelgesi.Siyah, TextAlignment.Center, new Thickness(0, 0, 0, 4)));
        icerik.Children.Add(ImsakiyeBelgesi.Yazi($"{ayAdi}  ·  {sehir}", 14, FontWeights.SemiBold, ImsakiyeBelgesi.Gri,
            TextAlignment.Center, new Thickness(0, 0, 0, 14)));

        var tablo = new Grid();
        tablo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        tablo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
        tablo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        for (int i = 0; i < 7; i++) tablo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        tablo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ImsakiyeBelgesi.Hucre(tablo, 0, 0, "Gün", 11, FontWeights.Bold, TextAlignment.Left, ImsakiyeBelgesi.BaslikZemini);
        ImsakiyeBelgesi.Hucre(tablo, 0, 1, "Tarih", 11, FontWeights.Bold, TextAlignment.Left, ImsakiyeBelgesi.BaslikZemini);
        ImsakiyeBelgesi.Hucre(tablo, 0, 2, "Günler", 11, FontWeights.Bold, TextAlignment.Left, ImsakiyeBelgesi.BaslikZemini);
        for (int i = 0; i < 7; i++)
            ImsakiyeBelgesi.Hucre(tablo, 0, 3 + i, Basliklar[i], 11, FontWeights.Bold, TextAlignment.Center, ImsakiyeBelgesi.BaslikZemini);

        int satir = 0;
        foreach (var gun in satirlar)
        {
            satir++;
            tablo.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var zemin = gun.Gun % 2 == 0 ? ImsakiyeBelgesi.SeritZemini : System.Windows.Media.Brushes.White;
            var gunZemin = gun.Cuma ? ImsakiyeBelgesi.VurguZemini : zemin; // Cuma günü satırı hafif koyu

            ImsakiyeBelgesi.Hucre(tablo, satir, 0, gun.Gun.ToString(CultureInfo.InvariantCulture), 11.5, FontWeights.Bold, TextAlignment.Left, gunZemin);
            ImsakiyeBelgesi.Hucre(tablo, satir, 1, gun.TarihMetni, 12, FontWeights.SemiBold, TextAlignment.Left, gunZemin);
            ImsakiyeBelgesi.Hucre(tablo, satir, 2, gun.HaftaGunu, 12, gun.Cuma ? FontWeights.Bold : FontWeights.Normal, TextAlignment.Left, gunZemin);
            var saatler = gun.Gorunenler;
            for (int i = 0; i < 7; i++)
                ImsakiyeBelgesi.Hucre(tablo, satir, 3 + i, saatler[i], 12.5, FontWeights.Normal, TextAlignment.Center, gunZemin);
        }

        icerik.Children.Add(new Border
        {
            BorderBrush = ImsakiyeBelgesi.Cizgi, BorderThickness = new Thickness(1), Child = tablo
        });
        icerik.Children.Add(ImsakiyeBelgesi.Yazi("Cuma günleri koyu satırla gösterilmiştir. Vakitler Vasatî vakitlerdir.", 10, FontWeights.Normal,
            ImsakiyeBelgesi.Gri, TextAlignment.Left, new Thickness(2, 8, 0, 4)));
        icerik.Children.Add(ImsakiyeBelgesi.Yazi("Vakitler turktakvim.com sitesinden alınmış, Türkiye Takvimi uygulamasıyla hazırlanmıştır.", 9, FontWeights.Normal,
            ImsakiyeBelgesi.Gri, TextAlignment.Left, new Thickness(2, 4, 0, 0)));

        return ImsakiyeBelgesi.Belgele(kok);
    }
}
