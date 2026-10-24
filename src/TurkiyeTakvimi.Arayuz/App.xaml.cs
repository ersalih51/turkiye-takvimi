using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TurkiyeTakvimi.Cekirdek;

namespace TurkiyeTakvimi.Arayuz;

/// <summary>Uygulama tek kopya çalışır: ikinci kez başlatılırsa çalışan pencere öne gelir.</summary>
public partial class App : Application
{
    private const string KopyaAdi = @"Local\TurkiyeTakvimi-tek-kopya";
    private const string AcOlayiAdi = @"Local\TurkiyeTakvimi-ac";

    private Mutex? _kopya;
    private EventWaitHandle? _acOlayi;
    private MainWindow? _pencere;

    private DateTime _sonHataIletisi = DateTime.MinValue;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        YuksekKontrast.Baslat(this);

        // Beklenmeyen hatalar bu bilgisayardaki hata kaydına yazılır; hiçbir yere gönderilmez.
        DispatcherUnhandledException += (_, a) =>
        {
            HataKaydi.Yaz("Arayüz hatası", a.Exception);
            a.Handled = true; // uygulama çalışmaya devam eder

            // Aynı hata art arda tekrarlansa bile bir dakikada en fazla bir kez iletilir.
            if ((DateTime.Now - _sonHataIletisi).TotalSeconds < 60) return;
            _sonHataIletisi = DateTime.Now;
            MessageBox.Show("Beklenmeyen bir hata oluştu ve hata kaydına yazıldı. Uygulama çalışmaya devam ediyor; sorun sürerse uygulamayı yeniden başlatın.\n\nKaydı Hakkında penceresindeki Hata kaydını aç düğmesiyle görebilirsiniz.",
                "Türkiye Takvimi", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, a) => HataKaydi.Yaz("Ölümcül hata", a.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, a) =>
        {
            HataKaydi.Yaz("Arka plan görevi hatası", a.Exception);
            a.SetObserved();
        };

        // NVDA kullanıcıları düğmeleri Boşluk ile de etkinleştirir. Menü çubuğunda Boşluk, Alt+Boşluk gibi sistem menüsünü
        // açmasın diye Enter gibi davranır (alt menüyü açar ya da komutu çalıştırır).
        EventManager.RegisterClassHandler(typeof(MenuItem), Keyboard.PreviewKeyDownEvent, new KeyEventHandler(MenuBoslukTusu));

        _kopya = new Mutex(initiallyOwned: true, KopyaAdi, out bool ilkKopya);
        _acOlayi = new EventWaitHandle(false, EventResetMode.AutoReset, AcOlayiAdi);
        if (!ilkKopya)
        {
            _acOlayi.Set(); // çalışan kopyaya "pencereni göster" de
            Shutdown();
            return;
        }

        _pencere = new MainWindow(gizliBaslat: e.Args.Contains(OtomatikBaslat.TepsiBayragi));
        MainWindow = _pencere;
        _pencere.Show();

        // Sınama: hata kaydının ve hata iletisinin çalıştığını görmek için bilerek hata üretir.
        if (Environment.GetEnvironmentVariable("TT_HATA_DENE") == "1")
            Dispatcher.BeginInvoke(new Action(() => throw new InvalidOperationException("Hata kaydı denemesi")));

        var bekleyen = new Thread(() =>
        {
            while (_acOlayi.WaitOne())
                Dispatcher.BeginInvoke(() => _pencere.PenceriyiGoster());
        }) { IsBackground = true };
        bekleyen.Start();
    }

    private static void MenuBoslukTusu(object sender, KeyEventArgs e)
    {
        bool bosluk = e.Key == Key.Space || (e.Key == Key.System && e.SystemKey == Key.Space);
        if (!bosluk || sender is not MenuItem oge || e.OriginalSource != oge) return;

        e.Handled = true;
        var kaynak = PresentationSource.FromVisual(oge);
        if (kaynak is null) return;
        oge.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, kaynak, Environment.TickCount, Key.Enter)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _kopya?.Dispose();
        base.OnExit(e);
    }
}
