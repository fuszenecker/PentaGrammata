using System.Linq;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;

namespace PentaGrammata.Services;

public sealed class PracticeSettingsValidator : IPracticeSettingsValidator
{
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

            if (noise.ApfEnabled && noise.ApfBandwidthHz <= 0)
            {
                error = "APF bandwidth must be greater than 0.";
                return false;
            }
        }

        if (settings.Practice.ErrorThreshold < 0 || settings.Practice.ErrorThreshold > 100)
        {
            error = "Error rate threshold must be between 0 and 100.";
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
}
