using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace TurkiyeTakvimi.Arayuz;

// WPF, AutomationProperties.AccessKey değerini bazı denetimlerde ekran okuyucuya iletmiyor; bu sınıflar iletir.
// NVDA bu bilgiyi denetimin kısayolu olarak okur (örn. "Günün Sözü, Alt+S").

public sealed class KisayolluTextBox : TextBox
{
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(KisayolluTextBox sahip) : TextBoxAutomationPeer(sahip)
    {
        protected override string GetAccessKeyCore() => AutomationProperties.GetAccessKey(sahip);
    }
}

public sealed class KisayolluListBox : ListBox
{
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(KisayolluListBox sahip) : ListBoxAutomationPeer(sahip)
    {
        protected override string GetAccessKeyCore() => AutomationProperties.GetAccessKey(sahip);
    }
}
