using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PentaGrammata.Composition;
using PentaGrammata.Interfaces;
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

    private async void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_serviceProvider is null)
        {
            return;
        }

        await _serviceProvider.GetRequiredService<IConfigurationService>().FlushAsync();
        _serviceProvider.Dispose();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging();

        services.AddInfrastructure();
        services.AddStores();
        services.AddServices();
        services.AddViewModels();
    }
}
