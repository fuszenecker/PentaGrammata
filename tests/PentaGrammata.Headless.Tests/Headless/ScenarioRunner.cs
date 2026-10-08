using Avalonia.Headless;
using PentaGrammata.Configuration;

namespace PentaGrammata.Tests.Headless;

internal static class ScenarioRunner
{
    // A single UI dispatcher, with Avalonia's per-dispatch application isolation. Each
    // journey still gets its own production service container, windows and profile.
    private static readonly HeadlessUnitTestSession Session =
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ScenarioRunner).Assembly);

    public static async Task Run(Func<ScenarioDesktop, Task> journey, Action<AppConfiguration>? arrangeProfile = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var cancellationToken = timeout.Token;
        // Explicit TResult selects the asynchronous overload; async void would lose failures.
        var dispatch = Session.Dispatch<bool>(async () =>
        {
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
