namespace TurkiyeTakvimi.Cekirdek;

public static class VakitAdlari
{
    public static readonly string[] Hepsi =
    {
        "İmsak", "Sabah", "Güneş", "İşrak", "Dahve-i Kübra", "Kerahet",
        "Öğle", "İkindi", "Asr-ı Sani", "İsfirar", "Akşam", "İştibak-i Nücum",
        "Yatsı", "İşa-i Sani", "Gece Yarısı", "Teheccüd", "Seher", "Kıble Saati"
    };

    /// <summary>"Ana Vakitler" sekmesinde gösterilen vakitlerin (İmsak, Sabah, Güneş, Öğle, İkindi, Akşam, Yatsı) <see cref="Hepsi"/> içindeki sıraları.</summary>
    public static readonly int[] Ana = { 0, 1, 2, 6, 7, 10, 12 };

    public static bool AnaMi(int sira) => Array.IndexOf(Ana, sira) >= 0;
}
