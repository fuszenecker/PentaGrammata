using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
        var practiceController = new PracticeController();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(practiceController),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}