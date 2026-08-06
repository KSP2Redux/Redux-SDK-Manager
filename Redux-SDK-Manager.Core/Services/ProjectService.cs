using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services.Merging;

namespace Redux_SDK_Manager.Services;

public interface IProjectService
{
    /// <summary>
    /// Creates a new project at <paramref name="targetPath"/> from a template version: fetches the
    /// version's tree, materializes it (minus git metadata), and records it in config. The
    /// <c>template.version</c> stamp rides along in the tree. Throws if the target is non-empty.
    /// </summary>
    void CreateProject(TemplateVersion version, string targetPath);

    /// <summary>
    /// Upgrades (or repairs) an existing managed project to <paramref name="toVersion"/>. Fetches
    /// the project's current version and the target, deletes template files that no longer exist in
    /// the target, overlays the target tree, deletes SDK-copied files so the SDK regenerates them,
    /// and clears the regenerated Unity/ThunderKit caches. Files outside the template tree (the
    /// authored mod, dependency drops) are untouched. Throws if the project has no readable
    /// <c>template.version</c>.
    /// </summary>
    void UpgradeProject(string projectPath, TemplateVersion toVersion);

    /// <summary>
    /// Adopts an existing pre-manager project (one with no <c>template.version</c>) and brings it to
    /// <paramref name="version"/>. Because the project's original template version is unknown this is
    /// an overlay only. The target tree is written over the project and stamped, but stale files
    /// from the original template that no longer exist in the target cannot be identified or removed
    /// (a later <see cref="UpgradeProject"/> from this version onward handles deletions). Throws if
    /// the path isn't a Unity project or is already a managed project.
    /// </summary>
    void IngestProject(string projectPath, TemplateVersion version);

    /// <summary>
    /// Registers an already-managed project (one that already has a <c>template.version</c>, e.g. a
    /// fresh clone from a git repo) with the manager as-is. Tracks it in config and touches nothing
    /// else. Returns the detected version. Throws if the directory is missing or the project isn't
    /// managed (use <see cref="IngestProject"/> for an unmanaged project).
    /// </summary>
    TemplateVersion ImportProject(string projectPath);
}

public class ProjectService(
    ITemplateCatalogService catalogService,
    ITemplateVersionService versionService,
    IProjectInfoService projectInfoService,
    IConfigService configService,
    IFileSystem fileSystem,
    ILogService logService,
    IPromptService promptService) : IProjectService
{
    private const string CreateAlert =
        "New project created. On first launch there will be compiler errors, ignore them and import via thunderkit as normal.";

    private const string ApplyAlert =
        "Project initialization complete, on next launch there will be compiler errors, ignore them and import via thunderkit as normal.";

    public void CreateProject(TemplateVersion version, string targetPath)
    {
        if (fileSystem.Directory.Exists(targetPath) &&
            fileSystem.Directory.EnumerateFileSystemEntries(targetPath).Any())
        {
            throw new InvalidOperationException(
                $"Target directory '{targetPath}' already exists and is not empty.");
        }

        var name = NonEmptyOr(promptService.Ask("Project name", FolderName(targetPath)), FolderName(targetPath));
        logService.Info($"Creating project '{name}' at '{targetPath}' from template {version.Raw}.");

        var fetchDir = NewFetchDir();
        try
        {
            catalogService.FetchVersion(version, fetchDir);
            CopyTree(fetchDir, targetPath);
        }
        finally
        {
            TryDeleteDirectory(fetchDir);
        }

        StampProject(targetPath, name, version);
        TrackProject(targetPath);
        logService.Info($"Created project '{name}' at '{targetPath}'.");
        promptService.Alert(CreateAlert);
    }

    public void UpgradeProject(string projectPath, TemplateVersion toVersion)
    {
        var currentVersion = versionService.DetectProjectVersion(projectPath)
            ?? throw new InvalidOperationException(
                $"'{projectPath}' is not a Redux template project (no readable template.version).");

        // The overlay resets template-owned files (and Unity's productName) but never touches
        // project.info, so the existing name carries across the upgrade. Fall back to the folder
        // name for a project the manager has not stamped project.info into yet.
        var name = NonEmptyOr(projectInfoService.Read(projectPath)?.Name, FolderName(projectPath));

        logService.Info($"Upgrading '{projectPath}' ('{name}') from {currentVersion.Raw} to {toVersion.Raw}.");
        ApplyVersion(projectPath, toVersion, currentVersion);
        StampProject(projectPath, name, toVersion);
        TrackProject(projectPath);
    }

    public void IngestProject(string projectPath, TemplateVersion version)
    {
        if (!LooksLikeUnityProject(projectPath))
        {
            throw new InvalidOperationException(
                $"'{projectPath}' does not look like a Unity project (no ProjectSettings/ProjectVersion.txt).");
        }

        if (versionService.DetectProjectVersion(projectPath) is not null)
        {
            throw new InvalidOperationException(
                $"'{projectPath}' is already a managed project. Use UpgradeProject instead.");
        }

        var name = NonEmptyOr(promptService.Ask("Project name", FolderName(projectPath)), FolderName(projectPath));
        logService.Info($"Ingesting '{projectPath}' as '{name}' at template {version.Raw}.");
        ApplyVersion(projectPath, version, fromVersion: null);
        StampProject(projectPath, name, version);
        TrackProject(projectPath);
    }

    public TemplateVersion ImportProject(string projectPath)
    {
        if (!fileSystem.Directory.Exists(projectPath))
        {
            throw new InvalidOperationException($"Directory '{projectPath}' does not exist.");
        }

        var version = versionService.DetectProjectVersion(projectPath)
            ?? throw new InvalidOperationException(
                $"'{projectPath}' is not a managed project (no template.version). Use IngestProject to adopt an unmanaged project.");

        TrackProject(projectPath);
        logService.Info($"Imported '{projectPath}' (template {version.Raw}).");
        return version;
    }

    // Overlays toVersion's tree onto the project. When fromVersion is known (upgrade), also removes
    // template files that no longer exist in the target; when null (ingest), overlays only.
    private void ApplyVersion(string projectPath, TemplateVersion toVersion, TemplateVersion? fromVersion)
    {
        var newFetch = NewFetchDir();
        var oldFetch = fromVersion is null ? null : NewFetchDir();
        try
        {
            catalogService.FetchVersion(toVersion, newFetch);

            if (fromVersion is not null)
            {
                catalogService.FetchVersion(fromVersion, oldFetch!);

                var newFiles = new HashSet<string>(EnumerateTemplateFiles(newFetch), StringComparer.OrdinalIgnoreCase);
                var removed = 0;
                foreach (var relative in EnumerateTemplateFiles(oldFetch!))
                {
                    if (newFiles.Contains(relative)) continue;
                    var path = fileSystem.Path.Combine(projectPath, relative);
                    if (!fileSystem.File.Exists(path)) continue;
                    fileSystem.File.Delete(path);
                    removed++;
                }

                if (removed > 0)
                {
                    logService.Debug($"Removed {removed} stale template file(s) not present in {toVersion.Raw}.");
                }
            }

            OverlayTree(newFetch, projectPath, oldFetch);
        }
        finally
        {
            TryDeleteDirectory(newFetch);
            if (oldFetch is not null) TryDeleteDirectory(oldFetch);
        }

        DeleteSdkCopiedFiles(projectPath);
        ClearRegeneratedCaches(projectPath);
        
        promptService.Alert(ApplyAlert);
    }

    // Records the manager's local metadata (name + version) in project.info, then drops the
    // template's remote version stamp - locally project.info is the source of truth for the version.
    private void StampProject(string projectPath, string name, TemplateVersion version)
    {
        projectInfoService.Write(projectPath, new ProjectInfo { Name = name, Version = version.Raw });

        var templateStamp = fileSystem.Path.Combine(projectPath, TemplateVersionService.VersionFileName);
        if (fileSystem.File.Exists(templateStamp)) fileSystem.File.Delete(templateStamp);
    }

    private string FolderName(string path) => fileSystem.Path.GetFileName(path.TrimEnd('/', '\\'));

    private static string NonEmptyOr(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private bool LooksLikeUnityProject(string projectPath) =>
        fileSystem.File.Exists(fileSystem.Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt"));

    private string NewFetchDir() => fileSystem.Path.Combine(
        fileSystem.Path.GetTempPath(), "ReduxSdkManager", "fetch", Guid.NewGuid().ToString("N"));

    private IEnumerable<string> EnumerateTemplateFiles(string dir) =>
        fileSystem.Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(file => fileSystem.Path.GetRelativePath(dir, file))
            .Where(relative => !IsGitMetadata(relative));

    // Plain copy of a template tree onto an (empty) target - used by CreateProject.
    private void CopyTree(string sourceDir, string destinationDir)
    {
        foreach (var relative in EnumerateTemplateFiles(sourceDir))
        {
            CopyFile(sourceDir, destinationDir, relative);
        }
    }

    // Overlay of a template tree onto an existing project. Most files overwrite, but a couple of
    // files that carry user additions (manifest.json, .gitignore) are merged instead so an upgrade
    // keeps the user's own packages / ignore rules. baseDir is the old template (null for ingest).
    private void OverlayTree(string sourceDir, string destinationDir, string? baseDir)
    {
        foreach (var relative in EnumerateTemplateFiles(sourceDir))
        {
            if (TryMergeFile(relative, sourceDir, destinationDir, baseDir, out var merged))
            {
                fileSystem.File.WriteAllText(fileSystem.Path.Combine(destinationDir, relative), merged);
            }
            else
            {
                CopyFile(sourceDir, destinationDir, relative);
            }
        }
    }

    private void CopyFile(string sourceDir, string destinationDir, string relative)
    {
        var destFile = fileSystem.Path.Combine(destinationDir, relative);
        var destParent = fileSystem.Path.GetDirectoryName(destFile);
        if (!string.IsNullOrEmpty(destParent))
        {
            fileSystem.Directory.CreateDirectory(destParent);
        }

        fileSystem.File.Copy(fileSystem.Path.Combine(sourceDir, relative), destFile, overwrite: true);
    }

    private bool TryMergeFile(string relative, string sourceDir, string destinationDir, string? baseDir, out string merged)
    {
        merged = "";
        var normalized = relative.Replace('\\', '/');
        var isManifest = normalized.Equals("Packages/manifest.json", StringComparison.OrdinalIgnoreCase);
        var isGitignore = normalized.Equals(".gitignore", StringComparison.OrdinalIgnoreCase);
        if (!isManifest && !isGitignore) return false;

        var minePath = fileSystem.Path.Combine(destinationDir, relative);
        if (!fileSystem.File.Exists(minePath)) return false; // nothing to merge into - let it copy

        var theirs = fileSystem.File.ReadAllText(fileSystem.Path.Combine(sourceDir, relative));
        var mine = fileSystem.File.ReadAllText(minePath);

        string? baseText = null;
        if (baseDir is not null)
        {
            var basePath = fileSystem.Path.Combine(baseDir, relative);
            if (fileSystem.File.Exists(basePath)) baseText = fileSystem.File.ReadAllText(basePath);
        }

        merged = isManifest
            ? ManifestMerge.Merge(baseText, theirs, mine)
            : GitignoreMerge.Merge(baseText, theirs, mine);
        return true;
    }

    // The SDK (KSP2UnityToolsManager, [InitializeOnLoad]) re-copies this from its package on next
    // editor load if it's absent, so deleting it forces a version-appropriate copy.
    private void DeleteSdkCopiedFiles(string projectPath)
    {
        foreach (var name in new[] { "ImportKsp2ToEditor.asset", "ImportKsp2ToEditor.asset.meta" })
        {
            var path = fileSystem.Path.Combine(projectPath, "Assets", name);
            if (fileSystem.File.Exists(path)) fileSystem.File.Delete(path);
        }
    }

    // Unity/ThunderKit regenerate these on next open - stale copies break a version bump. Dropping
    // packages-lock.json forces the package manager to re-resolve every dependency against the
    // freshly applied manifest instead of reusing pinned versions from before the upgrade.
    private void ClearRegeneratedCaches(string projectPath)
    {
        DeleteDirectory(fileSystem.Path.Combine(projectPath, "Library"));
        DeleteDirectory(fileSystem.Path.Combine(projectPath, "Packages", "KSP2_x64"));

        var packagesLock = fileSystem.Path.Combine(projectPath, "Packages", "packages-lock.json");
        if (fileSystem.File.Exists(packagesLock)) fileSystem.File.Delete(packagesLock);

        logService.Debug($"Cleared regenerated caches (Library, Packages/KSP2_x64, packages-lock.json) for '{projectPath}'.");
    }

    // Clears read-only attributes before deleting: git marks its pack files (.idx/.pack) read-only
    // on Windows, which would otherwise make a recursive delete throw UnauthorizedAccessException.
    private void DeleteDirectory(string dir)
    {
        if (!fileSystem.Directory.Exists(dir)) return;

        foreach (var file in fileSystem.Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var attributes = fileSystem.File.GetAttributes(file);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                fileSystem.File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
        }

        fileSystem.Directory.Delete(dir, recursive: true);
    }

    // Best-effort cleanup of a scratch dir - a leftover temp clone must never abort the operation.
    private void TryDeleteDirectory(string dir)
    {
        try
        {
            DeleteDirectory(dir);
        }
        catch (Exception)
        {
            logService.Warn($"Cleanup of temporary directory {dir} failed.");
        }
    }

    // The fetched tree is a git clone, so its .git directory must not land in the project.
    private static bool IsGitMetadata(string relativePath) =>
        relativePath.Split('/', '\\').Any(segment => segment.Equals(".git", StringComparison.Ordinal));

    private void TrackProject(string targetPath)
    {
        if (configService.Config.ProjectPaths.Contains(targetPath)) return;

        configService.Config.ProjectPaths.Add(targetPath);
        configService.Save();
    }
}
