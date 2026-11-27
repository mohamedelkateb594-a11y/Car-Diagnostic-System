using Diagnostic.Desktop.Navigation;
using Diagnostic.Desktop.Services;
using Diagnostic.Desktop.ViewModels;
using Xunit;

namespace Desktop.Tests;

public class MainViewModelTests
{
    private static (MainViewModel ViewModel, NavigationService Navigation) Create()
    {
        var navigation = new NavigationService();
        var viewModel = new MainViewModel(navigation, new ThemeService());
        return (viewModel, navigation);
    }

    [Fact]
    public void Constructor_RegistersEightScreensAndSelectsDashboard()
    {
        var (viewModel, navigation) = Create();

        Assert.Equal(8, navigation.Items.Count);
        Assert.Equal("dashboard", viewModel.SelectedItem?.Key);
        Assert.Equal("dashboard", navigation.CurrentKey);
    }

    [Fact]
    public void SelectingItem_NavigatesToPlaceholderThatSaysNotImplemented()
    {
        var (viewModel, navigation) = Create();

        viewModel.SelectedItem = navigation.Items.First(item => item.Key == "dtc");

        Assert.Equal("dtc", navigation.CurrentKey);
        var placeholder = Assert.IsType<PlaceholderViewModel>(navigation.Current);
        Assert.Equal("Fault Codes (DTC)", placeholder.Title);
        Assert.Contains("not implemented", placeholder.Message, StringComparison.OrdinalIgnoreCase);
    }
}
