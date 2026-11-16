namespace PentaGrammata.Tests.Headless;

// The retry of ScenarioRunner: only a setup failure before the journey starts is retried.
[TestClass]
public sealed class ScenarioRunnerTests
{
    private static Exception SetupFailure() =>
        new InvalidOperationException("The calling thread cannot access this object because a different thread owns it.");

    [TestMethod]
    public async Task SetupFailureBeforeTheJourney_IsRetriedUntilItStarts()
    {
        int calls = 0;
        bool journeyRan = false;

        await ScenarioRunner.RetryBeforeStart(onStarted =>
        {
            calls++;
            if (calls < ScenarioRunner.SetupAttempts) throw SetupFailure();
            onStarted();
            journeyRan = true;
            return Task.CompletedTask;
        });

        Assert.AreEqual(ScenarioRunner.SetupAttempts, calls);
        Assert.IsTrue(journeyRan);
    }

    [TestMethod]
    public async Task SetupFailingEveryTime_FailsAfterTheLastAttempt()
    {
        int calls = 0;

        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ScenarioRunner.RetryBeforeStart(_ => { calls++; throw SetupFailure(); }));

        Assert.AreEqual(ScenarioRunner.SetupAttempts, calls);
        StringAssert.Contains(error.Message, "different thread owns it");
    }

    [TestMethod]
    public async Task FailureInsideTheJourney_IsNeverRetried()
    {
        int calls = 0;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            ScenarioRunner.RetryBeforeStart(onStarted =>
            {
                calls++;
                onStarted();
                throw new InvalidOperationException("A journey failure.");
            }));

        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task FailedAssertionInsideTheJourney_IsNeverRetried()
    {
        int calls = 0;

        await Assert.ThrowsExactlyAsync<AssertFailedException>(() =>
            ScenarioRunner.RetryBeforeStart(onStarted =>
            {
                calls++;
                onStarted();
                Assert.Fail("A failed assertion.");
                return Task.CompletedTask;
            }));

        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task OtherFailureBeforeTheJourney_IsNotRetried()
    {
        int calls = 0;

        await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            ScenarioRunner.RetryBeforeStart(_ => { calls++; throw new TimeoutException(); }));

        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task RealJourneyFailure_IsReportedOnceThroughTheHeadlessSession()
    {
        int runs = 0;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ScenarioRunner.Run(_ =>
        {
            runs++;
            throw new InvalidOperationException("A journey failure.");
        }));

        Assert.AreEqual(1, runs, "A journey that started is never started again.");
    }
}
