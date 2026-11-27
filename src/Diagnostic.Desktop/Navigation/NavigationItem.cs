using Diagnostic.Desktop.ViewModels;

namespace Diagnostic.Desktop.Navigation;

public sealed record NavigationItem(string Key, string Title, Func<ViewModelBase> CreateViewModel);
