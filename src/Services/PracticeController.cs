using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Players;

namespace PentaGrammata.Services;

public class PracticeController : IPracticeController
{
    private const double LengthCorrector = 0.695;

    private readonly IMorsePlayer _morsePlayer;
    private readonly IMorseGenerator _morseGenerator;
    private readonly IPracticeSettingsValidator _settingsValidator;
    private readonly IPracticeResultEvaluator _resultEvaluator;
    private readonly IConfigurationService _configurationService;
    private readonly IDynamicWpmAdjuster _dynamicWpmAdjuster;
    private readonly ILogger<PracticeController> _logger;
    private CancellationTokenSource? _cancellationTokenSource;

    // True once the current session's error rate has been fed to the dynamic WPM adjuster, so
    // repeated BuildResult calls for the same session never stack adjustments.
    private bool _sessionResultRecorded;

    // Captured at the start of each session so the result window/statistics record reflects
    // the WPM actually used, even after the post-session adjustment moves the dynamic values.
    private int _lastUsedCharacterWpm;
    private int _lastUsedAverageWpm;

    public string LastGeneratedText { get; private set; } = string.Empty;
    public string LastReceivedText { get; private set; } = string.Empty;

    public int LastUsedCharacterWpm => _lastUsedCharacterWpm;
    public int LastUsedAverageWpm => _lastUsedAverageWpm;

    /// <summary>
    /// Character WPM that the next session will use: the dynamic value when auto-adjust is
    /// on, otherwise the configured value.
    /// </summary>
    public int CurrentCharacterWpm => _configurationService.Current.Practice.AutoAdjustWpm
        ? _dynamicWpmAdjuster.DynamicCharacterWpm
        : _configurationService.Current.Practice.CharacterWpm;

    /// <summary>
    /// Average (Farnsworth) WPM that the next session will use: the dynamic value when
    /// auto-adjust is on, otherwise the configured value.
    /// </summary>
    public int CurrentAverageWpm => _configurationService.Current.Practice.AutoAdjustWpm
        ? _dynamicWpmAdjuster.DynamicAverageWpm
        : _configurationService.Current.Practice.AverageWpm;

    /// <summary>
    /// Restarts the in-memory dynamic WPM from the configured values. Called on construction
    /// and whenever settings are applied.
    /// </summary>
    private void ResetDynamicWpm()
    {
        var practice = _configurationService.Current.Practice;
        _dynamicWpmAdjuster.Reset(practice.CharacterWpm, practice.AverageWpm);
        _sessionResultRecorded = false;
    }

    public int PracticeDurationMins
    {
        get => _configurationService.Current.Practice.DefaultDurationMins;
        set => _configurationService.SetPracticeDuration(value);
    }

    public IReadOnlyList<KeyValuePair<string, string>> CharacterSets => _configurationService.Current.CharacterSets
        .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value))
        .ToList();

    public string SelectedCharacterSet
    {
        get => _configurationService.Current.Practice.DefaultCharacterSet;
        set => _configurationService.SetSelectedCharacterSet(value);
    }

    public bool IsPracticing { get; private set; }

    public PracticeController(
        IMorsePlayer morsePlayer,
        IMorseGenerator morseGenerator,
        IPracticeSettingsValidator settingsValidator,
        IPracticeResultEvaluator resultEvaluator,
        IConfigurationService configurationService,
        IDynamicWpmAdjuster dynamicWpmAdjuster,
        ILogger<PracticeController> logger)
    {
        _morsePlayer = morsePlayer;
        _morseGenerator = morseGenerator;
        _settingsValidator = settingsValidator;
        _resultEvaluator = resultEvaluator;
        _configurationService = configurationService;
        _dynamicWpmAdjuster = dynamicWpmAdjuster;
        _logger = logger;

        // Configuration invariants (at least one character set, a non-null default
        // selection) are established by the configuration owner on load, so this
        // orchestrator only needs to seed its in-memory dynamic WPM state.
        ResetDynamicWpm();
    }

    public async Task StartAsync()
    {
        _logger.LogInformation("Starting practice session");
        _cancellationTokenSource = new CancellationTokenSource();
        IsPracticing = true;
        _sessionResultRecorded = false;

        // Capture the WPM used for this session before any post-session adjustment can move
        // the dynamic values, so the result window and statistics record stay accurate.
        _lastUsedCharacterWpm = CurrentCharacterWpm;
        _lastUsedAverageWpm = CurrentAverageWpm;

        try
        {
            // A configured custom text overrides generation entirely: the user hears exactly
            // that text (duration and character set no longer apply).
            var customText = CustomTextNormalizer.Normalize(_configurationService.Current.Practice.CustomText);
            string morseCode;

            if (customText.Length > 0)
            {
                _logger.LogDebug("Using configured custom text");
                morseCode = customText;
            }
            else
            {
                string characterSetCharacters = _configurationService.Current.CharacterSets.TryGetValue(SelectedCharacterSet, out var selectedCharacters)
                    && !string.IsNullOrWhiteSpace(selectedCharacters)
                        ? selectedCharacters
                        : CharacterSets.FirstOrDefault().Value;

                if (string.IsNullOrWhiteSpace(characterSetCharacters))
                    characterSetCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>";

                int numberOfGroups = (int)Math.Round(PracticeDurationMins * CurrentAverageWpm * LengthCorrector);
                _logger.LogDebug("Generating {GroupCount} morse groups", numberOfGroups);

                morseCode = _morseGenerator.GenerateGroupsOf5(characterSetCharacters, numberOfGroups);
            }

            LastGeneratedText = morseCode;

            try
            {
                string morseCodeToPlay = "vvv = " + morseCode + " <ar>";
                var audio = _configurationService.Current.Audio;
                var playbackSettings = new MorsePlaybackSettings
                {
                    CharacterWpm = _lastUsedCharacterWpm,
                    AverageWpm = _lastUsedAverageWpm,
                    SampleRate = audio.SampleRate,
                    Frequency = audio.Frequency,
                    VolumeDb = audio.VolumeDb,
                    BeepRampMs = audio.BeepRampMs,
                    NoiseType = audio.Noise.Type,
                    NoiseLevelDb = audio.Noise.LevelDb,
                    NoiseBandwidthHz = audio.Noise.BandwidthHz,
                    AgcEnabled = audio.Noise.AgcEnabled,
                    AgcDelaySeconds = audio.Noise.AgcDelaySeconds,
                    ApfEnabled = audio.Noise.ApfEnabled,
                    ApfBandwidthHz = audio.Noise.ApfBandwidthHz,
                    ApfPeakGainDb = audio.Noise.ApfPeakGainDb,
                };

                await _morsePlayer.PlayMorseCodeAsync(
                    morseCodeToPlay,
                    playbackSettings,
                    _cancellationTokenSource.Token);
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Practice session cancelled");
                throw;
            }
        }
        finally
        {
            IsPracticing = false;
            _logger.LogInformation("Practice session finished");
        }
    }

    public void Stop()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
    }

    public PracticeResult BuildResult(string receivedText)
    {
        LastReceivedText = receivedText ?? string.Empty;
        var practice = _configurationService.Current.Practice;
        var result = _resultEvaluator.Evaluate(LastGeneratedText, LastReceivedText, practice.ErrorThreshold);

        // Adjust the in-memory dynamic WPM once per session, only when auto-adjust is on.
        // Repeated BuildResult calls for the same session (e.g. reopening the result window)
        // must not re-record the same error rate or stack adjustments.
        if (practice.AutoAdjustWpm && !_sessionResultRecorded)
        {
            _sessionResultRecorded = true;
            _dynamicWpmAdjuster.Adjust(result.ErrorRatePercent, practice.ErrorThreshold, practice.AutoAdjustWindowSize);
        }

        return result;
    }

    public AppConfig CreateSettingsSnapshot()
    {
        return _configurationService.Current.Clone();
    }

    public bool TryApplySettings(AppConfig settings, out string error)
    {
        if (!_settingsValidator.TryValidate(settings, out error))
        {
            return false;
        }

        // The configuration owner performs the wholesale copy and persists; this orchestrator
        // only resets its in-memory dynamic WPM state from the newly configured values.
        _configurationService.ApplyPracticeSettings(settings);
        error = string.Empty;
        ResetDynamicWpm();
        return true;
    }
}