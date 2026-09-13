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

    public static readonly StyledProperty<bool> ShowDailyMaxSeriesProperty =
        AvaloniaProperty.Register<TrendsTimelineChart, bool>(nameof(ShowDailyMaxSeries), true);

    public static readonly StyledProperty<bool> ShowQsbSeriesProperty =
        AvaloniaProperty.Register<TrendsTimelineChart, bool>(nameof(ShowQsbSeries), true);

    private const double LeftAxisWidth = 52;
    private const double RightAxisWidth = 58;
    private const double TopPadding = 10;
    // Reserved band above the plot for the "WPM" / "Percent" axis titles, which are
    // drawn just above chartRect.Top and would otherwise be clipped off the top edge.
    private const double AxisTitleHeight = 18;
    private const double BottomPadding = 8;
    // The band carries two series (the SNR area and the QSB line) plus two stacked axis
    // titles, so it gets double the height a single-series strip would need.
    private const double NoiseBandHeight = 68;
    private const double TimeAxisHeight = 26;

    // Full scale of the right-hand percent axis. Error rates and thresholds worth reading
    // sit well under this, so the axis stops here instead of at 100 % and the series get
    // four times the vertical resolution.
    private const double PercentMax = 25;

    // Full-scale QSB fade depth for the noise band, matching the settings dialog's maximum.
    // Fixed rather than data-derived so the line's height is comparable across zoom levels.
    private const double QsbDepthScaleDb = 40;

    private double _viewStart;
    private double _viewSpan = 1;
    private bool _isPanning;
    private Point _panStartPoint;
    private double _panStartViewStart;
    private bool _isHovering;
    private Point _hoverPoint;

    // Categorical palette validated (dark surface #0F111A) for CVD separation and the
    // lightness band — see the dataviz skill's palette reference. The speed series stay
    // in cool/warm slots far enough apart that character (blue) and average (orange) no
    // longer read as the same color.
    private static readonly Color CharacterColor = Color.Parse("#DC2626");
    private static readonly Color AverageColor = Color.Parse("#3987E5");
    private static readonly Color DailyMaxFillColor = Color.FromArgb(28, 250, 204, 21);
    private static readonly Color ErrorColor = Color.Parse("#E66767");
    private static readonly Color LimitColor = Color.Parse("#C98500");
    private static readonly Color NoiseColor = Color.Parse("#008300");

    // QSB shares the noise band with the SNR area, so it needs a hue that separates from
    // the green fill at a glance: a solid, bright red line drawn on top of it.
    private static readonly Color QsbColor = Color.Parse("#FF4D4D");

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

    public bool ShowDailyMaxSeries
    {
        get => GetValue(ShowDailyMaxSeriesProperty);
        set => SetValue(ShowDailyMaxSeriesProperty, value);
    }

    public bool ShowQsbSeries
    {
        get => GetValue(ShowQsbSeriesProperty);
        set => SetValue(ShowQsbSeriesProperty, value);
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
            TopPadding + AxisTitleHeight,
            Math.Max(1, bounds.Width - LeftAxisWidth - RightAxisWidth),
            Math.Max(1, bounds.Height - TopPadding - AxisTitleHeight - BottomPadding - NoiseBandHeight - TimeAxisHeight));

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
        DrawDailyMaxSeries(context, chartRect, visible);
        DrawPercentSeries(context, chartRect, visible);
        DrawNoiseBand(context, noiseRect, visible);
        DrawQsbSeries(context, noiseRect, visible);
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

        // Ctrl + wheel zooms (anchored at the cursor); plain wheel pans. This keeps a
        // touchpad's fast wheel bursts from zooming by accident.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
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
        }
        else
        {
            // Pan by a fraction of the visible span so the step feels the same at any zoom.
            // Wheel up scrolls toward earlier sessions, wheel down toward later ones.
            var panStep = _viewSpan * 0.15 * (delta > 0 ? -1 : 1);
            _viewStart = Math.Clamp(_viewStart + panStep, 0, 1 - _viewSpan);
        }

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
            || change.Property == ShowNoiseSeriesProperty
            || change.Property == ShowDailyMaxSeriesProperty
            || change.Property == ShowQsbSeriesProperty)
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
            || ShowNoiseSeries
            || ShowDailyMaxSeries
            || ShowQsbSeries;
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

            var percentValue = ratio * PercentMax;
            var percentLabel = CreateText($"{percentValue:0.#}%", 10, "#CBD5E1");
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

    private void DrawDailyMaxSeries(DrawingContext context, Rect chartRect, IReadOnlyList<PracticeTrendPoint> visible)
    {
        if (!ShowDailyMaxSeries)
        {
            return;
        }

        var speedMax = GetSpeedMax();
        if (speedMax <= 0)
        {
            return;
        }

        var range = Math.Max(0.001, speedMax);

        // The daily max is a per-day value repeated on every session of the day, so the
        // fill's top edge traces every daily value as a step. NaN marks days with no
        // passing session; the fill breaks there, so each contiguous run of valid points
        // becomes its own shaded lobe. Every point in the run is included so the top edge
        // follows the real daily-max profile rather than a straight line run-start→run-end.
        var fillGeometry = new StreamGeometry();
        using (var fillGc = fillGeometry.Open())
        {
            var runPoints = new List<Point>();
            for (var i = 0; i < visible.Count; i++)
            {
                var value = visible[i].DailyMaxWpm;
                if (double.IsNaN(value))
                {
                    CloseFillRun(fillGc, chartRect, runPoints);
                    runPoints.Clear();
                    continue;
                }

                var x = chartRect.Left + (double)i / Math.Max(1, visible.Count - 1) * chartRect.Width;
                var normalized = Math.Clamp(value / range, 0, 1);
                var y = chartRect.Bottom - normalized * chartRect.Height;
                runPoints.Add(new Point(x, y));
            }

            CloseFillRun(fillGc, chartRect, runPoints);
        }

        context.DrawGeometry(new SolidColorBrush(DailyMaxFillColor), null, fillGeometry);
    }

    private static void CloseFillRun(StreamGeometryContext gc, Rect chartRect, List<Point> runPoints)
    {
        if (runPoints.Count == 0)
        {
            return;
        }

        // Fill down to the baseline so each gap-free run reads as its own shaded area:
        // baseline → first point → …every daily point… → last point → baseline.
        gc.BeginFigure(new Point(runPoints[0].X, chartRect.Bottom), true);
        foreach (var p in runPoints)
        {
            gc.LineTo(p);
        }

        gc.LineTo(new Point(runPoints[^1].X, chartRect.Bottom));
        gc.EndFigure(true);
    }

    private void DrawPercentSeries(DrawingContext context, Rect chartRect, IReadOnlyList<PracticeTrendPoint> visible)
    {
        // DrawLineSeries clamps to the range, so a session worse than PercentMax rides
        // along the top of the plot rather than disappearing.
        if (ShowErrorSeries)
        {
            DrawLineSeries(context, chartRect, visible, p => p.ErrorRatePercent, 0, PercentMax, ErrorColor);
        }

        if (ShowLimitSeries)
        {
            DrawLineSeries(context, chartRect, visible, p => p.ErrorThresholdPercent, 0, PercentMax, LimitColor, isDashed: true);
        }
    }

    private void DrawNoiseBand(DrawingContext context, Rect noiseRect, IReadOnlyList<PracticeTrendPoint> visible)
    {
        var panelBrush = new SolidColorBrush(Color.Parse("#111827"));
        context.FillRectangle(panelBrush, noiseRect);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#374151"))), noiseRect);

        if (!ShowNoiseSeries || visible.Count == 0)
        {
            DrawNoiseBandLabels(context, noiseRect, snrMin: null, snrMax: null);
            return;
        }

        // Scaled over the whole data set, not the visible slice, so the band keeps one
        // fixed scale while zooming and panning: the labelled extremes stay put and a
        // session's height stays comparable with every other session's.
        var (min, max) = GetSnrRange();
        var range = Math.Max(0.001, max - min);

        var fillGeometry = new StreamGeometry();
        using (var gc = fillGeometry.Open())
        {
            var firstX = noiseRect.Left;
            gc.BeginFigure(new Point(firstX, noiseRect.Bottom), true);

            for (var i = 0; i < visible.Count; i++)
            {
                var snr = -visible[i].NoiseLevelDb;
                var x = noiseRect.Left + (double)i / Math.Max(1, visible.Count - 1) * noiseRect.Width;
                var normalized = (snr - min) / range;
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

        DrawNoiseBandLabels(context, noiseRect, snrMin: min, snrMax: max);
    }

    /// <summary>
    /// Axis titles and value scales for the shared noise band, matching the "WPM"/"Percent"
    /// gutters of the main plot: the SNR scale sits in the left gutter, the QSB scale in the
    /// right one. The SNR scale is derived from the whole data set
    /// (<paramref name="snrMin"/> to <paramref name="snrMax"/>), so it is omitted while the
    /// series is off; the QSB scale is the fixed 0 to <see cref="QsbDepthScaleDb"/> dB range
    /// the line is drawn against. Neither depends on the zoom window.
    /// </summary>
    private void DrawNoiseBandLabels(DrawingContext context, Rect noiseRect, double? snrMin, double? snrMax)
    {
        // Both series inset their top by 6 px (see DrawNoiseBand / DrawQsbSeries), so the
        // scale extremes have to line up with that same plot area, not the band border.
        var plotTop = noiseRect.Top + 6;

        var snrTitle = CreateText("SNR (dB)", 9.5, snrMin.HasValue ? "#BBF7D0" : "#6B7280");
        DrawGutterTitle(context, snrTitle, noiseRect, isLeftGutter: true);

        if (snrMin.HasValue && snrMax.HasValue)
        {
            // A single SNR value across every session collapses the scale: the area is
            // drawn along the bottom, so that value is labelled there and the top is left
            // blank rather than repeating the same number twice.
            var isFlat = Math.Abs(snrMax.Value - snrMin.Value) < 0.05;
            if (!isFlat)
            {
                DrawScaleValue(context, snrMax.Value, plotTop, noiseRect, "#BBF7D0", isLeftGutter: true, isTop: true);
            }

            DrawScaleValue(context, snrMin.Value, noiseRect.Bottom, noiseRect, "#BBF7D0", isLeftGutter: true, isTop: false);
        }

        if (!ShowQsbSeries)
        {
            return;
        }

        var qsbTitle = CreateText("QSB (dB)", 9.5, "#FCA5A5");
        DrawGutterTitle(context, qsbTitle, noiseRect, isLeftGutter: false);
        DrawScaleValue(context, QsbDepthScaleDb, plotTop, noiseRect, "#FCA5A5", isLeftGutter: false, isTop: true);
        DrawScaleValue(context, 0, noiseRect.Bottom, noiseRect, "#FCA5A5", isLeftGutter: false, isTop: false);
    }

    private static void DrawGutterTitle(DrawingContext context, FormattedText title, Rect noiseRect, bool isLeftGutter)
    {
        var x = isLeftGutter
            ? Math.Max(0, noiseRect.Left - title.Width - 8)
            : noiseRect.Right + 8;

        context.DrawText(title, new Point(x, noiseRect.Top + (noiseRect.Height - title.Height) / 2));
    }

    /// <summary>
    /// Draws one end of a noise-band scale in the gutter. The label is pinned inside the band
    /// (top edge for the maximum, bottom edge for the minimum) rather than centered on the
    /// tick, so it never spills over the band border into the time axis or the plot above.
    /// </summary>
    private static void DrawScaleValue(
        DrawingContext context,
        double value,
        double tickY,
        Rect noiseRect,
        string colorHex,
        bool isLeftGutter,
        bool isTop)
    {
        var text = CreateText(value.ToString("0.#", CultureInfo.InvariantCulture), 9, colorHex);

        var x = isLeftGutter
            ? Math.Max(0, noiseRect.Left - text.Width - 8)
            : noiseRect.Right + 8;

        var y = isTop
            ? Math.Max(noiseRect.Top, tickY - text.Height)
            : Math.Min(noiseRect.Bottom - text.Height, tickY - text.Height);

        context.DrawText(text, new Point(x, y));
    }

    /// <summary>
    /// Draws the QSB fade depth as a solid red line inside the noise band, on top of the SNR
    /// area. The scale is fixed at 0 to <see cref="QsbDepthScaleDb"/> dB — the range the
    /// settings dialog allows — so the height means the same thing at any zoom level and
    /// stays comparable between sessions. NaN depth (fading off for that session) breaks the
    /// line, so a run of faded sessions reads as its own segment.
    /// </summary>
    private void DrawQsbSeries(DrawingContext context, Rect noiseRect, IReadOnlyList<PracticeTrendPoint> visible)
    {
        if (!ShowQsbSeries || visible.Count == 0)
        {
            return;
        }

        var pen = new Pen(new SolidColorBrush(QsbColor), 1.8);
        var runPoints = new List<Point>();

        for (var i = 0; i < visible.Count; i++)
        {
            var depth = visible[i].QsbDepthDb;
            if (double.IsNaN(depth))
            {
                DrawQsbRun(context, pen, runPoints);
                runPoints.Clear();
                continue;
            }

            var x = noiseRect.Left + (double)i / Math.Max(1, visible.Count - 1) * noiseRect.Width;
            var normalized = Math.Clamp(depth / QsbDepthScaleDb, 0, 1);
            var y = noiseRect.Bottom - normalized * (noiseRect.Height - 6);
            runPoints.Add(new Point(x, y));
        }

        DrawQsbRun(context, pen, runPoints);
    }

    private static void DrawQsbRun(DrawingContext context, Pen pen, List<Point> runPoints)
    {
        if (runPoints.Count == 0)
        {
            return;
        }

        // A single faded session surrounded by clean ones is a one-point run, which a
        // polyline cannot show. Give it a short horizontal stub so it is still visible.
        if (runPoints.Count == 1)
        {
            var point = runPoints[0];
            context.DrawLine(pen, new Point(point.X - 2, point.Y), new Point(point.X + 2, point.Y));
            return;
        }

        var geometry = new StreamGeometry();
        using (var gc = geometry.Open())
        {
            gc.BeginFigure(runPoints[0], false);
            for (var i = 1; i < runPoints.Count; i++)
            {
                gc.LineTo(runPoints[i]);
            }

            gc.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
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

        if (ShowDailyMaxSeries)
        {
            lines.Add(double.IsNaN(point.DailyMaxWpm)
                ? "Daily max: no passing session"
                : $"Daily max: {point.DailyMaxWpm:0.##} WPM");
        }

        if (ShowErrorSeries)
        {
            lines.Add($"Error rate: {point.ErrorRatePercent:0.##}%");
        }

        if (ShowLimitSeries)
        {
            lines.Add($"Error threshold: {point.ErrorThresholdPercent:0.##}%");
        }

        if (ShowNoiseSeries)
        {
            lines.Add($"SNR: {-point.NoiseLevelDb:0.##} dB");
        }

        if (ShowQsbSeries)
        {
            lines.Add(point.QsbEnabled
                ? $"QSB: {point.QsbDepthDb:0.##} dB depth, {point.QsbPeriodSeconds:0.##} s period"
                : "QSB: off");
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

    /// <summary>
    /// SNR range across the whole data set. Like <see cref="GetSpeedMax"/> this reads
    /// <see cref="Items"/> rather than the visible slice, so zooming and panning never
    /// rescale the noise band under the user.
    /// </summary>
    private (double Min, double Max) GetSnrRange()
    {
        var points = Items;
        if (points is null || points.Count == 0)
        {
            return (0, 1);
        }

        // Stored noise_level_db is relative to the CW signal; the UI shows the
        // signal-to-noise ratio, which is its negation (SNR = -NoiseLevelDb).
        return (points.Min(x => -x.NoiseLevelDb), points.Max(x => -x.NoiseLevelDb));
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
