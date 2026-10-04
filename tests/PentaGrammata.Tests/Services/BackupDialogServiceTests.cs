using System;
using System.Threading.Tasks;
using NSubstitute;
using PentaGrammata.Interfaces;
using PentaGrammata.Presentation;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class BackupDialogServiceTests
{
    [TestMethod]
    public void Constructor_NullArguments_ThrowArgumentNullException()
    {
        var windowContext = Substitute.For<IWindowContext>();
        var backupService = Substitute.For<IUserBackupService>();
        var confirmDialogService = Substitute.For<IConfirmDialogService>();
        var infoDialogService = Substitute.For<IInfoDialogService>();

        Assert.ThrowsExactly<ArgumentNullException>(() => new BackupDialogService(null!, backupService, confirmDialogService, infoDialogService));
        Assert.ThrowsExactly<ArgumentNullException>(() => new BackupDialogService(windowContext, null!, confirmDialogService, infoDialogService));
        Assert.ThrowsExactly<ArgumentNullException>(() => new BackupDialogService(windowContext, backupService, null!, infoDialogService));
        Assert.ThrowsExactly<ArgumentNullException>(() => new BackupDialogService(windowContext, backupService, confirmDialogService, null!));
    }

    [TestMethod]
    public async Task ShowExportAsync_WhenMainWindowIsNull_ReturnsCancelledWithoutExporting()
    {
        var windowContext = Substitute.For<IWindowContext>();
        windowContext.MainWindow.Returns((Avalonia.Controls.Window?)null);
        var backupService = Substitute.For<IUserBackupService>();
        var sut = CreateSut(windowContext, backupService);

        var outcome = await sut.ShowExportAsync();

        Assert.AreEqual(BackupDialogOutcome.Cancelled, outcome);
        await backupService.DidNotReceive().ExportAsync(Arg.Any<System.IO.Stream>());
    }

    [TestMethod]
    public async Task ShowImportAsync_WhenMainWindowIsNull_ReturnsCancelledWithoutImporting()
    {
        var windowContext = Substitute.For<IWindowContext>();
        windowContext.MainWindow.Returns((Avalonia.Controls.Window?)null);
        var backupService = Substitute.For<IUserBackupService>();
        var sut = CreateSut(windowContext, backupService);

        var outcome = await sut.ShowImportAsync();

        Assert.AreEqual(BackupDialogOutcome.Cancelled, outcome);
        await backupService.DidNotReceive().ImportAsync(Arg.Any<System.IO.Stream>(), Arg.Any<System.Threading.CancellationToken>());
    }

    private static BackupDialogService CreateSut(
        IWindowContext windowContext,
        IUserBackupService backupService)
    {
        return new BackupDialogService(
            windowContext,
            backupService,
            Substitute.For<IConfirmDialogService>(),
            Substitute.For<IInfoDialogService>());
    }
}
