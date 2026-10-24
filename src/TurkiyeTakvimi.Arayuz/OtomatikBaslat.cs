using System.IO;
using Microsoft.Win32;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>"Windows ile birlikte başlat": yalnızca bu kullanıcının Çalıştır listesine bir satır ekler/kaldırır.</summary>
public static class OtomatikBaslat
{
    private const string AnahtarYolu = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Ad = "TurkiyeTakvimi";
    public const string TepsiBayragi = "--tepsi";

    public static bool Acik()
    {
        try
        {
            using var anahtar = Registry.CurrentUser.OpenSubKey(AnahtarYolu);
            return anahtar?.GetValue(Ad) is string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>Başarılıysa true. Kayıt, uygulamanın şu anki konumuna işaret eder.</summary>
    public static bool Ayarla(bool ac)
    {
        try
        {
            using var anahtar = Registry.CurrentUser.CreateSubKey(AnahtarYolu);
            if (ac)
                anahtar.SetValue(Ad, $"\"{Environment.ProcessPath}\" {TepsiBayragi}");
            else
                anahtar.DeleteValue(Ad, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
