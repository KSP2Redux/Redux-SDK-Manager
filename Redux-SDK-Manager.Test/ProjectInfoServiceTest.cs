using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Testably.Abstractions.Testing;

namespace Redux_SDK_Manager.Test;

public class ProjectInfoServiceTest
{
    private const string ProjectDir = @"C:\proj";

    private static ProjectInfoService NewService(out MockFileSystem fs)
    {
        fs = new MockFileSystem(o => o.SimulatingOperatingSystem(SimulationMode.Windows));
        return new ProjectInfoService(fs, Mock.Of<ILogService>());
    }

    [Test]
    public void WriteThenRead_RoundTripsNameAndVersion()
    {
        var service = NewService(out var fs);

        service.Write(ProjectDir, new ProjectInfo { Name = "My Cool Mod", Version = "26w32b" });
        var read = service.Read(ProjectDir);

        Assert.Multiple(() =>
        {
            Assert.That(read!.Name, Is.EqualTo("My Cool Mod"));
            Assert.That(read.Version, Is.EqualTo("26w32b"));
        });
    }

    [Test]
    public void Write_EscapesSpecialCharactersInName()
    {
        var service = NewService(out var fs);

        service.Write(ProjectDir, new ProjectInfo { Name = "Name \"with\" quotes", Version = "26w32b" });

        Assert.That(service.Read(ProjectDir)!.Name, Is.EqualTo("Name \"with\" quotes"));
    }

    [Test]
    public void Read_ReturnsNull_WhenMissing()
    {
        var service = NewService(out var fs);
        fs.Directory.CreateDirectory(ProjectDir);

        Assert.That(service.Read(ProjectDir), Is.Null);
    }

    [Test]
    public void Read_ReturnsNull_WhenMalformed()
    {
        var service = NewService(out var fs);
        fs.Directory.CreateDirectory(ProjectDir);
        fs.File.WriteAllText(fs.Path.Combine(ProjectDir, "project.info"), "this = is = not valid toml");

        Assert.That(service.Read(ProjectDir), Is.Null);
    }
}
