using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>İmsakiye ve aylık çizelge için ortak yazdırma, PDF ve resim işlemleri.</summary>
public partial class MainWindow
{
    /// <summary>
    /// Belgeyi hiçbir soru sormadan A4 dikey kâğıda, varsayılan yazıcıdan (pdf ise Windows'un PDF yazıcısından) yazdırır.
    /// Sınama için TT_YAZDIR_RESIM=dosya.png ayarlıysa yazıcı yerine sayfanın resmini kaydeder.
    /// </summary>
    private void BelgeyiYazdir(FixedDocument belge, string isAdi, string ad, bool pdf)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("TT_YAZDIR_RESIM") is { Length: > 0 } dosya)
            {
                BelgeResmiKaydet(belge, dosya);
                Duyur($"{ad} resim olarak kaydedildi (sınama).");
                return;
            }

            var kuyruk = pdf ? PdfKuyrugu() : LocalPrintServer.GetDefaultPrintQueue();
            var bilet = kuyruk.UserPrintTicket ?? kuyruk.DefaultPrintTicket;
            bilet.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
            bilet.PageOrientation = PageOrientation.Portrait;
            var gecerli = kuyruk.MergeAndValidatePrintTicket(kuyruk.UserPrintTicket ?? kuyruk.DefaultPrintTicket, bilet).ValidatedPrintTicket;

            var pencere = new PrintDialog { PrintQueue = kuyruk, PrintTicket = gecerli };
            pencere.PrintDocument(belge.DocumentPaginator, isAdi);
            Duyur(pdf ? $"{ad} PDF olarak hazırlandı." : $"{ad} {kuyruk.Name} yazıcısına gönderildi.");
        }
        catch (Exception ex) when (ex is PrintQueueException or PrintSystemException or InvalidOperationException or IOException)
        {
            Duyur(pdf
                ? "PDF oluşturulamadı: Windows'ta Microsoft Print to PDF yazıcısı bulunamadı."
                : $"{ad} yazdırılamadı: varsayılan bir yazıcı bulunamadı ya da yazıcıya ulaşılamadı. PDF olarak kaydetmeyi deneyebilirsiniz.");
        }
    }

    /// <summary>A4 sayfayı PNG resmi olarak kaydeder (paylaşım ve sınama için).</summary>
    private static void BelgeResmiKaydet(FixedDocument belge, string dosya)
    {
        var sayfa = belge.Pages[0].GetPageRoot(false);
        // 2 kat çözünürlük: telefonda büyütülünce de okunaklı kalır.
        var resim = new RenderTargetBitmap((int)sayfa.Width * 2, (int)sayfa.Height * 2, 192, 192, PixelFormats.Pbgra32);
        resim.Render(sayfa);
        var kodlayici = new PngBitmapEncoder();
        kodlayici.Frames.Add(BitmapFrame.Create(resim));
        using var akis = File.Create(dosya);
        kodlayici.Save(akis);
    }

    /// <summary>Windows'un yerleşik PDF yazıcısı; dosya adını Windows kendi kaydetme penceresinde sorar.</summary>
    private static PrintQueue PdfKuyrugu()
    {
        using var sunucu = new LocalPrintServer();
        foreach (var kuyruk in sunucu.GetPrintQueues())
            if (kuyruk.Name.Contains("Print to PDF", StringComparison.OrdinalIgnoreCase)) return kuyruk;
        throw new PrintQueueException("PDF yazıcısı yok.");
    }
}
