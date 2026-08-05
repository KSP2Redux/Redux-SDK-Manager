using System;
using System.IO.Abstractions;

namespace Redux_SDK_Manager.Services;

/// <summary>Resolves the manager's per-user storage location under %LocalAppData%.</summary>
internal static class LocalStoragePaths
{
    public const string ReduxFolder = "ReduxSdkManager";

    public static string GetLocalStorageDirectory(IFileSystem fileSystem, IEnvironmentProvider environmentProvider)
    {
        var appDataPath = environmentProvider.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(appDataPath)
            ? throw new InvalidOperationException("Local application data folder path is unavailable.")
            : fileSystem.Path.Combine(appDataPath, ReduxFolder);
    }
}
