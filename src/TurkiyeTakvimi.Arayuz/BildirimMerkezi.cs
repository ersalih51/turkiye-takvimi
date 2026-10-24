using System.Media;
using System.Windows;
using Forms = System.Windows.Forms;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>
/// Bildirim alanı (sistem tepsisi) simgesi, sağ tık menüsü ve Windows bildirimleri. Bildirimler Windows'un kendi
/// bildirim sistemiyle gösterildiği için NVDA tarafından okunur.
/// </summary>
public sealed class BildirimMerkezi : IDisposable
{
    private readonly Forms.NotifyIcon _simge;
    private readonly Forms.ToolStripMenuItem _siradaki;

    public event Action? AcIstendi;
    public event Action? AyarlarIstendi;
    public event Action? SiradakiIstendi;
    public event Action? CikisIstendi;

    public BildirimMerkezi()
    {
        var menu = new Forms.ContextMenuStrip();
        var ac = new Forms.ToolStripMenuItem("Türkiye Takvimi'ni aç") { Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold) };
        ac.Click += (_, _) => AcIstendi?.Invoke();
        _siradaki = new Forms.ToolStripMenuItem("Sıradaki vakit");
        _siradaki.Click += (_, _) => SiradakiIstendi?.Invoke();
        var ayarlar = new Forms.ToolStripMenuItem("Ayarlar");
        ayarlar.Click += (_, _) => AyarlarIstendi?.Invoke();
        var cikis = new Forms.ToolStripMenuItem("Çıkış");
        cikis.Click += (_, _) => CikisIstendi?.Invoke();
        menu.Items.AddRange(new Forms.ToolStripItem[] { ac, _siradaki, ayarlar, new Forms.ToolStripSeparator(), cikis });

        _simge = new Forms.NotifyIcon
        {
            Icon = SimgeyiYukle(),
            Text = "Türkiye Takvimi",
            ContextMenuStrip = menu,
            Visible = true
        };
        _simge.DoubleClick += (_, _) => AcIstendi?.Invoke();
        _simge.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) AcIstendi?.Invoke();
        };
    }

    private static System.Drawing.Icon SimgeyiYukle()
    {
        var kaynak = Application.GetResourceStream(new Uri("pack://application:,,,/Varliklar/uygulama.ico"));
        return kaynak is null ? System.Drawing.SystemIcons.Application : new System.Drawing.Icon(kaynak.Stream, new System.Drawing.Size(32, 32));
    }

    /// <summary>Simgenin üzerine gelince ve menüde görünen sıradaki vakit metni (en çok 63 karakter).</summary>
    public void SiradakiniYaz(string metin)
    {
        _siradaki.Text = metin;
        _simge.Text = metin.Length <= 63 ? metin : metin[..62] + "…";
    }

    public void Goster(string baslik, string metin, bool ses, string? sesAnahtari = null)
    {
        if (ses) BildirimSesleri.Cal(sesAnahtari);
        _simge.ShowBalloonTip(15000, baslik, metin, Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _simge.Visible = false;
        _simge.Dispose();
    }
}
