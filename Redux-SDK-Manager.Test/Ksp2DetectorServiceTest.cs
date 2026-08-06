using Moq;
using Redux_SDK_Manager.Services;
using Redux_SDK_Manager.Wrappers;
using Testably.Abstractions.Testing;

namespace Redux_SDK_Manager.Test;

public class Ksp2DetectorServiceTest
{
    private const string DefaultSteamRoot = @"C:\Program Files (x86)\Steam";
    private const string InstallDir = "Kerbal Space Program 2";

    private static MockFileSystem WindowsFs() => new(o => o.SimulatingOperatingSystem(SimulationMode.Windows));

    private static Ksp2DetectorService NewService(MockFileSystem fs, MockEnvironmentProvider? env = null)
        => new(fs, env ?? new MockEnvironmentProvider(), Mock.Of<ILogService>());

    private static void WriteFile(MockFileSystem fs, string path, string content)
    {
        var dir = fs.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) fs.Directory.CreateDirectory(dir);
        fs.File.WriteAllText(path, content);
    }

    // Lays down a Steam library at the given root with the KSP2 app manifest and executable.
    private static string InstallSteamKsp2(MockFileSystem fs, string steamRoot)
    {
        var steamapps = fs.Path.Combine(steamRoot, "steamapps");
        WriteFile(fs, fs.Path.Combine(steamapps, "appmanifest_954850.acf"),
            $"\"AppState\"\n{{\n\t\"appid\"\t\"954850\"\n\t\"installdir\"\t\"{InstallDir}\"\n}}");
        var exe = fs.Path.Combine(steamapps, "common", InstallDir, "KSP2_x64.exe");
        WriteFile(fs, exe, "");
        return exe;
    }

    [Test]
    public void DetectsKsp2_InDefaultSteamRoot()
    {
        var fs = WindowsFs();
        var exe = InstallSteamKsp2(fs, DefaultSteamRoot);

        var found = NewService(fs).DetectKsp2InstallLocation();

        Assert.That(found, Is.EqualTo(exe));
    }

    [Test]
    public void DetectsKsp2_InLibraryFolderFromVdf()
    {
        var fs = WindowsFs();
        // The default root exists but only points at another library that holds the game.
        fs.Directory.CreateDirectory(fs.Path.Combine(DefaultSteamRoot, "steamapps"));
        WriteFile(fs, fs.Path.Combine(DefaultSteamRoot, "steamapps", "libraryfolders.vdf"),
            "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\"D:\\\\SteamLibrary\"\n\t}\n}");
        var exe = InstallSteamKsp2(fs, @"D:\SteamLibrary");

        var found = NewService(fs).DetectKsp2InstallLocation();

        Assert.That(found, Is.EqualTo(exe));
    }

    [Test]
    public void ReturnsNull_WhenNothingInstalled()
    {
        var fs = WindowsFs();

        var found = NewService(fs).DetectKsp2InstallLocation();

        Assert.That(found, Is.Null);
    }

    [Test]
    public void FallsBackToEpicLocation()
    {
        var fs = WindowsFs();
        const string epic = @"C:\Program Files\Epic Games\KerbalSpaceProgram2\KSP2_x64.exe";
        WriteFile(fs, epic, "");

        var found = NewService(fs).DetectKsp2InstallLocation();

        Assert.That(found, Is.EqualTo(epic));
    }

    [Test]
    public void FallsBackToPrivateDivisionLocation()
    {
        var fs = WindowsFs();
        const string localAppData = @"C:\Users\me\AppData\Local";
        var env = new MockEnvironmentProvider();
        env.SetFolderPath(System.Environment.SpecialFolder.LocalApplicationData, localAppData);
        var exe = fs.Path.Combine(localAppData, "Programs", "Kerbal Space Program 2", "KSP2_x64.exe");
        WriteFile(fs, exe, "");

        var found = NewService(fs, env).DetectKsp2InstallLocation();

        Assert.That(found, Is.EqualTo(exe));
    }
}
