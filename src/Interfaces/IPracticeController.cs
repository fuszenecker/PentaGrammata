using System.Collections.Generic;
using System.Threading.Tasks;
using AppConfig = PentaGrammata.Configuration.Configuration;
using PentaGrammata.Models;

namespace PentaGrammata.Interfaces;

public interface IPracticeController
{
    int PracticeDurationMins { get; set; }

    List<KeyValuePair<string, string>> CharacterSets { get; }

    string SelectedCharacterSet { get; set; }

    Task StartAsync();

    void Stop();

    PracticeResult BuildResult(string receivedText);

    AppConfig CreateSettingsSnapshot();

    bool TryApplySettings(AppConfig settings, out string error);
}