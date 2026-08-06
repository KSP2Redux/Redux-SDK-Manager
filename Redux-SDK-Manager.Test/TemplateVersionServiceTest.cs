using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Testably.Abstractions.Testing;

namespace Redux_SDK_Manager.Test;

public class TemplateVersionServiceTest
{
    private const string ProjectDir = @"C:\proj";
    private const string VersionFile = @"C:\proj\template.version";
    private const string ProjectInfoFile = @"C:\proj\project.info";

    private static (ITemplateVersionService service, MockFileSystem fs) CreateService()
    {
        var fs = new MockFileSystem(o => o.SimulatingOperatingSystem(SimulationMode.Windows));
        fs.Directory.CreateDirectory(ProjectDir);
        var info = new ProjectInfoService(fs, Mock.Of<ILogService>());
        return (new TemplateVersionService(fs, info), fs);
    }

    [Test]
    public void DetectProjectVersion_ReadsAndClassifies_FromTemplateStamp()
    {
        var (service, fs) = CreateService();
        fs.File.WriteAllText(VersionFile, "0.2.8.5");

        var version = service.DetectProjectVersion(ProjectDir);

        Assert.That(version, Is.Not.Null);
        Assert.That(version!.Raw, Is.EqualTo("0.2.8.5"));
        Assert.That(version.Channel, Is.EqualTo(TemplateChannel.Release));
    }

    [Test]
    public void DetectProjectVersion_TrimsTrailingNewline()
    {
        var (service, fs) = CreateService();
        fs.File.WriteAllText(VersionFile, "26w32a\r\n");

        var version = service.DetectProjectVersion(ProjectDir);

        Assert.That(version!.Raw, Is.EqualTo("26w32a"));
        Assert.That(version.Channel, Is.EqualTo(TemplateChannel.Snapshot));
    }

    [Test]
    public void DetectProjectVersion_PrefersProjectInfo_OverTemplateStamp()
    {
        var (service, fs) = CreateService();
        fs.File.WriteAllText(VersionFile, "0.2.8.5");          // stale remote stamp
        fs.File.WriteAllText(ProjectInfoFile, "name = \"My Mod\"\nversion = \"26w32b\"\n");

        var version = service.DetectProjectVersion(ProjectDir);

        Assert.That(version!.Raw, Is.EqualTo("26w32b"));
        Assert.That(version.Channel, Is.EqualTo(TemplateChannel.Snapshot));
    }

    [Test]
    public void DetectProjectVersion_FallsBackToTemplateStamp_WhenProjectInfoHasNoVersion()
    {
        var (service, fs) = CreateService();
        fs.File.WriteAllText(VersionFile, "0.2.8.5");
        fs.File.WriteAllText(ProjectInfoFile, "name = \"My Mod\"\n");

        Assert.That(service.DetectProjectVersion(ProjectDir)!.Raw, Is.EqualTo("0.2.8.5"));
    }

    [Test]
    public void DetectProjectVersion_ReturnsNull_WhenNeitherPresent()
    {
        var (service, _) = CreateService();

        Assert.That(service.DetectProjectVersion(ProjectDir), Is.Null);
    }

    [Test]
    public void DetectProjectVersion_ReturnsNull_WhenStampBlank()
    {
        var (service, fs) = CreateService();
        fs.File.WriteAllText(VersionFile, "   \r\n");

        Assert.That(service.DetectProjectVersion(ProjectDir), Is.Null);
    }
}
