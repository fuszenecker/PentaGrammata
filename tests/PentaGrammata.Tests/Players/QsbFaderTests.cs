using PentaGrammata.Players;

namespace PentaGrammata.Tests.Players;

[TestClass]
public sealed class QsbFaderTests
{
    private const int SampleRate = 8000;
    private const short Amplitude = 16000;

    /// <summary>A buffer at constant amplitude, so any variation in the output is the fade gain.</summary>
    private static short[] Constant(double seconds, short amplitude)
    {
        var samples = new short[(int)(SampleRate * seconds)];
        Array.Fill(samples, amplitude);
        return samples;
    }

    [TestMethod]
    public void Apply_ConstantInput_NeverExceedsInput()
    {
        // The fade gain never rises above 1, so a full-amplitude buffer can only lose energy.
        var samples = Constant(10.0, Amplitude);

        new QsbFader(depthDb: 40, periodSeconds: 1, SampleRate, new System.Random(1)).Apply(samples);

        Assert.IsTrue(samples.All(s => Math.Abs(s) <= Amplitude),
            "fading must never amplify the signal");
    }

    [TestMethod]
    public void Apply_SilenceStaysSilent()
    {
        var samples = new short[SampleRate * 5];

        new QsbFader(depthDb: 20, periodSeconds: 1, SampleRate, new System.Random(2)).Apply(samples);

        Assert.IsTrue(samples.All(s => s == 0),
            "fading multiplies and must never create sound out of silence");
    }

    [TestMethod]
    public void Apply_FadesWanderDownAndRecover_OverLongBuffer()
    {
        // With depth 20 dB the minimum possible gain is 0.1 and targets are drawn
        // uniformly in dB between 0 and −20, so over many retargets the gain must both
        // dip well down and rise back near full strength — wandering, not just decaying.
        var samples = Constant(30.0, Amplitude);

        new QsbFader(depthDb: 20, periodSeconds: 1, SampleRate, new System.Random(3)).Apply(samples);

        // Skip the opening half-second, which always starts at full strength.
        int skip = SampleRate / 2;
        double minRatio = samples.Skip(skip).Min(s => Math.Abs(s)) / (double)Amplitude;
        double maxRatio = samples.Skip(skip).Max(s => Math.Abs(s)) / (double)Amplitude;

        Assert.IsLessThanOrEqualTo(0.35, minRatio, $"expected deep fades, deepest gain ratio was {minRatio:F3}");
        Assert.IsGreaterThanOrEqualTo(0.75, maxRatio, $"expected recovery toward full strength, highest gain ratio was {maxRatio:F3}");
    }

    [TestMethod]
    public void Apply_IsSmooth_NoPerSampleJumps()
    {
        // One-pole smoothing bounds the per-sample gain step at alpha*(1-minGain)
        // (~3.7e-4 here, ~6 counts at this amplitude); at a retarget boundary only the
        // target jumps, never the gain, so the output stays click-free throughout.
        var samples = Constant(10.0, Amplitude);

        new QsbFader(depthDb: 40, periodSeconds: 1, SampleRate, new System.Random(4)).Apply(samples);

        double maxJump = 0;
        for (int i = 1; i < samples.Length; i++)
        {
            maxJump = Math.Max(maxJump, Math.Abs(samples[i] - samples[i - 1]));
        }

        Assert.IsLessThan(0.001 * Amplitude, maxJump,
            $"per-sample jump of {maxJump:F1} counts is too large for click-free fading");
    }

    [TestMethod]
    public void Apply_ZeroDepth_IsExactNoOp()
    {
        var original = Constant(5.0, 12345);
        var samples = (short[])original.Clone();

        new QsbFader(depthDb: 0, periodSeconds: 1, SampleRate, new System.Random(5)).Apply(samples);

        CollectionAssert.AreEqual(original, samples,
            "zero depth must leave the signal element-for-element untouched");
    }

    [TestMethod]
    public void Apply_TinyPeriod_DoesNotThrowOrAmplify()
    {
        // A hand-edited config could carry an absurd period; the ctor clamps turn that
        // into fast but bounded flutter instead of an overflow or division blow-up.
        var samples = Constant(1.0, -20000);

        new QsbFader(depthDb: 40, periodSeconds: 1e-6, SampleRate, new System.Random(6)).Apply(samples);

        Assert.IsTrue(samples.All(s => Math.Abs(s) <= 20000),
            "degenerate period must not amplify or overflow");
    }

    [TestMethod]
    public void Apply_EmptyBuffer_IsSafe()
    {
        var samples = Array.Empty<short>();

        new QsbFader(depthDb: 20, periodSeconds: 1, SampleRate, new System.Random(7)).Apply(samples);

        Assert.HasCount(0, samples);
    }

    [TestMethod]
    public void Apply_StartsAtFullStrength()
    {
        // The fade eases down from unity gain, so the first sample of a transmission is
        // never attenuated — every message starts at full strength.
        var samples = Constant(2.0, 20000);

        new QsbFader(depthDb: 20, periodSeconds: 1, SampleRate, new System.Random(8)).Apply(samples);

        Assert.AreEqual(20000, samples[0]);
    }
}