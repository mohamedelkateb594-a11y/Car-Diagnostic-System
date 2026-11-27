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
        var viewModel = new MainViewModel(navigation, new ThemeService(), new LocalizationService());
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
    public void SelectingItem_NavigatesToPlaceholderWithTitleKeyAndTaskReference()
    {
        var (viewModel, navigation) = Create();

        viewModel.SelectedItem = navigation.Items.First(item => item.Key == "dtc");

        Assert.Equal("dtc", navigation.CurrentKey);
        var placeholder = Assert.IsType<PlaceholderViewModel>(navigation.Current);
        Assert.Equal("NavDtc", placeholder.TitleKey);
        Assert.Equal("C-104", placeholder.TaskReference);
        Assert.True(placeholder.HasTaskReference);
    }

    [Fact]
    public void Dashboard_HasNoTaskReference()
    {
        var (_, navigation) = Create();

        var placeholder = Assert.IsType<PlaceholderViewModel>(navigation.Current);
        Assert.Null(placeholder.TaskReference);
        Assert.False(placeholder.HasTaskReference);
    }

    [Fact]
    public void AllNavigationTitleKeysAreDistinct()
    {
        var (_, navigation) = Create();

        var keys = navigation.Items.Select(item => item.TitleKey).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }
}
