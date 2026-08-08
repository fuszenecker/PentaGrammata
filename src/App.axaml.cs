using System;
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

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_serviceProvider is null)
        {
            return;
        }

        try
        {
            // Block briefly so the final pending configuration save completes before the
            // process exits — an async-void handler would let shutdown race the flush and
            // drop the last save. The save chain runs on the threadpool (TaskScheduler.Default
            // with ConfigureAwait(false)), so blocking the UI thread here cannot deadlock it.
            // A short timeout bounds shutdown even if the store hangs.
            _serviceProvider.GetRequiredService<IConfigurationService>()
                .FlushAsync()
                .Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best-effort: a failed or timed-out flush must not prevent shutdown.
        }
        finally
        {
            _serviceProvider.Dispose();
        }
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
