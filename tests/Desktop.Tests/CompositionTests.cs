using Diagnostic.Desktop.Composition;
using Diagnostic.Desktop.Navigation;
using Diagnostic.Desktop.ViewModels;
using Diagnostic.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Desktop.Tests;

public class CompositionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDesktopServices();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    [Fact]
    public async Task EveryRegisteredServiceCanBeResolved_AndTheBackendIsSimulated()
    {
        await using var provider = BuildProvider();

        var backend = provider.GetRequiredService<IDiagnosticBackend>();
        var main = provider.GetRequiredService<MainViewModel>();

        Assert.IsType<SimulatedBackend>(backend);
        Assert.Contains("Simulated", main.BackendName);
    }

    [Fact]
    public async Task TheScreensThatAreWired_ResolveToRealViewModels_NotPlaceholders()
    {
        await using var provider = BuildProvider();
        _ = provider.GetRequiredService<MainViewModel>();
        var navigation = provider.GetRequiredService<INavigationService>();

        foreach (var key in new[] { "connection", "vehicle", "scan", "dtc", "livedata" })
        {
            Assert.True(navigation.NavigateTo(key));
            Assert.IsNotType<PlaceholderViewModel>(navigation.Current);
        }

        Assert.True(navigation.NavigateTo("reports"));
        Assert.IsType<PlaceholderViewModel>(navigation.Current);
    }

    [Fact]
    public async Task AScreenKeepsItsInstance_SoResultsSurviveNavigation()
    {
        await using var provider = BuildProvider();
        _ = provider.GetRequiredService<MainViewModel>();
        var navigation = provider.GetRequiredService<INavigationService>();

        navigation.NavigateTo("scan");
        var first = navigation.Current;
        navigation.NavigateTo("dtc");
        navigation.NavigateTo("scan");

        Assert.Same(first, navigation.Current);
    }
}
