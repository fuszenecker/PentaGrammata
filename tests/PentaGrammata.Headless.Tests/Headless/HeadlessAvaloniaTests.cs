using Avalonia.Controls;
using Avalonia.Headless;
using PentaGrammata.Interfaces;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;
using Microsoft.Extensions.DependencyInjection;

namespace PentaGrammata.Tests.Headless;

[TestClass, DoNotParallelize]
public sealed class HeadlessAvaloniaTests
{
    private static Task Run(Func<ScenarioDesktop, Task> scenario) => ScenarioRunner.Run(scenario);

    [TestMethod]
    public Task UserCopiesCustomTextSavesAndReopensResults() => Run(async app =>
    {
        await app.Practice("TEST");
        var result = await app.Result();
        var summary = (PracticeResultWindowViewModel)result.DataContext!;
        Assert.AreEqual("0", summary.ErrorsText);
        Assert.AreEqual(StatusLevel.Success, summary.ResultStatus);
        await app.SaveResult(result);
        await app.CloseResult(result);
        result = await app.Result();
        Assert.IsFalse(ScenarioDesktop.Button(result, "Save results").IsEffectivelyEnabled);
        await app.CloseResult(result);
        var records = await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync();
        Assert.HasCount(1, records);
        Assert.AreEqual(4, records[0].CharacterCount);
        Assert.IsTrue(File.Exists(Path.Combine(app.DirectoryPath, "practice-results.db")));
    });
}
