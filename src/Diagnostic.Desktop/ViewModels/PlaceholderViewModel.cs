namespace Diagnostic.Desktop.ViewModels;

/// <summary>
/// Temporary screen shown until the real screen is built.
/// It shows no diagnostic data of any kind.
/// </summary>
public sealed class PlaceholderViewModel : ViewModelBase
{
    public PlaceholderViewModel(string title, string taskReference)
    {
        Title = title;
        Message = $"This screen is not implemented yet ({taskReference}).";
    }

    public string Title { get; }

    public string Message { get; }
}
