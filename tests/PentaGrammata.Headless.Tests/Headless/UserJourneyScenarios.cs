using System.IO.Compression;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;
using PentaGrammata.Views.Controls;

namespace PentaGrammata.Tests.Headless;

[TestClass, DoNotParallelize]
public sealed partial class UserJourneyScenarios
{
    private static Task Run(Func<ScenarioDesktop, Task> scenario, Action<AppConfiguration>? arrangeProfile = null) =>
        ScenarioRunner.Run(scenario, arrangeProfile);

    private static async Task<MorseSettingsDialog> Settings(ScenarioDesktop app)
    {
        app.Menu("_Morse settings");
        return await app.Dialog<MorseSettingsDialog>();
    }

    private static async Task ApplySettings(ScenarioDesktop app, MorseSettingsDialog settings)
    {
        ScenarioDesktop.Click(settings, "Save");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenSettingsCommand.IsRunning, "apply settings from dialog");
        Assert.IsFalse(settings.IsVisible);
    }

    private static async Task ScoreAndSave(ScenarioDesktop app, string received)
    {
        await app.Practice(received);
        var result = await app.Result();
        await app.SaveResult(result);
        await app.CloseResult(result);
    }

    [TestMethod]
    [DataRow("TEST", "TEXT", "1")]
    [DataRow("TEST", "TES", "1")]
    [DataRow("TEST", "TESTX", "1")]
    [DataRow("CQ <ar>", "CQ <sk>", "1")]
    [DataRow("TEST", "test", "0")]
    public Task UserReviewsTheirCopyingMistakes(string sent, string received, string errors) => Run(async app =>
    {
        await app.Practice(received);
        var result = await app.Result();
        var vm = (PracticeResultWindowViewModel)result.DataContext!;
        Assert.AreEqual(errors, vm.ErrorsText);
        Assert.IsNotEmpty(vm.Rows);
        Assert.IsTrue(ScenarioDesktop.Controls<ItemsControl>(result).Any(i => ReferenceEquals(i.ItemsSource, vm.Rows)));
        await app.SaveResult(result);
        await app.CloseResult(result);
    }, c => c.Practice.CustomText = sent);

    [TestMethod]
    public Task UserSelectsACharacterSetDurationAndCopiesRandomFiveCharacterGroups() => Run(async app =>
    {
        ScenarioDesktop.Controls<ComboBox>(app.Main).Single().SelectedItem = "Digits";
        ScenarioDesktop.Controls<NumericUpDown>(app.Main).Single().Value = 2;
        await app.Practice("@sent");
        var sent = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText;
        Assert.IsTrue(sent.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(g => g.Length == 5 && g.All(char.IsDigit)));
        Assert.IsGreaterThan(5, sent.Length);
        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        await app.SaveResult(result);
        await app.CloseResult(result);
        Assert.AreEqual("Digits", app.Configuration.Current.Practice.DefaultCharacterSet);
        Assert.AreEqual(2, app.Configuration.Current.Practice.DefaultDurationMins);
    }, c => c.Practice.CustomText = "");

    [TestMethod]
    public Task UserStopsEarlyAndCanScoreThenStartAnotherSession() => Run(async app =>
    {
        await app.Practice("TEST", stop: true);
        Assert.AreEqual("Stopped.", app.ViewModel.Practice.TimeCounterText);
        var result = await app.Result();
        await app.CloseResult(result);
        await app.Practice("TEXT");
        result = await app.Result();
        Assert.AreEqual("1", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        Assert.IsTrue(ScenarioDesktop.Button(result, "Save results").IsEffectivelyEnabled);
        await app.CloseResult(result);
    });

    [TestMethod]
    public Task UserCannotChangePracticeConfigurationWhileAudioIsRunning() => Run(async app =>
    {
        app.Audio.Begin(false);
        ScenarioDesktop.Click(app.Main, "Practice");
        await ScenarioDesktop.Until(() => app.Audio.Started, "start playback");
        Assert.IsFalse(ScenarioDesktop.Button(app.Main, "Practice").IsEffectivelyEnabled);
        Assert.IsFalse(ScenarioDesktop.Button(app.Main, "Check result").IsEffectivelyEnabled);
        Assert.IsFalse(ScenarioDesktop.Controls<ComboBox>(app.Main).Single().IsEffectivelyEnabled);
        foreach (var caption in new[] { "_Morse settings", "_UI settings", "_Import", "_Restore", "_Confusions" })
            Assert.IsFalse(ScenarioDesktop.Controls<MenuItem>(app.Main).Single(m => Equals(m.Header, caption)).Command!.CanExecute(null));
        ScenarioDesktop.Text(app.Main, app.Main.FindControl<TextBox>("ReceivedTextBox")!, "TEST");
        ScenarioDesktop.Click(app.Main, "Stop");
        await ScenarioDesktop.Until(() => !app.ViewModel.Practice.IsPracticeOperationActive, "stop playback");
        Assert.IsTrue(ScenarioDesktop.Button(app.Main, "Practice").IsEffectivelyEnabled);
        Assert.IsTrue(ScenarioDesktop.Button(app.Main, "Check result").IsEffectivelyEnabled);
    });

    [TestMethod]
    public Task UserSeesAudioFailureAndCanRecoverWithAnotherPractice() => Run(async app =>
    {
        await app.Practice("TEST", failAudio: true);
        Assert.AreEqual(StatusLevel.Error, app.ViewModel.Practice.TimeCounterStatus);
        Assert.IsTrue(ScenarioDesktop.Button(app.Main, "Practice").IsEffectivelyEnabled);
        await app.Practice("TEST");
        Assert.AreEqual(StatusLevel.Success, app.ViewModel.Practice.TimeCounterStatus);
    });

    [TestMethod]
    [DataRow(true, false, "CQ <ar>")]
    [DataRow(true, true, "cq <ar>")]
    [DataRow(false, false, "")]
    public Task UserChoosesWhetherCompletedTextIsRevealed(bool reveal, bool lowercase, string expected) => Run(async app =>
    {
        app.Menu("_UI settings");
        var settings = await app.Dialog<UiSettingsDialog>();
        var checks = ScenarioDesktop.Controls<CheckBox>(settings).ToArray();
        checks.Single(c => Equals(c.Content, "Reveal sent text after practice")).IsChecked = reveal;
        var lower = checks.Single(c => Equals(c.Content, "Reveal sent text in lowercase"));
        Assert.AreEqual(reveal, lower.IsEffectivelyEnabled);
        if (reveal) lower.IsChecked = lowercase;
        ScenarioDesktop.Controls<NumericUpDown>(settings).Single().Value = 30;
        ScenarioDesktop.Click(settings, "Save");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenUiSettingsCommand.IsRunning, "save text display preferences");
        await app.Practice(null);
        Assert.AreEqual(expected, app.Main.FindControl<TextBox>("ReceivedTextBox")!.Text ?? "");
        Assert.AreEqual(30d, app.Main.FindControl<TextBox>("ReceivedTextBox")!.FontSize);
        var persisted = JsonSerializer.Deserialize<AppConfiguration>(await File.ReadAllTextAsync(Path.Combine(app.DirectoryPath, "appsettings.json")))!;
        Assert.AreEqual(reveal, persisted.UiPreferences.RevealSentTextAfterPractice);
    }, c => c.Practice.CustomText = "CQ <ar>");

    [TestMethod]
    public Task UserCancelsSettingsAndTheirPreviousPracticeAndDisplayRemain() => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Speed at which").Value = 35;
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, "Leave empty"), "CHANGED");
        ScenarioDesktop.Click(settings, "Cancel");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenSettingsCommand.IsRunning, "cancel Morse settings");
        app.Menu("_UI settings");
        var ui = await app.Dialog<UiSettingsDialog>();
        ScenarioDesktop.Controls<NumericUpDown>(ui).Single().Value = 42;
        ScenarioDesktop.Click(ui, "Cancel");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenUiSettingsCommand.IsRunning, "cancel UI settings");
        await app.Practice("TEST");
        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        Assert.AreEqual(20, app.ViewModel.Practice.NextCharacterWpm);
        Assert.AreNotEqual(42d, app.Main.FindControl<TextBox>("ReceivedTextBox")!.FontSize);
        await app.CloseResult(result);
    });

    [TestMethod]
    [DataRow("TEST ☃", false)]
    [DataRow("BAD LINE", true)]
    public Task UserCorrectsRejectedCustomTextOrCharacterSets(string invalid, bool characterSets) => Run(async app =>
    {
        var settings = await Settings(app);
        if (characterSets) ScenarioDesktop.Tab(settings, 3);
        var box = ScenarioDesktop.Tip<TextBox>(settings, characterSets ? "Format: name" : "Leave empty");
        ScenarioDesktop.Text(settings, box, invalid);
        ScenarioDesktop.Click(settings, "Save");
        Assert.IsTrue(settings.IsVisible);
        Assert.IsFalse(string.IsNullOrWhiteSpace(((MorseSettingsDialogViewModel)settings.DataContext!).ErrorMessage));
        ScenarioDesktop.Text(settings, box, characterSets ? "Scenario = AB<ar>" : "CQ\n  TEST <ar>");
        await ApplySettings(app, settings);
        await app.Practice(characterSets ? "TEST" : "CQ TEST <ar>");
        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        await app.CloseResult(result);
    });

    [TestMethod]
    public Task UserLocksAndUnlocksCharacterAndAverageSpeed() => Run(async app =>
    {
        var settings = await Settings(app);
        var character = ScenarioDesktop.Tip<NumericUpDown>(settings, "Speed at which");
        var average = ScenarioDesktop.Tip<NumericUpDown>(settings, "Overall speed");
        var speedLock = ScenarioDesktop.Controls<ToggleButton>(settings).Single(b => ToolTip.GetTip(b)?.ToString() == "Lock both WPM values together");
        ScenarioDesktop.Click(settings, speedLock);
        Assert.IsFalse(average.IsEffectivelyEnabled);
        character.Value = 25;
        Assert.AreEqual(25m, average.Value);
        ScenarioDesktop.Click(settings, speedLock);
        average.Value = 18;
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "TEST");
        var saved = (await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync()).Single();
        Assert.AreEqual(25, saved.CharacterWpm);
        Assert.AreEqual(18, saved.AverageWpm);
    });

    [TestMethod]
    [DataRow(NoiseType.None, 8000)]
    [DataRow(NoiseType.Gaussian, 11025)]
    [DataRow(NoiseType.Uniform, 16000)]
    [DataRow(NoiseType.Pink, 22050)]
    [DataRow(NoiseType.None, 32000)]
    [DataRow(NoiseType.None, 44100)]
    [DataRow(NoiseType.None, 48000)]
    public Task UserChangesAudioAndReceiverSettingsThenPractices(NoiseType noise, int rate) => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, 1);
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Pitch of the CW").Value = 700;
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Signal level in dB").Value = -18;
        ScenarioDesktop.Tip<ComboBox>(settings, "Audio sample rate").SelectedItem = rate;
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Envelope ramp").Value = 8;
        ScenarioDesktop.Tab(settings, 2);
        ScenarioDesktop.Tip<ComboBox>(settings, "Background noise added").SelectedItem = noise;
        var noisy = noise != NoiseType.None;
        Assert.AreEqual(noisy, ScenarioDesktop.Tip<NumericUpDown>(settings, "How much louder").IsEffectivelyEnabled);
        ScenarioDesktop.Controls<CheckBox>(settings).Single(c => Equals(c.Content, "QSB enabled")).IsChecked = true;
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Deepest fade").Value = 12;
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Seconds per fade").Value = 7;
        if (noisy)
        {
            ScenarioDesktop.Tip<NumericUpDown>(settings, "How much louder").Value = 30;
            ScenarioDesktop.Tip<NumericUpDown>(settings, "Width of the shared").Value = 400;
            ScenarioDesktop.Controls<CheckBox>(settings).Single(c => Equals(c.Content, "AGC enabled")).IsChecked = true;
            ScenarioDesktop.Controls<CheckBox>(settings).Single(c => Equals(c.Content, "APF enabled")).IsChecked = true;
            ScenarioDesktop.Tip<NumericUpDown>(settings, "AGC release").Value = 0.2m;
            ScenarioDesktop.Tip<NumericUpDown>(settings, "Maximum amount the AGC").Value = 12;
            ScenarioDesktop.Tip<NumericUpDown>(settings, "Width of the audio peak").Value = 100;
            ScenarioDesktop.Tip<NumericUpDown>(settings, "How much of the narrow").Value = -6;
        }
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "TEST");
        Assert.AreEqual(rate, app.Audio.SampleRate);
        Assert.IsTrue(app.Audio.Samples.Any(s => s != 0), "The production renderer must synthesize audio.");
        var saved = (await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync()).Single();
        Assert.AreEqual(noise, saved.NoiseType);
        Assert.IsTrue(saved.QsbEnabled);
        Assert.AreEqual(12d, saved.QsbDepthDb);
        Assert.AreEqual(7d, saved.QsbPeriodSeconds);
        if (noisy) { Assert.IsTrue(saved.AgcEnabled); Assert.IsTrue(saved.ApfEnabled); Assert.AreEqual(-30d, saved.NoiseLevelDb); }
    });

    [TestMethod]
    public Task UserPracticesWithAdaptiveSpeedAndFailedCopySlowsTheNextSession() => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Controls<CheckBox>(settings).Single(c => Equals(c.Content, "Auto-adjust WPM")).IsChecked = true;
        ScenarioDesktop.Tip<NumericUpDown>(settings, "When auto-adjust is on").Value = 3;
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "TEST");
        Assert.AreEqual(16, app.ViewModel.Practice.NextAverageWpm);
        await ScoreAndSave(app, "TEST");
        Assert.AreEqual(17, app.ViewModel.Practice.NextAverageWpm);
        await ScoreAndSave(app, "TEXT");
        Assert.AreEqual(16, app.ViewModel.Practice.NextAverageWpm);
        Assert.AreEqual(15, app.Configuration.Current.Practice.AverageWpm);
        var records = await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync();
        Assert.IsTrue(records.Select(r => r.AverageWpm).Order().SequenceEqual(new[] { 15, 16, 17 }));
        settings = await Settings(app);
        await ApplySettings(app, settings);
        Assert.AreEqual(15, app.ViewModel.Practice.NextAverageWpm);
    });

    [TestMethod]
    public Task UserOpensEmptyAnalysisAndAboutDialogs() => Run(async app =>
    {
        app.Menu("_Trends");
        var trends = await app.Dialog<TrendsDialog>();
        Assert.IsFalse(ScenarioDesktop.Button(trends, "Export CSV").IsEffectivelyEnabled);
        Assert.AreEqual("No saved results yet.", ((TrendsDialogViewModel)trends.DataContext!).SummaryText);
        ScenarioDesktop.Click(trends, "Close");
        app.Menu("C_orrelation");
        var correlation = await app.Dialog<CorrelationDialog>();
        Assert.IsFalse(((CorrelationDialogViewModel)correlation.DataContext!).Correlation.HasFit);
        ScenarioDesktop.Click(correlation, "Close");
        app.Menu("_Confusions");
        var confusions = await app.Dialog<ConfusionsDialog>();
        Assert.IsFalse(ScenarioDesktop.Button(confusions, "Practice confusions").IsEffectivelyEnabled);
        ScenarioDesktop.Click(confusions, "Close");
        app.Menu("_About");
        var about = await app.Dialog<AboutWindow>();
        Assert.IsTrue(ScenarioDesktop.Controls<TextBlock>(about).Any(t => t.Text?.StartsWith("Version ") == true));
        ScenarioDesktop.Click(about, "Close");
    });

    [TestMethod]
    public Task UserExploresSavedTrendsTogglesEverySeriesAndExportsCsv() => Run(async app =>
    {
        await ScoreAndSave(app, "TEST");
        await ScoreAndSave(app, "TEXT");
        app.Menu("_Trends");
        var trends = await app.Dialog<TrendsDialog>();
        var vm = (TrendsDialogViewModel)trends.DataContext!;
        Assert.HasCount(2, vm.Points);
        var chart = ScenarioDesktop.Controls<TrendsTimelineChart>(trends).Single();
        foreach (var check in ScenarioDesktop.Controls<CheckBox>(trends)) check.IsChecked = false;
        Assert.IsFalse(chart.ShowCharacterSeries || chart.ShowAverageSeries || chart.ShowDailyRangeSeries || chart.ShowErrorSeries || chart.ShowLimitSeries || chart.ShowNoiseSeries || chart.ShowQsbSeries);
        foreach (var check in ScenarioDesktop.Controls<CheckBox>(trends)) check.IsChecked = true;
        var center = chart.TranslatePoint(new Point(chart.Bounds.Width / 2, chart.Bounds.Height / 2), trends)!.Value;
        trends.MouseMove(center);
        trends.MouseWheel(center, new Vector(0, 1), RawInputModifiers.Control);
        trends.MouseDown(center, MouseButton.Left);
        trends.MouseMove(center + new Vector(20, 0));
        trends.MouseUp(center + new Vector(20, 0), MouseButton.Left);
        using var frame = trends.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
        ScenarioDesktop.Click(trends, "Export CSV"); // Native picker cancellation.
        await ScenarioDesktop.Until(() => !vm.ExportCsvCommand.IsRunning, "cancel CSV export");
        var csv = Path.Combine(app.DirectoryPath, "sessions.csv");
        app.SaveChoices.Enqueue(csv);
        ScenarioDesktop.Click(trends, "Export CSV");
        await ScenarioDesktop.Until(() => !vm.ExportCsvCommand.IsRunning, "export CSV");
        Assert.HasCount(3, File.ReadAllLines(csv));
        Assert.IsNotEmpty(vm.ExportStatusText);
        ScenarioDesktop.Click(trends, "Close");
    });

    [TestMethod]
    public Task UserChangesCorrelationWindowAndPracticesTheirConfusions() => Run(async app =>
    {
        await ScoreAndSave(app, "TEST");
        var settings = await Settings(app);
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Overall speed").Value = 16;
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "TEXT");
        app.Menu("C_orrelation");
        var correlation = await app.Dialog<CorrelationDialog>();
        var vm = (CorrelationDialogViewModel)correlation.DataContext!;
        Assert.IsTrue(vm.Correlation.HasFit);
        Assert.AreEqual(5d, vm.ErrorThresholdPercent);
        ScenarioDesktop.Controls<NumericUpDown>(correlation).Single().Value = 20;
        var scatter = ScenarioDesktop.Controls<CorrelationScatterChart>(correlation).Single();
        correlation.MouseMove(scatter.TranslatePoint(new Point(100, 80), correlation)!.Value);
        using var frame = correlation.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
        ScenarioDesktop.Click(correlation, "Close");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenCorrelationCommand.IsRunning, "save correlation window");
        Assert.AreEqual(20d, app.Configuration.Current.Analytics.CorrelationWindowDays);
        app.Menu("_Confusions");
        var confusions = await app.Dialog<ConfusionsDialog>();
        Assert.IsTrue(ScenarioDesktop.Button(confusions, "Practice confusions").IsEffectivelyEnabled);
        ScenarioDesktop.Controls<NumericUpDown>(confusions).Single().Value = 15;
        ScenarioDesktop.Click(confusions, "Practice confusions");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenConfusionsCommand.IsRunning, "select confusion practice set");
        Assert.AreEqual("Practice confusions", ScenarioDesktop.Controls<ComboBox>(app.Main).Single().SelectedItem);
        Assert.IsNotEmpty(app.Configuration.Current.CharacterSets["Practice confusions"]);
        settings = await Settings(app);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, "Leave empty"), " ");
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "@sent");
    });

    [TestMethod]
    [DataRow("new")]
    [DataRow("current")]
    [DataRow("offline")]
    public Task UserChecksForUpdatesAndReadsTheOutcome(string outcome) => Run(async app =>
    {
        app.Http.Fail = outcome == "offline";
        if (outcome == "current") app.Http.Version = "0.0.0.0";
        app.Menu("Check _updates");
        var info = await app.Dialog<InfoDialog>();
        Assert.AreEqual(outcome == "new" ? "Update available" : "Check for updates", info.Title);
        var message = ((InfoDialogViewModel)info.DataContext!).PrimaryMessage;
        StringAssert.Contains(message, outcome == "new" ? "new version" : outcome == "offline" ? "Could not reach" : "latest version");
        ScenarioDesktop.Click(info, "OK");
    });

    [TestMethod]
    public Task UserExportsImportsCancelsAndRestoresTheirWholeProfile() => Run(async app =>
    {
        await ScoreAndSave(app, "TEST");
        var zip = Path.Combine(app.DirectoryPath, "export.zip");
        app.SaveChoices.Enqueue(zip);
        app.Menu("_Export");
        await ScenarioDesktop.Until(() => !app.ViewModel.ExportCommand.IsRunning, "export backup");
        using (var archive = ZipFile.OpenRead(zip))
        {
            Assert.IsTrue(archive.Entries.Any(e => e.FullName == "appsettings.json"));
            Assert.IsTrue(archive.Entries.Any(e => e.FullName == "practice-results.db"));
            Assert.IsTrue(archive.Entries.Any(e => e.FullName == "window-sizes.json"));
        }
        var settings = await Settings(app);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, "Leave empty"), "NEW");
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "NEW");
        app.OpenChoices.Enqueue(zip);
        app.Menu("_Import");
        var confirm = await app.Dialog<ConfirmDialog>();
        ScenarioDesktop.Click(confirm, "Cancel");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "cancel backup import");
        Assert.AreEqual("NEW", app.Configuration.Current.Practice.CustomText);
        app.OpenChoices.Enqueue(zip);
        app.Menu("_Import");
        confirm = await app.Dialog<ConfirmDialog>();
        ScenarioDesktop.Click(confirm, "Replace");
        var info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Import complete", info.Title);
        Assert.IsTrue(File.Exists(((InfoDialogViewModel)info.DataContext!).DetailMessage));
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "finish backup import");
        Assert.AreEqual("TEST", app.Configuration.Current.Practice.CustomText);
        Assert.HasCount(1, await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync());
        app.Menu("_Restore");
        confirm = await app.Dialog<ConfirmDialog>();
        ScenarioDesktop.Click(confirm, "Restore");
        info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Restore complete", info.Title);
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.RestoreCommand.IsRunning, "restore previous profile");
        Assert.AreEqual("NEW", app.Configuration.Current.Practice.CustomText);
        Assert.HasCount(2, await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync());
    });

    [TestMethod]
    public Task UserCancelsNativePickersAndSeesInvalidBackupAndMissingRestoreErrors() => Run(async app =>
    {
        app.Menu("_Export");
        await ScenarioDesktop.Until(() => !app.ViewModel.ExportCommand.IsRunning, "cancel export picker");
        app.Menu("_Import");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "cancel import picker");
        var invalid = Path.Combine(app.DirectoryPath, "invalid.zip");
        File.WriteAllText(invalid, "not a ZIP");
        app.OpenChoices.Enqueue(invalid);
        app.Menu("_Import");
        var info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Import", info.Title);
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "dismiss invalid backup");
        app.Menu("_Restore");
        var confirm = await app.Dialog<ConfirmDialog>();
        ScenarioDesktop.Click(confirm, "Cancel");
        await ScenarioDesktop.Until(() => !app.ViewModel.RestoreCommand.IsRunning, "cancel restore");
        app.Menu("_Restore");
        confirm = await app.Dialog<ConfirmDialog>();
        ScenarioDesktop.Click(confirm, "Restore");
        info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Restore failed", info.Title);
        ScenarioDesktop.Click(info, "OK");
    });
}
