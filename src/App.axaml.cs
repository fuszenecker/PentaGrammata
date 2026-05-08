using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var audioPlayer = new Services.AudioPlayer();

        const int sampleRate = 44100;
        const int frequency = 1000;

        var sampleData = Enumerable.Range(0, sampleRate)
            .Select(i => (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * short.MaxValue))
            .ToArray();

        audioPlayer.PlayAudio(sampleData, sampleRate);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}