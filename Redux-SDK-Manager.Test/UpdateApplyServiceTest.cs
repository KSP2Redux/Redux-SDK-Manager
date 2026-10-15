using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Testably.Abstractions.Testing;

namespace Redux_SDK_Manager.Test;

public class UpdateApplyServiceTest
{
    private const string Storage = @"C:\storage";
    private const string LinuxStorage = "/storage";
    private static readonly byte[] Payload = [1, 2, 3, 4, 5];

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static UpdateCheckResult Update(string? assetName = "redux-sdk-gui.exe", string? sha = null)
        => new()
        {
            IsUpdateAvailable = true,
            CurrentVersion = Version.Parse("0.1.0"),
            LatestVersion = Version.Parse("0.2.0"),
            ReleasesPageUrl = "https://github.com/o/r/releases",
            Assets = assetName is null
                ? []
                : [new ReleaseAsset { Name = assetName, DownloadUrl = "https://dl/gui", Sha256 = sha }],
        };

    private static (UpdateApplyService svc, Mock<IApplicationRestarter> restarter, MockFileSystem fs) Build(
        byte[]? downloadBytes = null, bool singleFile = true, Mock<IFileDownloader>? downloaderMock = null,
        bool windows = true)
    {
        var fs = new MockFileSystem(o => o.SimulatingOperatingSystem(windows ? SimulationMode.Windows : SimulationMode.Linux));
        var downloader = downloaderMock ?? new Mock<IFileDownloader>();
        if (downloaderMock is null)
        {
            downloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(downloadBytes ?? Payload);
        }

        var restarter = new Mock<IApplicationRestarter>();
        restarter.Setup(r => r.IsSingleFileDeployment).Returns(singleFile);

        var config = new Mock<IConfigService>();
        config.Setup(c => c.GetLocalStorageDirectory()).Returns(windows ? Storage : LinuxStorage);

        var svc = new UpdateApplyService(downloader.Object, restarter.Object, config.Object, fs,
            Mock.Of<IHostPlatform>(p => p.Os == (windows ? HostOs.Windows : HostOs.Linux)
                && p.RuntimeIdentifier == (windows ? "win-x64" : "linux-x64")),
            Mock.Of<ILogService>());
        return (svc, restarter, fs);
    }

    [Test]
    public async Task Apply_DownloadsVerifiesWritesAndRestarts()
    {
        var (svc, restarter, fs) = Build(Payload);

        var result = await svc.DownloadAndApplyAsync(Update(sha: Sha256Hex(Payload)));

        Assert.That(result, Is.EqualTo(UpdateApplyResult.RestartTriggered));
        var expectedPath = @"C:\storage\update\redux-sdk-gui.exe";
        Assert.That(fs.File.Exists(expectedPath), Is.True);
        Assert.That(fs.File.ReadAllBytes(expectedPath), Is.EqualTo(Payload));
        restarter.Verify(r => r.LaunchUpdaterAndExit(expectedPath), Times.Once);
    }

    [Test]
    public async Task Apply_ReturnsNotSingleFile_AndDoesNotDownload()
    {
        var downloader = new Mock<IFileDownloader>();
        var (svc, restarter, _) = Build(singleFile: false, downloaderMock: downloader);

        var result = await svc.DownloadAndApplyAsync(Update(sha: Sha256Hex(Payload)));

        Assert.That(result, Is.EqualTo(UpdateApplyResult.NotSingleFile));
        downloader.Verify(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        restarter.Verify(r => r.LaunchUpdaterAndExit(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Apply_ReturnsNoAsset_WhenGuiAssetMissing()
    {
        var (svc, _, _) = Build();

        var result = await svc.DownloadAndApplyAsync(Update(assetName: "redux-sdk-cli.exe"));

        Assert.That(result, Is.EqualTo(UpdateApplyResult.NoAsset));
    }

    [Test]
    public async Task Apply_OnLinux_ReturnsNoAsset_WhenOnlyTheWindowsExeIsReleased()
    {
        var downloader = new Mock<IFileDownloader>();
        var (svc, restarter, _) = Build(downloaderMock: downloader, windows: false);

        var result = await svc.DownloadAndApplyAsync(Update(sha: Sha256Hex(Payload)));

        Assert.That(result, Is.EqualTo(UpdateApplyResult.NoAsset));
        downloader.Verify(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        restarter.Verify(r => r.LaunchUpdaterAndExit(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Apply_OnLinux_StagesLinuxAsset_AsExecutable()
    {
        var (svc, restarter, fs) = Build(Payload, windows: false);

        var result = await svc.DownloadAndApplyAsync(
            Update(assetName: "redux-sdk-gui-linux-x64", sha: Sha256Hex(Payload)));

        Assert.That(result, Is.EqualTo(UpdateApplyResult.RestartTriggered));
        const string expectedPath = "/storage/update/redux-sdk-gui-linux-x64";
        Assert.That(fs.File.ReadAllBytes(expectedPath), Is.EqualTo(Payload));
#pragma warning disable CA1416
        Assert.That(fs.File.GetUnixFileMode(expectedPath).HasFlag(UnixFileMode.UserExecute), Is.True);
#pragma warning restore CA1416
        restarter.Verify(r => r.LaunchUpdaterAndExit(expectedPath), Times.Once);
    }

    [Test]
    public async Task Apply_ReturnsChecksumMismatch_AndDoesNotRestart()
    {
        var (svc, restarter, fs) = Build(Payload);

        var result = await svc.DownloadAndApplyAsync(Update(sha: Sha256Hex([9, 9, 9])));

        Assert.That(result, Is.EqualTo(UpdateApplyResult.ChecksumMismatch));
        restarter.Verify(r => r.LaunchUpdaterAndExit(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Apply_ReturnsFailed_WhenDownloadThrows()
    {
        var downloader = new Mock<IFileDownloader>();
        downloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("network"));
        var (svc, restarter, _) = Build(downloaderMock: downloader);

        var result = await svc.DownloadAndApplyAsync(Update(sha: Sha256Hex(Payload)));

        Assert.That(result, Is.EqualTo(UpdateApplyResult.Failed));
        restarter.Verify(r => r.LaunchUpdaterAndExit(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Apply_SucceedsWhenAssetHasNoDigest_SkippingChecksum()
    {
        var (svc, restarter, _) = Build(Payload);

        var result = await svc.DownloadAndApplyAsync(Update(sha: null));

        Assert.That(result, Is.EqualTo(UpdateApplyResult.RestartTriggered));
        restarter.Verify(r => r.LaunchUpdaterAndExit(It.IsAny<string>()), Times.Once);
    }

    [TestCase(HostOs.Windows, "win-x64", "redux-sdk-gui.exe")] // the name released before other platforms
    [TestCase(HostOs.Windows, "win-arm64", "redux-sdk-gui-win-arm64.exe")]
    [TestCase(HostOs.Linux, "linux-x64", "redux-sdk-gui-linux-x64")]
    [TestCase(HostOs.MacOS, "osx-arm64", "redux-sdk-gui-osx-arm64")]
    public void GuiAssetName_IsPerRuntime(HostOs os, string runtimeIdentifier, string expected)
    {
        Assert.That(UpdateApplyService.GuiAssetName(os, runtimeIdentifier), Is.EqualTo(expected));
    }

    [Test]
    public void ChecksumMatches_IsCaseInsensitiveAndTrims()
    {
        Assert.That(UpdateApplyService.ChecksumMatches(Payload, "  " + Sha256Hex(Payload).ToUpperInvariant() + "  "), Is.True);
        Assert.That(UpdateApplyService.ChecksumMatches(Payload, Sha256Hex([0])), Is.False);
    }
}
