using System;
using System.IO.Abstractions;
using System.Text.Json;
using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Services;

public interface IConfigService
{
    SdkManagerConfig Config { get; }
    void Save();
    string GetLocalStorageDirectory();
}

public class ConfigService : IConfigService
{
    private const string ConfigFileName = "redux-sdk-manager-config.json";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironmentProvider _environmentProvider;

    public SdkManagerConfig Config { get; private set; } = null!;

    public ConfigService(IFileSystem fileSystem, IEnvironmentProvider environmentProvider)
    {
        _fileSystem = fileSystem;
        _environmentProvider = environmentProvider;
        LoadOrCreate();
    }

    public string GetLocalStorageDirectory()
        => LocalStoragePaths.GetLocalStorageDirectory(_fileSystem, _environmentProvider);

    private string GetConfigFilePath()
        => _fileSystem.Path.Combine(GetLocalStorageDirectory(), ConfigFileName);

    private void LoadOrCreate()
    {
        var storageDir = GetLocalStorageDirectory();
        _fileSystem.Directory.CreateDirectory(storageDir);
        var configFilePath = GetConfigFilePath();

        SdkManagerConfig? config = null;
        if (_fileSystem.File.Exists(configFilePath))
        {
            try
            {
                config = JsonSerializer.Deserialize<SdkManagerConfig>(_fileSystem.File.ReadAllText(configFilePath));
            }
            catch (Exception)
            {
                // Corrupt/unreadable config - recreate a fresh one below rather than crashing on start.
                config = null;
            }
        }

        if (config is null)
        {
            Config = new SdkManagerConfig { StoragePath = configFilePath };
            Save();
        }
        else
        {
            Config = config;
            Config.StoragePath = configFilePath;
        }
    }

    // Swallows I/O failures so a transient save error (AppData briefly unreachable, read-only
    // media, etc.) never crashes the app. Once log / message-box services exist, surface it here.
    public void Save()
    {
        try
        {
            var directory = _fileSystem.Path.GetDirectoryName(Config.StoragePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _fileSystem.Directory.CreateDirectory(directory);
            }

            _fileSystem.File.WriteAllText(Config.StoragePath, JsonSerializer.Serialize(Config, SerializerOptions));
        }
        catch (Exception)
        {
            // Intentionally swallowed for now (see note above).
        }
    }
}
