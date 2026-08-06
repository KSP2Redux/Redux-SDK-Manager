using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Moq;
using Redux_SDK_Manager.Services;
using Redux_SDK_Manager.Wrappers;
using Testably.Abstractions.Testing;

namespace Redux_SDK_Manager.Test;

public class UnityServiceTest
{
    private const string ProgramFiles = @"C:\Program Files";
    private const string AppData = @"C:\Users\me\AppData\Roaming";
    private const string HubExe = @"C:\Program Files\Unity Hub\Unity Hub.exe";

    private static (MockFileSystem fs, MockEnvironmentProvider env) BuildEnv()
    {
        var fs = new MockFileSystem(o => o.SimulatingOperatingSystem(SimulationMode.Windows));
        var env = new MockEnvironmentProvider();
        env.SetFolderPath(System.Environment.SpecialFolder.ProgramFiles, ProgramFiles);
        env.SetFolderPath(System.Environment.SpecialFolder.ApplicationData, AppData);
        return (fs, env);
    }

    private static UnityService NewService(MockFileSystem fs, MockEnvironmentProvider env, IProcessRunner? runner = null)
        => new(fs, env, runner ?? Mock.Of<IProcessRunner>());

    private static void WriteFile(MockFileSystem fs, string path, string content)
    {
        var dir = fs.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) fs.Directory.CreateDirectory(dir);
        fs.File.WriteAllText(path, content);
    }

    [Test]
    public void DetectInstalls_FindsEditors_FromDefaultSecondaryAndManualSources()
    {
        var (fs, env) = BuildEnv();

        // 1. Default Hub editor folder
        WriteFile(fs, @"C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe", "exe");
        // 2. Secondary install path (secondaryInstallPath.json holds a JSON string path)
        WriteFile(fs, @"C:\Users\me\AppData\Roaming\UnityHub\secondaryInstallPath.json",
            """
            "D:\\Editors"
            """);
        WriteFile(fs, @"D:\Editors\6000.5.0f1\Editor\Unity.exe", "exe");
        // 3. Manually-added editor (editors.json)
        WriteFile(fs, @"C:\Users\me\AppData\Roaming\UnityHub\editors.json",
            """
            {"custom":{"version":"2022.3.5f1","location":["E:\\Unity\\2022.3.5f1\\Editor\\Unity.exe"]}}
            """);
        WriteFile(fs, @"E:\Unity\2022.3.5f1\Editor\Unity.exe", "exe");

        var installs = NewService(fs, env).DetectInstalls();

        Assert.That(installs.Select(i => i.Version),
            Is.EquivalentTo(new[] { "6000.4.1f1", "6000.5.0f1", "2022.3.5f1" }));
    }

    [Test]
    public void DetectInstalls_Empty_WhenNothingInstalled()
    {
        var (fs, env) = BuildEnv();
        Assert.That(NewService(fs, env).DetectInstalls(), Is.Empty);
    }

    [Test]
    public void GetProjectUnityVersion_ParsesEditorVersion()
    {
        var (fs, env) = BuildEnv();
        WriteFile(fs, @"C:\proj\ProjectSettings\ProjectVersion.txt",
            "m_EditorVersion: 6000.4.1f1\r\nm_EditorVersionWithRevision: 6000.4.1f1 (336a400b9ea2)\r\n");

        Assert.That(NewService(fs, env).GetProjectUnityVersion(@"C:\proj"), Is.EqualTo("6000.4.1f1"));
    }

    [Test]
    public void GetProjectUnityVersion_ReturnsNull_WhenMissing()
    {
        var (fs, env) = BuildEnv();
        fs.Directory.CreateDirectory(@"C:\proj");
        Assert.That(NewService(fs, env).GetProjectUnityVersion(@"C:\proj"), Is.Null);
    }

    [Test]
    public void IsHubInstalled_ReflectsHubExePresence()
    {
        var (fs, env) = BuildEnv();
        Assert.That(NewService(fs, env).IsHubInstalled(), Is.False);

        WriteFile(fs, HubExe, "exe");
        Assert.That(NewService(fs, env).IsHubInstalled(), Is.True);
    }

    [Test]
    public void OpenProject_LaunchesHubWithProjectPath()
    {
        var (fs, env) = BuildEnv();
        WriteFile(fs, HubExe, "exe");
        var runner = new Mock<IProcessRunner>();

        NewService(fs, env, runner.Object).OpenProject(@"C:\proj");

        runner.Verify(r => r.Start(HubExe,
            It.Is<IReadOnlyList<string>>(a => a.Contains("--projectPath") && a.Contains(@"C:\proj")),
            It.IsAny<string?>()), Times.Once);
    }

    [Test]
    public void OpenProject_Throws_WhenHubMissing()
    {
        var (fs, env) = BuildEnv();
        var service = NewService(fs, env);

        Assert.That(() => service.OpenProject(@"C:\proj"), Throws.TypeOf<System.InvalidOperationException>());
    }
}
