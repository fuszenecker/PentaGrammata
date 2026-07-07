using AppConfig = PentaGrammata.Configuration.Configuration;
using PentaGrammata.Configuration;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Interfaces;

/// <summary>
/// Creates dialog view models, supplying their container-injected dependencies so
/// that dialog services only pass runtime arguments. Centralizes object
/// composition without reflection: each method constructs its view model
/// explicitly, so dependency wiring is compile-checked and greppable.
/// </summary>
public interface IDialogViewModelFactory
{
    MorseSettingsDialogViewModel CreateMorseSettings(AppConfig currentSettings);

    PracticeResultWindowViewModel CreatePracticeResult(
        PracticeResult result,
        int characterWpm,
        int averageWpm,
        bool alreadySaved);

    UiSettingsDialogViewModel CreateUiSettings(UiPreferences current);

    AboutWindowViewModel CreateAbout();
}
