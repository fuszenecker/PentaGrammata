using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Tests.Headless;

public sealed partial class UserJourneyScenarios
{
    [TestMethod]
    [DataRow("TEST", "TEST", DiffSegmentKind.Unchanged, "Gainsboro")]
    [DataRow("TEST", "TESTX", DiffSegmentKind.Inserted, "LimeGreen")]
    [DataRow("TEST", "TES", DiffSegmentKind.Deleted, "IndianRed")]
    [DataRow("TEST", "TEXT", DiffSegmentKind.Substituted, "Gold")]
    [DataRow(".", "E", DiffSegmentKind.Substituted, "Gold")]
    [DataRow("<ar>", "<sk>", DiffSegmentKind.Substituted, "Gold")]
    public Task UserSeesTheCorrectColorsOnEachKindOfCopyingDifference(string sent, string copy, DiffSegmentKind kind, string color) => Run(async app =>
    {
        await app.Practice(copy);
        var result = await app.Result();
        var vm = (PracticeResultWindowViewModel)result.DataContext!;
        var segments = vm.Rows.SelectMany(r => r.DifferenceSegments).Where(s => s.Kind == kind).ToArray();
        Assert.IsNotEmpty(segments);
        foreach (var segment in segments)
        {
            var text = ScenarioDesktop.Controls<TextBlock>(result).Single(t => ReferenceEquals(t.DataContext, segment));
            Assert.AreEqual(segment.Text, text.Text);
            Assert.IsInstanceOfType<ISolidColorBrush>(text.Foreground);
            Assert.AreEqual(Color.Parse(color), ((ISolidColorBrush)text.Foreground).Color);
        }
        foreach (var row in vm.Rows)
        {
            Assert.IsTrue(ScenarioDesktop.Controls<TextBlock>(result).Any(t => ReferenceEquals(t.DataContext, row) && t.Text == row.SentGroup));
            Assert.IsTrue(ScenarioDesktop.Controls<TextBlock>(result).Any(t => ReferenceEquals(t.DataContext, row) && t.Text == row.ReceivedGroup));
        }
        await app.CloseResult(result);
    }, c => c.Practice.CustomText = sent);

    [TestMethod]
    public Task UserCanKeepTheSaveNotificationAndSuppressItForLaterSessions() => Run(async app =>
    {
        await app.Practice("TEST");
        var result = await app.Result();
        await app.SaveResult(result, suppress: false);
        Assert.IsFalse(app.Configuration.IsDialogSuppressed("ResultsSaved"));
        await app.CloseResult(result);
        await app.Practice("TEST");
        result = await app.Result();
        ScenarioDesktop.Click(result, "Save results");
        var info = await app.Dialog<InfoDialog>();
        var vm = (InfoDialogViewModel)info.DataContext!;
        Assert.AreEqual("Database location", vm.DetailHeading);
        Assert.AreEqual(app.Services.GetRequiredService<IPracticeResultStatisticsService>().DatabasePath, vm.DetailMessage);
        Assert.IsTrue(ScenarioDesktop.Controls<SelectableTextBlock>(info).Any(t => t.Text == vm.DetailMessage));
        ScenarioDesktop.Click(info, "Do not show again");
        await ScenarioDesktop.Until(() => !((PracticeResultWindowViewModel)result.DataContext!).IsSaving, "suppress results notification");
        await app.CloseResult(result);
        Assert.IsTrue(app.Configuration.IsDialogSuppressed("ResultsSaved"));
        await ScoreAndSave(app, "TEST");
        Assert.HasCount(3, await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync());
    });

    [TestMethod]
    public Task UserCanScoreRepeatedlyWithoutAdvancingAdaptiveSpeedOrSavingDuplicates() => Run(async app =>
    {
        await app.Practice("TEST");
        var result = await app.Result();
        await app.CloseResult(result);
        Assert.AreEqual(16, app.ViewModel.Practice.NextAverageWpm);
        result = await app.Result();
        Assert.AreEqual(16, app.ViewModel.Practice.NextAverageWpm);
        await app.SaveResult(result);
        await app.CloseResult(result);
        result = await app.Result();
        Assert.AreEqual(16, app.ViewModel.Practice.NextAverageWpm);
        Assert.IsFalse(ScenarioDesktop.Button(result, "Save results").IsEffectivelyEnabled);
        await app.CloseResult(result);
        var records = await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync();
        Assert.HasCount(1, records);
        Assert.AreEqual(15, records.Single().AverageWpm);
    }, c => c.Practice.AutoAdjustWpm = true);

    [TestMethod]
    public Task UserSeesDisplaySettingsSaveFailureAndCanRetry() => Run(async app =>
    {
        app.Menu("_UI settings");
        var ui = await app.Dialog<UiSettingsDialog>();
        ScenarioDesktop.Controls<NumericUpDown>(ui).Single().Value = 32;
        var path = Path.Combine(app.DirectoryPath, "appsettings.json");
        var original = File.ReadAllText(path);
        File.Delete(path);
        Directory.CreateDirectory(path);
        try
        {
            ScenarioDesktop.Click(ui, "Save");
            var info = await app.Dialog<InfoDialog>();
            Assert.AreEqual("Settings not saved", info.Title);
            Assert.IsNotEmpty(((InfoDialogViewModel)info.DataContext!).PrimaryMessage);
            ScenarioDesktop.Click(info, "OK");
            await ScenarioDesktop.Until(() => !app.ViewModel.OpenUiSettingsCommand.IsRunning, "dismiss display settings save error");
            Assert.AreEqual(32d, app.Main.FindControl<TextBox>("ReceivedTextBox")!.FontSize);
        }
        finally
        {
            Directory.Delete(path);
            File.WriteAllText(path, original);
        }
        app.Menu("_UI settings");
        ui = await app.Dialog<UiSettingsDialog>();
        ScenarioDesktop.Click(ui, "Save");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenUiSettingsCommand.IsRunning, "retry saving display settings");
        StringAssert.Contains(File.ReadAllText(path), "32");
    });

    [TestMethod]
    public Task UserSeesBackupExportFailureAndCanRetry() => Run(async app =>
    {
        app.SaveChoices.Enqueue(app.DirectoryPath);
        app.Menu("_Export");
        var info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Export failed", info.Title);
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.ExportCommand.IsRunning, "dismiss backup export error");
        var zip = Path.Combine(app.DirectoryPath, "retry.zip");
        app.SaveChoices.Enqueue(zip);
        app.Menu("_Export");
        await ScenarioDesktop.Until(() => !app.ViewModel.ExportCommand.IsRunning, "retry backup export");
        Assert.IsTrue(File.Exists(zip));
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task UserSeesAnImportReadFailureAndKeepsTheirActiveProfile(bool disappearsAfterConfirmation) => Run(async app =>
    {
        var zip = Path.Combine(app.DirectoryPath, "source.zip");
        if (disappearsAfterConfirmation)
        {
            app.SaveChoices.Enqueue(zip);
            app.Menu("_Export");
            await ScenarioDesktop.Until(() => !app.ViewModel.ExportCommand.IsRunning, "prepare import source");
        }
        app.OpenChoices.Enqueue(zip);
        app.Menu("_Import");
        if (disappearsAfterConfirmation)
        {
            var confirm = await app.Dialog<ConfirmDialog>();
            File.Delete(zip); // The external file disappears while the user reviews the confirmation.
            ScenarioDesktop.Click(confirm, "Replace");
        }
        var info = await app.Dialog<InfoDialog>();
        Assert.AreEqual(disappearsAfterConfirmation ? "Import failed" : "Import", info.Title);
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "dismiss import read failure");
        Assert.IsFalse(app.ViewModel.IsBackupOperationRunning);
        Assert.IsTrue(ScenarioDesktop.Button(app.Main, "Practice").IsEffectivelyEnabled);
        Assert.AreEqual("TEST", app.Configuration.Current.Practice.CustomText);
        await ScoreAndSave(app, "TEST");
    });
}
