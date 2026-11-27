using System.Windows.Input;

namespace Diagnostic.Desktop.Mvvm;

/// <summary>
/// Command for async work. While it runs, CanExecute is false (prevents double execution).
/// Expected failures must be handled by the view model; ExecuteAsync does not swallow exceptions.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        try
        {
            await ExecuteAsync();
        }
        catch (OperationCanceledException)
        {
            // Cancellation is an expected outcome, not a failure.
        }
    }

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        _isRunning = true;
        CommandRequery.Invalidate();
        try
        {
            await _execute();
        }
        finally
        {
            _isRunning = false;
            CommandRequery.Invalidate();
        }
    }
}
