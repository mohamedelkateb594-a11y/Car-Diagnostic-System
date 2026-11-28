using System.Windows;
using Microsoft.Win32;

namespace Diagnostic.Desktop.Services;

public enum AppTheme
{
    Light,
    Dark
}

public sealed class ThemeService
{
    private ResourceDictionary? _active;

    public AppTheme Current { get; private set; } = AppTheme.Light;

    public void Apply(AppTheme theme)
    {
        var merged = Application.Current.Resources.MergedDictionaries;

        if (_active is not null)
        {
            merged.Remove(_active);
        }

        _active = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Diagnostic.Desktop;component/Theme/{theme}.xaml", UriKind.Absolute)
        };
        merged.Add(_active);
        Current = theme;
    }

    public void Toggle() => Apply(Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);

    public static AppTheme DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value && value == 0)
            {
                return AppTheme.Dark;
            }
        }
        catch (Exception)
        {
            // Registry unavailable or blocked: fall back to Light.
        }

        return AppTheme.Light;
    }
}
