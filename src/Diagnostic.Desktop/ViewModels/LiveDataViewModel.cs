using System.Collections.ObjectModel;
using Contracts;
using Diagnostic.Desktop.Mvvm;
using Diagnostic.Desktop.Services;

namespace Diagnostic.Desktop.ViewModels;

public sealed class LiveDataViewModel : EcuScopedViewModel
{
    private readonly IDiagnosticService _diagnostics;
    private CancellationTokenSource? _cts;
    private string _parameterIdsText = string.Empty;
    private bool _isRunning;
    private bool _hasSimulatedData;

    public LiveDataViewModel(IDiagnosticService diagnostics, AppSession session)
        : base(session)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

        StartCommand = new AsyncRelayCommand(
            StartAsync, () => Session.IsActive && SelectedEcu is not null && !IsRunning);
        StopCommand = new RelayCommand(_ => Stop(), _ => IsRunning);
    }

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    public ObservableCollection<LiveSeriesViewModel> Series { get; } = new();

    /// <summary>Comma-separated parameter ids. Empty lets the service choose its default set.</summary>
    public string ParameterIdsText
    {
        get => _parameterIdsText;
        set => SetProperty(ref _parameterIdsText, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                CommandRequery.Invalidate();
            }
        }
    }

    public bool HasSimulatedData
    {
        get => _hasSimulatedData;
        private set => SetProperty(ref _hasSimulatedData, value);
    }

    public AsyncRelayCommand StartCommand { get; }

    public RelayCommand StopCommand { get; }

    public void Stop() => _cts?.Cancel();

    protected override void OnEcusRefreshed()
    {
        Stop();
        Series.Clear();
        HasSimulatedData = false;
    }

    private async Task StartAsync()
    {
        using var cts = new CancellationTokenSource();
        _cts = cts;
        IsRunning = true;
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (!await PollOnceAsync(cts.Token))
                {
                    break;
                }

                await Task.Delay(PollInterval, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Stop requested.
        }
        finally
        {
            _cts = null;
            IsRunning = false;
        }
    }

    /// <summary>Reads one sample set. Returns false when polling should stop (error or no session).</summary>
    internal async Task<bool> PollOnceAsync(CancellationToken ct)
    {
        var info = Session.Info;
        var ecu = SelectedEcu;
        if (info is null || ecu is null)
        {
            return false;
        }

        try
        {
            var ids = ParameterIdsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var result = await _diagnostics.ReadLiveDataAsync(info.SessionId, ecu.EcuId, ids, ct);
            SetResultStatus(result);
            if (!IsUsable(result))
            {
                return false;
            }

            foreach (var point in result.Data!)
            {
                var series = Series.FirstOrDefault(candidate => candidate.ParameterId == point.ParameterId);
                if (series is null)
                {
                    series = new LiveSeriesViewModel(point.ParameterId, point.Name, point.Unit);
                    Series.Add(series);
                }

                series.Add(point);
            }

            HasSimulatedData = Series.Any(candidate => candidate.IsSimulated);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetStatus("OpStatus_Failed", ex.Message);
            return false;
        }
    }
}
