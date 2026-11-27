using System.Windows;
using Diagnostic.Desktop.Mock;
using Diagnostic.Desktop.Navigation;
using Diagnostic.Desktop.Services;
using Diagnostic.Desktop.ViewModels;
using Diagnostic.Desktop.Views;

namespace Diagnostic.Desktop;

public partial class App : Application
{
    // Manual composition root (DI is approved but the object graph is still tiny).
    // This is the ONLY place that knows about the UI mock services. When Developer A's
    // Diagnostic.Core / real VCI adapter are ready, replace the two lines marked SWAP below.
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var themeService = new ThemeService();
        themeService.Apply(ThemeService.DetectSystemTheme());

        // XAML binds to LocalizationService.Instance, so the same instance is used here.
        var localization = LocalizationService.Instance;
        localization.Apply(LocalizationService.DetectSystemLanguage());

        var session = new AppSession();
        var adapter = new UiMockVciAdapter();                   // SWAP: real IVciAdapter
        var diagnostics = new UiMockDiagnosticService(adapter); // SWAP: real IDiagnosticService

        // Created once and kept, so results survive navigation between screens.
        var screens = new Dictionary<string, Func<ViewModelBase>>
        {
            ["connection"] = Cached(() => new ConnectionViewModel(adapter, diagnostics, session)),
            ["vehicle"] = Cached(() => new VehicleViewModel(diagnostics, session)),
            ["scan"] = Cached(() => new ScanViewModel(diagnostics, session)),
            ["dtc"] = Cached(() => new DtcViewModel(diagnostics, session)),
            ["livedata"] = Cached(() => new LiveDataViewModel(diagnostics, session)),
        };

        var navigation = new NavigationService();
        var mainViewModel = new MainViewModel(navigation, themeService, localization, screens);

        var window = new MainWindow { DataContext = mainViewModel };
        window.Show();
    }

    private static Func<ViewModelBase> Cached(Func<ViewModelBase> create)
    {
        ViewModelBase? instance = null;
        return () => instance ??= create();
    }
}
