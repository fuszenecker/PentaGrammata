using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using PentaGrammata.Models;

namespace PentaGrammata.Views.Controls;

public sealed class TrendsTimelineChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<PracticeTrendPoint>?> ItemsProperty =
        AvaloniaProperty.Register<TrendsTimelineChart, IReadOnlyList<PracticeTrendPoint>?>(nameof(Items));

    public static readonly StyledProperty<bool> ShowCharacterSeriesProperty =
        AvaloniaProperty.Register<TrendsTimelineChart, bool>(nameof(ShowCharacterSeries), true);

    public static readonly StyledProperty<bool> ShowAverageSeriesProperty =
        AvaloniaProperty.Register<TrendsTimelineChart, bool>(nameof(ShowAverageSeries), true);

    public static readonly StyledProperty<bool> ShowErrorSeriesProperty =
        AvaloniaProperty.Register<TrendsTimelineChart, bool>(nameof(ShowErrorSeries), true);

    public static readonly StyledProperty<bool> ShowLimitSeriesProperty =
        AvaloniaProperty.Register<TrendsTimelineChart, bool>(nameof(ShowLimitSeries), true);

    public static readonly StyledProperty<bool> ShowNoiseSeriesProperty =
        AvaloniaProperty.Register<TrendsTimelineChart, bool>(nameof(ShowNoiseSeries), true);

    private const double LeftAxisWidth = 52;
    private const double RightAxisWidth = 58;
    private const double TopPadding = 10;
    private const double BottomPadding = 8;
    private const double NoiseBandHeight = 34;
    private const double TimeAxisHeight = 26;

    private double _viewStart;
    private double _viewSpan = 1;
    private bool _isPanning;
    private Point _panStartPoint;
    private double _panStartViewStart;
    private bool _isHovering;
    private Point _hoverPoint;

    private static readonly Color CharacterColor = Color.Parse("#1D4ED8");
    private static readonly Color AverageColor = Color.Parse("#0891B2");
    private static readonly Color ErrorColor = Color.Parse("#DC2626");
    private static readonly Color LimitColor = Color.Parse("#F59E0B");
    private static readonly Color NoiseColor = Color.Parse("#16A34A");

    public IReadOnlyList<PracticeTrendPoint>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public bool ShowCharacterSeries
    {
        get => GetValue(ShowCharacterSeriesProperty);
        set => SetValue(ShowCharacterSeriesProperty, value);
    }

    public bool ShowAverageSeries
    {
        get => GetValue(ShowAverageSeriesProperty);
        set => SetValue(ShowAverageSeriesProperty, value);
    }

    public bool ShowErrorSeries
    {
        get => GetValue(ShowErrorSeriesProperty);
        set => SetValue(ShowErrorSeriesProperty, value);
    }

    public bool ShowLimitSeries
    {
        get => GetValue(ShowLimitSeriesProperty);
        set => SetValue(ShowLimitSeriesProperty, value);
    }

    public bool ShowNoiseSeries
    {
        get => GetValue(ShowNoiseSeriesProperty);
        set => SetValue(ShowNoiseSeriesProperty, value);
    }

    public TrendsTimelineChart()
    {
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0F111A")), bounds);

        var points = Items;
        if (points is null || points.Count == 0)
        {
            return;
        }

        var ordered = points.OrderBy(x => x.RecordedAt).ToArray();
        var visible = GetVisibleSlice(ordered);
        if (visible.Count == 0)
        {
            return;
        }

        var chartRect = new Rect(
            LeftAxisWidth,
            TopPadding,
            Math.Max(1, bounds.Width - LeftAxisWidth - RightAxisWidth),
            Math.Max(1, bounds.Height - TopPadding - BottomPadding - NoiseBandHeight - TimeAxisHeight));

        var noiseRect = new Rect(
            chartRect.Left,
            chartRect.Bottom + 6,
            chartRect.Width,
            NoiseBandHeight - 6);

        var xAxisY = noiseRect.Bottom + 4;

        if (!AnySeriesEnabled())
        {
            DrawNoSeriesMessage(context, bounds);
            return;
        }

        DrawAxes(context, chartRect);
        DrawSpeedSeries(context, chartRect, visible);
        DrawPercentSeries(context, chartRect, visible);
        DrawNoiseBand(context, noiseRect, visible);
        DrawTimeAxis(context, chartRect, xAxisY, visible);

        if (_isHovering)
        {
            DrawHoverOverlay(context, chartRect, noiseRect, xAxisY, visible);
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var delta = e.Delta.Y;
        if (Math.Abs(delta) < double.Epsilon)
        {
            return;
        }

        var zoomFactor = delta > 0 ? 0.85 : 1.15;
        var oldSpan = _viewSpan;

        // Don't allow zooming in past two visible samples: a narrower window can fall
        // between sparse sessions and select no points, which used to blank the chart.
        var count = Items?.Count ?? 0;
        var minSpan = count > 2 ? Math.Min(1.0, 2.0 / count) : 1.0;
        var newSpan = Math.Clamp(_viewSpan * zoomFactor, minSpan, 1.0);
        var cursorX = e.GetPosition(this).X;
        var ratio = Bounds.Width > 0 ? Math.Clamp(cursorX / Bounds.Width, 0, 1) : 0.5;

        var pivot = _viewStart + oldSpan * ratio;
        _viewSpan = newSpan;
        _viewStart = Math.Clamp(pivot - _viewSpan * ratio, 0, 1 - _viewSpan);

        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPanning = true;
            _panStartPoint = e.GetPosition(this);
            _panStartViewStart = _viewStart;
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        _hoverPoint = e.GetPosition(this);
        _isHovering = true;

        if (!_isPanning)
        {
            InvalidateVisual();
            return;
        }

        var current = e.GetPosition(this);
        var dx = current.X - _panStartPoint.X;
        if (Bounds.Width <= 0)
        {
            return;
        }

        var delta = -dx / Bounds.Width * _viewSpan;
        _viewStart = Math.Clamp(_panStartViewStart + delta, 0, 1 - _viewSpan);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isHovering = false;
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsProperty)
        {
            _viewStart = 0;
            _viewSpan = 1;
            InvalidateVisual();
            return;
        }

        if (change.Property == ShowCharacterSeriesProperty
            || change.Property == ShowAverageSeriesProperty
            || change.Property == ShowErrorSeriesProperty
            || change.Property == ShowLimitSeriesProperty
            || change.Property == ShowNoiseSeriesProperty)
        {
            InvalidateVisual();
        }
    }

    private bool AnySeriesEnabled()
    {
        return ShowCharacterSeries
            || ShowAverageSeries
            || ShowErrorSeries
            || ShowLimitSeries
            || ShowNoiseSeries;
    }

    private void DrawAxes(DrawingContext context, Rect chartRect)
    {
        var axisPen = new Pen(new SolidColorBrush(Color.Parse("#6B7280")), 1);
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#2A2D3A")), 1);

        context.DrawLine(axisPen, new Point(chartRect.Left, chartRect.Top), new Point(chartRect.Left, chartRect.Bottom));
        context.DrawLine(axisPen, new Point(chartRect.Right, chartRect.Top), new Point(chartRect.Right, chartRect.Bottom));

        var speedMax = GetSpeedMax();
        const int tickCount = 5;

        for (var i = 0; i <= tickCount; i++)
        {
            var ratio = (double)i / tickCount;
            var y = chartRect.Bottom - ratio * chartRect.Height;

            context.DrawLine(gridPen, new Point(chartRect.Left, y), new Point(chartRect.Right, y));

            var speedValue = ratio * speedMax;
            var speedLabel = CreateText(speedValue.ToString("0", CultureInfo.InvariantCulture), 10, "#CBD5E1");
            context.DrawText(speedLabel, new Point(Math.Max(0, chartRect.Left - speedLabel.Width - 8), y - speedLabel.Height / 2));

            var percentValue = ratio * 100;
            var percentLabel = CreateText($"{percentValue:0}%", 10, "#CBD5E1");
            context.DrawText(percentLabel, new Point(chartRect.Right + 8, y - percentLabel.Height / 2));
        }

        var leftTitle = CreateText("WPM", 10.5, "#93C5FD");
        context.DrawText(leftTitle, new Point(chartRect.Left - leftTitle.Width - 8, chartRect.Top - leftTitle.Height - 4));

        var rightTitle = CreateText("Percent", 10.5, "#FCA5A5");
        context.DrawText(rightTitle, new Point(chartRect.Right + 8, chartRect.Top - rightTitle.Height - 4));
    }

    private void DrawSpeedSeries(DrawingContext context, Rect chartRect, IReadOnlyList<PracticeTrendPoint> visible)
    {
        var speedMax = GetSpeedMax();
        if (speedMax <= 0)
        {
            return;
        }

        if (ShowCharacterSeries)
        {
            DrawLineSeries(context, chartRect, visible, p => p.CharacterWpm, 0, speedMax, CharacterColor);
        }

        if (ShowAverageSeries)
        {
            DrawLineSeries(context, chartRect, visible, p => p.AverageWpm, 0, speedMax, AverageColor);
        }
    }

    private void DrawPercentSeries(DrawingContext context, Rect chartRect, IReadOnlyList<PracticeTrendPoint> visible)
    {
        const double percentMax = 100;

        if (ShowErrorSeries)
        {
            DrawLineSeries(context, chartRect, visible, p => p.ErrorRatePercent, 0, percentMax, ErrorColor);
        }

        if (ShowLimitSeries)
        {
            DrawLineSeries(context, chartRect, visible, p => p.ErrorThresholdPercent, 0, percentMax, LimitColor, isDashed: true);
        }
    }

    private void DrawNoiseBand(DrawingContext context, Rect noiseRect, IReadOnlyList<PracticeTrendPoint> visible)
    {
        var panelBrush = new SolidColorBrush(Color.Parse("#111827"));
        context.FillRectangle(panelBrush, noiseRect);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#374151"))), noiseRect);

        if (!ShowNoiseSeries || visible.Count == 0)
        {
            var muted = CreateText("Noise hidden", 10, "#9CA3AF");
            context.DrawText(muted, new Point(noiseRect.Left + 6, noiseRect.Top + 2));
            return;
        }

        var min = visible.Min(x => x.NoiseLevelDb);
        var max = visible.Max(x => x.NoiseLevelDb);
        var range = Math.Max(0.001, max - min);

        var fillGeometry = new StreamGeometry();
        using (var gc = fillGeometry.Open())
        {
            var firstX = noiseRect.Left;
            gc.BeginFigure(new Point(firstX, noiseRect.Bottom), true);

            for (var i = 0; i < visible.Count; i++)
            {
                var point = visible[i];
                var x = noiseRect.Left + (double)i / Math.Max(1, visible.Count - 1) * noiseRect.Width;
                var normalized = (point.NoiseLevelDb - min) / range;
                var y = noiseRect.Bottom - normalized * (noiseRect.Height - 6);
                gc.LineTo(new Point(x, y));
            }

            gc.LineTo(new Point(noiseRect.Right, noiseRect.Bottom));
            gc.EndFigure(true);
        }

        context.DrawGeometry(
            new SolidColorBrush(Color.FromArgb(100, NoiseColor.R, NoiseColor.G, NoiseColor.B)),
            new Pen(new SolidColorBrush(NoiseColor), 1.4),
            fillGeometry);

        var label = CreateText($"Noise {min:0.0}..{max:0.0} dB", 10, "#BBF7D0");
        context.DrawText(label, new Point(noiseRect.Left + 6, noiseRect.Top + 2));
    }

    private static void DrawTimeAxis(
        DrawingContext context,
        Rect chartRect,
        double axisY,
        IReadOnlyList<PracticeTrendPoint> visible)
    {
        if (visible.Count == 0)
        {
            return;
        }

        var axisPen = new Pen(new SolidColorBrush(Color.Parse("#6B7280")), 1);
        context.DrawLine(axisPen, new Point(chartRect.Left, axisY), new Point(chartRect.Right, axisY));

        var tickCount = Math.Min(6, Math.Max(2, visible.Count));
        for (var tick = 0; tick < tickCount; tick++)
        {
            var ratio = tickCount == 1 ? 0 : (double)tick / (tickCount - 1);
            var x = chartRect.Left + ratio * chartRect.Width;
            var index = (int)Math.Round(ratio * Math.Max(0, visible.Count - 1), MidpointRounding.AwayFromZero);
            index = Math.Clamp(index, 0, visible.Count - 1);

            context.DrawLine(axisPen, new Point(x, axisY), new Point(x, axisY + 4));

            var label = CreateText(
                visible[index].RecordedAt.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture),
                9.5,
                "#CBD5E1");
            context.DrawText(label, new Point(x - label.Width / 2, axisY + 4));
        }
    }

    private void DrawHoverOverlay(
        DrawingContext context,
        Rect chartRect,
        Rect noiseRect,
        double xAxisY,
        IReadOnlyList<PracticeTrendPoint> visible)
    {
        if (visible.Count == 0)
        {
            return;
        }

        var hoverBounds = new Rect(chartRect.Left, chartRect.Top, chartRect.Width, xAxisY - chartRect.Top + 20);
        if (!hoverBounds.Contains(_hoverPoint))
        {
            return;
        }

        var ratio = Math.Clamp((_hoverPoint.X - chartRect.Left) / chartRect.Width, 0, 1);
        var index = (int)Math.Round(ratio * Math.Max(0, visible.Count - 1), MidpointRounding.AwayFromZero);
        index = Math.Clamp(index, 0, visible.Count - 1);

        var point = visible[index];
        var x = chartRect.Left + (double)index / Math.Max(1, visible.Count - 1) * chartRect.Width;

        var crossPen = new Pen(new SolidColorBrush(Color.Parse("#9CA3AF")), 1, dashStyle: new DashStyle([4, 4], 0));
        context.DrawLine(crossPen, new Point(x, chartRect.Top), new Point(x, xAxisY + 2));

        var lines = new List<string>
        {
            point.RecordedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
        };

        if (ShowCharacterSeries)
        {
            lines.Add($"Character speed: {point.CharacterWpm:0.##} WPM");
        }

        if (ShowAverageSeries)
        {
            lines.Add($"Average speed: {point.AverageWpm:0.##} WPM");
        }

        if (ShowErrorSeries)
        {
            lines.Add($"Error rate: {point.ErrorRatePercent:0.##}%");
        }

        if (ShowLimitSeries)
        {
            lines.Add($"Error limit: {point.ErrorThresholdPercent:0.##}%");
        }

        if (ShowNoiseSeries)
        {
            lines.Add($"Noise: {point.NoiseLevelDb:0.##} dB");
        }

        var texts = lines.Select(line => CreateText(line, 11, "#F9FAFB")).ToArray();
        var width = texts.Max(x => x.Width) + 14;
        var height = texts.Sum(x => x.Height) + 12;

        var tooltipX = x + 10;
        if (tooltipX + width > chartRect.Right)
        {
            tooltipX = x - width - 10;
        }

        var tooltipY = Math.Clamp(_hoverPoint.Y + 10, chartRect.Top + 2, noiseRect.Bottom - height - 2);

        var tooltipRect = new Rect(tooltipX, tooltipY, width, height);
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(235, 17, 24, 39)), tooltipRect);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#374151"))), tooltipRect);

        var drawY = tooltipRect.Top + 6;
        foreach (var text in texts)
        {
            context.DrawText(text, new Point(tooltipRect.Left + 7, drawY));
            drawY += text.Height;
        }
    }

    private double GetSpeedMax()
    {
        var points = Items;
        if (points is null || points.Count == 0)
        {
            return 10;
        }

        var maxSpeed = points.Max(x => Math.Max((double)x.CharacterWpm, x.AverageWpm));
        if (maxSpeed <= 0)
        {
            return 10;
        }

        return Math.Max(10, Math.Ceiling(maxSpeed / 5.0) * 5.0);
    }

    private IReadOnlyList<PracticeTrendPoint> GetVisibleSlice(IReadOnlyList<PracticeTrendPoint> ordered)
    {
        if (ordered.Count <= 2)
        {
            return ordered;
        }

        // Slice by index rather than by absolute time. The series are drawn evenly spaced
        // by index, so slicing the same way keeps zoom/pan consistent and — crucially —
        // always yields at least two points, even when the window lands in a time gap
        // between sparse sessions (which previously blanked the chart).
        var lastIndex = ordered.Count - 1;
        var startIndex = (int)Math.Floor(_viewStart * lastIndex);
        var endIndex = (int)Math.Ceiling((_viewStart + _viewSpan) * lastIndex);

        startIndex = Math.Clamp(startIndex, 0, lastIndex - 1);
        endIndex = Math.Clamp(endIndex, startIndex + 1, lastIndex);

        var slice = new PracticeTrendPoint[endIndex - startIndex + 1];
        for (var i = 0; i < slice.Length; i++)
        {
            slice[i] = ordered[startIndex + i];
        }

        return slice;
    }

    private static void DrawLineSeries(
        DrawingContext context,
        Rect rect,
        IReadOnlyList<PracticeTrendPoint> points,
        Func<PracticeTrendPoint, double> selector,
        double min,
        double max,
        Color color,
        bool isDashed = false)
    {
        if (points.Count == 0)
        {
            return;
        }

        var range = Math.Max(0.001, max - min);

        var geometry = new StreamGeometry();
        using (var gc = geometry.Open())
        {
            for (var i = 0; i < points.Count; i++)
            {
                var x = rect.Left + (double)i / Math.Max(1, points.Count - 1) * rect.Width;
                var normalized = (selector(points[i]) - min) / range;
                normalized = Math.Clamp(normalized, 0, 1);
                var y = rect.Bottom - normalized * rect.Height;

                if (i == 0)
                {
                    gc.BeginFigure(new Point(x, y), false);
                }
                else
                {
                    gc.LineTo(new Point(x, y));
                }
            }
        }

        var pen = isDashed
            ? new Pen(new SolidColorBrush(color), 1.6, dashStyle: new DashStyle([5, 4], 0))
            : new Pen(new SolidColorBrush(color), 1.8);

        context.DrawGeometry(null, pen, geometry);
    }

    private static FormattedText CreateText(string text, double fontSize, string colorHex)
    {
        return new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            fontSize,
            new SolidColorBrush(Color.Parse(colorHex)));
    }

    private static void DrawNoSeriesMessage(DrawingContext context, Rect bounds)
    {
        var text = CreateText("No active series. Enable at least one checkbox above.", 13, "#D1D5DB");
        context.DrawText(
            text,
            new Point(
                Math.Max(8, (bounds.Width - text.Width) / 2),
                Math.Max(8, (bounds.Height - text.Height) / 2)));
    }
}
