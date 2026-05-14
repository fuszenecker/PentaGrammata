using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using AppConfig = PentaGrammata.Configuration.Configuration;
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
        var configRoot = BuildConfiguration();

        var appConfig = configRoot.Get<AppConfig>() ?? new AppConfig();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(new PracticeController(appConfig)),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        foreach (var userConfigPath in GetPerUserConfigPaths())
        {
            builder.AddJsonFile(userConfigPath, optional: true, reloadOnChange: true);
        }

        return builder.Build();
    }

    private static string[] GetPerUserConfigPaths()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return
            [
                Path.Combine(roaming, "PentaGrammata", "appsettings.json"),
                Path.Combine(local, "PentaGrammata", "appsettings.json")
            ];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configRoot = !string.IsNullOrWhiteSpace(xdgConfigHome)
                ? xdgConfigHome
                : Path.Combine(home, ".config");

            return
            [
                Path.Combine(configRoot, "PentaGrammata", "appsettings.json")
            ];
        }

        return [];
    }
}