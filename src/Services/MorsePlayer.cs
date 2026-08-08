using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using PentaGrammata.Configuration;

namespace PentaGrammata.Services;

public class MorsePlayer(IAudioPlayer audioPlayer, INoiseGeneratorFactory noiseGeneratorFactory) : IMorsePlayer
{
    private readonly IAudioPlayer _audioPlayer = audioPlayer;
    private readonly INoiseGeneratorFactory _noiseGeneratorFactory = noiseGeneratorFactory;

    public async Task PlayMorseCodeAsync(string morseCode, MorsePlaybackSettings settings, CancellationToken cancellationToken)
    {
        var audioData = await Task.Run(
            () => GenerateAudioData(morseCode.ToLower(), settings),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        await _audioPlayer.PlayAudioAsync(audioData, settings.SampleRate, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static short[] GenerateBeep(int sampleRate, int durationMs, double frequency, double volume, int beepRampMs)
    {
        int sampleCount = (sampleRate * durationMs) / 1000;
        int rampSamples = Math.Min((sampleRate * beepRampMs) / 1000, sampleCount / 2);
        var audioData = new short[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            double envelope = 1.0;

            if (i < rampSamples)
            {
                envelope = (double)i / rampSamples;
            }
            else if (i >= sampleCount - rampSamples)
            {
                envelope = (double)(sampleCount - 1 - i) / rampSamples;
            }

            audioData[i] = (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * short.MaxValue * volume * envelope);
        }

        return audioData;
    }

    private static short[] GenerateSilence(int sampleRate, int durationMs)
    {
        int sampleCount = (sampleRate * durationMs) / 1000;
        return new short[sampleCount]; // 16-bit audio silence
    }

    /// <summary>Converts a decibel value to a linear amplitude ratio (0 dB == 1.0).</summary>
    private static double DecibelsToLinear(double decibels) => Math.Pow(10.0, decibels / 20.0);

    private short[] GenerateAudioData(string morseCode, MorsePlaybackSettings settings)
    {
        int charWpm = settings.CharacterWpm;
        int averageWpm = Math.Min(settings.AverageWpm, charWpm);
        int sampleRate = settings.SampleRate;
        double frequency = settings.Frequency;
        double volume = DecibelsToLinear(settings.VolumeDb);
        int beepRampMs = settings.BeepRampMs;

        // Standard PARIS word = 50 dit-units:
        //   31 units of character elements and intra-character gaps (at character speed),
        //   19 units of inter-character and inter-word gaps (stretched for Farnsworth).
        //
        // Element timing at character speed:
        //   T_char = 1200 / charWpm  ms
        //
        // Farnsworth gap unit (stretched so one full PARIS word = 60000 / averageWpm ms):
        //   T_gap = (60000 / averageWpm − 31 × T_char) / 19
        //   When averageWpm == charWpm: T_gap == T_char (no stretching).
        double tCharMs = 1200.0 / charWpm;
        double tGapMs  = (60000.0 / averageWpm - 31.0 * tCharMs) / 19.0;

        int ditMs       = (int)Math.Round(tCharMs);
        int intraCharMs = ditMs;                              // between elements within a character
        int interCharMs = (int)Math.Round(3.0 * tGapMs);     // between characters
        int interWordMs = (int)Math.Round(7.0 * tGapMs);     // between words

        var audioData = new List<short>();
        bool firstToken = true;  // whether a gap needs to precede the next character

        for (int i = 0; i < morseCode.Length; i++)
        {
            string token;
            char c = morseCode[i];

            if (c == '<')
            {
                int end = morseCode.IndexOf('>', i);
                if (end == -1) continue;
                token = morseCode.Substring(i, end - i + 1);
                i = end;
            }
            else
            {
                token = c.ToString();
            }

            if (token == " ")
            {
                // Word boundary: emit the inter-word gap, then mark the next character as
                // the start of a new word (it does not need a preceding inter-char gap).
                if (!firstToken)
                    audioData.AddRange(GenerateSilence(sampleRate, interWordMs));
                firstToken = true;
                continue;
            }

            string morseSymbols = CharToMorse(token);
            if (morseSymbols.Length == 0)
                continue;

            // Prepend inter-character gap before every character except the first.
            if (!firstToken)
                audioData.AddRange(GenerateSilence(sampleRate, interCharMs));
            firstToken = false;

            // Emit elements with intra-character gaps between them (but not trailing).
            for (int j = 0; j < morseSymbols.Length; j++)
            {
                if (j > 0)
                    audioData.AddRange(GenerateSilence(sampleRate, intraCharMs));

                char sym = morseSymbols[j];
                if (sym == '.')
                    audioData.AddRange(GenerateBeep(sampleRate, ditMs, frequency, volume, beepRampMs));
                else if (sym == '-')
                    audioData.AddRange(GenerateBeep(sampleRate, 3 * ditMs, frequency, volume, beepRampMs));
            }
        }

        // Append a trailing inter-character gap so the receiver chain processes the final
        // character's elements before the buffer ends.
        if (!firstToken)
            audioData.AddRange(GenerateSilence(sampleRate, interCharMs));

        var samples = audioData.ToArray();
        ApplyReceiverChain(samples, settings);
        return samples;
    }

    /// <summary>
    /// Passes the rendered Morse signal through a model of a receiver's audio chain so
    /// it sounds like it came off the air: broadband background noise is mixed with the
    /// clean tone <em>first</em>, then both are pushed through the SAME band-pass filter
    /// (a real "filter width" knob), optionally emphasized by an audio peak filter, and
    /// optionally levelled by an AGC whose slow release lets the noise floor breathe up
    /// in the gaps and duck under the signal. No-ops when noise is disabled, leaving the
    /// clean tone untouched.
    /// </summary>
    private void ApplyReceiverChain(short[] samples, MorsePlaybackSettings settings)
    {
        if (samples.Length == 0)
        {
            return;
        }

        var generator = _noiseGeneratorFactory.Create(settings.NoiseType);
        if (generator is null)
        {
            return;
        }

        // 1. Broadband noise from the generator. Its raw amplitude is arbitrary; step 2
        //    rescales it, so here we only capture the samples.
        var noise = new double[samples.Length];
        for (int i = 0; i < noise.Length; i++)
        {
            noise[i] = generator.Next();
        }

        double volumeLinear = DecibelsToLinear(settings.VolumeDb);
        double toneRms = (short.MaxValue * volumeLinear) / Math.Sqrt(2.0);
        double targetNoiseRms = toneRms * DecibelsToLinear(settings.NoiseLevelDb);

        // 2. Scale the noise so its level sits NoiseLevelDb relative to the tone AFTER the
        //    shared passband — i.e. -NoiseLevelDb is the in-band signal-to-noise ratio
        //    actually heard (what the UI exposes), not a pre-filter figure that silently
        //    improves as the band narrows.
        //    We measure the noise's post-filter RMS with a throwaway filter identical to
        //    the chain's. Because the biquad is linear, filter(tone + g·noise) =
        //    filter(tone) + g·filter(noise), so this measured noise component is exactly
        //    what reaches the output. The tone sits at the passband center (unity gain),
        //    so filter(tone) keeps toneRms as the reference. Normalizing by the noise's own
        //    post-filter RMS also makes the requested dB mean the same thing for Gaussian,
        //    uniform and pink alike.
        var noiseMeasurementFilter = new BandPassFilter(settings.Frequency, settings.NoiseBandwidthHz, settings.SampleRate);
        double filteredNoiseSumSquares = 0.0;
        for (int i = 0; i < noise.Length; i++)
        {
            double filtered = noiseMeasurementFilter.Process(noise[i]);
            filteredNoiseSumSquares += filtered * filtered;
        }

        double filteredNoiseRms = Math.Sqrt(filteredNoiseSumSquares / noise.Length);
        if (filteredNoiseRms <= double.Epsilon)
        {
            return;
        }

        double noiseGain = targetNoiseRms / filteredNoiseRms;

        // 3. Combine tone + noise, then run the sum through the shared receiver passband.
        //    Because the tone sits inside the passband it passes almost untouched while
        //    the broadband noise is trimmed to the band, so a narrower "bandwidth" removes
        //    out-of-band noise (the in-band SNR set in step 2 is preserved regardless).
        var passband = new BandPassFilter(settings.Frequency, settings.NoiseBandwidthHz, settings.SampleRate);

        // 4. AGC (optional): aim to restore the signal to the volume level, with a fast
        //    attack and a slow release ("delay") so the noise floor swells between
        //    characters and ducks under a tone. maxGain caps how far quiet passages are
        //    boosted. The AGC sees only the passband signal (see the step 6 loop).
        double target = short.MaxValue * volumeLinear;
        AutomaticGainControl? agc = settings.AgcEnabled
            ? new AutomaticGainControl(settings.SampleRate, target, maxGain: 8.0, releaseSeconds: settings.AgcDelaySeconds)
            : null;

        // 5. APF (optional): a resonant peak at the tone, added AFTER the AGC and driven
        //    by the AGC-leveled signal. Placing it downstream of the AGC keeps the AGC
        //    from riding over (and thus fighting) the peak filter's contribution.
        BandPassFilter? peakFilter = null;
        double peakBlend = 0.0;
        if (settings.ApfEnabled)
        {
            // Cap the peak width to the main passband so it always peaks rather than
            // merely repeating the passband.
            double peakWidth = Math.Min(settings.ApfBandwidthHz, settings.NoiseBandwidthHz);
            peakFilter = new BandPassFilter(settings.Frequency, peakWidth, settings.SampleRate);
            peakBlend = DecibelsToLinear(settings.ApfPeakGainDb);
        }

        // 6. Run the tone + scaled noise through the chain: shared passband, then AGC,
        //    then the optional APF blend.
        var receiverOutput = new double[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            double mixed = samples[i] + noise[i] * noiseGain;
            double filtered = passband.Process(mixed);
            double leveled = agc is not null ? agc.Process(filtered) : filtered;
            if (peakFilter is not null)
            {
                leveled += peakBlend * peakFilter.Process(leveled);
            }

            receiverOutput[i] = leveled;
        }

        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Clamp(receiverOutput[i], short.MinValue, short.MaxValue);
        }
    }

    private static string CharToMorse(string morseChar) => MorseAlphabet.GetSymbols(morseChar);
}