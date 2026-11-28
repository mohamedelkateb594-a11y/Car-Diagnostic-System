using Diagnostic.Presentation.Mvvm;
using Diagnostic.Presentation.ViewModels;

namespace Diagnostic.Desktop.Navigation;

public sealed class NavigationService : ObservableObject, INavigationService
{
    private readonly List<NavigationItem> _items = new();
    private ViewModelBase? _current;
    private string? _currentKey;

    public IReadOnlyList<NavigationItem> Items => _items;

    public ViewModelBase? Current
    {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    public string? CurrentKey
    {
        get => _currentKey;
        private set => SetProperty(ref _currentKey, value);
    }

    public void Register(NavigationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_items.Any(existing => existing.Key == item.Key))
        {
            throw new InvalidOperationException($"Navigation key '{item.Key}' is already registered.");
        }

        _items.Add(item);
    }

    public bool NavigateTo(string key)
    {
        var item = _items.FirstOrDefault(candidate => candidate.Key == key);
        if (item is null)
        {
            return false;
        }

        if (CurrentKey == key)
        {
            return true;
        }

        Current = item.CreateViewModel();
        CurrentKey = key;
        return true;
    }
}
