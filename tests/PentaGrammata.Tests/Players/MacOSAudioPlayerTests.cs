using PentaGrammata.Players;

namespace PentaGrammata.Tests.Players;

[TestClass]
public sealed class MacOSAudioPlayerTests
{
    [TestMethod]
    public void BufferSampleCount_IsAboutTwoHundredMilliseconds()
    {
        Assert.AreEqual(1600, MacOSAudioPlayer.BufferSampleCount(8000));
        Assert.AreEqual(8820, MacOSAudioPlayer.BufferSampleCount(44100));
    }

    [TestMethod]
    public void BufferSampleCount_NeverReturnsZero()
    {
        Assert.AreEqual(1, MacOSAudioPlayer.BufferSampleCount(1));
    }

    [TestMethod]
    public void DescribeStatus_PrintableFourCharCode_IsQuoted()
    {
        const int formatNotSupported = 0x666D743F; // 'fmt?'

        Assert.AreEqual("'fmt?'", MacOSAudioPlayer.DescribeStatus(formatNotSupported));
    }

    [TestMethod]
    public void DescribeStatus_PlainNegativeCode_HasNoDescription()
    {
        Assert.IsNull(MacOSAudioPlayer.DescribeStatus(-50));
    }

    [TestMethod]
    public async Task PlayAudioAsync_EmptyAudio_ReturnsWithoutLoadingTheNativeLibrary()
    {
        await new MacOSAudioPlayer().PlayAudioAsync([], 8000, CancellationToken.None);
    }

    [TestMethod]
    public async Task PlayAudioAsync_AlreadyCancelled_ThrowsBeforeTouchingTheNativeLibrary()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new MacOSAudioPlayer().PlayAudioAsync([1, 2, 3], 8000, cts.Token));
    }
}
