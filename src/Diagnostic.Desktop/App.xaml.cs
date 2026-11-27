using System.Windows;
using Diagnostic.Desktop.Navigation;
using Diagnostic.Desktop.Services;
using Diagnostic.Desktop.ViewModels;
using Diagnostic.Desktop.Views;

namespace Diagnostic.Desktop;

public partial class App : Application
{
    // Manual composition root. DI is approved but not wired yet: it is not needed until
    // services from A's approved contracts exist (C-102).
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var themeService = new ThemeService();
        themeService.Apply(ThemeService.DetectSystemTheme());

        // XAML binds to LocalizationService.Instance, so the same instance is used here.
        var localization = LocalizationService.Instance;
        localization.Apply(LocalizationService.DetectSystemLanguage());

        var navigation = new NavigationService();
        var mainViewModel = new MainViewModel(navigation, themeService, localization);

        var window = new MainWindow { DataContext = mainViewModel };
        window.Show();
    }
}
