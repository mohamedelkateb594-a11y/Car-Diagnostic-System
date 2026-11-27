using System.ComponentModel;
using Diagnostic.Desktop.ViewModels;

namespace Diagnostic.Desktop.Navigation;

public interface INavigationService : INotifyPropertyChanged
{
    IReadOnlyList<NavigationItem> Items { get; }

    ViewModelBase? Current { get; }

    string? CurrentKey { get; }

    void Register(NavigationItem item);

    /// <returns>false when the key is not registered.</returns>
    bool NavigateTo(string key);
}
