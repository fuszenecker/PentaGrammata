using PentaGrammata.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

public interface IPracticeController
{
    int PracticeDurationMins { get; set; }

    IReadOnlyList<KeyValuePair<string, string>> CharacterSets { get; }

    string SelectedCharacterSet { get; set; }

    /// <summary>What the next session sends (persisted as <c>Practice.Topic</c>).</summary>
    PentaGrammata.Configuration.PracticeTopic SelectedTopic { get; set; }

    string LastGeneratedText { get; }

    /// <summary>
    /// Character WPM used by the most recent session. Reflects the in-memory dynamic WPM
    /// when auto-adjust is enabled, otherwise the configured WPM.
    /// </summary>
    int LastUsedCharacterWpm { get; }

    /// <summary>
    /// Average (Farnsworth) WPM used by the most recent session. Reflects the in-memory
    /// dynamic WPM when auto-adjust is enabled, otherwise the configured WPM.
    /// </summary>
    int LastUsedAverageWpm { get; }

    /// <summary>
    /// Character WPM the next session will use (the current dynamic value when auto-adjust
    /// is enabled, otherwise the configured value).
    /// </summary>
    int CurrentCharacterWpm { get; }

    /// <summary>
    /// Average (Farnsworth) WPM the next session will use (the current dynamic value when
    /// auto-adjust is enabled, otherwise the configured value).
    /// </summary>
    int CurrentAverageWpm { get; }

    /// <summary>
    /// Receiver settings used by the most recent session: the configured ones with the dynamic
    /// signal-to-noise ratio and QSB depth in place when they auto-adjust. A copy.
    /// </summary>
    NoiseSettings LastUsedNoise { get; }

    /// <summary>Signal-to-noise ratio (dB) the next session will use, or null without noise.</summary>
    double? CurrentSnrDb { get; }

    /// <summary>Deepest QSB fade (dB) the next session will use, or null without QSB.</summary>
    double? CurrentQsbDepthDb { get; }

    Task StartAsync();

    void Stop();

    PracticeResult BuildResult(string receivedText);

    Task<PracticeResult> BuildResultAsync(string receivedText);

    /// <summary>
    /// Feeds the saved result of the current session to the auto-adjusting WPM (when it is
    /// on). Only a saved result moves the dynamic WPM, at most once per session.
    /// </summary>
    void RecordSavedResult(PracticeResult result);

    AppConfig CreateSettingsSnapshot();

    bool TryApplySettings(AppConfig settings, out string error);
}
