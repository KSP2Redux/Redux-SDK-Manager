using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Testably.Abstractions.Testing;

namespace Redux_SDK_Manager.Test;

public class ProjectServiceTest
{
    private const string TargetPath = @"C:\projects\NewMod";
    private const string ProjectPath = @"C:\projects\ExistingMod";

    private static MockFileSystem NewFs() =>
        new(o => o.SimulatingOperatingSystem(SimulationMode.Windows));

    // MockFileSystem (like real System.IO) requires the parent directory to exist before a write.
    private static void WriteFile(MockFileSystem fs, string path, string content)
    {
        var dir = fs.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) fs.Directory.CreateDirectory(dir);
        fs.File.WriteAllText(path, content);
    }

    // --- CreateProject ---

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
                WriteFile(fs, fs.Path.Combine(dir, "template.version"), "26w32a");
                WriteFile(fs, fs.Path.Combine(dir, "Assets", "foo.txt"), "hi");
                WriteFile(fs, fs.Path.Combine(dir, ".git", "config"), "gitdata");
            });

        var service = new ProjectService(catalog.Object, Mock.Of<ITemplateVersionService>(), configMock.Object, fs);
        service.CreateProject(TemplateVersion.Parse("26w32a"), TargetPath);

        Assert.That(fs.File.ReadAllText(fs.Path.Combine(TargetPath, "template.version")), Is.EqualTo("26w32a"));
        Assert.That(fs.File.Exists(fs.Path.Combine(TargetPath, "Assets", "foo.txt")), Is.True);
        Assert.That(fs.Directory.Exists(fs.Path.Combine(TargetPath, ".git")), Is.False);
        Assert.That(config.ProjectPaths, Does.Contain(TargetPath));
        configMock.Verify(c => c.Save(), Times.Once);
    }

    [Test]
    public void CreateProject_SucceedsAndTracks_WhenFetchedTreeHasReadOnlyFiles()
    {
        var fs = NewFs();
        var config = new SdkManagerConfig();
        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(config);

        var catalog = new Mock<ITemplateCatalogService>();
        catalog.Setup(c => c.FetchVersion(It.IsAny<TemplateVersion>(), It.IsAny<string>()))
            .Callback<TemplateVersion, string>((_, dir) =>
            {
                WriteFile(fs, fs.Path.Combine(dir, "template.version"), "26w32a");
                // git leaves pack files read-only on Windows; the temp cleanup must cope with them.
                var pack = fs.Path.Combine(dir, ".git", "objects", "pack", "pack-abc.idx");
                WriteFile(fs, pack, "packdata");
                fs.File.SetAttributes(pack, System.IO.FileAttributes.ReadOnly);
            });

        var service = new ProjectService(catalog.Object, Mock.Of<ITemplateVersionService>(), configMock.Object, fs);

        Assert.DoesNotThrow(() => service.CreateProject(TemplateVersion.Parse("26w32a"), TargetPath));
        Assert.That(config.ProjectPaths, Does.Contain(TargetPath));
    }

    [Test]
    public void CreateProject_Throws_WhenTargetNonEmpty()
    {
        var fs = NewFs();
        WriteFile(fs, fs.Path.Combine(TargetPath, "existing.txt"), "x");

        var catalog = new Mock<ITemplateCatalogService>();
        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(new SdkManagerConfig());

        var service = new ProjectService(catalog.Object, Mock.Of<ITemplateVersionService>(), configMock.Object, fs);

        Assert.That(() => service.CreateProject(TemplateVersion.Parse("26w32a"), TargetPath),
            Throws.TypeOf<System.InvalidOperationException>());
        catalog.Verify(c => c.FetchVersion(It.IsAny<TemplateVersion>(), It.IsAny<string>()), Times.Never);
    }

    // --- UpgradeProject ---

    [Test]
    public void UpgradeProject_OverlaysNewTree_DropsOldOnly_DeletesSdkCopies_ClearsCaches_KeepsUserWork()
    {
        var fs = NewFs();

        // Existing 0.2.8.5 project.
        WriteFile(fs, fs.Path.Combine(ProjectPath, "template.version"), "0.2.8.5");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Assets", "boot-ksp.unity"), "old-boot");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Packages", "com.unity.postprocessing@3.2.2", "Foo.cs"), "pp");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Assets", "ImportKsp2ToEditor.asset"), "sdk");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Assets", "ImportKsp2ToEditor.asset.meta"), "sdkmeta");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Assets", "MyMod", "MyMod.cs"), "usercode");   // authored mod
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Assets", "Mods", "SomeDep.dll"), "dep");        // dependency drop
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Library", "cache.bin"), "cache");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Packages", "KSP2_x64", "Assembly-CSharp.dll"), "game");

        var catalog = new Mock<ITemplateCatalogService>();
        catalog.Setup(c => c.FetchVersion(It.Is<TemplateVersion>(v => v.Raw == "0.2.8.5"), It.IsAny<string>()))
            .Callback<TemplateVersion, string>((_, dir) =>
            {
                WriteFile(fs, fs.Path.Combine(dir, "template.version"), "0.2.8.5");
                WriteFile(fs, fs.Path.Combine(dir, "Assets", "boot-ksp.unity"), "old-boot");
                WriteFile(fs, fs.Path.Combine(dir, "Packages", "com.unity.postprocessing@3.2.2", "Foo.cs"), "pp");
            });
        catalog.Setup(c => c.FetchVersion(It.Is<TemplateVersion>(v => v.Raw == "26w32a"), It.IsAny<string>()))
            .Callback<TemplateVersion, string>((_, dir) =>
            {
                WriteFile(fs, fs.Path.Combine(dir, "template.version"), "26w32a");
                WriteFile(fs, fs.Path.Combine(dir, "Assets", "boot-ksp.unity"), "new-boot");
            });

        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(new SdkManagerConfig());

        var service = new ProjectService(catalog.Object, new TemplateVersionService(fs), configMock.Object, fs);
        service.UpgradeProject(ProjectPath, TemplateVersion.Parse("26w32a"));

        // template.version + template-owned files overlaid to the new version
        Assert.That(fs.File.ReadAllText(fs.Path.Combine(ProjectPath, "template.version")), Is.EqualTo("26w32a"));
        Assert.That(fs.File.ReadAllText(fs.Path.Combine(ProjectPath, "Assets", "boot-ksp.unity")), Is.EqualTo("new-boot"));
        // old-only template file removed
        Assert.That(fs.File.Exists(fs.Path.Combine(ProjectPath, "Packages", "com.unity.postprocessing@3.2.2", "Foo.cs")), Is.False);
        // SDK-copied files deleted for regeneration
        Assert.That(fs.File.Exists(fs.Path.Combine(ProjectPath, "Assets", "ImportKsp2ToEditor.asset")), Is.False);
        Assert.That(fs.File.Exists(fs.Path.Combine(ProjectPath, "Assets", "ImportKsp2ToEditor.asset.meta")), Is.False);
        // regenerated caches cleared
        Assert.That(fs.Directory.Exists(fs.Path.Combine(ProjectPath, "Library")), Is.False);
        Assert.That(fs.Directory.Exists(fs.Path.Combine(ProjectPath, "Packages", "KSP2_x64")), Is.False);
        // user work untouched (authored mod + dependency drop)
        Assert.That(fs.File.ReadAllText(fs.Path.Combine(ProjectPath, "Assets", "MyMod", "MyMod.cs")), Is.EqualTo("usercode"));
        Assert.That(fs.File.ReadAllText(fs.Path.Combine(ProjectPath, "Assets", "Mods", "SomeDep.dll")), Is.EqualTo("dep"));
    }

    [Test]
    public void UpgradeProject_Throws_WhenNoTemplateVersion()
    {
        var fs = NewFs();
        fs.Directory.CreateDirectory(ProjectPath);

        var catalog = new Mock<ITemplateCatalogService>();
        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(new SdkManagerConfig());

        var service = new ProjectService(catalog.Object, new TemplateVersionService(fs), configMock.Object, fs);

        Assert.That(() => service.UpgradeProject(ProjectPath, TemplateVersion.Parse("26w32a")),
            Throws.TypeOf<System.InvalidOperationException>());
        catalog.Verify(c => c.FetchVersion(It.IsAny<TemplateVersion>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void UpgradeProject_MergesManifest_ApplyingTemplateBump_KeepingUserPackage()
    {
        var fs = NewFs();
        WriteFile(fs, fs.Path.Combine(ProjectPath, "template.version"), "0.2.8.5");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Packages", "manifest.json"),
            "{\n  \"dependencies\": {\n    \"com.unity.burst\": \"1.8.28\",\n    \"user.pkg\": \"9.9.9\"\n  }\n}");

        var catalog = new Mock<ITemplateCatalogService>();
        catalog.Setup(c => c.FetchVersion(It.Is<TemplateVersion>(v => v.Raw == "0.2.8.5"), It.IsAny<string>()))
            .Callback<TemplateVersion, string>((_, dir) =>
                WriteFile(fs, fs.Path.Combine(dir, "Packages", "manifest.json"),
                    "{\n  \"dependencies\": {\n    \"com.unity.burst\": \"1.8.28\"\n  }\n}"));
        catalog.Setup(c => c.FetchVersion(It.Is<TemplateVersion>(v => v.Raw == "26w32a"), It.IsAny<string>()))
            .Callback<TemplateVersion, string>((_, dir) =>
                WriteFile(fs, fs.Path.Combine(dir, "Packages", "manifest.json"),
                    "{\n  \"dependencies\": {\n    \"com.unity.burst\": \"1.8.29\"\n  }\n}")); // burst bumped

        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(new SdkManagerConfig());

        var service = new ProjectService(catalog.Object, new TemplateVersionService(fs), configMock.Object, fs);
        service.UpgradeProject(ProjectPath, TemplateVersion.Parse("26w32a"));

        var manifest = fs.File.ReadAllText(fs.Path.Combine(ProjectPath, "Packages", "manifest.json"));
        Assert.Multiple(() =>
        {
            Assert.That(manifest, Does.Contain("\"com.unity.burst\": \"1.8.29\"")); // template bump applied
            Assert.That(manifest, Does.Contain("\"user.pkg\": \"9.9.9\""));         // user package survived
        });
    }

    // --- IngestProject ---

    [Test]
    public void IngestProject_OverlaysAndStamps_TracksAndClearsCaches_KeepsUserWork()
    {
        var fs = NewFs();

        // Pre-manager project: a Unity project with no template.version.
        WriteFile(fs, fs.Path.Combine(ProjectPath, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 6000.4.1f1");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Assets", "boot-ksp.unity"), "old-boot");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Assets", "ImportKsp2ToEditor.asset"), "sdk");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Assets", "MyMod", "MyMod.cs"), "usercode");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Library", "cache.bin"), "cache");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Packages", "KSP2_x64", "Assembly-CSharp.dll"), "game");

        var catalog = new Mock<ITemplateCatalogService>();
        catalog.Setup(c => c.FetchVersion(It.Is<TemplateVersion>(v => v.Raw == "26w32a"), It.IsAny<string>()))
            .Callback<TemplateVersion, string>((_, dir) =>
            {
                WriteFile(fs, fs.Path.Combine(dir, "template.version"), "26w32a");
                WriteFile(fs, fs.Path.Combine(dir, "Assets", "boot-ksp.unity"), "new-boot");
            });

        var config = new SdkManagerConfig();
        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(config);

        var service = new ProjectService(catalog.Object, new TemplateVersionService(fs), configMock.Object, fs);
        service.IngestProject(ProjectPath, TemplateVersion.Parse("26w32a"));

        Assert.That(fs.File.ReadAllText(fs.Path.Combine(ProjectPath, "template.version")), Is.EqualTo("26w32a"));
        Assert.That(fs.File.ReadAllText(fs.Path.Combine(ProjectPath, "Assets", "boot-ksp.unity")), Is.EqualTo("new-boot"));
        Assert.That(fs.File.Exists(fs.Path.Combine(ProjectPath, "Assets", "ImportKsp2ToEditor.asset")), Is.False);
        Assert.That(fs.Directory.Exists(fs.Path.Combine(ProjectPath, "Library")), Is.False);
        Assert.That(fs.Directory.Exists(fs.Path.Combine(ProjectPath, "Packages", "KSP2_x64")), Is.False);
        Assert.That(fs.File.ReadAllText(fs.Path.Combine(ProjectPath, "Assets", "MyMod", "MyMod.cs")), Is.EqualTo("usercode"));
        Assert.That(config.ProjectPaths, Does.Contain(ProjectPath));
    }

    [Test]
    public void IngestProject_Throws_WhenNotUnityProject()
    {
        var fs = NewFs();
        fs.Directory.CreateDirectory(ProjectPath); // empty - no ProjectVersion.txt

        var catalog = new Mock<ITemplateCatalogService>();
        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(new SdkManagerConfig());

        var service = new ProjectService(catalog.Object, new TemplateVersionService(fs), configMock.Object, fs);

        Assert.That(() => service.IngestProject(ProjectPath, TemplateVersion.Parse("26w32a")),
            Throws.TypeOf<System.InvalidOperationException>());
        catalog.Verify(c => c.FetchVersion(It.IsAny<TemplateVersion>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void IngestProject_Throws_WhenAlreadyManaged()
    {
        var fs = NewFs();
        WriteFile(fs, fs.Path.Combine(ProjectPath, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 6000.4.1f1");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "template.version"), "0.2.8.5");

        var catalog = new Mock<ITemplateCatalogService>();
        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(new SdkManagerConfig());

        var service = new ProjectService(catalog.Object, new TemplateVersionService(fs), configMock.Object, fs);

        Assert.That(() => service.IngestProject(ProjectPath, TemplateVersion.Parse("26w32a")),
            Throws.TypeOf<System.InvalidOperationException>());
        catalog.Verify(c => c.FetchVersion(It.IsAny<TemplateVersion>(), It.IsAny<string>()), Times.Never);
    }

    // --- ImportProject ---

    [Test]
    public void ImportProject_TracksManagedProject_WithoutModifying_AndReturnsVersion()
    {
        var fs = NewFs();
        WriteFile(fs, fs.Path.Combine(ProjectPath, "template.version"), "0.2.8.5");
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Assets", "MyMod", "MyMod.cs"), "usercode");

        var catalog = new Mock<ITemplateCatalogService>();
        var config = new SdkManagerConfig();
        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(config);

        var service = new ProjectService(catalog.Object, new TemplateVersionService(fs), configMock.Object, fs);
        var version = service.ImportProject(ProjectPath);

        Assert.That(version.Raw, Is.EqualTo("0.2.8.5"));
        Assert.That(config.ProjectPaths, Does.Contain(ProjectPath));
        configMock.Verify(c => c.Save(), Times.Once);
        // untouched, and never fetched
        Assert.That(fs.File.ReadAllText(fs.Path.Combine(ProjectPath, "Assets", "MyMod", "MyMod.cs")), Is.EqualTo("usercode"));
        catalog.Verify(c => c.FetchVersion(It.IsAny<TemplateVersion>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void ImportProject_Throws_WhenNotManaged()
    {
        var fs = NewFs();
        fs.Directory.CreateDirectory(ProjectPath); // exists but no template.version

        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(new SdkManagerConfig());
        var service = new ProjectService(Mock.Of<ITemplateCatalogService>(), new TemplateVersionService(fs), configMock.Object, fs);

        Assert.That(() => service.ImportProject(ProjectPath), Throws.TypeOf<System.InvalidOperationException>());
    }

    [Test]
    public void ImportProject_Throws_WhenDirectoryMissing()
    {
        var fs = NewFs();
        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(new SdkManagerConfig());
        var service = new ProjectService(Mock.Of<ITemplateCatalogService>(), new TemplateVersionService(fs), configMock.Object, fs);

        Assert.That(() => service.ImportProject(@"C:\does\not\exist"), Throws.TypeOf<System.InvalidOperationException>());
    }
}
