using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Testably.Abstractions.Testing;

namespace Redux_SDK_Manager.Test;

public class SdkEmbedServiceTest
{
    private const string ProjectPath = @"C:\proj";
    private const string MirrorPath = @"C:\storage\templates-repo";
    private const string PackageDir = @"C:\proj\Packages\ksp2community.ksp2unitytools";

    private static MockFileSystem NewFs() => new(o => o.SimulatingOperatingSystem(SimulationMode.Windows));

    private static void WriteFile(MockFileSystem fs, string path, string content)
    {
        var dir = fs.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) fs.Directory.CreateDirectory(dir);
        fs.File.WriteAllText(path, content);
    }

    private static string Manifest(string sdkValue) =>
        $$"""{ "dependencies": { "ksp2community.ksp2unitytools": "{{sdkValue}}" } }""";

    private static (SdkEmbedService svc, Mock<IGitService> git, MockFileSystem fs) Build(MockFileSystem fs)
    {
        var git = new Mock<IGitService>();
        var cache = new Mock<ITemplateRepositoryCache>();
        cache.Setup(c => c.RepositoryPath).Returns(MirrorPath);
        return (new SdkEmbedService(git.Object, cache.Object, fs, Mock.Of<ILogService>()), git, fs);
    }

    [Test]
    public void IsEmbedded_ReflectsPackagePresence()
    {
        var fs = NewFs();
        var (svc, _, _) = Build(fs);

        Assert.That(svc.IsEmbedded(ProjectPath), Is.False);
        WriteFile(fs, fs.Path.Combine(PackageDir, "package.json"), "{}");
        Assert.That(svc.IsEmbedded(ProjectPath), Is.True);
    }

    [Test]
    public void StageClone_UsesUrlAndRefFromProjectManifest()
    {
        var fs = NewFs();
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Packages", "manifest.json"),
            Manifest("https://github.com/KSP2Redux/SDK.git#beta-6"));
        var (svc, git, _) = Build(fs);
        (string url, string reference, string dest) captured = default;
        git.Setup(g => g.CloneAndCheckout(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((u, r, d) => captured = (u, r, d));

        var staging = svc.StageClone(ProjectPath, TemplateVersion.Parse("26w32a"));

        Assert.That(captured.url, Is.EqualTo("https://github.com/KSP2Redux/SDK.git"));
        Assert.That(captured.reference, Is.EqualTo("beta-6"));
        Assert.That(captured.dest, Is.EqualTo(staging));
    }

    [Test]
    public void StageClone_DependencyWithoutRef_FallsBackToMain()
    {
        var fs = NewFs();
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Packages", "manifest.json"),
            Manifest("https://github.com/KSP2Redux/SDK.git"));
        var (svc, git, _) = Build(fs);
        string? reference = null;
        git.Setup(g => g.CloneAndCheckout(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, r, _) => reference = r);

        svc.StageClone(ProjectPath, TemplateVersion.Parse("26w32a"));

        Assert.That(reference, Is.EqualTo("main"));
    }

    [Test]
    public void StageClone_NoProjectDependency_ReadsTemplateFromMirror()
    {
        var fs = NewFs();
        // Project manifest has no SDK dependency; the template for the version (in the mirror) does.
        WriteFile(fs, fs.Path.Combine(ProjectPath, "Packages", "manifest.json"),
            """{ "dependencies": { "com.unity.burst": "1.8.28" } }""");
        var (svc, git, _) = Build(fs);
        git.Setup(g => g.ShowFile(MirrorPath, "26w32a", "Packages/manifest.json"))
            .Returns(Manifest("https://github.com/KSP2Redux/SDK.git#26w32a"));
        (string url, string reference) captured = default;
        git.Setup(g => g.CloneAndCheckout(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((u, r, _) => captured = (u, r));

        svc.StageClone(ProjectPath, TemplateVersion.Parse("26w32a"));

        Assert.That(captured.url, Is.EqualTo("https://github.com/KSP2Redux/SDK.git"));
        Assert.That(captured.reference, Is.EqualTo("26w32a"));
    }

    [Test]
    public void Commit_MovesStagingIntoPackages_AndIgnoresIt()
    {
        var fs = NewFs();
        var staging = @"C:\staging\sdk";
        WriteFile(fs, fs.Path.Combine(staging, "package.json"), "{}");
        WriteFile(fs, fs.Path.Combine(ProjectPath, ".gitignore"), "/Library/\n");
        var (svc, _, _) = Build(fs);

        svc.Commit(ProjectPath, staging);

        Assert.That(fs.File.Exists(fs.Path.Combine(PackageDir, "package.json")), Is.True);
        Assert.That(fs.Directory.Exists(staging), Is.False);
        Assert.That(fs.File.ReadAllText(fs.Path.Combine(ProjectPath, ".gitignore")),
            Does.Contain("/Packages/ksp2community.ksp2unitytools/"));
    }

    [Test]
    public void Commit_NullStaging_OnlyEnsuresGitignore()
    {
        var fs = NewFs();
        WriteFile(fs, fs.Path.Combine(ProjectPath, ".gitignore"), "/Library/\n");
        var (svc, _, _) = Build(fs);

        svc.Commit(ProjectPath, null);

        Assert.That(fs.Directory.Exists(PackageDir), Is.False);
        Assert.That(fs.File.ReadAllText(fs.Path.Combine(ProjectPath, ".gitignore")),
            Does.Contain("/Packages/ksp2community.ksp2unitytools/"));
    }

    [Test]
    public void Commit_DoesNotDuplicateGitignoreEntry()
    {
        var fs = NewFs();
        WriteFile(fs, fs.Path.Combine(ProjectPath, ".gitignore"), "/Packages/ksp2community.ksp2unitytools/\n");
        var (svc, _, _) = Build(fs);

        svc.Commit(ProjectPath, null);

        var lines = fs.File.ReadAllText(fs.Path.Combine(ProjectPath, ".gitignore"));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(lines, "ksp2community.ksp2unitytools").Count, Is.EqualTo(1));
    }
}
