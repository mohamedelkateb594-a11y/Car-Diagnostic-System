using Diagnostic.Desktop.ViewModels;

namespace Diagnostic.Desktop.Navigation;

/// <param name="Key">Stable identifier used for navigation.</param>
/// <param name="TitleKey">Localization key of the title (see Resources/Strings).</param>
public sealed record NavigationItem(string Key, string TitleKey, Func<ViewModelBase> CreateViewModel);
