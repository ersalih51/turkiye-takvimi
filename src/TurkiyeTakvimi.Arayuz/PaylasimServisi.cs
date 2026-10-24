using System.IO;
using System.Windows;
using System.Windows.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>
/// Windows'un klasik Paylaş penceresini açar (e-posta, WhatsApp, Yakındaki paylaşım vb.). Paylaşılacak metin (ve isteğe
/// bağlı resim dosyası) pencere açılmadan önce verilir; Windows veriyi seçilen uygulamaya iletir.
/// </summary>
public sealed class PaylasimServisi
{
    private readonly Window _pencere;
    private DataTransferManager? _yonetici;
    private string _baslik = "";
    private string _metin = "";
    private string? _resimDosyasi;

    public PaylasimServisi(Window pencere) => _pencere = pencere;

    /// <summary>Paylaş penceresini açar. Başarısızsa false (örn. Windows paylaşımı desteklenmiyor).</summary>
    public bool Paylas(string baslik, string metin, string? resimDosyasi = null)
    {
        try
        {
            _baslik = baslik;
            _metin = metin;
            _resimDosyasi = resimDosyasi;

            var hwnd = new WindowInteropHelper(_pencere).Handle;
            if (_yonetici is null)
            {
                _yonetici = DataTransferManagerInterop.GetForWindow(hwnd);
                _yonetici.DataRequested += VeriIstendi;
            }
            DataTransferManagerInterop.ShowShareUIForWindow(hwnd);
            return true;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }

    private async void VeriIstendi(DataTransferManager gonderen, DataRequestedEventArgs e)
    {
        var istek = e.Request;
        var erteleme = istek.GetDeferral();
        try
        {
            istek.Data.Properties.Title = _baslik;
            istek.Data.SetText(_metin);
            if (_resimDosyasi is { } yol && File.Exists(yol))
            {
                var dosya = await StorageFile.GetFileFromPathAsync(yol);
                istek.Data.SetStorageItems(new IStorageItem[] { dosya });
            }
        }
        catch (Exception)
        {
            // Resim eklenemezse yalnızca metin paylaşılır.
        }
        finally
        {
            erteleme.Complete();
        }
    }
}
