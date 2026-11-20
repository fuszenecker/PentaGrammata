using System;
using System.Collections.Generic;
using System.Linq;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;

namespace PentaGrammata.Services;

public sealed class PracticeSettingsValidator : IPracticeSettingsValidator
{
    /// <summary>The largest step, in dB, the SNR and QSB auto-adjustment may take at once.</summary>
    public const double MaxAutoAdjustStepDb = 20;

    public bool TryValidate(AppConfig settings, out string error)
    {
        if (settings.Practice.DefaultDurationMins < 1)
        {
            error = "Default duration must be at least 1 minute.";
            return false;
        }

        if (settings.Practice.CharacterWpm < 1 || settings.Practice.AverageWpm < 1)
        {
            error = "Character and average WPM must be positive values.";
            return false;
        }

        if (settings.Practice.AverageWpm > settings.Practice.CharacterWpm)
        {
            error = "Average WPM cannot exceed character WPM.";
            return false;
        }

        if (settings.Practice.AutoAdjustWindowSize < 1)
        {
            error = "Auto-adjust window size must be at least 1.";
            return false;
        }

        if (settings.Practice.AutoAdjustSnrStepDb is not (> 0 and <= MaxAutoAdjustStepDb))
        {
            error = $"The auto-adjust SNR step must be greater than 0 and at most {MaxAutoAdjustStepDb} dB.";
            return false;
        }

        if (settings.Practice.AutoAdjustQsbStepDb is not (> 0 and <= MaxAutoAdjustStepDb))
        {
            error = $"The auto-adjust QSB step must be greater than 0 and at most {MaxAutoAdjustStepDb} dB.";
            return false;
        }

        if (settings.Audio.SampleRate < 8000)
        {
            error = "Sample rate must be at least 8000.";
            return false;
        }

        if (settings.Audio.Frequency <= 0)
        {
            error = "Frequency must be greater than 0.";
            return false;
        }

        if (settings.Audio.VolumeDb > 0)
        {
            error = "Volume must be 0 dBFS or lower.";
            return false;
        }

        if (settings.Audio.BeepRampMs < 0)
        {
            error = "Beep ramp must be 0 or greater.";
            return false;
        }

        if (settings.Audio.Noise.Type != NoiseType.None)
        {
            var noise = settings.Audio.Noise;

            if (noise.BandwidthHz <= 0)
            {
                error = "Noise filter width must be greater than 0.";
                return false;
            }

            if (noise.AgcEnabled && noise.AgcDelaySeconds <= 0)
            {
                error = "AGC delay must be greater than 0.";
                return false;
            }

            if (noise.AgcEnabled && noise.AgcMaxGainDb < 0)
            {
                error = "AGC max gain must be 0 dB or higher.";
                return false;
            }

            if (noise.ApfEnabled && noise.ApfBandwidthHz <= 0)
            {
                error = "APF bandwidth must be greater than 0.";
                return false;
            }
        }

        // QSB is a property of the signal, not the receiver chain, so it is validated
        // independently of the noise type.
        if (settings.Audio.Noise.QsbEnabled)
        {
            if (settings.Audio.Noise.QsbDepthDb <= 0)
            {
                error = "QSB fade depth must be greater than 0.";
                return false;
            }

            if (settings.Audio.Noise.QsbPeriodSeconds <= 0)
            {
                error = "QSB fade period must be greater than 0.";
                return false;
            }
        }

        if (settings.Practice.ErrorThreshold < 0 || settings.Practice.ErrorThreshold > 100)
        {
            error = "Error rate threshold must be between 0 and 100.";
            return false;
        }

        // Every token of the custom text has to be something the player can actually send.
        var customText = CustomTextNormalizer.Normalize(settings.Practice.CustomText);
        var unsendable = FirstUnsendable(customText);
        if (unsendable is not null)
        {
            error = $"Custom text contains something that cannot be sent as Morse code: \"{unsendable}\".";
            return false;
        }

        if (!Enum.IsDefined(settings.Practice.Topic))
        {
            error = "Unknown practice topic.";
            return false;
        }

        if (settings.Practice.Topic == PracticeTopic.CustomText && customText.Length == 0)
        {
            error = "The custom text topic needs a custom text.";
            return false;
        }

        var topics = settings.Topics ?? new Topics();
        if (!TryValidateCallsigns(topics.CallsignPatternsEmea, "EMEA callsign", out error)
            || !TryValidateCallsigns(topics.CallsignPatternsGlobal, "Global callsign", out error)
            || !TryValidateWords(topics.Cities, "City", out error)
            || !TryValidateWords(topics.Names, "Name", out error)
            || !TryValidateWords(topics.Abbreviations, "Abbreviation", out error))
        {
            return false;
        }

        var repetitions = topics.Repetitions ?? new TopicRepetitions();
        if (!TryValidateRepetition(repetitions.CallsignsEmea, "EMEA callsigns", out error)
            || !TryValidateRepetition(repetitions.CallsignsGlobal, "global callsigns", out error)
            || !TryValidateRepetition(repetitions.Cities, "cities", out error)
            || !TryValidateRepetition(repetitions.Names, "names", out error)
            || !TryValidateRepetition(repetitions.Abbreviations, "abbreviations", out error)
            || !TryValidateRepetition(repetitions.CustomText, "custom text", out error))
        {
            return false;
        }

        var requiredList = settings.Practice.Topic switch
        {
            PracticeTopic.CallsignsEmea => (topics.CallsignPatternsEmea, "EMEA callsign patterns"),
            PracticeTopic.CallsignsGlobal => (topics.CallsignPatternsGlobal, "global callsign patterns"),
            PracticeTopic.Cities => (topics.Cities, "cities"),
            PracticeTopic.Names => (topics.Names, "names"),
            PracticeTopic.Abbreviations => (topics.Abbreviations, "abbreviations"),
            _ => ((List<string>?)null, string.Empty),
        };
        if (requiredList.Item1 is not null && !requiredList.Item1.Any(i => !string.IsNullOrWhiteSpace(i)))
        {
            error = $"The selected topic needs at least one entry in the list of {requiredList.Item2}.";
            return false;
        }

        if (settings.CharacterSets.Count == 0)
        {
            error = "At least one character set is required.";
            return false;
        }

        if (settings.CharacterSets.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
        {
            error = "Character set names and values must be non-empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.Practice.DefaultCharacterSet) || !settings.CharacterSets.ContainsKey(settings.Practice.DefaultCharacterSet))
        {
            error = "Default character set must match one of the configured character set names.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string? FirstUnsendable(string text) =>
        MorseAlphabet.Tokenize(text).FirstOrDefault(t => t != " " && !MorseAlphabet.Supports(t));

    private static bool TryValidateWords(IEnumerable<string>? words, string label, out string error)
    {
        foreach (var word in words ?? [])
        {
            var unsendable = FirstUnsendable(PracticeItems.NormalizeEntry(word));
            if (unsendable is not null)
            {
                error = $"{label} \"{word}\" contains something that cannot be sent as Morse code: \"{unsendable}\".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateRepetition(Repetition? repetition, string label, out string error)
    {
        int minimum = repetition?.Minimum ?? 1;
        int maximum = repetition?.Maximum ?? 1;
        if (minimum < 1 || maximum > Repetition.Limit || minimum > maximum)
        {
            error = $"The repetition of the {label} must be between 1 and {Repetition.Limit}, the minimum not above the maximum.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateCallsigns(IEnumerable<string>? patterns, string label, out string error)
    {
        foreach (var pattern in patterns ?? [])
        {
            var trimmed = pattern.Trim();
            if (trimmed.Any(char.IsWhiteSpace))
            {
                error = $"{label} pattern \"{trimmed}\" must not contain spaces.";
                return false;
            }

            // # and lowercase letters are placeholders; everything else is sent as written.
            var literal = new string(trimmed.Where(c => c != '#' && c is not (>= 'a' and <= 'z')).ToArray());
            var unsendable = FirstUnsendable(literal);
            if (unsendable is not null)
            {
                error = $"{label} pattern \"{trimmed}\" contains something that cannot be sent as Morse code: \"{unsendable}\".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
