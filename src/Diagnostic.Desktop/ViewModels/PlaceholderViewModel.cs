namespace Diagnostic.Desktop.ViewModels;

/// <summary>
/// Temporary screen shown until the real screen is built.
/// It shows no diagnostic data of any kind.
/// </summary>
public sealed class PlaceholderViewModel : ViewModelBase
{
    public PlaceholderViewModel(string titleKey, string? taskReference)
    {
        TitleKey = titleKey;
        TaskReference = taskReference;
    }

    /// <summary>Localization key of the screen title.</summary>
    public string TitleKey { get; }

    /// <summary>Task id from TASKS.md, or null when the screen has none.</summary>
    public string? TaskReference { get; }

    public bool HasTaskReference => !string.IsNullOrEmpty(TaskReference);
}
