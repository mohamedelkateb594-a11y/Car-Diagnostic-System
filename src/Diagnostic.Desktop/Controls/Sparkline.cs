using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

namespace Diagnostic.Desktop.Controls;

/// <summary>
/// Minimal dependency-free line chart. LiveCharts 2 stays postponed until its WPF package is tested on Windows.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IEnumerable<double>),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<double>? Values
    {
        get => (IEnumerable<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Sparkline)d;
        if (e.OldValue is INotifyCollectionChanged oldNotifier)
        {
            oldNotifier.CollectionChanged -= control.OnCollectionChanged;
        }

        if (e.NewValue is INotifyCollectionChanged newNotifier)
        {
            newNotifier.CollectionChanged += control.OnCollectionChanged;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var values = Values?.ToArray();
        if (values is null || values.Length < 2 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var min = values.Min();
        var max = values.Max();
        var range = max - min;
        if (range < 1e-9)
        {
            range = 1;
        }

        const double padding = 4;
        var width = ActualWidth - (2 * padding);
        var height = ActualHeight - (2 * padding);
        Point ToPoint(int index, double value) => new(
            padding + (width * index / (values.Length - 1)),
            padding + (height * (1 - ((value - min) / range))));

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(ToPoint(0, values[0]), false, false);
            for (var i = 1; i < values.Length; i++)
            {
                context.LineTo(ToPoint(i, values[i]), true, true);
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, new Pen(Stroke ?? Brushes.Gray, 1.5), geometry);
    }
}
