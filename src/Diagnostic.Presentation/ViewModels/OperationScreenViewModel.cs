using Contracts;
using Diagnostic.Presentation.Mvvm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diagnostic.Presentation.ViewModels;

/// <summary>
/// Base for screens that call services: busy flag, localized status key and the raw service message.
/// The engine's OperationStatus is always shown; it is never turned into a success.
/// </summary>
public abstract class OperationScreenViewModel : ViewModelBase
{
    private readonly List<IRaiseCanExecute> _commands = new();
    private AppSession? _session;
    private bool _isBusy;
    private string? _statusKey;
    private string? _statusDetail;

    protected OperationScreenViewModel(ILogger? logger)
    {
        Logger = logger ?? NullLogger.Instance;
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Localization key of the current status (resolved in the view).</summary>
    public string? StatusKey
    {
        get => _statusKey;
        private set => SetProperty(ref _statusKey, value);
    }

    /// <summary>Message returned by the service, shown as-is (not localized).</summary>
    public string? StatusDetail
    {
        get => _statusDetail;
        private set => SetProperty(ref _statusDetail, value);
    }

    protected ILogger Logger { get; }

    protected TCommand Track<TCommand>(TCommand command)
        where TCommand : IRaiseCanExecute
    {
        _commands.Add(command);
        return command;
    }

    protected void RaiseCanExecuteChanged()
    {
        foreach (var command in _commands)
        {
            command.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Re-evaluates commands whenever the session changes, and lets a lost connection end the session.</summary>
    protected void ObserveSession(AppSession session)
    {
        _session = session;
        session.PropertyChanged += (_, _) => RaiseCanExecuteChanged();
    }

    protected void SetStatus(string? key, string? detail = null)
    {
        StatusKey = key;
        StatusDetail = detail;
    }

    protected void SetResultStatus<T>(OperationResult<T> result)
    {
        SetStatus($"OpStatus_{result.Status}", result.Message);

        var level = result.Status == OperationStatus.Success ? LogLevel.Debug : LogLevel.Information;
        Logger.Log(
            level,
            "{Screen}: {Status} ({ErrorCode}) {Message}",
            GetType().Name,
            result.Status,
            result.ErrorCode,
            result.Message);

        if (result.Status == OperationStatus.NotConnected && _session is { IsActive: true } active)
        {
            Logger.LogWarning("The engine reports the connection is closed; ending the session in the UI.");
            active.Clear();
        }
    }

    /// <summary>True when the result carries usable data (Success, or PartialSuccess with data).</summary>
    protected static bool IsUsable<T>(OperationResult<T> result)
        => result.Status is OperationStatus.Success or OperationStatus.PartialSuccess && result.Data is not null;

    protected async Task RunBusyAsync(Func<Task> work)
    {
        IsBusy = true;
        try
        {
            await work();
        }
        catch (OperationCanceledException)
        {
            SetStatus("OpStatus_Cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Screen}: unexpected failure", GetType().Name);
            SetStatus("OpStatus_Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
