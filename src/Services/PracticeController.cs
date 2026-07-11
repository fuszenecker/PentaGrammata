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
    private readonly IMorseGenerator _morseGenerator;
    private readonly IPracticeSettingsValidator _settingsValidator;
    private readonly IPracticeResultEvaluator _resultEvaluator;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<PracticeController> _logger;
    private CancellationTokenSource? _cancellationTokenSource;

    public string LastGeneratedText { get; private set; } = string.Empty;
    public string LastReceivedText { get; private set; } = string.Empty;

    public int PracticeDurationMins
    {
        get => _configurationService.Current.Practice.DefaultDurationMins;
        set
        {
            if (_configurationService.Current.Practice.DefaultDurationMins == value)
                return;

            _configurationService.Current.Practice.DefaultDurationMins = value;
            _configurationService.RequestSave();
        }
    }

    public IReadOnlyList<KeyValuePair<string, string>> CharacterSets => _configurationService.Current.CharacterSets
        .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value))
        .ToList();

    public string SelectedCharacterSet
    {
        get => _configurationService.Current.Practice.DefaultCharacterSet;
        set
        {
            if (_configurationService.Current.Practice.DefaultCharacterSet == value)
                return;

            _configurationService.Current.Practice.DefaultCharacterSet = value;
            _configurationService.RequestSave();
        }
    }

    public bool IsPracticing { get; private set; }

    public bool IsResultSaved { get; set; }

    public PracticeController(
        IMorsePlayer morsePlayer,
        IMorseGenerator morseGenerator,
        IPracticeSettingsValidator settingsValidator,
        IPracticeResultEvaluator resultEvaluator,
        IConfigurationService configurationService,
        ILogger<PracticeController> logger)
    {
        _morsePlayer = morsePlayer;
        _morseGenerator = morseGenerator;
        _settingsValidator = settingsValidator;
        _resultEvaluator = resultEvaluator;
        _configurationService = configurationService;
        _logger = logger;

        var config = _configurationService.Current;
        var characterSets = new List<KeyValuePair<string, string>>();

        foreach (var characterSet in config.CharacterSets)
        {
            if (!string.IsNullOrWhiteSpace(characterSet.Value))
                characterSets.Add(new KeyValuePair<string, string>(characterSet.Key, characterSet.Value));
        }

        if (characterSets.Count == 0)
        {
            characterSets.Add(new KeyValuePair<string, string>("Default", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>"));
            config.CharacterSets["Default"] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>";
        }

        config.Practice.DefaultCharacterSet = config.Practice.DefaultCharacterSet ?? characterSets[0].Key;
    }

    public async Task StartAsync()
    {
        _logger.LogInformation("Starting practice session");
        _cancellationTokenSource = new CancellationTokenSource();
        IsPracticing = true;
        IsResultSaved = false;

        try
        {
            string characterSetCharacters = _configurationService.Current.CharacterSets.TryGetValue(SelectedCharacterSet, out var selectedCharacters)
                && !string.IsNullOrWhiteSpace(selectedCharacters)
                    ? selectedCharacters
                    : CharacterSets.FirstOrDefault().Value;

            if (string.IsNullOrWhiteSpace(characterSetCharacters))
                characterSetCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/+?=<bk><sk>";

            int numberOfGroups = (int)Math.Round(PracticeDurationMins * _configurationService.Current.Practice.AverageWpm * LengthCorrector);
            _logger.LogDebug("Generating {GroupCount} morse groups", numberOfGroups);
            
            string morseCode = _morseGenerator.GenerateGroupsOf5(characterSetCharacters, numberOfGroups);
            LastGeneratedText = morseCode;

            try
            {
                string morseCodeToPlay = "vvv = " + morseCode + " <ar>";
                var practice = _configurationService.Current.Practice;
                var audio = _configurationService.Current.Audio;
                var playbackSettings = new MorsePlaybackSettings
                {
                    CharacterWpm = practice.CharacterWpm,
                    AverageWpm = practice.AverageWpm,
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
        return _resultEvaluator.Evaluate(LastGeneratedText, LastReceivedText, _configurationService.Current.Practice.ErrorThreshold);
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

        var config = _configurationService.Current;
        config.Practice.DefaultDurationMins = settings.Practice.DefaultDurationMins;
        config.Practice.CharacterWpm = settings.Practice.CharacterWpm;
        config.Practice.AverageWpm = settings.Practice.AverageWpm;
        config.Audio.SampleRate = settings.Audio.SampleRate;
        config.Audio.Frequency = settings.Audio.Frequency;
        config.Audio.VolumeDb = settings.Audio.VolumeDb;
        config.Audio.BeepRampMs = settings.Audio.BeepRampMs;
        config.Audio.Noise = settings.Audio.Noise.Clone();
        config.Practice.DefaultCharacterSet = settings.Practice.DefaultCharacterSet;
        config.Practice.ErrorThreshold = settings.Practice.ErrorThreshold;

        config.CharacterSets.Clear();
        foreach (var item in settings.CharacterSets)
        {
            if (!string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            {
                config.CharacterSets[item.Key] = item.Value;
            }
        }

        _configurationService.RequestSave();
        error = string.Empty;
        return true;
    }
}