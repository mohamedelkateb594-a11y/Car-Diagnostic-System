using System.Windows.Input;

namespace Diagnostic.Desktop.Mvvm;

/// <summary>Asks WPF to re-evaluate CanExecute of all commands (used after async state changes).</summary>
public static class CommandRequery
{
    public static void Invalidate() => CommandManager.InvalidateRequerySuggested();
}
