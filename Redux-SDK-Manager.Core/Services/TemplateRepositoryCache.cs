using System;
using System.IO.Abstractions;

namespace Redux_SDK_Manager.Services;

public interface ITemplateRepositoryCache
{
    /// <summary>Absolute path of the local clone of the template distribution repo.</summary>
    string RepositoryPath { get; }

    /// <summary>
    /// Brings the local mirror up to date: clones it if it is missing, otherwise fetches. A fetch
    /// failure on an existing mirror is tolerated (the stale local copy is kept) so the manager still
    /// works offline once it has cloned once.
    /// </summary>
    void Sync();

    /// <summary>Clones the mirror if it is missing; a no-op when it already exists (no fetch).</summary>
    void EnsureCloned();
}

/// <summary>
/// Keeps a local clone of the template distribution repo under the manager's storage, so listing
/// versions, reading each version's Unity target, and fetching a version to apply all read from one
/// local checkout instead of hitting the network per operation.
/// </summary>
public class TemplateRepositoryCache : ITemplateRepositoryCache
{
    private const string RepoFolderName = "templates-repo";

    private readonly IGitService _gitService;
    private readonly IConfigService _configService;
    private readonly IFileSystem _fileSystem;
    private readonly ILogService _log;

    public TemplateRepositoryCache(
        IGitService gitService, IConfigService configService, IFileSystem fileSystem, ILogService log)
    {
        _gitService = gitService;
        _configService = configService;
        _fileSystem = fileSystem;
        _log = log;
    }

    public string RepositoryPath
        => _fileSystem.Path.Combine(_configService.GetLocalStorageDirectory(), RepoFolderName);

    public void Sync()
    {
        if (Exists())
        {
            try
            {
                _gitService.Fetch(RepositoryPath);
            }
            catch (Exception e)
            {
                _log.Warn($"Could not refresh the template mirror, using the existing local copy. {e.Message}");
            }
        }
        else
        {
            Clone();
        }
    }

    public void EnsureCloned()
    {
        if (!Exists()) Clone();
    }

    private bool Exists()
        => _fileSystem.Directory.Exists(_fileSystem.Path.Combine(RepositoryPath, ".git"));

    private void Clone()
    {
        var url = _configService.Config.TemplatesRepositoryUrl;
        _log.Info($"Cloning the template mirror from {url} into {RepositoryPath}.");
        _fileSystem.Directory.CreateDirectory(_configService.GetLocalStorageDirectory());
        _gitService.CloneMirror(url, RepositoryPath);
    }
}
