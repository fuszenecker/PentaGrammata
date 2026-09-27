using System;
using System.Linq;

using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Players;

namespace PentaGrammata.Services;

/// <summary>
/// Measures the headroom a set of audio settings leaves by rendering a short probe message
/// through the REAL signal chain (<see cref="MorseSignalRenderer"/>) and reading the peak off
/// the unclamped output. Running the actual chain rather than an analytical estimate is what
/// makes the verdict trustworthy: AGC attack overshoot, the APF blend and the noise crest
/// factor all interact, and none of them has a closed form.
/// </summary>
public sealed class AudioHeadroomAnalyzer : IAudioHeadroomAnalyzer
{
    /// <summary>
    /// Probe word, repeated as needed. PARIS is the WPM yardstick: one word plus its trailing
    /// word gap lasts exactly 60 / averageWpm seconds, and it covers dits, dahs, and
    /// intra-character, inter-character and inter-word gaps — so the AGC gets to wind its gain
    /// up in the long gaps and be hit by a tone right afterwards, which is where the worst
    /// overshoot happens.
    /// </summary>
    private const string ProbeWord = "paris ";

    /// <summary>Shortest probe: two words already exercise every gap length.</summary>
    private const int MinimumProbeWords = 2;

    /// <summary>
    /// How many QSB fade periods the probe must span when fading is on. One period would only
    /// just reach the first deep fade; two also catch the recovery out of it, and the AGC boost
    /// that rides along with a faded signal is exactly what pushes the chain into clipping.
    /// </summary>
    private const double QsbProbePeriods = 2.0;

    /// <summary>
    /// Ceiling on the probe length, so a hand-edited fade period cannot turn an analysis that
    /// runs on every keystroke into a minutes-long render. The dialog caps the period at 20 s,
    /// so every setting reachable from the UI still gets its full two periods.
    /// </summary>
    private const double MaxProbeSeconds = 45.0;

    /// <summary>
    /// Fixed seed for the noise and the QSB fade: the same settings must always produce the
    /// same verdict, otherwise the warning would flicker while the user edits the dialog.
    /// </summary>
    private const int ProbeSeed = 5150;

    public AudioHeadroomReport Analyze(MorsePlaybackSettings settings)
    {
        var renderer = new MorseSignalRenderer(
            new NoiseGeneratorFactory(new Random(ProbeSeed)),
            new Random(ProbeSeed));

        var rendered = renderer.Render(BuildProbeText(settings), settings);
        if (rendered.Length == 0)
        {
            return new AudioHeadroomReport(double.NegativeInfinity, 0.0, 0.0);
        }

        double peak = 0.0;
        int clipped = 0;

        foreach (double sample in rendered)
        {
            double magnitude = Math.Abs(sample);
            if (magnitude > peak)
            {
                peak = magnitude;
            }

            if (magnitude > MorseSignalRenderer.FullScale)
            {
                clipped++;
            }
        }

        double peakDbFs = peak > 0.0
            ? 20.0 * Math.Log10(peak / MorseSignalRenderer.FullScale)
            : double.NegativeInfinity;

        return new AudioHeadroomReport(
            peakDbFs,
            (double)clipped / rendered.Length,
            (double)rendered.Length / settings.SampleRate);
    }

    /// <summary>
    /// Repeats the probe word until it spans <see cref="QsbProbePeriods"/> fade periods, so a
    /// slow fade is actually inside the measured window instead of sitting just past its end.
    /// </summary>
    private static string BuildProbeText(MorsePlaybackSettings settings)
    {
        int words = MinimumProbeWords;

        if (settings.QsbEnabled)
        {
            // The renderer keys the gaps at the average speed, capped by the character speed.
            int averageWpm = Math.Max(Math.Min(settings.AverageWpm, settings.CharacterWpm), 1);
            double wordSeconds = 60.0 / averageWpm;
            double probeSeconds = Math.Min(QsbProbePeriods * Math.Max(settings.QsbPeriodSeconds, 0.0), MaxProbeSeconds);

            words = Math.Max(words, (int)Math.Ceiling(probeSeconds / wordSeconds));
        }

        // The trailing space is deliberate: it keys the word gap that completes the last PARIS
        // word, so the rendered probe is exactly words × 60 / averageWpm seconds long.
        return string.Concat(Enumerable.Repeat(ProbeWord, words));
    }
}
