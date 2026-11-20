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

namespace PentaGrammata.Services;

public class PracticeController : IPracticeController
{
    private const double LengthCorrector = 0.695;

    private readonly IMorsePlayer _morsePlayer;
    private readonly IPracticeTextGeneratorFactory _textGeneratorFactory;
    private readonly IPracticeSettingsValidator _settingsValidator;
    private readonly IPracticeResultEvaluator _resultEvaluator;
    private readonly IConfigurationService _configurationService;
    private readonly IDynamicDifficultyAdjuster _difficulty;
    private readonly ILogger<PracticeController> _logger;
    private CancellationTokenSource? _cancellationTokenSource;

    // True once the current session's saved error rate has been fed to the dynamic difficulty
    // adjuster, so a session never stacks adjustments.
    private bool _sessionResultRecorded;

    // Captured at the start of each session so the result window/statistics record reflects
    // the WPM actually used, even after the post-session adjustment moves the dynamic values.
    private int _lastUsedCharacterWpm;
    private int _lastUsedAverageWpm;
    private NoiseSettings _lastUsedNoise = new();

    public string LastGeneratedText { get; private set; } = string.Empty;
    public string LastReceivedText { get; private set; } = string.Empty;

    public int LastUsedCharacterWpm => _lastUsedCharacterWpm;
    public int LastUsedAverageWpm => _lastUsedAverageWpm;

    /// <summary>
    /// The receiver settings the most recent session actually played with: the configured ones,
    /// with the dynamic signal-to-noise ratio and QSB depth in place when they auto-adjust.
    /// </summary>
    public NoiseSettings LastUsedNoise => _lastUsedNoise.Clone();

    /// <summary>
    /// Signal-to-noise ratio, in dB, the next session will use, or null when there is no noise:
    /// the dynamic value when it auto-adjusts, otherwise the configured one.
    /// </summary>
    public double? CurrentSnrDb
    {
        get
        {
            var noise = _configurationService.Current.Audio.Noise;
            if (noise.Type == NoiseType.None) return null;
            return _configurationService.Current.Practice.AutoAdjustSnr ? _difficulty.DynamicSnrDb : -noise.LevelDb;
        }
    }

    /// <summary>
    /// Deepest QSB fade, in dB, the next session will use, or null when QSB is off: the dynamic
    /// value when it auto-adjusts, otherwise the configured one.
    /// </summary>
    public double? CurrentQsbDepthDb
    {
        get
        {
            var noise = _configurationService.Current.Audio.Noise;
            if (!noise.QsbEnabled) return null;
            return _configurationService.Current.Practice.AutoAdjustQsb ? _difficulty.DynamicQsbDepthDb : noise.QsbDepthDb;
        }
    }

    /// <summary>The configured receiver settings with the levels the next session will use.</summary>
    private NoiseSettings CurrentNoise()
    {
        var noise = _configurationService.Current.Audio.Noise.Clone();
        if (CurrentSnrDb is { } snr) noise.LevelDb = -snr;
        if (CurrentQsbDepthDb is { } depth) noise.QsbDepthDb = depth;
        return noise;
    }

    /// <summary>
    /// What a saved session's decision moves: each quantity whose auto-adjust is on, the levels
    /// only while their noise or QSB is active (a level nobody hears is left alone).
    /// </summary>
    private AutoAdjustTargets CurrentTargets()
    {
        var config = _configurationService.Current;
        var practice = config.Practice;
        return new AutoAdjustTargets
        {
            Wpm = practice.AutoAdjustWpm,
            Snr = practice.AutoAdjustSnr && config.Audio.Noise.Type != NoiseType.None,
            SnrStepDb = practice.AutoAdjustSnrStepDb,
            Qsb = practice.AutoAdjustQsb && config.Audio.Noise.QsbEnabled,
            QsbStepDb = practice.AutoAdjustQsbStepDb,
        };
    }

    /// <summary>
    /// Character WPM that the next session will use: the dynamic value when auto-adjust is
    /// on, otherwise the configured value.
    /// </summary>
    public int CurrentCharacterWpm => _configurationService.Current.Practice.AutoAdjustWpm
        ? _difficulty.DynamicCharacterWpm
        : _configurationService.Current.Practice.CharacterWpm;

    /// <summary>
    /// Average (Farnsworth) WPM that the next session will use: the dynamic value when
    /// auto-adjust is on, otherwise the configured value.
    /// </summary>
    public int CurrentAverageWpm => _configurationService.Current.Practice.AutoAdjustWpm
        ? _difficulty.DynamicAverageWpm
        : _configurationService.Current.Practice.AverageWpm;

    /// <summary>
    /// Restarts the in-memory dynamic speed and levels from the configured values. Called on
    /// construction and whenever settings are applied.
    /// </summary>
    private void ResetDynamicDifficulty()
    {
        var practice = _configurationService.Current.Practice;
        var noise = _configurationService.Current.Audio.Noise;
        _difficulty.Reset(practice.CharacterWpm, practice.AverageWpm, -noise.LevelDb, noise.QsbDepthDb);
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

    public PracticeTopic SelectedTopic
    {
        get => _configurationService.Current.Practice.Topic;
        set => _configurationService.SetSelectedTopic(value);
    }

    public bool IsPracticing { get; private set; }

    public PracticeController(
        IMorsePlayer morsePlayer,
        IPracticeTextGeneratorFactory textGeneratorFactory,
        IPracticeSettingsValidator settingsValidator,
        IPracticeResultEvaluator resultEvaluator,
        IConfigurationService configurationService,
        IDynamicDifficultyAdjuster difficulty,
        ILogger<PracticeController> logger)
    {
        _morsePlayer = morsePlayer;
        _textGeneratorFactory = textGeneratorFactory;
        _settingsValidator = settingsValidator;
        _resultEvaluator = resultEvaluator;
        _configurationService = configurationService;
        _difficulty = difficulty;
        _logger = logger;

        // Configuration invariants (at least one character set, a non-null default
        // selection) are established by the configuration owner on load, so this
        // orchestrator only needs to seed its in-memory dynamic difficulty.
        ResetDynamicDifficulty();
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
        _lastUsedNoise = CurrentNoise();

        try
        {
            // The topic decides what is sent; its generator is built from the live configuration.
            var config = _configurationService.Current;
            int numberOfGroups = (int)Math.Round(PracticeDurationMins * CurrentAverageWpm * LengthCorrector);
            _logger.LogDebug("Generating {Topic} text worth {GroupCount} groups", config.Practice.Topic, numberOfGroups);
            string morseCode = _textGeneratorFactory
                .Create(config.Practice.Topic, config)
                .Generate(numberOfGroups);

            // Each item is checked, revealed and saved once; its repetitions are only played.
            LastGeneratedText = PracticeItems.ToChecked(morseCode);

            try
            {
                // Every repetition is played; the underscore joining the words of a multi-word
                // item and the bar joining the repetitions are ordinary word gaps.
                string morseCodeToPlay = "vvv = " + PracticeItems.ToPlayback(morseCode) + " <ar>";
                var audio = _configurationService.Current.Audio;
                var playbackSettings = MorsePlaybackSettings.From(audio, _lastUsedCharacterWpm, _lastUsedAverageWpm, _lastUsedNoise);

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
        return _resultEvaluator.Evaluate(LastGeneratedText, LastReceivedText, practice.ErrorThreshold);
    }

    public async Task<PracticeResult> BuildResultAsync(string receivedText)
    {
        LastReceivedText = receivedText ?? string.Empty;
        var sentText = LastGeneratedText;
        var received = LastReceivedText;
        var threshold = _configurationService.Current.Practice.ErrorThreshold;
        return await Task.Run(() => _resultEvaluator.Evaluate(sentText, received, threshold));
    }

    public void RecordSavedResult(PracticeResult result)
    {
        // The dynamic difficulty moves when a session's result is saved, never when it is merely
        // checked: an unsaved session does not count, and a session counts only once.
        var practice = _configurationService.Current.Practice;
        var targets = CurrentTargets();
        if (targets.Any && !_sessionResultRecorded)
        {
            _sessionResultRecorded = true;
            _difficulty.Adjust(result.ErrorRatePercent, practice.ErrorThreshold, practice.AutoAdjustWindowSize, targets);
        }
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
        // only resets its in-memory dynamic difficulty from the newly configured values.
        _configurationService.ApplyPracticeSettings(settings);
        error = string.Empty;
        ResetDynamicDifficulty();
        return true;
    }
}
