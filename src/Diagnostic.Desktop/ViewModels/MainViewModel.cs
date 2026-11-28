using System.ComponentModel;
using System.Windows.Input;
using Diagnostic.Desktop.Navigation;
using Diagnostic.Desktop.Services;
using Diagnostic.Presentation.Mvvm;
using Diagnostic.Presentation.ViewModels;

namespace Diagnostic.Desktop.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;
    private readonly LocalizationService _localization;
    private readonly IReadOnlyDictionary<string, Func<ViewModelBase>> _screens;
    private NavigationItem? _selectedItem;

    /// <param name="screens">
    /// Real screens by navigation key. Keys without an entry fall back to a placeholder,
    /// so screens can be wired one by one as their services become available.
    /// </param>
    /// <param name="backendName">Name of the engine behind the services, shown in the window.</param>
    public MainViewModel(
        INavigationService navigation,
        ThemeService themeService,
        LocalizationService localization,
        IReadOnlyDictionary<string, Func<ViewModelBase>>? screens = null,
        string? backendName = null)
    {
        Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _screens = screens ?? new Dictionary<string, Func<ViewModelBase>>();
        BackendName = backendName ?? string.Empty;

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

    public string BackendName { get; }

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

    // Task ids follow TASKS.md (null where TASKS.md has no id). Keys without a real screen show a placeholder.
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
    {
        Func<ViewModelBase> factory = _screens.TryGetValue(key, out var screen)
            ? screen
            : () => new PlaceholderViewModel(titleKey, taskReference);
        Navigation.Register(new NavigationItem(key, titleKey, factory));
    }

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
