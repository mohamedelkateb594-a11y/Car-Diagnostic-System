using System.Windows;
using Diagnostic.Desktop.Composition;
using Diagnostic.Desktop.Services;
using Diagnostic.Desktop.ViewModels;
using Diagnostic.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diagnostic.Desktop;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var collection = new ServiceCollection();
        collection.AddDesktopServices();
        _services = collection.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        DispatcherUnhandledException += (_, args) =>
        {
            _services.GetService<ILogger<App>>()?.LogError(args.Exception, "Unhandled UI exception.");
            MessageBox.Show(args.Exception.Message, "Car Diagnostic System", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // Theme and language first: the main view model and the window read them when they are created.
        _services.GetRequiredService<ThemeService>().Apply(ThemeService.DetectSystemTheme());
        _services.GetRequiredService<LocalizationService>().Apply(LocalizationService.DetectSystemLanguage());

        var window = _services.GetRequiredService<MainWindow>();
        window.DataContext = _services.GetRequiredService<MainViewModel>();
        window.Show();

        _services.GetService<ILogger<App>>()?.LogInformation(
            "Started. Engine: {Backend}", _services.GetRequiredService<IDiagnosticBackend>().DisplayName);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }
}
