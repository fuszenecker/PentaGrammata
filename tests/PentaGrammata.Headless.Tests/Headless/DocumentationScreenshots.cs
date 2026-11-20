using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Services;
using PentaGrammata.Views;
using Promptuarium.Serializer;

namespace PentaGrammata.Tests.Headless;

/// <summary>
/// Renders every window of a realistic profile, the pictures of the user guides in docs/.
/// Always runs, so it is also a smoke test that each window renders; the pictures are written
/// only when PENTAGRAMMATA_DOCS_IMAGES names a directory (see docs/README.md):
/// <c>PENTAGRAMMATA_DOCS_IMAGES=$PWD/docs/images dotnet test tests/PentaGrammata.Headless.Tests --filter FullyQualifiedName~DocumentationScreenshots</c>
/// </summary>
[TestClass, DoNotParallelize]
public sealed class DocumentationScreenshots
{
    private static readonly string? ImageDirectory = Environment.GetEnvironmentVariable("PENTAGRAMMATA_DOCS_IMAGES") is { Length: > 0 } dir ? dir : null;

    private static void Shot(Window window, string name)
    {
        window.UpdateLayout();
        using var frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame, $"{name} must render.");
        if (ImageDirectory is null) return;
        Directory.CreateDirectory(ImageDirectory);
        using var file = File.Create(Path.Combine(ImageDirectory, name + ".png"));
        frame.Save(file);
    }

    /// <summary>The bundled defaults (topic lists, Koch sets), as a fresh install has them.</summary>
    private static void RealisticProfile(AppConfiguration config)
    {
        var bundled = PromptuariumSerializer.Deserialize<AppConfiguration>(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "settings.prmt")), ConfigurationFormat.Options)!;
        config.CharacterSets = bundled.CharacterSets;
        config.Topics = bundled.Topics;
        config.Practice = bundled.Practice;
        config.Practice.Topic = PracticeTopic.Groups;
        config.Practice.DefaultCharacterSet = "Koch-LCWO-10-L";
        config.Practice.DefaultDurationMins = 1;
        config.Practice.CharacterWpm = 20;
        config.Practice.AverageWpm = 15;
        config.Practice.ErrorThreshold = 10;
        config.Practice.AutoAdjustWpm = true;
        config.Practice.AutoAdjustSnr = true;
        config.Practice.CustomText = "CQ CQ DE HA5XYZ K\nTNX FER CALL\nUR RST 599 599";
        config.Audio.Noise = new NoiseSettings { Type = NoiseType.Pink, LevelDb = -12, QsbEnabled = true, QsbDepthDb = 10 };
        config.UiPreferences.RevealSentTextAfterPractice = true;
    }

    /// <summary>Two weeks of saved sessions, so the analysis windows have something to show.</summary>
    private static async Task SeedHistory(ScenarioDesktop app)
    {
        var statistics = app.Services.GetRequiredService<IPracticeResultStatisticsService>();
        var random = new Random(7);
        var now = DateTimeOffset.Now;
        string[] confusable = ["E", "I", "S", "H", "5", "T", "M", "O", "A", "N", "U", "V"];
        for (var day = 13; day >= 0; day--)
        {
            for (var session = 0; session < 3; session++)
            {
                var speed = 8 + (13 - day) / 2 + session;
                var snr = 22 - (13 - day) + random.Next(0, 4);
                var depth = 4 + (13 - day) / 2.0;
                var error = Math.Max(0, 1 + (speed - 8) * 0.8 + (15 - snr) * 0.35 + random.NextDouble() * 3);
                var at = now.AddDays(-day).AddMinutes(-90 + session * 20);
                await statistics.SaveAsync(new PracticeResultStatisticsRecord
                {
                    SessionId = Guid.NewGuid(), RecordedAt = at,
                    CharacterWpm = Math.Max(20, speed), AverageWpm = speed, CharacterCount = 100,
                    ErrorCount = (int)Math.Round(error), ErrorRatePercent = error, ErrorThresholdPercent = 10,
                    NoiseType = NoiseType.Pink, NoiseLevelDb = -snr, NoiseBandwidthHz = 500,
                    QsbEnabled = true, QsbDepthDb = depth, QsbPeriodSeconds = 5,
                    Confusions = Enumerable.Range(0, 3).Select(_ =>
                    {
                        var i = random.Next(0, confusable.Length / 2) * 2;
                        return new ConfusionObservation { RecordedAt = at, ExpectedSymbol = confusable[i], ActualSymbol = confusable[i + 1], Distance = 1, Count = 1 };
                    }).ToList(),
                });
            }
        }
    }

    [TestMethod]
    public Task EveryWindowRendersForTheUserGuides() => ScenarioRunner.Run(async app =>
    {
        // The app forces the dark theme whatever the system uses; the pictures show exactly that.
        Assert.AreEqual(ThemeVariant.Dark, Application.Current!.ActualThemeVariant);
        await Journey(app);
    }, RealisticProfile);

    private static async Task Journey(ScenarioDesktop app)
    {
        app.Main.Width = 760;
        app.Main.Height = 480;
        Shot(app.Main, "main-window");

        // A session copied with two mistakes, then checked.
        await app.Practice(null);
        var sent = PracticeItems.ToPlain(app.Services.GetRequiredService<IPracticeController>().LastGeneratedText);
        var copied = sent.Length > 8 ? sent[..3] + "X" + sent[4..^2] : sent;
        ScenarioDesktop.Text(app.Main, app.Main.FindControl<TextBox>("ReceivedTextBox")!, copied);
        Shot(app.Main, "main-window-copied");
        var result = await app.Result();
        Shot(result, "result-window");
        await app.SaveResult(result);
        await app.CloseResult(result);
        Shot(app.Main, "main-window-after-save");

        app.Menu("_Morse settings");
        var settings = await app.Dialog<MorseSettingsDialog>();
        Shot(settings, "settings-practice");
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.EmeaCallsignsSubTab);
        Shot(settings, "settings-topics-callsigns");
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.CitiesSubTab);
        Shot(settings, "settings-topics-cities");
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.CustomTextSubTab);
        Shot(settings, "settings-topics-custom-text");
        ScenarioDesktop.Tab(settings, ScenarioDesktop.CharacterSetsTab);
        Shot(settings, "settings-character-sets");
        ScenarioDesktop.Tab(settings, ScenarioDesktop.AudioTab);
        Shot(settings, "settings-audio");
        ScenarioDesktop.Tab(settings, ScenarioDesktop.NoiseTab);
        Shot(settings, "settings-noise");
        ScenarioDesktop.Click(settings, "Cancel");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenSettingsCommand.IsRunning, "close settings");

        app.Menu("_UI settings");
        var ui = await app.Dialog<UiSettingsDialog>();
        Shot(ui, "ui-settings");
        ScenarioDesktop.Click(ui, "Cancel");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenUiSettingsCommand.IsRunning, "close UI settings");

        await SeedHistory(app);

        app.Menu("_Trends");
        var trends = await app.Dialog<TrendsDialog>();
        await ScenarioDesktop.Until(() => ((PentaGrammata.ViewModels.TrendsDialogViewModel)trends.DataContext!).Points.Count > 40, "load trends");
        Shot(trends, "trends");
        ScenarioDesktop.Click(trends, "Close");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenTrendsCommand.IsRunning, "close trends");

        app.Menu("C_orrelation");
        var correlation = await app.Dialog<CorrelationDialog>();
        var vm = (PentaGrammata.ViewModels.CorrelationDialogViewModel)correlation.DataContext!;
        ScenarioDesktop.Controls<NumericUpDown>(correlation).Single().Value = 14;
        await ScenarioDesktop.Until(() => vm.Speed.Correlation.Points.Count > 40 && vm.QsbDepth.Correlation.HasFit, "analyze two weeks");
        Shot(correlation, "correlation-speed");
        ScenarioDesktop.Tab(correlation, 1);
        Shot(correlation, "correlation-snr");
        ScenarioDesktop.Tab(correlation, 2);
        Shot(correlation, "correlation-qsb");
        ScenarioDesktop.Click(correlation, "Close");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenCorrelationCommand.IsRunning, "close correlation");

        app.Menu("_Confusions");
        var confusions = await app.Dialog<ConfusionsDialog>();
        await Task.Delay(300);
        Shot(confusions, "confusions");
        ScenarioDesktop.Click(confusions, "Close");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenConfusionsCommand.IsRunning, "close confusions");

        app.Menu("_About");
        var about = await app.Dialog<AboutWindow>();
        Shot(about, "about");
        ScenarioDesktop.Click(about, "Close");
    }
}
