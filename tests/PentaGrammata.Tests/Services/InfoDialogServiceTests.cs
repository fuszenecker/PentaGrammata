using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PentaGrammata.Interfaces;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class InfoDialogServiceTests
{
    [TestMethod]
    public void Constructor_NullWindowContext_ThrowsArgumentNullException()
    {
        var configStore = Substitute.For<IPracticeConfigurationStore>();
        Assert.ThrowsExactly<ArgumentNullException>(() => new InfoDialogService(null!, configStore));
    }

    [TestMethod]
    public async Task ShowInfoAsync_WhenActiveWindowIsNull_CompletesWithoutShowingDialog()
    {
        var windowContext = Substitute.For<IWindowContext>();
        windowContext.ActiveWindow.Returns((Avalonia.Controls.Window?)null);
        var configStore = Substitute.For<IPracticeConfigurationStore>();
        var sut = new InfoDialogService(windowContext, configStore);

        // Must not throw; no Avalonia dialog is created when there is no owner window.
        await sut.ShowInfoAsync("Title", "Message");
    }
}
