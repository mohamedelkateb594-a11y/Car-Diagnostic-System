using Contracts;
using Diagnostic.Desktop.Mvvm;

namespace Diagnostic.Desktop.ViewModels;

/// <summary>Base for screens that call services: busy flag, localized status key and raw service message.</summary>
public abstract class OperationScreenViewModel : ViewModelBase
{
    private bool _isBusy;
    private string? _statusKey;
    private string? _statusDetail;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CommandRequery.Invalidate();
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

    protected void SetStatus(string? key, string? detail = null)
    {
        StatusKey = key;
        StatusDetail = detail;
    }

    protected void SetResultStatus<T>(OperationResult<T> result)
        => SetStatus($"OpStatus_{result.Status}", result.Message);

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
            SetStatus("OpStatus_Failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
