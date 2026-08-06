using System;
using System.IO.Abstractions;
using Moq;
using Redux_SDK_Manager.Services;
using Testably.Abstractions.Testing;

namespace Redux_SDK_Manager.Test;

public class ConfigServiceTest
{
    private const string AppData = @"C:\AppDataLocal";
    private const string StorageDir = @"C:\AppDataLocal\ReduxSdkManager";
    private const string ConfigPath = @"C:\AppDataLocal\ReduxSdkManager\redux-sdk-manager-config.json";
    private const string DefaultTemplatesUrl = "https://github.com/KSP2Redux/Redux.Templates.git";

    private static (MockFileSystem fs, MockEnvironmentProvider env) BuildEnv()
    {
        var fs = new MockFileSystem(o => o.SimulatingOperatingSystem(SimulationMode.Windows));
        var env = new MockEnvironmentProvider();
        env.SetFolderPath(Environment.SpecialFolder.LocalApplicationData, AppData);
        return (fs, env);
    }

    [Test]
    public void Constructor_CreatesFreshConfig_WhenNoneExists()
    {
        var (fs, env) = BuildEnv();

        var service = new ConfigService(fs, env, Mock.Of<ILogService>());

        Assert.That(fs.File.Exists(ConfigPath), Is.True);
        Assert.That(service.Config.StoragePath, Is.EqualTo(ConfigPath));
        Assert.That(service.Config.TemplatesRepositoryUrl, Is.EqualTo(DefaultTemplatesUrl));
        Assert.That(service.Config.ProjectPaths, Is.Empty);
    }

    [Test]
    public void Save_PersistsChanges_AcrossReload()
    {
        var (fs, env) = BuildEnv();
        var service = new ConfigService(fs, env, Mock.Of<ILogService>());
        service.Config.ProjectPaths.Add(@"C:\mods\MyMod");
        service.Save();

        var reloaded = new ConfigService(fs, env, Mock.Of<ILogService>());

        Assert.That(reloaded.Config.ProjectPaths, Does.Contain(@"C:\mods\MyMod"));
    }

    [Test]
    public void Constructor_RecreatesConfig_WhenFileCorrupt()
    {
        var (fs, env) = BuildEnv();
        fs.Directory.CreateDirectory(StorageDir);
        fs.File.WriteAllText(ConfigPath, "{ not valid json ]");

        var service = new ConfigService(fs, env, Mock.Of<ILogService>());

        Assert.That(service.Config.TemplatesRepositoryUrl, Is.EqualTo(DefaultTemplatesUrl));
    }
}
