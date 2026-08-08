using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PentaGrammata.Interfaces;
using PentaGrammata.Presentation;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class InfoDialogServiceTests
{
    [TestMethod]
    public void Constructor_NullWindowContext_ThrowsArgumentNullException()
    {
        var configurationService = Substitute.For<IConfigurationService>();
        Assert.ThrowsExactly<ArgumentNullException>(() => new InfoDialogService(null!, configurationService));
    }

    [TestMethod]
    public async Task ShowInfoAsync_WhenActiveWindowIsNull_CompletesWithoutShowingDialog()
    {
        var windowContext = Substitute.For<IWindowContext>();
        windowContext.ActiveWindow.Returns((Avalonia.Controls.Window?)null);
        var configurationService = Substitute.For<IConfigurationService>();
        var sut = new InfoDialogService(windowContext, configurationService);

        // Must not throw; no Avalonia dialog is created when there is no owner window.
        await sut.ShowInfoAsync("Title", "Message");
    }
}
