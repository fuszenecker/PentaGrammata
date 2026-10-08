using System.Net;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PentaGrammata.Composition;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Presentation;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Tests.Headless;

// All domain, presentation and persistence services come from production composition.
// Only machine boundaries (audio device, HTTP server, native file selection and profile path)
// are controlled by the scenario driver.
internal sealed class ScenarioDesktop : IAsyncDisposable
{
    public static IStorageProvider? Storage { get; private set; }
    public ServiceProvider Services { get; }
    public MainWindow Main { get; }
    public MainWindowViewModel ViewModel => (MainWindowViewModel)Main.DataContext!;
    public IConfigurationService Configuration => Services.GetRequiredService<IConfigurationService>();
    public DeviceAudio Audio { get; } = new();
    public ScenarioHttp Http { get; } = new();
    public Queue<string?> SaveChoices { get; } = new();
    public Queue<string?> OpenChoices { get; } = new();
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "pentagrammata-scenario-" + Guid.NewGuid().ToString("N"));

    public ScenarioDesktop(Action<AppConfiguration>? configure = null)
    {
        Directory.CreateDirectory(DirectoryPath);
        var config = new AppConfiguration
        {
            Practice = new Practice { CustomText = "TEST", DefaultDurationMins = 1, DefaultCharacterSet = "Scenario", CharacterWpm = 20, AverageWpm = 15, ErrorThreshold = 5 },
            CharacterSets = new CharacterSets { ["Scenario"] = "A" },
            Audio = new Audio { SampleRate = 8000, Frequency = 650, VolumeDb = -12, BeepRampMs = 4 },
            UiPreferences = new UiPreferences { RevealSentTextAfterPractice = false },
        };
        configure?.Invoke(config);
        var paths = new ProfilePaths(DirectoryPath);
        File.WriteAllText(paths.PreferredUserConfigPath, JsonSerializer.Serialize(config));
        Storage = Substitute.For<IStorageProvider>();
        Storage.CanOpen.Returns(true);
        Storage.CanSave.Returns(true);
        Storage.SaveFilePickerAsync(Arg.Any<FilePickerSaveOptions>()).Returns(_ =>
            Task.FromResult(SaveChoices.Count == 0 ? null : FileChoice(SaveChoices.Dequeue())));
        Storage.OpenFilePickerAsync(Arg.Any<FilePickerOpenOptions>()).Returns(_ =>
        {
            var choice = OpenChoices.Count == 0 ? null : FileChoice(OpenChoices.Dequeue());
            return Task.FromResult<IReadOnlyList<IStorageFile>>(choice is null ? [] : [choice]);
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure();
        services.AddStores();
        services.AddServices();
        services.AddViewModels();
        services.AddSingleton<IAppPaths>(paths);
        services.AddSingleton<IAudioPlayer>(Audio);
        services.AddSingleton(new HttpClient(Http));
        Services = services.BuildServiceProvider();
        Main = new MainWindow { DataContext = Services.GetRequiredService<MainWindowViewModel>() };
        Services.GetRequiredService<IWindowContext>().MainWindow = Main;
        Services.GetRequiredService<IWindowSizeService>().Track(Main);
        Main.Show();
        Main.UpdateLayout();
    }

    private static IStorageFile? FileChoice(string? path)
    {
        if (path is null) return null;
        var file = Substitute.For<IStorageFile>();
        file.Name.Returns(Path.GetFileName(path));
        file.Path.Returns(new Uri(path));
        file.OpenWriteAsync().Returns(_ => Task.FromResult<Stream>(File.Create(path)));
        file.OpenReadAsync().Returns(_ => Task.FromResult<Stream>(File.OpenRead(path)));
        return file;
    }

    public static IEnumerable<T> Controls<T>(Window window) where T : class =>
        window.GetLogicalDescendants().OfType<T>().Concat(window.GetVisualDescendants().OfType<T>()).Distinct();

    public static Button Button(Window window, string caption) => Controls<Button>(window).Single(b => Equals(b.Content, caption));

    public static T Tip<T>(Window window, string prefix) where T : Control =>
        Controls<T>(window).Single(c => ToolTip.GetTip(c)?.ToString()?.StartsWith(prefix, StringComparison.Ordinal) == true);

    public static void Click(TopLevel window, Control control)
    {
        window.UpdateLayout();
        Assert.IsTrue(control.IsEffectivelyEnabled, $"Cannot operate disabled control {control}.");
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window);
        Assert.IsNotNull(point, "Control must be attached to the visible window.");
        window.MouseDown(point.Value, MouseButton.Left);
        window.MouseUp(point.Value, MouseButton.Left);
    }

    public static void Click(Window window, string caption) => Click(window, Button(window, caption));

    public void Menu(string caption)
    {
        var item = Controls<MenuItem>(Main).Single(m => Equals(m.Header, caption));
        Assert.IsTrue(item.IsEnabled, $"Menu item {caption} is disabled.");
        Assert.IsNotNull(item.Command);
        Assert.IsTrue(item.Command.CanExecute(item.CommandParameter));
        var parent = item.GetLogicalAncestors().OfType<MenuItem>().First();
        Click(Main, parent);
        var popup = TopLevel.GetTopLevel(item);
        Assert.IsNotNull(popup, $"The submenu containing {caption} must open.");
        Click(popup, item);
    }

    public static void Text(Window window, TextBox box, string text)
    {
        box.Focus();
        box.SelectAll();
        window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        window.KeyRelease(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        window.KeyTextInput(text);
    }

    public static void Tab(Window window, int index)
    {
        Controls<TabControl>(window).Single().SelectedIndex = index;
        window.UpdateLayout();
    }

    private IEnumerable<Window> Windows(Window owner) => owner.OwnedWindows.SelectMany(w => new[] { w }.Concat(Windows(w)));

    public async Task<T> Dialog<T>() where T : Window
    {
        T? dialog = null;
        await Until(() => (dialog = Windows(Main).OfType<T>().LastOrDefault(w => w.IsVisible)) is not null, $"open {typeof(T).Name}");
        dialog!.UpdateLayout();
        return dialog;
    }

    public static async Task Until(Func<bool> condition, string activity, int timeoutSeconds = 15)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        while (!condition())
        {
            try { await Task.Delay(10, timeout.Token); }
            catch (OperationCanceledException) { Assert.Fail($"Timed out waiting to {activity}."); }
        }
    }

    public async Task Practice(string? received, bool stop = false, bool failAudio = false)
    {
        Audio.Begin(failAudio);
        Click(Main, "Practice");
        await Until(() => Audio.Started || !ViewModel.Practice.IsPracticeOperationActive, "start audio playback");
        Assert.IsTrue(Audio.Started, "The real Morse renderer must reach the audio device.");
        if (received is not null) Text(Main, Main.FindControl<TextBox>("ReceivedTextBox")!,
            received == "@sent" ? Services.GetRequiredService<IPracticeController>().LastGeneratedText : received);
        if (stop) Click(Main, "Stop"); else Audio.Complete();
        await Until(() => !ViewModel.Practice.IsPracticeOperationActive, "finish practice");
    }

    public async Task<PracticeResultWindow> Result()
    {
        Click(Main, "Check result");
        return await Dialog<PracticeResultWindow>();
    }

    public async Task SaveResult(PracticeResultWindow result, bool suppress = true)
    {
        Click(result, "Save results");
        if (!Configuration.IsDialogSuppressed("ResultsSaved"))
        {
            var info = await Dialog<InfoDialog>();
            Assert.AreEqual("Results saved", info.Title);
            Click(info, suppress ? "Do not show again" : "OK");
        }
        await Until(() => !((PracticeResultWindowViewModel)result.DataContext!).IsSaving, "save results");
        Assert.IsFalse(Button(result, "Save results").IsEffectivelyEnabled);
    }

    public async Task CloseResult(PracticeResultWindow result)
    {
        result.Close();
        await Until(() => !ViewModel.Practice.CheckResultCommand.IsRunning, "close result dialog");
    }

    public async ValueTask DisposeAsync()
    {
        Audio.Complete();
        var windows = Windows(Main).ToArray();
        var pendingCommands = windows
            .Append(Main)
            .Select(w => w.DataContext)
            .OfType<object>()
            .Concat(windows.Append(Main).Select(w => w.DataContext).OfType<MainWindowViewModel>().Select(vm => vm.Practice))
            .SelectMany(AsyncCommands)
            .Distinct()
            .ToArray();
        foreach (var dialog in windows.Reverse()) dialog.Close();
        Main.Close();
        await Until(() => pendingCommands.All(command => !command.IsRunning), "finish pending window commands during cleanup");
        await Configuration.FlushAsync();
        await Services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        await DeleteProfileAsync();
        Storage = null;
    }

    private static IEnumerable<IAsyncRelayCommand> AsyncCommands(object viewModel) =>
        viewModel.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => typeof(IAsyncRelayCommand).IsAssignableFrom(property.PropertyType))
            .Select(property => property.GetValue(viewModel))
            .OfType<IAsyncRelayCommand>();

    private async Task DeleteProfileAsync()
    {
        const int attempts = 20;
        for (var attempt = 0; ; attempt++)
        {
            if (!Directory.Exists(DirectoryPath)) return;
            try
            {
                Directory.Delete(DirectoryPath, recursive: true);
                return;
            }
            catch (IOException) when (OperatingSystem.IsWindows() && attempt < attempts - 1)
            {
                // Windows reports sharing violations while a just-closed SQLite handle is
                // being released. All test commands have completed and pools were cleared;
                // retry briefly to cover that close window, then fail with the original path.
                SqliteConnection.ClearAllPools();
                await Task.Delay(100);
            }
            catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows() && attempt < attempts - 1)
            {
                SqliteConnection.ClearAllPools();
                await Task.Delay(100);
            }
        }
    }

    private sealed class ProfilePaths(string directory) : IAppPaths
    {
        public string AppDataDirectory => directory;
        public string PreferredUserConfigPath => Path.Combine(directory, "appsettings.json");
        public IReadOnlyList<string> UserConfigPaths => [PreferredUserConfigPath];
    }
}

internal sealed class DeviceAudio : IAudioPlayer
{
    private TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _fail;
    public bool Started { get; private set; }
    public int SampleRate { get; private set; }
    public short[] Samples { get; private set; } = [];
    public void Begin(bool fail) { Started = false; _fail = fail; _completion = new(TaskCreationOptions.RunContinuationsAsynchronously); }
    public void Complete() => _completion.TrySetResult();
    public async Task PlayAudioAsync(short[] audioData, int sampleRate, CancellationToken cancellationToken)
    {
        Samples = audioData;
        SampleRate = sampleRate;
        Started = true;
        await _completion.Task.WaitAsync(cancellationToken);
        if (_fail) throw new IOException("Scenario audio device disconnected");
    }
}

internal sealed class ScenarioHttp : HttpMessageHandler
{
    public string Version { get; set; } = "99.0.0.0";
    public bool Fail { get; set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Fail) throw new HttpRequestException("Scenario server unavailable");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { tag_name = Version, html_url = "https://example.test/release" })),
        });
    }
}

public sealed class HeadlessTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var factory = Substitute.For<IStorageProviderFactory>();
        factory.CreateProvider(Arg.Any<TopLevel>()).Returns(_ => ScenarioDesktop.Storage!);
        return AppBuilder.Configure<ScenarioApplication>()
            .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont().With(factory);
    }
}

public sealed class ScenarioApplication : Application
{
    public override void Initialize()
    {
        var app = new App();
        app.Initialize();
        var resources = app.Resources;
        app.Resources = new ResourceDictionary();
        Resources = resources;
        var styles = app.Styles.ToArray();
        app.Styles.Clear();
        foreach (var style in styles) Styles.Add(style);
    }
}
