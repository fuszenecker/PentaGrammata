using AppConfig = PentaGrammata.Configuration.AppConfiguration;
using PentaGrammata.Configuration;
using PentaGrammata.Models;
using PentaGrammata.ViewModels;

namespace PentaGrammata.Presentation;

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
        bool alreadySaved,
        double errorThresholdPercent,
        NoiseSettings noise);

    UiSettingsDialogViewModel CreateUiSettings(UiPreferences current);

    AboutWindowViewModel CreateAbout();

    TrendsDialogViewModel CreateTrends();

    ConfusionsDialogViewModel CreateConfusions();
}
