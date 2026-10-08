using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PentaGrammata.Models;
using PentaGrammata.Views.Controls;

namespace PentaGrammata.Tests.Headless;

[TestClass, DoNotParallelize]
public sealed class ChartRenderingScenarios
{
    private static byte[] Pixels(Bitmap bitmap)
    {
        var stride = bitmap.PixelSize.Width * 4;
        var length = stride * bitmap.PixelSize.Height;
        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), buffer, length, stride);
            var bytes = new byte[length];
            Marshal.Copy(buffer, bytes, 0, length);
            Assert.IsTrue(bitmap.Format == PixelFormat.Bgra8888 || bitmap.Format == PixelFormat.Rgba8888,
                $"Expected a 32 bit color frame, received {bitmap.Format}.");
            if (bitmap.Format == PixelFormat.Rgba8888)
                for (var i = 0; i < bytes.Length; i += 4)
                    (bytes[i], bytes[i + 2]) = (bytes[i + 2], bytes[i]);
            return bytes;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static bool ContainsColor(byte[] pixels, byte r, byte g, byte b)
    {
        for (var i = 0; i < pixels.Length; i += 4)
            if (pixels[i] == b && pixels[i + 1] == g && pixels[i + 2] == r && pixels[i + 3] == 255) return true;
        return false;
    }

    [TestMethod]
    public Task CorrelationDotsUseOpaqueAgeColorsAndTodayIsFullYellow() => ScenarioRunner.Run(async app =>
    {
        app.Menu("C_orrelation");
        var dialog = await app.Dialog<PentaGrammata.Views.CorrelationDialog>();
        var chart = ScenarioDesktop.Controls<CorrelationScatterChart>(dialog).Single();
        var today = new DateTimeOffset(DateTime.Today.AddHours(12));
        chart.Correlation = new SpeedErrorCorrelation
        {
            Points =
            [
                new() { RecordedAt = today.AddDays(-2), AverageWpm = 10, ErrorRatePercent = 2 },
                new() { RecordedAt = today.AddDays(-1), AverageWpm = 20, ErrorRatePercent = 4 },
                new() { RecordedAt = today, AverageWpm = 30, ErrorRatePercent = 1 },
            ],
        };
        chart.InvalidateVisual();
        using var frame = dialog.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
        var pixels = Pixels(frame);
        Assert.IsTrue(ContainsColor(pixels, 255, 210, 74), "Today's dots must be full yellow (#FFD24A).");
        Assert.IsTrue(ContainsColor(pixels, 28, 52, 87), "Oldest dots must retain 30 percent of session blue blended into the surface.");
        ScenarioDesktop.Click(dialog, "Close");
    });

    [TestMethod]
    public Task OverlappingCorrelationDotsDoNotAccumulateBrightness() => ScenarioRunner.Run(async app =>
    {
        app.Menu("C_orrelation");
        var dialog = await app.Dialog<PentaGrammata.Views.CorrelationDialog>();
        var chart = ScenarioDesktop.Controls<CorrelationScatterChart>(dialog).Single();
        var today = new DateTimeOffset(DateTime.Today.AddHours(12));
        var oldest = new SpeedErrorPoint { RecordedAt = today.AddDays(-3), AverageWpm = 10, ErrorRatePercent = 2 };
        var newest = new SpeedErrorPoint { RecordedAt = today.AddDays(-1), AverageWpm = 20, ErrorRatePercent = 4 };
        chart.Correlation = new SpeedErrorCorrelation { Points = [oldest, newest] };
        chart.InvalidateVisual();
        using var single = dialog.CaptureRenderedFrame();
        Assert.IsNotNull(single);
        chart.Correlation = new SpeedErrorCorrelation { Points = [oldest, oldest, newest] };
        chart.InvalidateVisual();
        using var overlapping = dialog.CaptureRenderedFrame();
        Assert.IsNotNull(overlapping);
        // Only inspect dot interiors; antialiased edges can legitimately differ when redrawn.
        var before = Pixels(single);
        var after = Pixels(overlapping);
        var compared = 0;
        for (var i = 0; i < before.Length; i += 4)
        {
            if (before[i] != 87 || before[i + 1] != 52 || before[i + 2] != 28) continue;
            CollectionAssert.AreEqual(before[i..(i + 4)], after[i..(i + 4)], "Overlapping sessions must retain the same shade.");
            compared++;
        }
        Assert.IsGreaterThan(0, compared, "The rendered oldest dot must have an opaque interior.");
        ScenarioDesktop.Click(dialog, "Close");
    });
}
