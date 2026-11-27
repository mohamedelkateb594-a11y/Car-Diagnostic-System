using System.Windows;
using Diagnostic.Desktop.Navigation;
using Diagnostic.Desktop.Services;
using Diagnostic.Desktop.ViewModels;
using Diagnostic.Desktop.Views;

namespace Diagnostic.Desktop;

public partial class App : Application
{
    // Manual composition root. A DI container is not added yet:
    // NuGet packages need approval first (DEVELOPMENT_RULES.md section 3).
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var themeService = new ThemeService();
        themeService.Apply(ThemeService.DetectSystemTheme());

        var navigation = new NavigationService();
        var mainViewModel = new MainViewModel(navigation, themeService);

        var window = new MainWindow { DataContext = mainViewModel };
        window.Show();
    }
}
