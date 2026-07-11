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
        int averageWpm = settings.AverageWpm;
        int sampleRate = settings.SampleRate;
        double frequency = settings.Frequency;
        double volume = DecibelsToLinear(settings.VolumeDb);
        int beepRampMs = settings.BeepRampMs;

        if (averageWpm > charWpm)
        {
            averageWpm = charWpm;
        }

        // Placeholder implementation: generate a simple beep for each dot and dash
        var audioData = new List<short>();

        // Calculate timing based on WPM
        int ditLengthMs = 1200 / charWpm; 

        // Extra time in ms to slow down to text WPM
        int extraTimeMs = 60000 * (charWpm - averageWpm) / charWpm;

        // Calculate extra time per character based on text WPM (assuming 5 characters per word: PARIS)
        int totalCharsPerMinute = averageWpm * 5; 

        // Extra time to add after each character to achieve the desired text WPM
        int extraTimePerCharMs = (int)((double)extraTimeMs / totalCharsPerMinute);

        for (int i = 0; i < morseCode.Length; i++)
        {
            string morseString;
            char c = morseCode[i];
            
            if (c == '<')
            {
                int endIndex = morseCode.IndexOf('>', i);
                if (endIndex == -1)
                {
                    continue; // Invalid format, skip
                }
                
                morseString = morseCode.Substring(i, endIndex - i + 1);
                i = endIndex;
            }
            else
            {
                morseString = c.ToString();
            }

            string morseSymbols = CharToMorse(morseString);
            foreach (char symbol in morseSymbols)
            {
                if (symbol == '.')
                {
                    audioData.AddRange(GenerateBeep(sampleRate, ditLengthMs, frequency, volume, beepRampMs));
                }
                else if (symbol == '-')
                {
                    audioData.AddRange(GenerateBeep(sampleRate, 3 * ditLengthMs, frequency, volume, beepRampMs));
                }
                else if (symbol == ' ')
                {
                    audioData.AddRange(GenerateSilence(sampleRate, (7 - 1 - 2) * ditLengthMs)); // Space between symbols: 400ms silence
                }

                audioData.AddRange(GenerateSilence(sampleRate, ditLengthMs)); // Space between dots/dashes: 100ms silence
            }

            audioData.AddRange(GenerateSilence(sampleRate, (3 - 1) * ditLengthMs)); // Space between characters: 200ms silence
            
            // Farnsworth timing: add extra silence after each character to slow down the overall speed to text WPM
            audioData.AddRange(GenerateSilence(sampleRate, extraTimePerCharMs));
        }

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

        // 1. Broadband (unfiltered) noise, scaled so its level sits NoiseLevelDb relative
        //    to the volume-corrected tone. We normalize by the generator's own RMS so the
        //    requested dB means the same thing for Gaussian, uniform and pink alike.
        var noise = new double[samples.Length];
        double noiseSumSquares = 0.0;
        for (int i = 0; i < noise.Length; i++)
        {
            double n = generator.Next();
            noise[i] = n;
            noiseSumSquares += n * n;
        }

        double rawNoiseRms = Math.Sqrt(noiseSumSquares / noise.Length);
        if (rawNoiseRms <= double.Epsilon)
        {
            return;
        }

        double volumeLinear = DecibelsToLinear(settings.VolumeDb);
        double toneRms = (short.MaxValue * volumeLinear) / Math.Sqrt(2.0);
        double targetNoiseRms = toneRms * DecibelsToLinear(settings.NoiseLevelDb);
        double noiseGain = targetNoiseRms / rawNoiseRms;

        // 2. Combine tone + noise, then run the sum through the shared receiver passband.
        //    Because the tone sits inside the passband it passes almost untouched while
        //    the broadband noise is trimmed to the band, so a narrower "bandwidth"
        //    genuinely improves the copy (as on a real filter).
        var passband = new BandPassFilter(settings.Frequency, settings.NoiseBandwidthHz, settings.SampleRate);

        // 3. AGC (optional): aim to restore the signal to the volume level, with a fast
        //    attack and a slow release ("delay") so the noise floor swells between
        //    characters and ducks under a tone. maxGain caps how far quiet passages are
        //    boosted. The AGC sees only the passband signal (see step 4).
        double target = short.MaxValue * volumeLinear;
        AutomaticGainControl? agc = settings.AgcEnabled
            ? new AutomaticGainControl(settings.SampleRate, target, maxGain: 8.0, releaseSeconds: settings.AgcDelaySeconds)
            : null;

        // 4. APF (optional): a resonant peak at the tone, added AFTER the AGC and driven
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

    private static string CharToMorse(string morseChar)
    {
        return morseChar switch
        {
            "a" => ".-",
            "b" => "-...",
            "c" => "-.-.",
            "d" => "-..",
            "e" => ".",
            "f" => "..-.",
            "g" => "--.",
            "h" => "....",
            "i" => "..",
            "j" => ".---",
            "k" => "-.-",
            "l" => ".-..",
            "m" => "--",
            "n" => "-.",
            "o" => "---",
            "p" => ".--.",
            "q" => "--.-",
            "r" => ".-.",
            "s" => "...",
            "t" => "-",
            "u" => "..-",
            "v" => "...-",
            "w" => ".--",
            "x" => "-..-",
            "y" => "-.--",
            "z" => "--..",
            "1" => ".----",
            "2" => "..---",
            "3" => "...--",
            "4" => "....-",
            "5" => ".....",
            "6" => "-....",
            "7" => "--...",
            "8" => "---..",
            "9" => "----.",
            "0" => "-----",
            " " => " ",
            "/" => "-..-.",
            "=" => "-...-",
            "?" => "..--..",
            "+" => ".-.-.",
            "<ar>" => ".-.-.",
            "<as>" => ".-...",
            "<bk>" => "-...-.-",
            "<bt>" => "-...-",
            "<kn>" => "-.--.",
            "<sk>" => "...-.-",
            _ => ""
        };
    }
}