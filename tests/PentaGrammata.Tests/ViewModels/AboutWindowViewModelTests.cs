using Microsoft.VisualStudio.TestTools.UnitTesting;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Tests.ViewModels;

[TestClass]
public sealed class AboutWindowViewModelTests
{
    [TestMethod]
    public void Constructor_PopulatesVersionText()
    {
        var sut = new AboutWindowViewModel();

        StringAssert.StartsWith(sut.VersionText, "Version ");
    }

    [TestMethod]
    public void Constructor_PopulatesCopyrightText()
    {
        var sut = new AboutWindowViewModel();

        StringAssert.Contains(sut.CopyrightText, "Fuszenecker");
        StringAssert.Contains(sut.CopyrightText, "HA8LHS");
    }

    [TestMethod]
    public void CloseCommand_RaisesCloseRequestedEvent()
    {
        var sut = new AboutWindowViewModel();
        var closeRaised = false;
        sut.CloseRequested += () => closeRaised = true;

        sut.CloseCommand.Execute(null);

        Assert.IsTrue(closeRaised);
    }
}
