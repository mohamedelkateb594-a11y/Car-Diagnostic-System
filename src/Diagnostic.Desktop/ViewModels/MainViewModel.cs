using System.ComponentModel;
using System.Windows.Input;
using Diagnostic.Desktop.Mvvm;
using Diagnostic.Desktop.Navigation;
using Diagnostic.Desktop.Services;

namespace Diagnostic.Desktop.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;
    private NavigationItem? _selectedItem;

    public MainViewModel(INavigationService navigation, ThemeService themeService)
    {
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

        RegisterScreens();

        ToggleThemeCommand = new RelayCommand(_ => _themeService.Toggle());
        Navigation.PropertyChanged += OnNavigationChanged;

        if (Navigation.Items.Count > 0)
        {
            SelectedItem = Navigation.Items[0];
        }
    }

    public INavigationService Navigation { get; }

    public ICommand ToggleThemeCommand { get; }

    public NavigationItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value) && value is not null)
            {
                Navigation.NavigateTo(value.Key);
            }
        }
    }

    // Task references follow TASKS.md. Real screens replace these placeholders in later tasks.
    private void RegisterScreens()
    {
        Register("dashboard", "Dashboard", "shell, no task id in TASKS.md");
        Register("connection", "VCI Connection", "C-102");
        Register("vehicle", "Vehicle", "C-103");
        Register("scan", "Full Scan", "C-104");
        Register("dtc", "Fault Codes (DTC)", "C-104");
        Register("livedata", "Live Data", "C-105");
        Register("freezeframe", "Freeze Frame", "no task id in TASKS.md");
        Register("reports", "Reports", "C-107");
    }

    private void Register(string key, string title, string taskReference)
        => Navigation.Register(new NavigationItem(key, title, () => new PlaceholderViewModel(title, taskReference)));

    private void OnNavigationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(INavigationService.CurrentKey))
        {
            var match = Navigation.Items.FirstOrDefault(item => item.Key == Navigation.CurrentKey);
            if (match is not null && !ReferenceEquals(match, _selectedItem))
            {
                _selectedItem = match;
                OnPropertyChanged(nameof(SelectedItem));
            }
        }
    }
}
