using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;
using PentaGrammata.Services;

namespace PentaGrammata;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var audioPlayer = new AudioPlayer();
        var morsePlayer = new MorsePlayer(audioPlayer);
        var morseGenerator = new MorseGenerator();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(morseGenerator, morsePlayer),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}