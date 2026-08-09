using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PentaGrammata.Interfaces;
using PentaGrammata.Stores;

namespace PentaGrammata.Tests.Stores;

[TestClass]
public sealed class WindowSizeStoreTests
{
    private string _tempDirectory = string.Empty;

    [TestInitialize]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PentaGrammataWindowSizeTests", Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void TryGetSize_WhenNothingSaved_ReturnsNull()
    {
        var sut = CreateStore();

        Assert.IsNull(sut.TryGetSize("TrendsDialog"));
    }

    [TestMethod]
    public void SaveSize_ThenTryGetSize_RoundTrips()
    {
        var sut = CreateStore();

        sut.SaveSize("TrendsDialog", 1024.5, 742);

        var size = sut.TryGetSize("TrendsDialog");
        Assert.IsNotNull(size);
        Assert.AreEqual(1024.5, size.Value.Width);
        Assert.AreEqual(742, size.Value.Height);
    }

    [TestMethod]
    public void SaveSize_PersistsAcrossStoreInstances()
    {
        CreateStore().SaveSize("MainWindow", 900, 600);

        // A fresh store (no in-memory cache) must read the value back from disk.
        var size = CreateStore().TryGetSize("MainWindow");

        Assert.IsNotNull(size);
        Assert.AreEqual(900, size.Value.Width);
        Assert.AreEqual(600, size.Value.Height);
    }

    [TestMethod]
    public void SaveSize_KeepsEntriesForDifferentKeysIndependent()
    {
        var sut = CreateStore();

        sut.SaveSize("TrendsDialog", 980, 600);
        sut.SaveSize("ConfusionsDialog", 720, 520);

        var trends = sut.TryGetSize("TrendsDialog");
        var confusions = sut.TryGetSize("ConfusionsDialog");

        Assert.IsNotNull(trends);
        Assert.IsNotNull(confusions);
        Assert.AreEqual(980, trends.Value.Width);
        Assert.AreEqual(720, confusions.Value.Width);
    }

    [TestMethod]
    public void SaveSize_OverwritesExistingKey()
    {
        var sut = CreateStore();

        sut.SaveSize("MainWindow", 800, 500);
        sut.SaveSize("MainWindow", 1200, 800);

        var size = sut.TryGetSize("MainWindow");
        Assert.IsNotNull(size);
        Assert.AreEqual(1200, size.Value.Width);
        Assert.AreEqual(800, size.Value.Height);
    }

    [DataRow(0.0, 600.0)]
    [DataRow(900.0, 0.0)]
    [DataRow(-10.0, 600.0)]
    [DataRow(double.NaN, 600.0)]
    [DataRow(double.PositiveInfinity, 600.0)]
    [TestMethod]
    public void SaveSize_IgnoresNonPositiveOrNonFiniteValues(double width, double height)
    {
        var sut = CreateStore();

        sut.SaveSize("MainWindow", width, height);

        Assert.IsNull(sut.TryGetSize("MainWindow"));
    }

    [TestMethod]
    public void SaveSize_WithBlankKey_IsIgnored()
    {
        var sut = CreateStore();

        sut.SaveSize("  ", 800, 600);

        Assert.IsNull(sut.TryGetSize("  "));
    }

    [TestMethod]
    public void TryGetSize_WhenFileIsCorrupt_ReturnsNullWithoutThrowing()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(Path.Combine(_tempDirectory, "window-sizes.json"), "{ this is not json");

        var sut = CreateStore();

        Assert.IsNull(sut.TryGetSize("MainWindow"));
    }

    private WindowSizeStore CreateStore()
    {
        var appPaths = Substitute.For<IAppPaths>();
        appPaths.AppDataDirectory.Returns(_tempDirectory);
        appPaths.UserConfigPaths.Returns(new List<string>());
        appPaths.PreferredUserConfigPath.Returns((string?)null);

        return new WindowSizeStore(appPaths, Substitute.For<ILogger<WindowSizeStore>>());
    }
}
