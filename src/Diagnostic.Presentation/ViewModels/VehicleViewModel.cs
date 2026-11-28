using System.ComponentModel;
using Contracts;
using Diagnostic.Presentation.Mvvm;
using Microsoft.Extensions.Logging;

namespace Diagnostic.Presentation.ViewModels;

public sealed class VehicleViewModel : OperationScreenViewModel
{
    private const string Missing = "—";

    private readonly IDiagnosticService _diagnostics;

    public VehicleViewModel(
        IDiagnosticService diagnostics,
        AppSession session,
        ILogger<VehicleViewModel>? logger = null)
        : base(logger)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        ObserveSession(Session);
        Session.PropertyChanged += OnSessionChanged;

        ReadCommand = Track(new AsyncRelayCommand(ReadAsync, () => !IsBusy && Session.IsActive));
    }

    public AppSession Session { get; }

    public AsyncRelayCommand ReadCommand { get; }

    // Missing values are shown as a dash, never as zero or a guessed value (API_CONTRACTS.md section 1).
    public string VinText => Display(Session.Vehicle?.Vin);

    public string MakeText => Display(Session.Vehicle?.Make);

    public string ModelText => Display(Session.Vehicle?.Model);

    public string YearText => Session.Vehicle?.ModelYear?.ToString() ?? Missing;

    public string EngineText => Display(Session.Vehicle?.Engine);

    public string MarketText => Display(Session.Vehicle?.Market);

    public string? VinStatusKey
    {
        get
        {
            var vin = Session.Vehicle?.Vin;
            if (vin is null)
            {
                return null;
            }

            return VinValidator.IsWellFormed(vin) ? "VinFormatOk" : "VinFormatInvalid";
        }
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? Missing : value;

    private Task ReadAsync() => RunBusyAsync(async () =>
    {
        var info = Session.Info;
        if (info is null)
        {
            return;
        }

        var result = await _diagnostics.ReadVehicleIdentityAsync(info.SessionId, CancellationToken.None);
        if (IsUsable(result))
        {
            Session.SetVehicle(result.Data!);
        }

        SetResultStatus(result);
    });

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSession.Vehicle) or nameof(AppSession.Info))
        {
            OnPropertyChanged(nameof(VinText));
            OnPropertyChanged(nameof(MakeText));
            OnPropertyChanged(nameof(ModelText));
            OnPropertyChanged(nameof(YearText));
            OnPropertyChanged(nameof(EngineText));
            OnPropertyChanged(nameof(MarketText));
            OnPropertyChanged(nameof(VinStatusKey));
        }
    }
}
