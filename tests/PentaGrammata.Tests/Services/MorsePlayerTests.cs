using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class MorsePlayerTests
{
    private static MorsePlaybackSettings Settings(int charWpm = 20, int averageWpm = 20) => new()
    {
        CharacterWpm = charWpm,
        AverageWpm = averageWpm,
        SampleRate = 8000,
        Frequency = 600,
        Volume = 0.5,
        BeepRampMs = 2,
    };

    /// <summary>Plays the given text and returns the audio buffer handed to the player.</summary>
    private static async Task<(short[] Audio, int SampleRate)> PlayAndCaptureAsync(string text, MorsePlaybackSettings settings)
    {
        var audioPlayer = Substitute.For<IAudioPlayer>();
        short[] captured = [];
        var capturedRate = 0;
        audioPlayer
            .PlayAudioAsync(Arg.Do<short[]>(a => captured = a), Arg.Do<int>(r => capturedRate = r), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new MorsePlayer(audioPlayer);
        await sut.PlayMorseCodeAsync(text, settings, CancellationToken.None);

        return (captured, capturedRate);
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_ForwardsGeneratedAudioAtSettingsSampleRate()
    {
        var (audio, rate) = await PlayAndCaptureAsync("e", Settings());

        Assert.IsGreaterThan(0, audio.Length);
        Assert.AreEqual(8000, rate);
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_KnownCharacters_ProduceAudio()
    {
        var (audio, _) = await PlayAndCaptureAsync("PARIS", Settings());

        Assert.IsGreaterThan(0, audio.Length);
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_UnknownCharacters_ProduceLessAudioThanKnown()
    {
        // '#' has no Morse mapping, so it contributes no beeps of its own.
        var (known, _) = await PlayAndCaptureAsync("e", Settings());
        var (unknown, _) = await PlayAndCaptureAsync("#", Settings());

        Assert.IsGreaterThan(unknown.Length, known.Length);
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_HigherWpm_ProducesShorterAudio()
    {
        var (slow, _) = await PlayAndCaptureAsync("PARIS", Settings(charWpm: 15, averageWpm: 15));
        var (fast, _) = await PlayAndCaptureAsync("PARIS", Settings(charWpm: 40, averageWpm: 40));

        Assert.IsGreaterThan(fast.Length, slow.Length);
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_IsCaseInsensitive()
    {
        var (upper, _) = await PlayAndCaptureAsync("PARIS", Settings());
        var (lower, _) = await PlayAndCaptureAsync("paris", Settings());

        Assert.AreEqual(upper.Length, lower.Length);
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_ProsignIsTreatedAsSingleCharacter()
    {
        // "<sk>" is one prosign (...-.-); its four literal characters must not each
        // be sounded out separately.
        var (prosign, _) = await PlayAndCaptureAsync("<sk>", Settings());
        var (literals, _) = await PlayAndCaptureAsync("sk", Settings());

        Assert.AreNotEqual(literals.Length, prosign.Length);
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_WhenCancelledBeforeStart_ThrowsAndDoesNotPlay()
    {
        var audioPlayer = Substitute.For<IAudioPlayer>();
        var sut = new MorsePlayer(audioPlayer);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => sut.PlayMorseCodeAsync("PARIS", Settings(), cts.Token));

        await audioPlayer.DidNotReceive().PlayAudioAsync(Arg.Any<short[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
