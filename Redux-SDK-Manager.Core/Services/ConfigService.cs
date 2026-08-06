using System;
using System.IO.Abstractions;
using System.Text.Json;
using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Services;

public interface IConfigService
{
    /// <summary>
    /// The current configuration for the SDK manager
    /// </summary>
    SdkManagerConfig Config { get; }

    /// <summary>
    /// Save the current configuration for the SDK manager
    /// </summary>
    void Save();

    /// <summary>
    /// Get the storage directory that the configuration will be in
    /// </summary>
    /// <returns>The local storage directory</returns>
    string GetLocalStorageDirectory();
}

public class ConfigService : IConfigService
{
    private const string ConfigFileName = "redux-sdk-manager-config.json";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironmentProvider _environmentProvider;
    private readonly ILogService _log;

    /// <inheritdoc/>
    public SdkManagerConfig Config { get; private set; } = null!;

    /// <summary>
    /// The dependency injected constructor for the config service
    /// </summary>
    /// <param name="fileSystem">The filesystem to be used</param>
    /// <param name="environmentProvider">The environment provider to be used</param>
    /// <param name="log">The logging service</param>
    public ConfigService(IFileSystem fileSystem, IEnvironmentProvider environmentProvider, ILogService log)
    {
        _fileSystem = fileSystem;
        _environmentProvider = environmentProvider;
        _log = log;
        LoadOrCreate();
    }

    /// <inheritdoc/>
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
                if (config is null)
                {
                    _log.Warn($"Config at {configFilePath} deserialized to null. A fresh config will be created.");
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Could not read config at {configFilePath} ({ex.GetType().Name}: {ex.Message}). A fresh config will be created.");
                config = null;
            }
        }

        if (config is null)
        {
            Config = new SdkManagerConfig { StoragePath = configFilePath };
            Save();
            _log.Info($"Fresh config written to {configFilePath}.");
        }
        else
        {
            Config = config;
            Config.StoragePath = configFilePath;
            _log.Info($"Config loaded from {configFilePath} (projects={Config.ProjectPaths.Count}).");
        }
    }


    /// <inheritdoc/>
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
        catch (Exception ex)
        {
            // A transient save failure (AppData briefly unreachable, read-only media) must never
            // crash the app - record it and carry on.
            _log.Error($"Failed to save config to {Config.StoragePath}.", ex);
        }
    }
}
