using System.Collections.ObjectModel;
using System.Globalization;
using Contracts;
using Diagnostic.Desktop.Mvvm;

namespace Diagnostic.Desktop.ViewModels;

/// <summary>History of one live parameter, bounded so a long session cannot grow without limit.</summary>
public sealed class LiveSeriesViewModel : ObservableObject
{
    public const int MaxPoints = 120;

    private string _latestText = string.Empty;
    private bool _isSimulated;

    public LiveSeriesViewModel(string parameterId, string name, string unit)
    {
        ParameterId = parameterId;
        Name = name;
        Unit = unit;
    }

    public string ParameterId { get; }

    public string Name { get; }

    public string Unit { get; }

    public ObservableCollection<double> Values { get; } = new();

    public string LatestText
    {
        get => _latestText;
        private set => SetProperty(ref _latestText, value);
    }

    public bool IsSimulated
    {
        get => _isSimulated;
        private set => SetProperty(ref _isSimulated, value);
    }

    public void Add(LiveDataPoint point)
    {
        Values.Add(point.Value);
        while (Values.Count > MaxPoints)
        {
            Values.RemoveAt(0);
        }

        LatestText = $"{point.Value.ToString("0.##", CultureInfo.CurrentCulture)} {Unit}";
        IsSimulated = point.IsSimulated;
    }
}
