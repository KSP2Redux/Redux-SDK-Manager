using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Test;

public class UpdateCoordinatorTest
{
    private static UpdateCheckResult Available(string? notes = null) => new()
    {
        IsUpdateAvailable = true,
        CurrentVersion = Version.Parse("0.1.0"),
        LatestVersion = Version.Parse("0.2.0"),
        ReleaseNotes = notes,
        ReleasesPageUrl = "https://github.com/o/r/releases",
        Assets = [new ReleaseAsset { Name = "redux-sdk-gui.exe", DownloadUrl = "https://dl", Sha256 = "abc" }],
    };

    private static UpdateCheckResult NotAvailable() =>
        UpdateCheckResult.NotAvailable(Version.Parse("0.2.0"), "https://github.com/o/r/releases");

    private static (UpdateCoordinator svc, Mock<IUpdateApplyService> apply, Mock<IDialogService> dialog) Build(
        UpdateCheckResult check, bool confirm = true, UpdateApplyResult applyResult = UpdateApplyResult.RestartTriggered)
    {
        var update = new Mock<IUpdateService>();
        update.Setup(u => u.CheckForUpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(check);

        var apply = new Mock<IUpdateApplyService>();
        apply.Setup(a => a.DownloadAndApplyAsync(It.IsAny<UpdateCheckResult>())).ReturnsAsync(applyResult);

        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(confirm);
        dialog.Setup(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        return (new UpdateCoordinator(update.Object, apply.Object, dialog.Object, Mock.Of<ILogService>()), apply, dialog);
    }

    [Test]
    public async Task Check_Available_Confirmed_Applies()
    {
        var (svc, apply, dialog) = Build(Available());

        await svc.CheckAsync(notifyWhenCurrent: false);

        dialog.Verify(d => d.ConfirmAsync("Update available", It.IsAny<string>(), "Update now", "Later"), Times.Once);
        apply.Verify(a => a.DownloadAndApplyAsync(It.IsAny<UpdateCheckResult>()), Times.Once);
    }

    [Test]
    public async Task Check_Available_Declined_DoesNotApply()
    {
        var (svc, apply, _) = Build(Available(), confirm: false);

        await svc.CheckAsync(notifyWhenCurrent: false);

        apply.Verify(a => a.DownloadAndApplyAsync(It.IsAny<UpdateCheckResult>()), Times.Never);
    }

    [Test]
    public async Task Check_Available_ApplyRestartTriggered_ShowsNoFailureDialog()
    {
        var (svc, _, dialog) = Build(Available());

        await svc.CheckAsync(notifyWhenCurrent: false);

        dialog.Verify(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Check_Available_ApplyNotSingleFile_Alerts()
    {
        var (svc, _, dialog) = Build(Available(), applyResult: UpdateApplyResult.NotSingleFile);

        await svc.CheckAsync(notifyWhenCurrent: false);

        dialog.Verify(d => d.AlertAsync("Update", It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task Check_Available_ApplyFailed_Alerts()
    {
        var (svc, _, dialog) = Build(Available(), applyResult: UpdateApplyResult.ChecksumMismatch);

        await svc.CheckAsync(notifyWhenCurrent: false);

        dialog.Verify(d => d.AlertAsync("Update failed", It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task Check_UpToDate_NotifyTrue_Alerts()
    {
        var (svc, _, dialog) = Build(NotAvailable());

        await svc.CheckAsync(notifyWhenCurrent: true);

        dialog.Verify(d => d.AlertAsync("Up to date", It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task Check_UpToDate_NotifyFalse_Silent()
    {
        var (svc, apply, dialog) = Build(NotAvailable());

        await svc.CheckAsync(notifyWhenCurrent: false);

        dialog.Verify(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        dialog.Verify(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        apply.Verify(a => a.DownloadAndApplyAsync(It.IsAny<UpdateCheckResult>()), Times.Never);
    }

    [Test]
    public void BuildAvailableMessage_WithNotes_IncludesThem()
    {
        var message = UpdateCoordinator.BuildAvailableMessage(Available("- Fixed a crash\n- Faster"));

        Assert.That(message, Does.Contain("v0.2.0"));
        Assert.That(message, Does.Contain("you have v0.1.0"));
        Assert.That(message, Does.Contain("Fixed a crash"));
    }

    [Test]
    public void BuildAvailableMessage_NoNotes_OmitsWhatsNew()
    {
        var message = UpdateCoordinator.BuildAvailableMessage(Available(notes: null));

        Assert.That(message, Does.Not.Contain("What's new"));
        Assert.That(message, Does.Contain("v0.2.0"));
    }

    [Test]
    public void BuildAvailableMessage_LongNotes_Truncated()
    {
        var message = UpdateCoordinator.BuildAvailableMessage(Available(new string('a', 900)));

        Assert.That(message, Does.Contain(new string('a', 500) + "..."));
        Assert.That(message, Does.Not.Contain(new string('a', 501)));
    }
}
