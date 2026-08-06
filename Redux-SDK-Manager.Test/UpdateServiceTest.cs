using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Test;

public class UpdateServiceTest
{
    private const string RepoUrl = "https://github.com/KSP2Redux/Redux-SDK-Manager";

    private static ReleaseInfo Release(string tag, bool prerelease = false, bool withAsset = true, string? notes = null)
        => new()
        {
            TagName = tag,
            Prerelease = prerelease,
            Notes = notes,
            Assets = withAsset
                ? [new ReleaseAsset { Name = "redux-sdk-gui.exe", DownloadUrl = $"https://dl/{tag}/gui", Sha256 = "abc123" }]
                : [],
        };

    private static UpdateService NewService(
        IReadOnlyList<ReleaseInfo> releases, string current, string repoUrl = RepoUrl, Mock<IReleaseClient>? clientMock = null)
    {
        var client = clientMock ?? new Mock<IReleaseClient>();
        if (clientMock is null)
        {
            client.Setup(c => c.GetReleasesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(releases);
        }

        var appVersion = new Mock<IAppVersion>();
        appVersion.Setup(a => a.Current).Returns(Version.Parse(current));

        var config = new Mock<IConfigService>();
        config.Setup(c => c.Config).Returns(new SdkManagerConfig { ManagerRepositoryUrl = repoUrl });

        return new UpdateService(client.Object, appVersion.Object, config.Object, Mock.Of<ILogService>());
    }

    [Test]
    public async Task Check_ReportsUpdate_WhenNewerReleaseWithAsset()
    {
        var svc = NewService([Release("v0.2.0", notes: "Fixes")], current: "0.1.0");

        var result = await svc.CheckForUpdateAsync();

        Assert.That(result.IsUpdateAvailable, Is.True);
        Assert.That(result.LatestVersion, Is.EqualTo(Version.Parse("0.2.0")));
        Assert.That(result.CurrentVersion, Is.EqualTo(Version.Parse("0.1.0")));
        Assert.That(result.ReleaseNotes, Is.EqualTo("Fixes"));
        Assert.That(result.Assets, Has.Count.EqualTo(1));
        Assert.That(result.ReleasesPageUrl, Is.EqualTo("https://github.com/KSP2Redux/Redux-SDK-Manager/releases"));
    }

    [Test]
    public async Task Check_PicksNewest_AcrossManyReleases()
    {
        var svc = NewService([Release("v0.1.5"), Release("v0.3.0"), Release("v0.2.0")], current: "0.1.0");

        var result = await svc.CheckForUpdateAsync();

        Assert.That(result.LatestVersion, Is.EqualTo(Version.Parse("0.3.0")));
    }

    [Test]
    public async Task Check_NoUpdate_WhenCurrentIsLatest()
    {
        var svc = NewService([Release("v0.1.0")], current: "0.1.0");

        var result = await svc.CheckForUpdateAsync();

        Assert.That(result.IsUpdateAvailable, Is.False);
        Assert.That(result.CurrentVersion, Is.EqualTo(Version.Parse("0.1.0")));
    }

    [Test]
    public async Task Check_SkipsPrereleases()
    {
        var svc = NewService([Release("v0.2.0", prerelease: true), Release("v0.1.0")], current: "0.1.0");

        var result = await svc.CheckForUpdateAsync();

        Assert.That(result.IsUpdateAvailable, Is.False);
    }

    [Test]
    public async Task Check_SkipsRelease_WithNoUploadedAssetsYet()
    {
        var svc = NewService([Release("v0.2.0", withAsset: false)], current: "0.1.0");

        var result = await svc.CheckForUpdateAsync();

        Assert.That(result.IsUpdateAvailable, Is.False);
    }

    [Test]
    public async Task Check_IgnoresNonVersionTags()
    {
        var svc = NewService([Release("nightly"), Release("v0.2.0")], current: "0.1.0");

        var result = await svc.CheckForUpdateAsync();

        Assert.That(result.LatestVersion, Is.EqualTo(Version.Parse("0.2.0")));
    }

    [Test]
    public async Task Check_NoUpdate_WhenClientThrows()
    {
        var client = new Mock<IReleaseClient>();
        client.Setup(c => c.GetReleasesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestExceptionStub());
        var svc = NewService([], current: "0.1.0", clientMock: client);

        var result = await svc.CheckForUpdateAsync();

        Assert.That(result.IsUpdateAvailable, Is.False);
        Assert.That(result.ReleasesPageUrl, Is.EqualTo("https://github.com/KSP2Redux/Redux-SDK-Manager/releases"));
    }

    [Test]
    public async Task Check_NoUpdate_WhenRepoUrlInvalid()
    {
        var svc = NewService([Release("v0.2.0")], current: "0.1.0", repoUrl: "not-a-repo");

        var result = await svc.CheckForUpdateAsync();

        Assert.That(result.IsUpdateAvailable, Is.False);
    }

    [Test]
    public async Task Check_ParsesShorthandOwnerRepo()
    {
        var client = new Mock<IReleaseClient>();
        client.Setup(c => c.GetReleasesAsync("acme", "manager", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Release("v0.2.0")]);
        var svc = NewService([], current: "0.1.0", repoUrl: "acme/manager", clientMock: client);

        var result = await svc.CheckForUpdateAsync();

        Assert.That(result.IsUpdateAvailable, Is.True);
        client.Verify(c => c.GetReleasesAsync("acme", "manager", It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class HttpRequestExceptionStub : Exception;
}
