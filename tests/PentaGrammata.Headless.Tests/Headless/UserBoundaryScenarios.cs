using System.Text.Json;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Tests.Headless;

public sealed partial class UserJourneyScenarios
{
    public static IEnumerable<object[]> ReceiverCombinations()
    {
        foreach (var noise in Enum.GetValues<NoiseType>())
        foreach (var agc in new[] { false, true })
        foreach (var apf in new[] { false, true })
        foreach (var qsb in new[] { false, true })
            yield return [noise, agc, apf, qsb];
    }

    [TestMethod]
    [DynamicData(nameof(ReceiverCombinations))]
    public Task UserPracticesWithEachReceiverToggleCombination(NoiseType noise, bool agc, bool apf, bool qsb) => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, 2);
        var noiseChoice = ScenarioDesktop.Tip<ComboBox>(settings, "Background noise added");
        // Configure the receiver before turning noise off: values must survive disabled controls.
        noiseChoice.SelectedItem = NoiseType.Gaussian;
        var checks = ScenarioDesktop.Controls<CheckBox>(settings).ToArray();
        checks.Single(c => Equals(c.Content, "AGC enabled")).IsChecked = agc;
        checks.Single(c => Equals(c.Content, "APF enabled")).IsChecked = apf;
        checks.Single(c => Equals(c.Content, "QSB enabled")).IsChecked = qsb;
        noiseChoice.SelectedItem = noise;
        Assert.AreEqual(noise != NoiseType.None && agc,
            ScenarioDesktop.Tip<NumericUpDown>(settings, "AGC release").IsEffectivelyEnabled);
        Assert.AreEqual(noise != NoiseType.None && apf,
            ScenarioDesktop.Tip<NumericUpDown>(settings, "Width of the audio peak").IsEffectivelyEnabled);
        Assert.AreEqual(qsb, ScenarioDesktop.Tip<NumericUpDown>(settings, "Deepest fade").IsEffectivelyEnabled);
        Assert.AreEqual(qsb, ScenarioDesktop.Tip<NumericUpDown>(settings, "Seconds per fade").IsEffectivelyEnabled);
        await ApplySettings(app, settings);
        await ScoreAndSave(app, "E");
        Assert.IsTrue(app.Audio.Samples.Any(s => s != 0));
        var record = (await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync()).Single();
        Assert.AreEqual(noise, record.NoiseType);
        Assert.AreEqual(agc, record.AgcEnabled);
        Assert.AreEqual(apf, record.ApfEnabled);
        Assert.AreEqual(qsb, record.QsbEnabled);
        settings = await Settings(app);
        ScenarioDesktop.Tab(settings, 2);
        checks = ScenarioDesktop.Controls<CheckBox>(settings).ToArray();
        Assert.AreEqual(agc, checks.Single(c => Equals(c.Content, "AGC enabled")).IsChecked);
        Assert.AreEqual(apf, checks.Single(c => Equals(c.Content, "APF enabled")).IsChecked);
        Assert.AreEqual(qsb, checks.Single(c => Equals(c.Content, "QSB enabled")).IsChecked);
        ScenarioDesktop.Click(settings, "Cancel");
    }, c => c.Practice.CustomText = "E");

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task UserCanRetryClosingAnalysisAfterSettingsCannotBeSaved(bool confusions) => Run(async app =>
    {
        app.Menu(confusions ? "_Confusions" : "C_orrelation");
        Window dialog = confusions ? await app.Dialog<ConfusionsDialog>() : await app.Dialog<CorrelationDialog>();
        ScenarioDesktop.Controls<NumericUpDown>(dialog).Single().Value = 31;
        var path = Path.Combine(app.DirectoryPath, "appsettings.json");
        var original = File.ReadAllText(path);
        File.Delete(path);
        Directory.CreateDirectory(path);
        try
        {
            ScenarioDesktop.Click(dialog, "Close");
            await ScenarioDesktop.Until(() => confusions
                ? !((ConfusionsDialogViewModel)dialog.DataContext!).CloseCommand.IsRunning
                : !((CorrelationDialogViewModel)dialog.DataContext!).CloseCommand.IsRunning, "report analysis save failure");
            Assert.IsTrue(dialog.IsVisible);
            var message = confusions ? ((ConfusionsDialogViewModel)dialog.DataContext!).SummaryText
                : ((CorrelationDialogViewModel)dialog.DataContext!).SummaryText;
            StringAssert.StartsWith(message, "Could not save settings:");
            Assert.IsTrue(ScenarioDesktop.Controls<TextBlock>(dialog).Any(t => t.Text == message && t.IsVisible));
        }
        finally
        {
            Directory.Delete(path);
            File.WriteAllText(path, original);
        }
        ScenarioDesktop.Click(dialog, "Close");
        await ScenarioDesktop.Until(() => !dialog.IsVisible, "retry closing analysis");
        var persisted = JsonSerializer.Deserialize<AppConfiguration>(File.ReadAllText(path))!;
        Assert.AreEqual(31d, confusions ? persisted.Analytics.ConfusionsHalfLifeDays : persisted.Analytics.CorrelationWindowDays);
    });

    [TestMethod]
    public Task UserRetriesCreatingAConfusionPaletteAfterSettingsCannotBeSaved() => Run(async app =>
    {
        await ScoreAndSave(app, "TEXT");
        app.Menu("_Confusions");
        var dialog = await app.Dialog<ConfusionsDialog>();
        var vm = (ConfusionsDialogViewModel)dialog.DataContext!;
        var path = Path.Combine(app.DirectoryPath, "appsettings.json");
        var original = File.ReadAllText(path);
        File.Delete(path);
        Directory.CreateDirectory(path);
        try
        {
            ScenarioDesktop.Click(dialog, "Practice confusions");
            await ScenarioDesktop.Until(() => !vm.PracticeConfusionsCommand.IsRunning, "report palette save failure");
            Assert.IsTrue(dialog.IsVisible);
            StringAssert.StartsWith(vm.SummaryText, "Could not save settings:");
        }
        finally
        {
            Directory.Delete(path);
            File.WriteAllText(path, original);
        }
        ScenarioDesktop.Click(dialog, "Practice confusions");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenConfusionsCommand.IsRunning, "retry creating confusion palette");
        Assert.AreEqual("Practice confusions", ScenarioDesktop.Controls<ComboBox>(app.Main).Single().SelectedItem);
        var persisted = JsonSerializer.Deserialize<AppConfiguration>(File.ReadAllText(path))!;
        Assert.AreEqual("Practice confusions", persisted.Practice.DefaultCharacterSet);
        Assert.IsNotEmpty(persisted.CharacterSets["Practice confusions"]);
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task UserClosesEditedSettingsFromTheTitleBarWithoutApplyingThem(bool ui) => Run(async app =>
    {
        if (ui)
        {
            app.Menu("_UI settings");
            var dialog = await app.Dialog<UiSettingsDialog>();
            ScenarioDesktop.Controls<NumericUpDown>(dialog).Single().Value = 72;
            dialog.Close();
            await ScenarioDesktop.Until(() => !app.ViewModel.OpenUiSettingsCommand.IsRunning, "cancel display edits via title bar");
            Assert.AreNotEqual(72d, app.Main.FindControl<TextBox>("ReceivedTextBox")!.FontSize);
        }
        else
        {
            var dialog = await Settings(app);
            ScenarioDesktop.Text(dialog, ScenarioDesktop.Tip<TextBox>(dialog, "Leave empty"), "CHANGED");
            dialog.Close();
            await ScenarioDesktop.Until(() => !app.ViewModel.OpenSettingsCommand.IsRunning, "cancel Morse edits via title bar");
            Assert.AreEqual("TEST", app.Configuration.Current.Practice.CustomText);
        }
        await app.Practice("TEST");
        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        await app.CloseResult(result);
    });

    [TestMethod]
    [DataRow(6)]
    [DataRow(72)]
    public Task UserSavesTheSmallestAndLargestTextSize(int size) => Run(async app =>
    {
        app.Menu("_UI settings");
        var dialog = await app.Dialog<UiSettingsDialog>();
        ScenarioDesktop.Controls<NumericUpDown>(dialog).Single().Value = size;
        ScenarioDesktop.Click(dialog, "Save");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenUiSettingsCommand.IsRunning, "save font size boundary");
        Assert.AreEqual((double)size, app.Main.FindControl<TextBox>("ReceivedTextBox")!.FontSize);
        app.Menu("_UI settings");
        dialog = await app.Dialog<UiSettingsDialog>();
        Assert.AreEqual((decimal)size, ScenarioDesktop.Controls<NumericUpDown>(dialog).Single().Value);
        ScenarioDesktop.Click(dialog, "Cancel");
    });

    [TestMethod]
    [DataRow("")]
    [DataRow("# only a comment")]
    [DataRow("Broken=AB")]
    [DataRow(" = AB")]
    [DataRow("Broken = ")]
    public Task UserSeesCharacterSetParserErrorsAndCanRepairTheirEdits(string invalid) => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, 3);
        var editor = ScenarioDesktop.Tip<TextBox>(settings, "Format: name");
        ScenarioDesktop.Text(settings, editor, invalid);
        ScenarioDesktop.Click(settings, "Save");
        var vm = (MorseSettingsDialogViewModel)settings.DataContext!;
        Assert.IsTrue(settings.IsVisible);
        Assert.IsNotEmpty(vm.ErrorMessage);
        Assert.IsTrue(ScenarioDesktop.Controls<TextBlock>(settings).Any(t => t.Text == vm.ErrorMessage && t.IsVisible));
        Assert.IsFalse(app.Configuration.Current.CharacterSets.ContainsKey("Broken"));
        ScenarioDesktop.Text(settings, editor, "# my alphabet\n\nRepaired = KM<ar>\n");
        await ApplySettings(app, settings);
        Assert.AreEqual("Repaired", ScenarioDesktop.Controls<ComboBox>(app.Main).Single().SelectedItem);
        Assert.AreEqual("KM<ar>", app.Configuration.Current.CharacterSets["Repaired"]);
    });

    [TestMethod]
    [DataRow("TEST", " ", 4)]
    [DataRow("TEST CQ", "TEST", 2)]
    [DataRow("TEST", "TEST CQ", 2)]
    [DataRow("<ar>", "<sk>", 1)]
    public Task UserScoresEmptyMissingExtraGroupsAndProsigns(string sent, string copy, int errors) => Run(async app =>
    {
        await app.Practice(copy);
        var result = await app.Result();
        var vm = (PracticeResultWindowViewModel)result.DataContext!;
        Assert.AreEqual(errors.ToString(System.Globalization.CultureInfo.InvariantCulture), vm.ErrorsText);
        Assert.AreEqual(StatusLevel.Error, vm.ResultStatus);
        Assert.IsTrue(ScenarioDesktop.Controls<TextBlock>(result).Any(t => t.Text == vm.ErrorRateText));
        await app.SaveResult(result);
        await app.CloseResult(result);
        var saved = (await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync()).Single();
        Assert.AreEqual(errors, saved.ErrorCount);
    }, c => c.Practice.CustomText = sent);

    [TestMethod]
    public Task UserMustEnterACopyBeforeCheckingResults() => Run(async app =>
    {
        Assert.IsFalse(ScenarioDesktop.Button(app.Main, "Check result").IsEffectivelyEnabled);
        await app.Practice("");
        Assert.IsFalse(ScenarioDesktop.Button(app.Main, "Check result").IsEffectivelyEnabled);
        var input = app.Main.FindControl<TextBox>("ReceivedTextBox")!;
        ScenarioDesktop.Text(app.Main, input, "TEST");
        Assert.IsTrue(ScenarioDesktop.Button(app.Main, "Check result").IsEffectivelyEnabled);
        ScenarioDesktop.Text(app.Main, input, "");
        Assert.IsFalse(ScenarioDesktop.Button(app.Main, "Check result").IsEffectivelyEnabled);
        ScenarioDesktop.Text(app.Main, input, "TEST");
        var result = await app.Result();
        await app.CloseResult(result);
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task UserSavesAndReopensEveryReceiverNumericControlAtItsLimits(bool maximum) => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, 2);
        ScenarioDesktop.Tip<ComboBox>(settings, "Background noise added").SelectedItem = NoiseType.Pink;
        foreach (var caption in new[] { "QSB enabled", "AGC enabled", "APF enabled" })
            ScenarioDesktop.Controls<CheckBox>(settings).Single(c => Equals(c.Content, caption)).IsChecked = true;
        var prefixes = new[] { "Deepest fade", "Seconds per fade", "How much louder", "Width of the shared",
            "AGC release", "Maximum amount the AGC", "Width of the audio peak", "How much of the narrow" };
        var expected = new Dictionary<string, decimal>();
        foreach (var prefix in prefixes)
        {
            var control = ScenarioDesktop.Tip<NumericUpDown>(settings, prefix);
            Assert.IsTrue(control.IsEffectivelyEnabled);
            var value = maximum ? control.Maximum : control.Minimum;
            expected[prefix] = value;
            control.Value = value;
        }
        await ApplySettings(app, settings);
        settings = await Settings(app);
        ScenarioDesktop.Tab(settings, 2);
        foreach (var prefix in prefixes)
            Assert.AreEqual(expected[prefix], ScenarioDesktop.Tip<NumericUpDown>(settings, prefix).Value, prefix);
        ScenarioDesktop.Click(settings, "Cancel");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenSettingsCommand.IsRunning, "close receiver boundary settings");
        await app.Practice("E");
        Assert.IsNotEmpty(app.Audio.Samples);
        var result = await app.Result();
        await app.SaveResult(result);
        await app.CloseResult(result);
        var saved = (await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync()).Single();
        Assert.AreEqual((double)expected["Deepest fade"], saved.QsbDepthDb);
        Assert.AreEqual((double)expected["Seconds per fade"], saved.QsbPeriodSeconds);
        Assert.AreEqual(-(double)expected["How much louder"], saved.NoiseLevelDb);
        Assert.AreEqual((double)expected["Width of the shared"], saved.NoiseBandwidthHz);
        Assert.AreEqual((double)expected["AGC release"], saved.AgcDelaySeconds);
        Assert.AreEqual((double)expected["Maximum amount the AGC"], saved.AgcMaxGainDb);
        Assert.AreEqual((double)expected["Width of the audio peak"], saved.ApfBandwidthHz);
        Assert.AreEqual((double)expected["How much of the narrow"], saved.ApfPeakGainDb);
    }, c => c.Practice.CustomText = "E");
}
