using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using PentaGrammata.Configuration;
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
        VolumeDb = -6,
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

        var sut = new MorsePlayer(audioPlayer, new NoiseGeneratorFactory());
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
    public async Task PlayMorseCodeAsync_NormalSpeed_PARISWordHasExactDuration()
    {
        // "PARIS " is the ITU standard for WPM measurement: 50 dit-units per word.
        // At charWpm=20, T_char=60 ms → 50 × 60 = 3000 ms → 24000 samples at 8000 Hz.
        // All timing values are integer multiples of 60 ms so there is no rounding error.
        const int charWpm = 20;
        const int sampleRate = 8000;
        int expectedSamples = 50 * (1200 / charWpm) * sampleRate / 1000; // 24000

        var (audio, _) = await PlayAndCaptureAsync("PARIS ", Settings(charWpm: charWpm, averageWpm: charWpm) with { SampleRate = sampleRate });

        Assert.AreEqual(expectedSamples, audio.Length);
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_Farnsworth_ElementTimingPreservedAtCharacterSpeed()
    {
        // Farnsworth: the character element speed (charWpm) must not change when averageWpm
        // is lowered. Only the inter-character and inter-word gaps grow. Verify by comparing
        // the beep portion of "e" (a single dit) at the same charWpm but different averageWpm.
        const int charWpm = 20;
        const int sampleRate = 8000;
        int ditSamples = (1200 / charWpm) * sampleRate / 1000; // 480

        var (eNormal, _)      = await PlayAndCaptureAsync("e", Settings(charWpm: charWpm, averageWpm: charWpm)      with { SampleRate = sampleRate });
        var (eFarnsworth, _)  = await PlayAndCaptureAsync("e", Settings(charWpm: charWpm, averageWpm: charWpm / 2)  with { SampleRate = sampleRate });

        // Both recordings start with the same dit beep.
        CollectionAssert.AreEqual(eNormal.Take(ditSamples).ToArray(), eFarnsworth.Take(ditSamples).ToArray());

        // Farnsworth recording is longer overall (wider inter-character gap).
        Assert.IsGreaterThan(eNormal.Length, eFarnsworth.Length);
    }

    [TestMethod]
    {
        var (audio, _) = await PlayAndCaptureAsync("e", Settings() with { NoiseType = NoiseType.None });

        // With noise disabled the trailing inter-character silence must be pure zeros.
        Assert.IsTrue(audio.Any(s => s != 0), "expected the beep to produce non-zero samples");
        Assert.AreEqual(0, audio[^1]);
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_WithGaussianNoise_FillsSilentGapsWithSignal()
    {
        var settings = Settings() with
        {
            NoiseType = NoiseType.Gaussian,
            NoiseLevelDb = -6,
            NoiseBandwidthHz = 500,
        };

        var (audio, _) = await PlayAndCaptureAsync("e", settings);

        // Continuous noise means the tail (former silence) now carries signal.
        int nonZeroInTail = audio.Skip(audio.Length - 50).Count(s => s != 0);
        Assert.IsGreaterThan(0, nonZeroInTail);
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_ReceiverChain_DoesNotClipToRails()
    {
        // A well-behaved AGC/filter chain should not slam most samples to the 16-bit
        // rails; runaway gain would show up as a buffer full of +/-32767.
        var settings = Settings() with
        {
            NoiseType = NoiseType.Gaussian,
            NoiseLevelDb = -6,
            NoiseBandwidthHz = 500,
        };

        var (audio, _) = await PlayAndCaptureAsync("PARIS", settings);

        int railed = audio.Count(s => s == short.MaxValue || s == short.MinValue);
        Assert.IsLessThan(audio.Length / 10, railed, "receiver chain should not clip most samples to the rails");
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_NoiseDoesNotChangeBufferLength()
    {
        var clean = Settings() with { NoiseType = NoiseType.None };
        var noisy = Settings() with { NoiseType = NoiseType.Pink, NoiseLevelDb = -3 };

        var (cleanAudio, _) = await PlayAndCaptureAsync("PARIS", clean);
        var (noisyAudio, _) = await PlayAndCaptureAsync("PARIS", noisy);

        Assert.AreEqual(cleanAudio.Length, noisyAudio.Length);
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_WhenCancelledBeforeStart_ThrowsAndDoesNotPlay()
    {
        var audioPlayer = Substitute.For<IAudioPlayer>();
        var sut = new MorsePlayer(audioPlayer, new NoiseGeneratorFactory());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => sut.PlayMorseCodeAsync("PARIS", Settings(), cts.Token));

        await audioPlayer.DidNotReceive().PlayAudioAsync(Arg.Any<short[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
