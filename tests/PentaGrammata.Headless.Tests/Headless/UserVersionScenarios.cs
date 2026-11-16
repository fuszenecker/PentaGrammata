using Microsoft.Extensions.DependencyInjection;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Services;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;
using Promptuarium.Serializer;

namespace PentaGrammata.Tests.Headless;

// The settings file records the application's major version: a file of another major version
// (a breaking change) is set aside and the app starts from the defaults.
public sealed partial class UserJourneyScenarios
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(99)]
    public Task UserStartsANewMajorVersionWithDefaultsAndKeepsTheOldSettingsFile(int oldMajor) => Run(async app =>
    {
        var info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Settings reset", info.Title);
        var backup = ((InfoDialogViewModel)info.DataContext!).DetailMessage;
        Assert.IsTrue(File.Exists(backup), backup);
        StringAssert.StartsWith(Path.GetFileName(backup), $"pentagrammata-settings-v{oldMajor}-");
        Assert.AreEqual(31, PromptuariumSerializer.Deserialize<AppConfiguration>(File.ReadAllBytes(backup), ConfigurationFormat.Options).Practice.CharacterWpm,
            "The old settings are kept in the backup.");
        ScenarioDesktop.Click(info, "OK");

        Assert.AreEqual(18, app.Configuration.Current.Practice.CharacterWpm, "The bundled defaults are in use.");
        Assert.AreEqual(PracticeTopic.Groups, app.Configuration.Current.Practice.Topic);
        Assert.AreEqual("Default", app.CharacterSetBox.SelectedItem);
        var settingsPath = Path.Combine(app.DirectoryPath, "settings.prmt");
        Assert.IsFalse(File.Exists(settingsPath));

        // The next save writes a settings file of the current major version.
        ScenarioDesktop.Controls<Avalonia.Controls.NumericUpDown>(app.Main).Single().Value = 3;
        await app.Configuration.FlushAsync();
        Assert.AreEqual(ConfigurationFormat.CurrentMajorVersion, ConfigurationFormat.ReadMajorVersion(File.ReadAllBytes(settingsPath)));
        var reloaded = app.Services.GetRequiredService<IConfigurationStore>();
        Assert.AreEqual(3, reloaded.Load().Practice.DefaultDurationMins);
        Assert.IsNull(reloaded.LastSetAsideBackupPath);
    }, c =>
    {
        c.AppMajorVersion = oldMajor;
        c.Practice.CharacterWpm = 31;
        c.Practice.AverageWpm = 15;
    });

    [TestMethod]
    public Task UserOfTheSameMajorVersionKeepsTheirSettingsWithoutANotice() => Run(async app =>
    {
        await Task.Delay(200);
        Assert.IsFalse(app.Main.OwnedWindows.OfType<InfoDialog>().Any(w => w.IsVisible));
        Assert.AreEqual(20, app.Configuration.Current.Practice.CharacterWpm);
        Assert.IsFalse(Directory.Exists(Path.Combine(app.DirectoryPath, "backups")));
    });

    [TestMethod]
    public Task UserCannotImportABackupOfAnotherMajorVersion() => Run(async app =>
    {
        var archivePath = Path.Combine(app.DirectoryPath, "old-major.prmt");
        var settings = PromptuariumSerializer.Serialize(new Dictionary<string, object?>
        {
            ["AppMajorVersion"] = 1.0,
            ["Practice"] = new Dictionary<string, object?> { ["CharacterWpm"] = 44.0 },
        }, ConfigurationFormat.Options);
        using (var archive = File.Create(archivePath))
        {
            BackupArchive.Write(archive, [("settings.prmt", settings)]);
        }

        app.OpenChoices.Enqueue(archivePath);
        app.Menu("_Import");
        // Refused before the confirmation: no ConfirmDialog, straight to the reason.
        var info = await app.Dialog<InfoDialog>();
        Assert.IsFalse(app.Main.OwnedWindows.OfType<ConfirmDialog>().Any(w => w.IsVisible));
        StringAssert.Contains(((InfoDialogViewModel)info.DataContext!).PrimaryMessage + ((InfoDialogViewModel)info.DataContext!).DetailMessage, "PentaGrammata 1.x");
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "refuse backup import");
        Assert.AreEqual(20, app.Configuration.Current.Practice.CharacterWpm);
        Assert.AreEqual("TEST", app.Configuration.Current.Practice.CustomText);
    });
}
