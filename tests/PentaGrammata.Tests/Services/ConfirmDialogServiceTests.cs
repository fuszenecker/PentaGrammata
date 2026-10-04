using System;
using System.Threading.Tasks;
using NSubstitute;
using PentaGrammata.Interfaces;
using PentaGrammata.Presentation;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class ConfirmDialogServiceTests
{
    [TestMethod]
    public void Constructor_NullWindowContext_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ConfirmDialogService(null!));
    }

    [TestMethod]
    public async Task ShowConfirmAsync_WhenActiveWindowIsNull_ReturnsFalse()
    {
        var windowContext = Substitute.For<IWindowContext>();
        windowContext.ActiveWindow.Returns((Avalonia.Controls.Window?)null);
        var sut = new ConfirmDialogService(windowContext);

        // Must not throw; no Avalonia dialog is created when there is no owner window.
        var confirmed = await sut.ShowConfirmAsync("Title", "Message", "Replace");

        Assert.IsFalse(confirmed);
    }
}
