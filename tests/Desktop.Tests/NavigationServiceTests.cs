using System.ComponentModel;
using Diagnostic.Desktop.Navigation;
using Diagnostic.Presentation.ViewModels;
using Xunit;

namespace Desktop.Tests;

public class NavigationServiceTests
{
    private static NavigationItem Item(string key, Func<ViewModelBase>? factory = null)
        => new(key, key, factory ?? (() => new PlaceholderViewModel(key, "test")));

    [Fact]
    public void Register_DuplicateKey_Throws()
    {
        var navigation = new NavigationService();
        navigation.Register(Item("a"));

        Assert.Throws<InvalidOperationException>(() => navigation.Register(Item("a")));
    }

    [Fact]
    public void NavigateTo_UnknownKey_ReturnsFalseAndChangesNothing()
    {
        var navigation = new NavigationService();
        navigation.Register(Item("a"));

        var result = navigation.NavigateTo("missing");

        Assert.False(result);
        Assert.Null(navigation.Current);
        Assert.Null(navigation.CurrentKey);
    }

    [Fact]
    public void NavigateTo_KnownKey_CreatesViewModelAndSetsCurrentKey()
    {
        var navigation = new NavigationService();
        navigation.Register(Item("a"));

        var result = navigation.NavigateTo("a");

        Assert.True(result);
        Assert.Equal("a", navigation.CurrentKey);
        Assert.IsType<PlaceholderViewModel>(navigation.Current);
    }

    [Fact]
    public void NavigateTo_SameKeyTwice_DoesNotRecreateViewModel()
    {
        var created = 0;
        var navigation = new NavigationService();
        navigation.Register(Item("a", () =>
        {
            created++;
            return new PlaceholderViewModel("a", "test");
        }));

        navigation.NavigateTo("a");
        navigation.NavigateTo("a");

        Assert.Equal(1, created);
    }

    [Fact]
    public void NavigateTo_RaisesPropertyChangedForCurrentAndCurrentKey()
    {
        var navigation = new NavigationService();
        navigation.Register(Item("a"));
        var raised = new List<string?>();
        navigation.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        navigation.NavigateTo("a");

        Assert.Contains(nameof(INavigationService.Current), raised);
        Assert.Contains(nameof(INavigationService.CurrentKey), raised);
    }
}
