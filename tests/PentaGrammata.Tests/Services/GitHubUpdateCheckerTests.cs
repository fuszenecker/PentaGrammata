using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PentaGrammata.Services;

namespace PentaGrammata.Tests.Services;

[TestClass]
public sealed class GitHubUpdateCheckerTests
{
    private static string CurrentVersion =>
        (Assembly.GetAssembly(typeof(GitHubUpdateChecker))!.GetName().Version ?? new Version(0, 0)).ToString();

    [TestMethod]
    public async Task CheckAsync_WhenLatestIsNewer_ReportsUpdateAvailable()
    {
        var sut = CreateChecker(HttpStatusCode.OK, """{ "tag_name": "v999.0.0", "html_url": "https://example/releases/latest" }""");

        var result = await sut.CheckAsync();

        Assert.IsTrue(result.Succeeded);
        Assert.IsTrue(result.UpdateAvailable);
        Assert.AreEqual("999.0.0", result.LatestVersion);
        Assert.AreEqual("https://example/releases/latest", result.ReleaseUrl);
    }

    [TestMethod]
    public async Task CheckAsync_WhenLatestIsOlder_ReportsNoUpdate()
    {
        var sut = CreateChecker(HttpStatusCode.OK, """{ "tag_name": "v0.0.1" }""");

        var result = await sut.CheckAsync();

        Assert.IsTrue(result.Succeeded);
        Assert.IsFalse(result.UpdateAvailable);
    }

    [TestMethod]
    public async Task CheckAsync_WhenLatestEqualsCurrent_ReportsNoUpdate()
    {
        var sut = CreateChecker(HttpStatusCode.OK, $$"""{ "tag_name": "{{CurrentVersion}}" }""");

        var result = await sut.CheckAsync();

        Assert.IsTrue(result.Succeeded);
        Assert.IsFalse(result.UpdateAvailable);
        Assert.AreEqual(CurrentVersion, result.CurrentVersion);
    }

    [TestMethod]
    public async Task CheckAsync_ToleratesTagWithoutVPrefix()
    {
        var sut = CreateChecker(HttpStatusCode.OK, """{ "tag_name": "999.1" }""");

        var result = await sut.CheckAsync();

        Assert.IsTrue(result.Succeeded);
        Assert.IsTrue(result.UpdateAvailable);
    }

    [TestMethod]
    public async Task CheckAsync_WhenTagUnparseable_FailsGracefully()
    {
        var sut = CreateChecker(HttpStatusCode.OK, """{ "tag_name": "not-a-version" }""");

        var result = await sut.CheckAsync();

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(result.UpdateAvailable);
        Assert.IsNotNull(result.Error);
    }

    [TestMethod]
    public async Task CheckAsync_WhenServerErrors_FailsGracefully()
    {
        var sut = CreateChecker(HttpStatusCode.ServiceUnavailable, "nope");

        var result = await sut.CheckAsync();

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.Error);
        // The current version is still reported even on failure.
        Assert.AreEqual(CurrentVersion, result.CurrentVersion);
    }

    [TestMethod]
    public async Task CheckAsync_WhenResponseIsNotJson_FailsGracefully()
    {
        var sut = CreateChecker(HttpStatusCode.OK, "<html>not json</html>");

        var result = await sut.CheckAsync();

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.Error);
    }

    private static GitHubUpdateChecker CreateChecker(HttpStatusCode status, string body)
    {
        var httpClient = new HttpClient(new StubHandler(status, body));
        return new GitHubUpdateChecker(httpClient, Substitute.For<ILogger<GitHubUpdateChecker>>());
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body),
            });
        }
    }
}
