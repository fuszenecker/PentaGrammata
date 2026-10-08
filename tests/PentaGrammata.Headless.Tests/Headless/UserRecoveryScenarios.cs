using System.IO.Compression;
using Avalonia.Controls;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Tests.Headless;

public sealed partial class UserJourneyScenarios
{
    [TestMethod]
    public Task UserImportsAnOlderBackupAndContinuesTheirHistory() => Run(async app =>
    {
        await ScoreAndSave(app, "TEST");
        var legacy = Path.Combine(app.DirectoryPath, "legacy.db");
        await using (var connection = new SqliteConnection($"Data Source={legacy};Pooling=False"))
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE practice_result_statistics (
                    id INTEGER PRIMARY KEY AUTOINCREMENT, recorded_at TEXT NOT NULL,
                    character_wpm INTEGER NOT NULL, average_wpm INTEGER NOT NULL,
                    character_count INTEGER NOT NULL, error_count INTEGER NOT NULL,
                    error_rate_percent REAL NOT NULL, noise_type TEXT NOT NULL,
                    noise_level_db REAL NOT NULL, noise_bandwidth_hz REAL NOT NULL,
                    agc_enabled INTEGER NOT NULL, agc_delay_seconds REAL NOT NULL,
                    apf_enabled INTEGER NOT NULL, apf_bandwidth_hz REAL NOT NULL,
                    apf_peak_gain_db REAL NOT NULL
                );
                INSERT INTO practice_result_statistics VALUES
                    (1, '2026-01-01T00:00:00.0000000+00:00', 20, 15, 10, 1, 10.0,
                     'None', -15.0, 500.0, 1, 0.4, 1, 120.0, -9.0);
                """;
            await command.ExecuteNonQueryAsync();
        }
        var zip = Path.Combine(app.DirectoryPath, "old-profile.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            archive.CreateEntryFromFile(legacy, "practice-results.db");
        app.OpenChoices.Enqueue(zip);
        app.Menu("_Import");
        var confirm = await app.Dialog<ConfirmDialog>();
        ScenarioDesktop.Click(confirm, "Replace");
        var info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Import complete", info.Title);
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "import older profile");
        app.Menu("_Trends");
        var trends = await app.Dialog<TrendsDialog>();
        var point = ((TrendsDialogViewModel)trends.DataContext!).Points.Single();
        Assert.AreEqual(15d, point.AverageWpm);
        Assert.AreEqual(10d, point.ErrorRatePercent);
        ScenarioDesktop.Click(trends, "Close");
        await ScoreAndSave(app, "TEST");
        Assert.HasCount(2, await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync());
    });

    [TestMethod]
    public Task UserRecoversTheirOriginalProfileWhenApplyingABackupFails() => Run(async app =>
    {
        await ScoreAndSave(app, "TEST");
        var zip = Path.Combine(app.DirectoryPath, "export.zip");
        app.SaveChoices.Enqueue(zip);
        app.Menu("_Export");
        await ScenarioDesktop.Until(() => !app.ViewModel.ExportCommand.IsRunning, "export original profile");
        var settings = await Settings(app);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, "Leave empty"), "NEW");
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "NEW");
        var sizes = Path.Combine(app.DirectoryPath, "window-sizes.json");
        var previousSizes = File.ReadAllBytes(sizes);
        File.Delete(sizes);
        Directory.CreateDirectory(sizes); // The disk refuses one replacement after staging.
        app.OpenChoices.Enqueue(zip);
        app.Menu("_Import");
        var confirm = await app.Dialog<ConfirmDialog>();
        ScenarioDesktop.Click(confirm, "Replace");
        var info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Import failed", info.Title);
        StringAssert.Contains(((InfoDialogViewModel)info.DataContext!).DetailMessage, "Recovery archive:");
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "roll back failed import");
        Directory.Delete(sizes);
        File.WriteAllBytes(sizes, previousSizes);
        Assert.AreEqual("NEW", app.Configuration.Current.Practice.CustomText);
        Assert.HasCount(2, await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync());
        Assert.IsNotEmpty(Directory.GetFiles(Path.Combine(app.DirectoryPath, "backups"), "*.zip"));
        await app.Practice("NEW");
        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        await app.CloseResult(result);
    });

    [TestMethod]
    public Task UserSeesTheLivePracticeClockAndPlaybackCompletion() => Run(async app =>
    {
        app.Audio.Begin(false);
        ScenarioDesktop.Click(app.Main, "Practice");
        await ScenarioDesktop.Until(() => app.Audio.Started, "start timed playback");
        ScenarioDesktop.Text(app.Main, app.Main.FindControl<TextBox>("ReceivedTextBox")!, "TEST");
        var initialClock = app.ViewModel.Practice.TimeCounterText;
        await ScenarioDesktop.Until(() => app.ViewModel.Practice.TimeCounterText != initialClock, "update the live practice clock");
        StringAssert.StartsWith(app.ViewModel.Practice.TimeCounterText, "Practicing:");
        app.Audio.Complete();
        await ScenarioDesktop.Until(() => !app.ViewModel.Practice.IsPracticeOperationActive, "finish timed playback");
        Assert.IsFalse(ScenarioDesktop.Button(app.Main, "Stop").IsEffectivelyEnabled);
        var result = await app.Result();
        await app.CloseResult(result);
    });

    [TestMethod]
    public Task UserRetriesSavingResultsAfterTheDatabaseBecomesWritable() => Run(async app =>
    {
        await app.Practice("TEST");
        var result = await app.Result();
        var database = Path.Combine(app.DirectoryPath, "practice-results.db");
        Directory.CreateDirectory(database); // The external disk is temporarily unavailable.
        ScenarioDesktop.Click(result, "Save results");
        var info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Save failed", info.Title);
        result.Close();
        Assert.IsTrue(result.IsVisible, "The result dialog must remain open while the save error is displayed.");
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !((PracticeResultWindowViewModel)result.DataContext!).IsSaving, "dismiss save failure");
        Assert.IsTrue(ScenarioDesktop.Button(result, "Save results").IsEffectivelyEnabled);
        Directory.Delete(database);
        await app.SaveResult(result);
        await app.CloseResult(result);
        Assert.HasCount(1, await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync());
    });

    [TestMethod]
    public Task UserSeesSettingsPersistenceFailureAndCanSaveAgain() => Run(async app =>
    {
        var settings = await Settings(app);
        var path = Path.Combine(app.DirectoryPath, "appsettings.json");
        var original = File.ReadAllText(path);
        File.Delete(path);
        Directory.CreateDirectory(path);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, "Leave empty"), "NEW");
        ScenarioDesktop.Click(settings, "Save");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenSettingsCommand.IsRunning, "handle settings persistence failure");
        StringAssert.Contains(app.ViewModel.Practice.TimeCounterText, "could not be saved");
        Directory.Delete(path);
        File.WriteAllText(path, original);
        settings = await Settings(app);
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "NEW");
        StringAssert.Contains(File.ReadAllText(path), "NEW");
    });

    [TestMethod]
    public Task UserRetriesCsvExportAfterAFileError() => Run(async app =>
    {
        await ScoreAndSave(app, "TEST");
        app.Menu("_Trends");
        var trends = await app.Dialog<TrendsDialog>();
        var vm = (TrendsDialogViewModel)trends.DataContext!;
        app.SaveChoices.Enqueue(app.DirectoryPath); // The chosen external target cannot be opened as a file.
        ScenarioDesktop.Click(trends, "Export CSV");
        await ScenarioDesktop.Until(() => !vm.ExportCsvCommand.IsRunning, "handle CSV export failure");
        StringAssert.StartsWith(vm.ExportStatusText, "CSV export failed:");
        app.SaveChoices.Enqueue(Path.Combine(app.DirectoryPath, "retry.csv"));
        ScenarioDesktop.Click(trends, "Export CSV");
        await ScenarioDesktop.Until(() => !vm.ExportCsvCommand.IsRunning, "retry CSV export");
        Assert.AreEqual("CSV exported.", vm.ExportStatusText);
        ScenarioDesktop.Click(trends, "Close");
    });

    [TestMethod]
    public Task UserKeepsTheirTypedCopyWhenRevealIsEnabled() => Run(async app =>
    {
        await app.Practice("MY COPY");
        Assert.AreEqual("MY COPY", app.Main.FindControl<TextBox>("ReceivedTextBox")!.Text);
    }, c => { c.UiPreferences.RevealSentTextAfterPractice = true; c.UiPreferences.RevealSentTextInLowercase = true; });

    [TestMethod]
    [DataRow(0d, "TEST")]
    [DataRow(25d, "TEXT")]
    public Task UserPassesAtTheirConfiguredErrorThreshold(double threshold, string copy) => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Maximum error rate").Value = (decimal)threshold;
        await ApplySettings(app, settings);
        await app.Practice(copy);
        var result = await app.Result();
        Assert.AreEqual(StatusLevel.Success, ((PracticeResultWindowViewModel)result.DataContext!).ResultStatus);
        await app.SaveResult(result);
        await app.CloseResult(result);
    });

    [TestMethod]
    public Task AdaptivePracticeRaisesCharacterSpeedWhenAverageCatchesUp() => Run(async app =>
    {
        await ScoreAndSave(app, "TEST");
        Assert.AreEqual(21, app.ViewModel.Practice.NextCharacterWpm);
        Assert.AreEqual(21, app.ViewModel.Practice.NextAverageWpm);
        await ScoreAndSave(app, "TEXT");
        Assert.AreEqual(20, app.ViewModel.Practice.NextAverageWpm);
        Assert.AreEqual(20, app.Configuration.Current.Practice.AverageWpm);
    }, c => { c.Practice.AutoAdjustWpm = true; c.Practice.CharacterWpm = 20; c.Practice.AverageWpm = 20; });

    [TestMethod]
    public Task UserEditsCharacterSetsAndTheMainSelectionRefreshes() => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, 3);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, "Format: name"), "My alphabet = KM<ar>\nMy digits = 0123");
        await ApplySettings(app, settings);
        var selection = ScenarioDesktop.Controls<ComboBox>(app.Main).Single();
        Assert.AreEqual("My alphabet", selection.SelectedItem);
        Assert.IsTrue(selection.Items.Contains("My digits"));
        selection.SelectedItem = "My digits";
        settings = await Settings(app);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, "Leave empty"), " ");
        await ApplySettings(app, settings);
        await app.Practice("@sent");
        var sent = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText;
        Assert.IsTrue(sent.Where(c => !char.IsWhiteSpace(c)).All(c => "0123".Contains(c)));
        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        await app.CloseResult(result);
    });

    [TestMethod]
    public Task UserClosesAndReopensResizedSettingsAndAnalysisDialogs() => Run(async app =>
    {
        var settings = await Settings(app);
        settings.Width = 800;
        settings.Height = 650;
        settings.Close();
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenSettingsCommand.IsRunning, "close settings from title bar");
        settings = await Settings(app);
        Assert.AreEqual(800d, settings.Width);
        Assert.AreEqual(650d, settings.Height);
        ScenarioDesktop.Click(settings, "Cancel");
        app.Menu("C_orrelation");
        var correlation = await app.Dialog<CorrelationDialog>();
        ScenarioDesktop.Controls<NumericUpDown>(correlation).Single().Value = 30;
        correlation.Close();
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenCorrelationCommand.IsRunning, "close correlation from title bar");
        app.Menu("C_orrelation");
        correlation = await app.Dialog<CorrelationDialog>();
        Assert.AreEqual(30m, ScenarioDesktop.Controls<NumericUpDown>(correlation).Single().Value);
        ScenarioDesktop.Click(correlation, "Close");
        app.Menu("_Confusions");
        var confusions = await app.Dialog<ConfusionsDialog>();
        ScenarioDesktop.Controls<NumericUpDown>(confusions).Single().Value = 40;
        confusions.Close();
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenConfusionsCommand.IsRunning, "close confusions from title bar");
        await app.Configuration.FlushAsync();
        Assert.AreEqual(40d, app.Configuration.Current.Analytics.ConfusionsHalfLifeDays);
    });

    [TestMethod]
    [DataRow(20, -60, 0)]
    [DataRow(20000, 0, 200)]
    public Task UserPracticesAtTheToneVolumeAndEnvelopeControlLimits(int frequency, int volume, int ramp) => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, 1);
        ScenarioDesktop.Tip<ComboBox>(settings, "Audio sample rate").SelectedItem = 48000;
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Pitch of the CW").Value = frequency;
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Signal level in dB").Value = volume;
        ScenarioDesktop.Tip<NumericUpDown>(settings, "Envelope ramp").Value = ramp;
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "TEST");
        Assert.AreEqual((double)frequency, app.Configuration.Current.Audio.Frequency);
        Assert.AreEqual((double)volume, app.Configuration.Current.Audio.VolumeDb);
        Assert.AreEqual(ramp, app.Configuration.Current.Audio.BeepRampMs);
    });

    [TestMethod]
    [DataRow("appsettings.json")]
    [DataRow("practice-results.db")]
    [DataRow("window-sizes.json")]
    public Task UserImportsAnArchiveContainingOnlyOneProfileComponent(string component) => Run(async app =>
    {
        await ScoreAndSave(app, "TEST");
        var full = Path.Combine(app.DirectoryPath, "full.zip");
        app.SaveChoices.Enqueue(full);
        app.Menu("_Export");
        await ScenarioDesktop.Until(() => !app.ViewModel.ExportCommand.IsRunning, "export source backup");
        var partial = Path.Combine(app.DirectoryPath, "partial.zip");
        using (var source = ZipFile.OpenRead(full))
        using (var target = ZipFile.Open(partial, ZipArchiveMode.Create))
        {
            using var input = source.GetEntry(component)!.Open();
            using var output = target.CreateEntry(component).Open();
            input.CopyTo(output);
        }
        var settings = await Settings(app);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, "Leave empty"), "NEW");
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "NEW");
        app.OpenChoices.Enqueue(partial);
        app.Menu("_Import");
        var confirm = await app.Dialog<ConfirmDialog>();
        ScenarioDesktop.Click(confirm, "Replace");
        var info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Import complete", info.Title);
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "import profile component");
        Assert.AreEqual(component == "appsettings.json" ? "TEST" : "NEW", app.Configuration.Current.Practice.CustomText);
        Assert.HasCount(component == "practice-results.db" ? 1 : 2,
            await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync());
    });

    [TestMethod]
    [DataRow("../appsettings.json", "{}")]
    [DataRow("unrelated.txt", "data")]
    [DataRow("appsettings.json", "not JSON")]
    [DataRow("practice-results.db", "not SQLite")]
    [DataRow("window-sizes.json", "not JSON")]
    public Task UserRejectsAnUnsafeOrCorruptBackupAndKeepsTheirData(string entry, string contents) => Run(async app =>
    {
        var zip = Path.Combine(app.DirectoryPath, "invalid.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry(entry).Open())) writer.Write(contents);
        app.OpenChoices.Enqueue(zip);
        app.Menu("_Import");
        // Invalid contents may be rejected either before confirmation or during staged validation.
        await ScenarioDesktop.Until(() => app.Main.OwnedWindows.Any(w => w.IsVisible), "inspect invalid backup");
        if (app.Main.OwnedWindows.OfType<ConfirmDialog>().LastOrDefault(w => w.IsVisible) is { } confirm)
            ScenarioDesktop.Click(confirm, "Replace");
        var info = await app.Dialog<InfoDialog>();
        Assert.IsTrue(info.Title == "Import" || info.Title == "Import failed");
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "reject invalid backup");
        await app.Practice("TEST");
        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        await app.CloseResult(result);
    });

    [TestMethod]
    public Task UserCannotImportWithoutASafetyBackupAndCanRetryAfterRepairingTheDisk() => Run(async app =>
    {
        var zip = Path.Combine(app.DirectoryPath, "export.zip");
        app.SaveChoices.Enqueue(zip);
        app.Menu("_Export");
        await ScenarioDesktop.Until(() => !app.ViewModel.ExportCommand.IsRunning, "export backup");
        var backups = Path.Combine(app.DirectoryPath, "backups");
        File.WriteAllText(backups, "external disk obstruction");
        app.OpenChoices.Enqueue(zip);
        app.Menu("_Import");
        var confirm = await app.Dialog<ConfirmDialog>();
        ScenarioDesktop.Click(confirm, "Replace");
        var info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Import failed", info.Title);
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "handle unavailable safety backup");
        Assert.AreEqual("TEST", app.Configuration.Current.Practice.CustomText);
        File.Delete(backups);
        app.OpenChoices.Enqueue(zip);
        app.Menu("_Import");
        confirm = await app.Dialog<ConfirmDialog>();
        ScenarioDesktop.Click(confirm, "Replace");
        info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Import complete", info.Title);
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "retry safe import");
    });
}
