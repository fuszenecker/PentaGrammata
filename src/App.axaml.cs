using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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

            _serviceProvider.GetRequiredService<IWindowSizeService>().Track(mainWindow);

            _serviceProvider.GetRequiredService<IWindowContext>().MainWindow = mainWindow;
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _serviceProvider?.Dispose();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging();

        services.AddSingleton<IDialogViewModelFactory, DialogViewModelFactory>();
        services.AddSingleton<IWindowContext, WindowContext>();
        services.AddSingleton<IAudioPlayer>(_ => AudioPlayerFactory.Create());
        services.AddSingleton<INoiseGeneratorFactory, NoiseGeneratorFactory>();
        services.AddSingleton<IMorsePlayer, MorsePlayer>();
        services.AddSingleton<IMorseGenerator, MorseGenerator>();

        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IWindowSizeStore, WindowSizeStore>();
        services.AddSingleton<IWindowSizeService, WindowSizeService>();
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
        services.AddSingleton<ITrendsDialogService, TrendsDialogService>();
        services.AddSingleton<IConfusionsDialogService, ConfusionsDialogService>();

        services.AddSingleton<MainWindowViewModel>();
    }
}