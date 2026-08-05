using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Testably.Abstractions.Testing;

namespace Redux_SDK_Manager.Test;

public class ProjectServiceTest
{
    private const string TargetPath = @"C:\projects\NewMod";

    private static MockFileSystem NewFs() =>
        new(o => o.SimulatingOperatingSystem(SimulationMode.Windows));

    [Test]
    public void CreateProject_MaterializesTree_ExcludesGit_AndTracksProject()
    {
        var fs = NewFs();
        var config = new SdkManagerConfig();
        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(config);

        var catalog = new Mock<ITemplateCatalogService>();
        catalog.Setup(c => c.FetchVersion(It.IsAny<TemplateVersion>(), It.IsAny<string>()))
            .Callback<TemplateVersion, string>((_, dir) =>
            {
                fs.Directory.CreateDirectory(fs.Path.Combine(dir, "Assets"));
                fs.Directory.CreateDirectory(fs.Path.Combine(dir, ".git"));
                fs.File.WriteAllText(fs.Path.Combine(dir, "template.version"), "26w32a");
                fs.File.WriteAllText(fs.Path.Combine(dir, "Assets", "foo.txt"), "hi");
                fs.File.WriteAllText(fs.Path.Combine(dir, ".git", "config"), "gitdata");
            });

        var service = new ProjectService(catalog.Object, configMock.Object, fs);
        service.CreateProject(TemplateVersion.Parse("26w32a"), TargetPath);

        Assert.That(fs.File.ReadAllText(fs.Path.Combine(TargetPath, "template.version")), Is.EqualTo("26w32a"));
        Assert.That(fs.File.Exists(fs.Path.Combine(TargetPath, "Assets", "foo.txt")), Is.True);
        Assert.That(fs.Directory.Exists(fs.Path.Combine(TargetPath, ".git")), Is.False);
        Assert.That(config.ProjectPaths, Does.Contain(TargetPath));
        configMock.Verify(c => c.Save(), Times.Once);
    }

    [Test]
    public void CreateProject_Throws_WhenTargetNonEmpty()
    {
        var fs = NewFs();
        fs.Directory.CreateDirectory(TargetPath);
        fs.File.WriteAllText(fs.Path.Combine(TargetPath, "existing.txt"), "x");

        var catalog = new Mock<ITemplateCatalogService>();
        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(new SdkManagerConfig());

        var service = new ProjectService(catalog.Object, configMock.Object, fs);

        Assert.That(() => service.CreateProject(TemplateVersion.Parse("26w32a"), TargetPath),
            Throws.TypeOf<System.InvalidOperationException>());
        catalog.Verify(c => c.FetchVersion(It.IsAny<TemplateVersion>(), It.IsAny<string>()), Times.Never);
    }
}
