using Avalonia.Headless;
using PentaGrammata.Configuration;

namespace PentaGrammata.Tests.Headless;

internal static class ScenarioRunner
{
    // A single UI dispatcher, with Avalonia's per-dispatch application isolation. Each
    // journey still gets its own production service container, windows and profile.
    private static readonly HeadlessUnitTestSession Session =
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ScenarioRunner).Assembly);

    // How many times a journey is started when Avalonia fails before the journey begins.
    internal const int SetupAttempts = 3;

    public static Task Run(Func<ScenarioDesktop, Task> journey, Action<AppConfiguration>? arrangeProfile = null) =>
        RetryBeforeStart(onStarted => RunOnce(onStarted, journey, arrangeProfile));

    /// <summary>
    /// Runs <paramref name="runOnce"/> (which calls the action it is given once the journey
    /// starts) and starts it again, up to <see cref="SetupAttempts"/> times in all, only when it
    /// failed with an <see cref="InvalidOperationException"/> before the journey started.
    /// </summary>
    /// <remarks>
    /// Avalonia's per-dispatch isolation occasionally fails to set up the isolated application
    /// ("The calling thread cannot access this object because a different thread owns it",
    /// from the headless platform's render loop) before the journey has run a single line.
    /// Nothing of the journey ran, so starting it again is safe. A failure inside the journey,
    /// an assertion included, is never retried.
    /// </remarks>
    internal static async Task RetryBeforeStart(Func<Action, Task> runOnce)
    {
        for (int attempt = 1; ; attempt++)
        {
            bool started = false;
            try
            {
                await runOnce(() => started = true).ConfigureAwait(false);
                return;
            }
            catch (InvalidOperationException error) when (!started && attempt < SetupAttempts)
            {
                Console.WriteLine($"Headless setup failed before the journey started (attempt {attempt}), retrying: {error.Message}");
            }
        }
    }

    private static async Task RunOnce(Action onStarted, Func<ScenarioDesktop, Task> journey, Action<AppConfiguration>? arrangeProfile)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var cancellationToken = timeout.Token;
        // Explicit TResult selects the asynchronous overload; async void would lose failures.
        var dispatch = Session.Dispatch<bool>(async () =>
        {
            onStarted();
            await using var app = new ScenarioDesktop(arrangeProfile);
            await journey(app);
            return true;
        }, cancellationToken);
        // Avalonia completes dispatch inline on its UI thread. Bridge through the pool
        // so MSTest's next test/assembly cleanup cannot run inside that dispatcher.
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = dispatch.ContinueWith(completed =>
        {
            if (completed.IsCanceled) completion.TrySetCanceled(cancellationToken);
            else if (completed.Exception is { } error) completion.TrySetException(error.InnerExceptions);
            else completion.TrySetResult(completed.Result);
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public static Task DisposeAsync() => Session.DisposeAsync().AsTask();
}

[TestClass]
public sealed class ScenarioAssemblyLifecycle
{
    [AssemblyCleanup]
    public static Task Cleanup() => ScenarioRunner.DisposeAsync();
}
