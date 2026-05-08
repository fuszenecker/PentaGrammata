using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace PentaGrammata.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string greeting = "Welcome to Avalonia!";

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private List<string> characterPalettes;

    [ObservableProperty]
    private int practiceDuration = 5;

    public MainWindowViewModel()
    {
        CharacterPalettes = new List<string>
        {
            "Palette 1",
            "Palette 2",
            "Palette 3"
        };
    }
}
