using Adapters.Abstractions;
using Contracts;
using Diagnostic.Desktop.Navigation;
using Diagnostic.Desktop.Services;
using Diagnostic.Desktop.ViewModels;
using Diagnostic.Desktop.Views;
using Diagnostic.Presentation;
using Diagnostic.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diagnostic.Desktop.Composition;

public static class ServiceRegistration
{
    /// <summary>Registers everything the desktop app needs. Used by App and by the composition test.</summary>
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        services.AddLogging(builder => builder
            .AddDebug()
            .SetMinimumLevel(LogLevel.Information));

        // SWAP POINT: register the real backend here once a real VCI engine is selected.
        services.AddSingleton<IDiagnosticBackend, SimulatedBackend>();
        services.AddSingleton<IVciAdapter>(sp => sp.GetRequiredService<IDiagnosticBackend>().Adapter);
        services.AddSingleton<IDiagnosticService>(sp => sp.GetRequiredService<IDiagnosticBackend>().Diagnostics);

        services.AddSingleton<AppSession>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton(LocalizationService.Instance);
        services.AddSingleton<INavigationService, NavigationService>();

        // Singletons so a screen keeps its results while the user moves between screens.
        services.AddSingleton<ConnectionViewModel>();
        services.AddSingleton<VehicleViewModel>();
        services.AddSingleton<ScanViewModel>();
        services.AddSingleton<DtcViewModel>();
        services.AddSingleton<LiveDataViewModel>();

        services.AddSingleton(sp =>
        {
            // Keys without an entry stay placeholders (dashboard, freeze frame, reports).
            var screens = new Dictionary<string, Func<ViewModelBase>>
            {
                ["connection"] = () => sp.GetRequiredService<ConnectionViewModel>(),
                ["vehicle"] = () => sp.GetRequiredService<VehicleViewModel>(),
                ["scan"] = () => sp.GetRequiredService<ScanViewModel>(),
                ["dtc"] = () => sp.GetRequiredService<DtcViewModel>(),
                ["livedata"] = () => sp.GetRequiredService<LiveDataViewModel>(),
            };

            return new MainViewModel(
                sp.GetRequiredService<INavigationService>(),
                sp.GetRequiredService<ThemeService>(),
                sp.GetRequiredService<LocalizationService>(),
                screens,
                sp.GetRequiredService<IDiagnosticBackend>().DisplayName);
        });

        services.AddSingleton<MainWindow>();
        return services;
    }
}
