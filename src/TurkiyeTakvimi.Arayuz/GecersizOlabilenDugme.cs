using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>
/// Klavyeyle odaklanabilen ama "geçersiz" durumdayken ekran okuyucuya kullanılamıyor olarak bildirilen düğme.
/// Normal pasif (IsEnabled=false) düğmeye Tab ile gelinemez; bu düğme ise odak alır, soluk görünür ve
/// UI Automation'da IsEnabled=false olarak raporlanır, NVDA "kullanılamıyor" der.
/// </summary>
public sealed class GecersizOlabilenDugme : Button
{
    public static readonly DependencyProperty GecersizProperty = DependencyProperty.Register(
        nameof(Gecersiz), typeof(bool), typeof(GecersizOlabilenDugme),
        new PropertyMetadata(false, GecersizDegisti));

    static GecersizOlabilenDugme()
    {
        // Normal bir Button gibi (Fluent teması dahil) görünsün.
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GecersizOlabilenDugme),
            new FrameworkPropertyMetadata(typeof(Button)));
    }

    public bool Gecersiz
    {
        get => (bool)GetValue(GecersizProperty);
        set => SetValue(GecersizProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DugmePeer(this);

    private static void GecersizDegisti(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var dugme = (GecersizOlabilenDugme)d;
        bool yeni = (bool)e.NewValue;
        dugme.Opacity = yeni ? 0.55 : 1.0;

        if (UIElementAutomationPeer.FromElement(dugme) is DugmePeer peer)
            peer.DurumDegisti(eskiGecersiz: (bool)e.OldValue, yeniGecersiz: yeni);
    }

    private sealed class DugmePeer : ButtonAutomationPeer
    {
        public DugmePeer(GecersizOlabilenDugme sahip) : base(sahip) { }

        protected override bool IsEnabledCore() =>
            base.IsEnabledCore() && !((GecersizOlabilenDugme)Owner).Gecersiz;

        public void DurumDegisti(bool eskiGecersiz, bool yeniGecersiz) =>
            RaisePropertyChangedEvent(AutomationElementIdentifiers.IsEnabledProperty, !eskiGecersiz, !yeniGecersiz);
    }
}
