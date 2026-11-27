using System.ComponentModel;
using System.Windows.Input;
using Diagnostic.Desktop.Mvvm;
using Diagnostic.Desktop.Navigation;
using Diagnostic.Desktop.Services;

namespace Diagnostic.Desktop.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;
    private readonly LocalizationService _localization;
    private NavigationItem? _selectedItem;

    public MainViewModel(
        INavigationService navigation,
        ThemeService themeService,
        LocalizationService localization)
    {
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));

        RegisterScreens();

        ToggleThemeCommand = new RelayCommand(_ => _themeService.Toggle());
        ToggleLanguageCommand = new RelayCommand(_ => _localization.Toggle());
        Navigation.PropertyChanged += OnNavigationChanged;

        if (Navigation.Items.Count > 0)
        {
            SelectedItem = Navigation.Items[0];
        }
    }

    public INavigationService Navigation { get; }

    public ICommand ToggleThemeCommand { get; }

    public ICommand ToggleLanguageCommand { get; }

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

    // Task ids follow TASKS.md (null where TASKS.md has no id). Real screens replace these placeholders later.
    private void RegisterScreens()
    {
        Register("dashboard", "NavDashboard", null);
        Register("connection", "NavConnection", "C-102");
        Register("vehicle", "NavVehicle", "C-103");
        Register("scan", "NavScan", "C-104");
        Register("dtc", "NavDtc", "C-104");
        Register("livedata", "NavLiveData", "C-105");
        Register("freezeframe", "NavFreezeFrame", null);
        Register("reports", "NavReports", "C-107");
    }

    private void Register(string key, string titleKey, string? taskReference)
        => Navigation.Register(new NavigationItem(key, titleKey, () => new PlaceholderViewModel(titleKey, taskReference)));

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
