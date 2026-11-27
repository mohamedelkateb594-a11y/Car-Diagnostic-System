using System.Globalization;
using System.Windows;
using Diagnostic.Desktop.Mvvm;

namespace Diagnostic.Desktop.Services;

public enum AppLanguage
{
    English,
    Arabic
}

public sealed class LocalizationService : ObservableObject
{
    // XAML binds to this instance. To be replaced by DI when DI is wired (approved, not yet in use).
    public static LocalizationService Instance { get; } = new();

    private ResourceDictionary? _strings;
    private AppLanguage _current = AppLanguage.English;
    private int _revision;

    public AppLanguage Current => _current;

    public FlowDirection FlowDirection =>
        _current == AppLanguage.Arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>Incremented on every language change so converters can re-evaluate.</summary>
    public int Revision => _revision;

    /// <summary>Returns the key itself when no string is loaded for it, so a missing string is visible.</summary>
    public string this[string key]
    {
        get
        {
            if (_strings is not null && _strings.Contains(key) && _strings[key] is string value)
            {
                return value;
            }

            return key;
        }
    }

    public void Apply(AppLanguage language)
    {
        var code = language == AppLanguage.Arabic ? "ar" : "en";
        var strings = new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/Diagnostic.Desktop;component/Resources/Strings/Strings.{code}.xaml",
                UriKind.Absolute)
        };

        Use(language, strings);
    }

    public void Toggle()
        => Apply(_current == AppLanguage.English ? AppLanguage.Arabic : AppLanguage.English);

    public static AppLanguage DetectSystemLanguage()
        => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? AppLanguage.Arabic : AppLanguage.English;

    // Separated from Apply so it can be tested without a running WPF Application.
    internal void Use(AppLanguage language, ResourceDictionary strings)
    {
        _strings = strings;
        _current = language;
        _revision++;

        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(FlowDirection));
        OnPropertyChanged(nameof(Revision));
    }
}
