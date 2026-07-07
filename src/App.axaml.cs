using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PentaGrammata.Interfaces;
using PentaGrammata.Services;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += OnDesktopExit;

            var mainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>(),
            };

            _serviceProvider.GetRequiredService<IWindowContext>().MainWindow = mainWindow;
            desktop.MainWindow = mainWindow;

            // Surface configuration persistence failures instead of letting them be
            // swallowed by the fire-and-forget save paths.
            _serviceProvider.GetRequiredService<IConfigurationService>().SaveFailed += OnConfigurationSaveFailed;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnConfigurationSaveFailed(object? sender, Exception exception)
    {
        var infoDialogService = _serviceProvider?.GetService<IInfoDialogService>();
        if (infoDialogService is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _ = infoDialogService.ShowInfoAsync(
            "Settings not saved",
            $"Your settings could not be saved:\n{exception.Message}"));
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        var configurationService = _serviceProvider?.GetService<IConfigurationService>();
        if (configurationService is not null)
        {
            configurationService.SaveFailed -= OnConfigurationSaveFailed;
        }

        _serviceProvider?.Dispose();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging();

        services.AddSingleton<IWindowContext, WindowContext>();
        services.AddSingleton<IAudioPlayer>(_ => AudioPlayerFactory.Create());
        services.AddSingleton<IMorsePlayer, MorsePlayer>();
        services.AddSingleton<IMorseGenerator, MorseGenerator>();

        services.AddSingleton<IConfigurationStore, ConfigurationStore>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IPracticeSettingsValidator, PracticeSettingsValidator>();
        services.AddSingleton<IPracticeResultEvaluator, PracticeResultEvaluator>();
        services.AddSingleton<IPracticeResultStatisticsStore, PracticeResultStatisticsStore>();
        services.AddSingleton<IPracticeController, PracticeController>();
        services.AddSingleton<IInfoDialogService, InfoDialogService>();

        services.AddSingleton<IMorseSettingsDialogService, MorseSettingsDialogService>();
        services.AddSingleton<IUiSettingsDialogService, UiSettingsDialogService>();
        services.AddSingleton<IPracticeResultWindowService, PracticeResultWindowService>();
        services.AddSingleton<IAboutDialogService, AboutDialogService>();

        services.AddSingleton<MainWindowViewModel>();
    }
}