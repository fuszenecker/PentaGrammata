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

    private bool _isHovering;
    private Point _hoverPoint;

    // Same validated dark-surface palette as the trends chart: the blue categorical slot for
    // the session marks, the amber one for the fitted line, so the annotation never reads as
    // a second data series.
    private static readonly Color SurfaceColor = Color.Parse("#0F111A");
    private static readonly Color MarkerColor = Color.Parse("#3987E5");
    private static readonly Color FitColor = Color.Parse("#C98500");

    public SpeedErrorCorrelation? Correlation
    {
        get => GetValue(CorrelationProperty);
        set => SetValue(CorrelationProperty, value);
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

        var speedScale = GetSpeedScale(points);
        var errorMax = GetErrorMax(points);

        DrawGrid(context, chartRect, speedScale, errorMax);
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

        if (change.Property == CorrelationProperty)
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
    /// percent as the floor so a clean window is not drawn on a hairline scale.
    /// </summary>
    private static double GetErrorMax(IReadOnlyList<SpeedErrorPoint> points)
    {
        var max = points.Max(p => p.ErrorRatePercent);
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

    private static void DrawMarkers(
        DrawingContext context,
        Rect chartRect,
        IReadOnlyList<SpeedErrorPoint> points,
        (double Min, double Max) speedScale,
        double errorMax)
    {
        var fill = new SolidColorBrush(Color.FromArgb(220, MarkerColor.R, MarkerColor.G, MarkerColor.B));
        var ring = new Pen(new SolidColorBrush(SurfaceColor), MarkerRingThickness);

        foreach (var point in points)
        {
            var center = Project(chartRect, point.AverageWpm, point.ErrorRatePercent, speedScale, errorMax);
            context.DrawEllipse(fill, ring, center, MarkerRadius, MarkerRadius);
        }
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
