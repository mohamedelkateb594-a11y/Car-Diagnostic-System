using System.Windows.Input;

namespace Diagnostic.Presentation.Mvvm;

/// <summary>
/// Command for async work. While it runs, CanExecute is false (prevents double execution).
/// The view model must handle expected failures; ExecuteAsync does not swallow exceptions.
/// </summary>
public sealed class AsyncRelayCommand : ICommand, IRaiseCanExecute
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

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
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
