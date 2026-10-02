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

/// <summary>
/// Scatter plot of average speed (horizontal, WPM) against error rate (vertical, percent) for
/// the sessions inside the analysis window, with the least-squares line drawn through them.
/// One mark per session and one annotation line, so there is no series cycling: the dots carry
/// the data and the line carries the trend.
/// </summary>
public sealed class CorrelationScatterChart : Control
{
    public static readonly StyledProperty<SpeedErrorCorrelation?> CorrelationProperty =
        AvaloniaProperty.Register<CorrelationScatterChart, SpeedErrorCorrelation?>(nameof(Correlation));

    public static readonly StyledProperty<double> ErrorThresholdPercentProperty =
        AvaloniaProperty.Register<CorrelationScatterChart, double>(nameof(ErrorThresholdPercent));

    private const double LeftAxisWidth = 52;
    private const double RightPadding = 18;
    private const double TopPadding = 10;
    // Reserved band above the plot for the "Error %" axis title, which is drawn just above
    // chartRect.Top and would otherwise be clipped off the top edge.
    private const double AxisTitleHeight = 18;
    private const double TimeAxisHeight = 22;
    // Band below the value axis for the "Average speed (WPM)" title.
    private const double BottomTitleHeight = 18;
    private const double BottomPadding = 6;

    // Marker geometry: an 8 px dot (the accessible minimum) plus a 2 px surface-colored ring,
    // so two sessions landing on the same spot still read as two marks.
    private const double MarkerRadius = 4;
    private const double MarkerRingThickness = 2;

    // Hover picks the nearest session within this many pixels of the cursor.
    private const double HoverRadius = 14;

    private const int TickCount = 5;

    // How much of the marker color the oldest session in the window keeps: the dots are faded
    // towards the surface by age, so the newest one is at full strength and the oldest is this
    // fraction of the way there. Not zero, because a mark that reaches the background is a mark
    // that has disappeared.
    private const double OldestMarkerStrength = 0.3;

    private bool _isHovering;
    private Point _hoverPoint;

    // Same validated dark-surface palette as the trends chart: the blue categorical slot for
    // the session marks, the amber one for the fitted line, so the annotation never reads as
    // a second data series.
    private static readonly Color SurfaceColor = Color.Parse("#0F111A");
    private static readonly Color MarkerColor = Color.Parse("#3987E5");
    // Today's sessions, in a brighter and more saturated yellow than the fitted line's amber so
    // the two do not read as the same thing: the line is an annotation, these are data.
    private static readonly Color TodayMarkerColor = Color.Parse("#FFD24A");
    private static readonly Color FitColor = Color.Parse("#C98500");
    private static readonly Color ThresholdColor = Color.Parse("#E66767");

    public SpeedErrorCorrelation? Correlation
    {
        get => GetValue(CorrelationProperty);
        set => SetValue(CorrelationProperty, value);
    }

    /// <summary>
    /// The error threshold currently configured for practice, in percent, drawn as a horizontal
    /// red line. Only the current value is shown: the per-session thresholds a session was
    /// actually judged against belong to the trends chart, not here.
    /// </summary>
    public double ErrorThresholdPercent
    {
        get => GetValue(ErrorThresholdPercentProperty);
        set => SetValue(ErrorThresholdPercentProperty, value);
    }

    public CorrelationScatterChart()
    {
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Bounds is parent-relative, so its origin is where the control sits in the dialog grid,
        // not (0, 0) of the drawing space. Everything below is drawn in local coordinates, so the
        // surface has to be the control's own size or the fill lands offset from the diagram.
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(new SolidColorBrush(SurfaceColor), bounds);

        var correlation = Correlation;
        var points = correlation?.Points;
        if (points is null || points.Count == 0)
        {
            DrawEmptyMessage(context, bounds);
            return;
        }

        var chartRect = new Rect(
            LeftAxisWidth,
            TopPadding + AxisTitleHeight,
            Math.Max(1, bounds.Width - LeftAxisWidth - RightPadding),
            Math.Max(1, bounds.Height - TopPadding - AxisTitleHeight - TimeAxisHeight - BottomTitleHeight - BottomPadding));

        var threshold = ErrorThresholdPercent;
        var speedScale = GetSpeedScale(points);
        var errorMax = GetErrorMax(points, threshold);

        DrawGrid(context, chartRect, speedScale, errorMax);
        DrawThresholdLine(context, chartRect, threshold, errorMax);
        DrawFitLine(context, chartRect, correlation!, speedScale, errorMax);
        DrawMarkers(context, chartRect, points, speedScale, errorMax);

        if (_isHovering)
        {
            DrawHoverOverlay(context, chartRect, points, speedScale, errorMax);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        _hoverPoint = e.GetPosition(this);
        _isHovering = true;
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

        if (change.Property == CorrelationProperty || change.Property == ErrorThresholdPercentProperty)
        {
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Horizontal domain in WPM. Padded by a WPM on each side so marks never sit on the axis
    /// line, then the span is rounded up to a whole multiple of <see cref="TickCount"/> so
    /// every tick falls on an integer WPM: speed is set and read as whole words per minute, so
    /// a 17.8 WPM gridline would be a value the user can never have practised at. The multiple
    /// is at least <see cref="TickCount"/>, which also spreads a window where every session ran
    /// within a WPM or two of the others instead of stacking it in one column.
    /// </summary>
    private static (double Min, double Max) GetSpeedScale(IReadOnlyList<SpeedErrorPoint> points)
    {
        var min = Math.Max(0, Math.Floor(points.Min(p => p.AverageWpm)) - 1);
        var max = Math.Ceiling(points.Max(p => p.AverageWpm)) + 1;

        var steps = Math.Max(1, Math.Ceiling((max - min) / TickCount));
        return (min, min + steps * TickCount);
    }

    /// <summary>
    /// Vertical full scale in percent: always anchored at zero (an error rate is a magnitude,
    /// so the baseline has to be zero) and rounded up to the next multiple of five, with five
    /// percent as the floor so a clean window is not drawn on a hairline scale. The threshold
    /// is part of the domain, so a window where every session stayed well under it still shows
    /// the line instead of hiding it above the top edge.
    /// </summary>
    private static double GetErrorMax(IReadOnlyList<SpeedErrorPoint> points, double thresholdPercent)
    {
        var max = points.Max(p => p.ErrorRatePercent);
        if (double.IsFinite(thresholdPercent))
        {
            max = Math.Max(max, thresholdPercent);
        }

        return Math.Max(5, Math.Ceiling(max / 5.0) * 5.0);
    }

    private static void DrawGrid(DrawingContext context, Rect chartRect, (double Min, double Max) speedScale, double errorMax)
    {
        var axisPen = new Pen(new SolidColorBrush(Color.Parse("#6B7280")), 1);
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#2A2D3A")), 1);

        context.DrawLine(axisPen, new Point(chartRect.Left, chartRect.Top), new Point(chartRect.Left, chartRect.Bottom));
        context.DrawLine(axisPen, new Point(chartRect.Left, chartRect.Bottom), new Point(chartRect.Right, chartRect.Bottom));

        for (var i = 0; i <= TickCount; i++)
        {
            var ratio = (double)i / TickCount;

            var y = chartRect.Bottom - ratio * chartRect.Height;
            context.DrawLine(gridPen, new Point(chartRect.Left, y), new Point(chartRect.Right, y));
            var errorLabel = CreateText($"{ratio * errorMax:0.#}%", 10, "#CBD5E1");
            context.DrawText(errorLabel, new Point(Math.Max(0, chartRect.Left - errorLabel.Width - 8), y - errorLabel.Height / 2));

            var x = chartRect.Left + ratio * chartRect.Width;
            context.DrawLine(gridPen, new Point(x, chartRect.Top), new Point(x, chartRect.Bottom));
            context.DrawLine(axisPen, new Point(x, chartRect.Bottom), new Point(x, chartRect.Bottom + 4));
            var speedValue = speedScale.Min + ratio * (speedScale.Max - speedScale.Min);
            var speedLabel = CreateText(speedValue.ToString("0", CultureInfo.InvariantCulture), 9.5, "#CBD5E1");
            context.DrawText(speedLabel, new Point(x - speedLabel.Width / 2, chartRect.Bottom + 5));
        }

        var errorTitle = CreateText("Error %", 10.5, "#FCA5A5");
        context.DrawText(errorTitle, new Point(Math.Max(0, chartRect.Left - errorTitle.Width - 8), chartRect.Top - errorTitle.Height - 4));

        var speedTitle = CreateText("Average speed (WPM)", 10.5, "#93C5FD");
        context.DrawText(
            speedTitle,
            new Point(
                chartRect.Left + Math.Max(0, (chartRect.Width - speedTitle.Width) / 2),
                chartRect.Bottom + TimeAxisHeight));
    }

    /// <summary>
    /// Draws one dot per session, faded towards the surface with age: the newest session in the
    /// window is at full marker color and the oldest is dimmed to
    /// <see cref="OldestMarkerStrength"/> of it, so the direction of travel is readable without
    /// a legend. The fade runs on the timestamps rather than on list position, so a cluster of
    /// sessions practised in one evening reads as one age, and the points arrive oldest first so
    /// the brighter recent dots are the ones on top where they overlap.
    /// <para>
    /// Sessions recorded today are drawn in <see cref="TodayMarkerColor"/> at full strength
    /// instead: today is the run the user is actually in the middle of, so it is picked out by
    /// hue rather than left to the last step of a brightness ramp. The day boundary is the local
    /// calendar day, which is the one the user practises against.
    /// </para>
    /// </summary>
    private static void DrawMarkers(
        DrawingContext context,
        Rect chartRect,
        IReadOnlyList<SpeedErrorPoint> points,
        (double Min, double Max) speedScale,
        double errorMax)
    {
        var ring = new Pen(new SolidColorBrush(SurfaceColor), MarkerRingThickness);

        var oldest = points.Min(p => p.RecordedAt).UtcTicks;
        var span = (double)(points.Max(p => p.RecordedAt).UtcTicks - oldest);
        var today = DateTimeOffset.Now.LocalDateTime.Date;

        foreach (var point in points)
        {
            var isToday = point.RecordedAt.ToLocalTime().Date == today;

            // A single session, or several recorded at the same instant, have no age to show and
            // are all drawn as the newest.
            var age = span > 0 ? (point.RecordedAt.UtcTicks - oldest) / span : 1.0;

            var center = Project(chartRect, point.AverageWpm, point.ErrorRatePercent, speedScale, errorMax);
            context.DrawEllipse(
                isToday ? MarkerBrush(TodayMarkerColor, 1) : MarkerBrush(MarkerColor, age),
                ring,
                center,
                MarkerRadius,
                MarkerRadius);
        }
    }

    /// <summary>
    /// The marker fill for a session at <paramref name="recency"/> of the window's age range
    /// (0 oldest, 1 newest). Blends the color towards the opaque surface instead of lowering its
    /// alpha, so two overlapping dots do not add up to a third, brighter shade.
    /// </summary>
    private static IBrush MarkerBrush(Color color, double recency)
    {
        var strength = OldestMarkerStrength + (1 - OldestMarkerStrength) * Math.Clamp(recency, 0, 1);

        static byte Blend(byte from, byte to, double amount) =>
            (byte)Math.Round(from + (to - from) * amount);

        return new SolidColorBrush(
            Color.FromArgb(
                220,
                Blend(SurfaceColor.R, color.R, strength),
                Blend(SurfaceColor.G, color.G, strength),
                Blend(SurfaceColor.B, color.B, strength)));
    }

    /// <summary>
    /// Draws the configured error threshold as a horizontal red line across the plot, labelled
    /// with its value. Sessions above it are the ones that failed, so the line tells apart the
    /// dots without needing a per-session marker. Skipped for a negative or non-finite value,
    /// which cannot be a rate at all.
    /// </summary>
    private static void DrawThresholdLine(DrawingContext context, Rect chartRect, double thresholdPercent, double errorMax)
    {
        if (!double.IsFinite(thresholdPercent) || thresholdPercent < 0)
        {
            return;
        }

        var y = chartRect.Bottom - thresholdPercent / Math.Max(0.001, errorMax) * chartRect.Height;

        context.DrawLine(
            new Pen(new SolidColorBrush(ThresholdColor), 1.5),
            new Point(chartRect.Left, y),
            new Point(chartRect.Right, y));

        var label = CreateText(
            string.Format(CultureInfo.InvariantCulture, "threshold {0:0.##}%", thresholdPercent),
            10,
            "#F2A3A3");

        // Above the line normally, below it when the line sits too close to the top edge for the
        // label to fit, so the text is never clipped by ClipToBounds.
        var labelY = y - label.Height - 2;
        if (labelY < chartRect.Top)
        {
            labelY = y + 2;
        }

        context.DrawText(label, new Point(Math.Max(chartRect.Left + 2, chartRect.Right - label.Width - 2), labelY));
    }

    /// <summary>
    /// Draws the least-squares line across the whole speed domain, over a ribbon spanning one
    /// residual standard deviation either side of it: the band's constant thickness is how far
    /// a typical session sits from the line, so roughly two thirds of the dots land inside it.
    /// Both are drawn in data space under a clip, so a steep fit leaves through the top or
    /// bottom edge instead of being flattened along it.
    /// </summary>
    private static void DrawFitLine(
        DrawingContext context,
        Rect chartRect,
        SpeedErrorCorrelation correlation,
        (double Min, double Max) speedScale,
        double errorMax)
    {
        if (!correlation.HasFit)
        {
            return;
        }

        var x0 = speedScale.Min;
        var x1 = speedScale.Max;
        var y0 = correlation.Intercept + correlation.Slope * x0;
        var y1 = correlation.Intercept + correlation.Slope * x1;

        using (context.PushClip(chartRect))
        {
            var sigma = correlation.ResidualStandardDeviation;
            if (sigma > 0)
            {
                var band = new StreamGeometry();
                using (var gc = band.Open())
                {
                    gc.BeginFigure(Project(chartRect, x0, y0 + sigma, speedScale, errorMax), true);
                    gc.LineTo(Project(chartRect, x1, y1 + sigma, speedScale, errorMax));
                    gc.LineTo(Project(chartRect, x1, y1 - sigma, speedScale, errorMax));
                    gc.LineTo(Project(chartRect, x0, y0 - sigma, speedScale, errorMax));
                    gc.EndFigure(true);
                }

                // Kept to a faint wash: the band covers a large area, so at a heavier alpha it
                // reads as a filled region of its own rather than as context behind the dots.
                context.DrawGeometry(
                    new SolidColorBrush(Color.FromArgb(28, FitColor.R, FitColor.G, FitColor.B)),
                    null,
                    band);
            }

            context.DrawLine(
                new Pen(new SolidColorBrush(FitColor), 2),
                Project(chartRect, x0, y0, speedScale, errorMax),
                Project(chartRect, x1, y1, speedScale, errorMax));
        }
    }

    private void DrawHoverOverlay(
        DrawingContext context,
        Rect chartRect,
        IReadOnlyList<SpeedErrorPoint> points,
        (double Min, double Max) speedScale,
        double errorMax)
    {
        SpeedErrorPoint? nearest = null;
        var nearestCenter = default(Point);
        var nearestDistance = double.MaxValue;

        foreach (var point in points)
        {
            var center = Project(chartRect, point.AverageWpm, point.ErrorRatePercent, speedScale, errorMax);
            var distance = Math.Sqrt(
                (center.X - _hoverPoint.X) * (center.X - _hoverPoint.X) +
                (center.Y - _hoverPoint.Y) * (center.Y - _hoverPoint.Y));

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = point;
                nearestCenter = center;
            }
        }

        if (nearest is null || nearestDistance > HoverRadius)
        {
            return;
        }

        context.DrawEllipse(
            null,
            new Pen(new SolidColorBrush(Color.Parse("#F9FAFB")), 1.5),
            nearestCenter,
            MarkerRadius + 3,
            MarkerRadius + 3);

        var texts = new[]
        {
            nearest.RecordedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            $"Average speed: {nearest.AverageWpm:0.##} WPM",
            $"Error rate: {nearest.ErrorRatePercent:0.##}%",
        }.Select(line => CreateText(line, 11, "#F9FAFB")).ToArray();

        var width = texts.Max(t => t.Width) + 14;
        var height = texts.Sum(t => t.Height) + 12;

        var tooltipX = nearestCenter.X + 12;
        if (tooltipX + width > chartRect.Right)
        {
            tooltipX = nearestCenter.X - width - 12;
        }

        var tooltipY = Math.Clamp(nearestCenter.Y + 12, chartRect.Top + 2, Math.Max(chartRect.Top + 2, chartRect.Bottom - height - 2));

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
    /// Maps a (speed, error rate) pair to a pixel in the plot. Deliberately not clamped to the
    /// plot: both scales are derived to cover every session, so only the fit and its band can
    /// fall outside, and those are drawn under a clip that cuts them at the edge.
    /// </summary>
    private static Point Project(
        Rect chartRect,
        double averageWpm,
        double errorRatePercent,
        (double Min, double Max) speedScale,
        double errorMax)
    {
        var speedRange = Math.Max(0.001, speedScale.Max - speedScale.Min);
        var x = chartRect.Left + (averageWpm - speedScale.Min) / speedRange * chartRect.Width;
        var y = chartRect.Bottom - errorRatePercent / Math.Max(0.001, errorMax) * chartRect.Height;
        return new Point(x, y);
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

    private static void DrawEmptyMessage(DrawingContext context, Rect bounds)
    {
        var text = CreateText("No sessions in the analysis window.", 13, "#D1D5DB");
        context.DrawText(
            text,
            new Point(
                Math.Max(8, (bounds.Width - text.Width) / 2),
                Math.Max(8, (bounds.Height - text.Height) / 2)));
    }
}
