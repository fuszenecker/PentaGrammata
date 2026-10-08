using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;
using PentaGrammata.Views.Controls;

namespace PentaGrammata.Tests.Headless;

public sealed partial class UserJourneyScenarios
{
    [TestMethod]
    public Task UserOpensQuickSettingsAndTypesImmediatelyAfterStartingOrStopping() => Run(async app =>
    {
        ScenarioDesktop.Click(app.Main, ScenarioDesktop.Tip<Button>(app.Main, "Open the Morse settings"));
        var settings = await app.Dialog<MorseSettingsDialog>();
        ScenarioDesktop.Click(settings, "Cancel");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenSettingsCommand.IsRunning, "close quick settings");
        app.Audio.Begin(false);
        ScenarioDesktop.Click(app.Main, "Practice");
        await ScenarioDesktop.Until(() => app.Audio.Started, "start practice from the button");
        var input = app.Main.FindControl<TextBox>("ReceivedTextBox")!;
        Assert.IsTrue(input.IsFocused);
        app.Main.KeyTextInput("TE");
        Assert.AreEqual("TE", input.Text);
        ScenarioDesktop.Click(app.Main, "Stop");
        await ScenarioDesktop.Until(() => !app.ViewModel.Practice.IsPracticeOperationActive, "stop and return to the copy field");
        Assert.IsTrue(input.IsFocused);
        Assert.AreEqual(0, input.CaretIndex);
        input.CaretIndex = input.Text!.Length;
        app.Main.KeyTextInput("ST");
        Assert.AreEqual("TEST", input.Text);
        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        await app.CloseResult(result);
    });

    [TestMethod]
    public Task UserSendsEverySupportedSymbolAsCustomText() => Run(async app =>
    {
        using var defaults = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        var allSymbols = defaults.RootElement.GetProperty("CharacterSets").GetProperty("Full").GetString()!;
        var settings = await Settings(app);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, "Leave empty"), allSymbols);
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "@sent");
        Assert.AreEqual(allSymbols, app.Services.GetRequiredService<IPracticeController>().LastGeneratedText);
    });

    [TestMethod]
    public Task UserSendsCustomTextRegardlessOfDurationOrSelectedPalette() => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, "Leave empty"), "CQ\n   TEST <ar>");
        await ApplySettings(app, settings);
        ScenarioDesktop.Controls<ComboBox>(app.Main).Single().SelectedItem = "Digits";
        ScenarioDesktop.Controls<NumericUpDown>(app.Main).Single().Value = 999;
        await ScoreAndSave(app, "CQ TEST <ar>");
        Assert.AreEqual("CQ TEST <ar>", app.Services.GetRequiredService<IPracticeController>().LastGeneratedText);
        Assert.IsLessThan(app.Audio.SampleRate * 60, app.Audio.Samples.Length);
    });

    public static IEnumerable<object[]> BuiltInCharacterSets()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        foreach (var set in document.RootElement.GetProperty("CharacterSets").EnumerateObject())
            yield return [set.Name];
    }

    [TestMethod]
    [DynamicData(nameof(BuiltInCharacterSets))]
    public Task UserSelectsAndPracticesEachBuiltInAndKochCharacterSet(string name) => Run(async app =>
    {
        var selection = ScenarioDesktop.Controls<ComboBox>(app.Main).Single();
        selection.SelectedItem = name;
        await app.Practice("@sent");
        var result = await app.Result();
        var summary = (PracticeResultWindowViewModel)result.DataContext!;
        Assert.AreEqual("0", summary.ErrorsText);
        Assert.AreEqual(StatusLevel.Success, summary.ResultStatus);
        Assert.IsNotEmpty(summary.Rows);
        await app.SaveResult(result);
        await app.CloseResult(result);
        Assert.AreEqual(name, app.Configuration.Current.Practice.DefaultCharacterSet);
    }, c => c.Practice.CustomText = "");

    [TestMethod]
    public Task UserSelectsAFontAndReopensTheirDisplayPreferences() => Run(async app =>
    {
        app.Menu("_UI settings");
        var ui = await app.Dialog<UiSettingsDialog>();
        var fonts = ScenarioDesktop.Controls<ComboBox>(ui).Single();
        var font = fonts.Items.OfType<string>().First();
        fonts.SelectedItem = font;
        ScenarioDesktop.Controls<NumericUpDown>(ui).Single().Value = 26;
        ScenarioDesktop.Click(ui, "Save");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenUiSettingsCommand.IsRunning, "apply font selection");
        Assert.AreEqual(font, app.Main.FindControl<TextBox>("ReceivedTextBox")!.FontFamily.Name);
        Assert.AreEqual(26d, app.Main.FindControl<TextBox>("ReceivedTextBox")!.FontSize);
        app.Menu("_UI settings");
        ui = await app.Dialog<UiSettingsDialog>();
        Assert.AreEqual(font, ScenarioDesktop.Controls<ComboBox>(ui).Single().SelectedItem);
        ScenarioDesktop.Click(ui, "Cancel");
    });

    [TestMethod]
    public Task UserLowersTheReceiverGainToClearAnAudioDistortionWarning() => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, 1);
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Signal level in dB").Value = 0;
        ScenarioDesktop.Tab(settings, 2);
        ScenarioDesktop.Tip<ComboBox>(settings, "Background noise added").SelectedItem = NoiseType.Gaussian;
        ScenarioDesktop.Tip<NumericUpDown>(settings, "How much louder").Value = -20;
        ScenarioDesktop.Controls<CheckBox>(settings).Single(c => Equals(c.Content, "AGC enabled")).IsChecked = true;
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Maximum amount the AGC").Value = 40;
        var vm = (MorseSettingsDialogViewModel)settings.DataContext!;
        await ScenarioDesktop.Until(() => !string.IsNullOrEmpty(vm.DistortionWarning), "display the real audio headroom warning");
        Assert.IsTrue(ScenarioDesktop.Controls<TextBlock>(settings).Any(t => t.Text == vm.DistortionWarning && t.IsVisible));
        ScenarioDesktop.Tip<ComboBox>(settings, "Background noise added").SelectedItem = NoiseType.None;
        ScenarioDesktop.Tab(settings, 1);
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Signal level in dB").Value = -40;
        await ScenarioDesktop.Until(() => string.IsNullOrEmpty(vm.DistortionWarning), "clear the distortion warning");
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "TEST");
    });

    [TestMethod]
    [DataRow("single")]
    [DataRow("same speed")]
    [DataRow("same error")]
    public Task UserSeesSessionsWithoutAMisleadingCorrelationFit(string history) => Run(async app =>
    {
        await ScoreAndSave(app, "TEST");
        if (history != "single")
        {
            if (history == "same error")
            {
                var settings = await Settings(app);
                ScenarioDesktop.Tip<NumericUpDown>(settings, "Overall speed").Value = 16;
                await ApplySettings(app, settings);
            }
            await ScoreAndSave(app, history == "same speed" ? "TEXT" : "TEST");
        }
        app.Menu("C_orrelation");
        var dialog = await app.Dialog<CorrelationDialog>();
        var chart = ScenarioDesktop.Controls<CorrelationScatterChart>(dialog).Single();
        Assert.IsNotNull(chart.Correlation);
        Assert.IsFalse(chart.Correlation.HasFit);
        Assert.HasCount(history == "single" ? 1 : 2, chart.Correlation.Points);
        Assert.AreEqual(5d, chart.ErrorThresholdPercent);
        using var frame = dialog.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
        ScenarioDesktop.Click(dialog, "Close");
    });

    [TestMethod]
    public Task ReturningUserExploresHistoricalSessionsDailySpeedRangesAndAgeWindow() => Run(async app =>
    {
        // This profile represents a returning user's existing history. Persist real records
        // before opening the UI, then exercise the same dialogs and controls as that user.
        var statistics = app.Services.GetRequiredService<IPracticeResultStatisticsService>();
        foreach (var (age, speed, error) in new[] { (12, 10, 0d), (2, 14, 1d), (1, 15, 10d), (0, 18, 2d), (0, 20, 4d) })
        {
            await statistics.SaveAsync(new PracticeResultStatisticsRecord
            {
                SessionId = Guid.NewGuid(), RecordedAt = DateTimeOffset.Now.AddDays(-age),
                CharacterWpm = 25, AverageWpm = speed, CharacterCount = 100, ErrorCount = (int)error,
                ErrorRatePercent = error, ErrorThresholdPercent = 5,
                NoiseType = age == 2 ? NoiseType.Pink : NoiseType.None, NoiseLevelDb = -20,
                QsbEnabled = age == 2, QsbDepthDb = 10, QsbPeriodSeconds = 5,
            });
        }
        app.Menu("_Trends");
        var trends = await app.Dialog<TrendsDialog>();
        var points = ((TrendsDialogViewModel)trends.DataContext!).Points;
        Assert.HasCount(5, points);
        Assert.IsTrue(double.IsNaN(points[2].DailyMinWpm) && double.IsNaN(points[2].DailyMaxWpm));
        Assert.AreEqual(18d, points[3].DailyMinWpm);
        Assert.AreEqual(20d, points[3].DailyMaxWpm);
        Assert.AreEqual(10d, points[1].QsbDepthDb);
        Assert.IsTrue(double.IsNaN(points[0].QsbDepthDb));
        using (var frame = trends.CaptureRenderedFrame()) Assert.IsNotNull(frame);
        ScenarioDesktop.Click(trends, "Close");
        app.Menu("C_orrelation");
        var correlation = await app.Dialog<CorrelationDialog>();
        var vm = (CorrelationDialogViewModel)correlation.DataContext!;
        Assert.HasCount(4, vm.Correlation.Points);
        ScenarioDesktop.Controls<NumericUpDown>(correlation).Single().Value = 1;
        await ScenarioDesktop.Until(() => vm.Correlation.Points.Count == 2, "restrict correlation to the recent analysis window");
        using (var frame = correlation.CaptureRenderedFrame()) Assert.IsNotNull(frame);
        ScenarioDesktop.Controls<NumericUpDown>(correlation).Single().Value = 30;
        await ScenarioDesktop.Until(() => vm.Correlation.Points.Count == 5, "include older sessions");
        ScenarioDesktop.Click(correlation, "Close");
    });
}
