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
    private static Task<(short[] Audio, int SampleRate)> PlayAndCaptureAsync(string text, MorsePlaybackSettings settings)
        => PlayAndCaptureAsync(text, settings, new NoiseGeneratorFactory());

    private static async Task<(short[] Audio, int SampleRate)> PlayAndCaptureAsync(
        string text, MorsePlaybackSettings settings, INoiseGeneratorFactory noiseFactory)
    {
        var audioPlayer = Substitute.For<IAudioPlayer>();
        short[] captured = [];
        var capturedRate = 0;
        audioPlayer
            .PlayAudioAsync(Arg.Do<short[]>(a => captured = a), Arg.Do<int>(r => capturedRate = r), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new MorsePlayer(audioPlayer, noiseFactory);
        await sut.PlayMorseCodeAsync(text, settings, CancellationToken.None);

        return (captured, capturedRate);
    }

    /// <summary>
    /// A factory that hands out Gaussian generators seeded identically on every call, so two
    /// separate playbacks see the exact same raw noise sequence and can be compared directly.
    /// </summary>
    private sealed class SeededGaussianNoiseFactory(int seed) : INoiseGeneratorFactory
    {
        public INoiseGenerator? Create(NoiseType type) => new GaussianNoiseGenerator(new System.Random(seed));
    }

    /// <summary>RMS over a window, used to measure the residual noise floor in a silent gap.</summary>
    private static double Rms(short[] samples, int start, int count)
    {
        double sumSquares = 0.0;
        for (int i = start; i < start + count; i++)
        {
            sumSquares += (double)samples[i] * samples[i];
        }

        return System.Math.Sqrt(sumSquares / count);
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
    public async Task PlayMorseCodeAsync_WithNoiseNone_LeavesTrailingSilenceZero()
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
    public async Task PlayMorseCodeAsync_NoiseFloor_IsInvariantToBandwidth()
    {
        // The point of measuring NoiseLevelDb AFTER the passband: the audible in-band noise
        // floor must be the same whether the filter is wide or narrow. A narrower filter
        // removes out-of-band noise but must not change the in-band SNR the operator set.
        // AGC/APF are nonlinear, so disable them to read the raw scaled+filtered noise.
        var baseSettings = Settings() with
        {
            NoiseType = NoiseType.Gaussian,
            NoiseLevelDb = -6,
            AgcEnabled = false,
            ApfEnabled = false,
            SampleRate = 8000,
        };
        var factory = new SeededGaussianNoiseFactory(1234);

        var (wide, _)   = await PlayAndCaptureAsync("e", baseSettings with { NoiseBandwidthHz = 1500 }, factory);
        var (narrow, _) = await PlayAndCaptureAsync("e", baseSettings with { NoiseBandwidthHz = 300 },  factory);

        // Measure the noise floor in the trailing gap (well clear of the single dit beep).
        int window = 800;
        double wideFloor   = Rms(wide,   wide.Length   - window, window);
        double narrowFloor = Rms(narrow, narrow.Length - window, window);

        Assert.IsGreaterThan(0.0, wideFloor);
        // Within 12% despite a 5x bandwidth difference. (Pre-fix, the narrow floor would be
        // far lower because the level was set before the filter trimmed the noise.)
        double ratio = narrowFloor / wideFloor;
        Assert.IsTrue(ratio is > 0.88 and < 1.12, $"noise floor drifted with bandwidth: ratio={ratio:F3}");
    }

    [TestMethod]
    public async Task PlayMorseCodeAsync_NoiseFloor_TracksNoiseLevelDb()
    {
        // Raising NoiseLevelDb by 6 dB must raise the in-band noise floor by ~6 dB
        // (a linear amplitude factor of ~2), confirming the dB scaling is applied correctly.
        var baseSettings = Settings() with
        {
            NoiseType = NoiseType.Gaussian,
            NoiseBandwidthHz = 500,
            AgcEnabled = false,
            ApfEnabled = false,
            SampleRate = 8000,
        };
        var factory = new SeededGaussianNoiseFactory(4321);

        var (quiet, _) = await PlayAndCaptureAsync("e", baseSettings with { NoiseLevelDb = -12 }, factory);
        var (loud, _)  = await PlayAndCaptureAsync("e", baseSettings with { NoiseLevelDb = -6 },  factory);

        int window = 800;
        double quietFloor = Rms(quiet, quiet.Length - window, window);
        double loudFloor  = Rms(loud,  loud.Length  - window, window);

        double ratio = loudFloor / quietFloor;
        // +6 dB == 10^(6/20) == ~1.995x amplitude.
        Assert.IsTrue(ratio is > 1.8 and < 2.2, $"expected ~2x floor for +6 dB, got ratio={ratio:F3}");
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
    public async Task PlayMorseCodeAsync_WhenAudioReturnsAfterCancellation_Throws()
    {
        var audioPlayer = Substitute.For<IAudioPlayer>();
        using var cts = new CancellationTokenSource();
        audioPlayer.PlayAudioAsync(Arg.Any<short[]>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            });
        var sut = new MorsePlayer(audioPlayer, new NoiseGeneratorFactory());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => sut.PlayMorseCodeAsync("PARIS", Settings(), cts.Token));
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
