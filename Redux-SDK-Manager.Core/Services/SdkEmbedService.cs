using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Services;

/// <summary>
/// Embeds the SDK package as a local <c>git</c> checkout under <c>Packages/</c> for SDK development.
/// Unity prefers an embedded package over the manifest's git dependency of the same name, so the mod
/// project builds against the local SDK. The embed is a two-step operation so it can fail before the
/// project is mutated: <see cref="StageClone"/> resolves and clones up front, then <see cref="Commit"/>
/// moves it into place after the rest of the operation has applied.
/// </summary>
public interface ISdkEmbedService
{
    /// <summary>True if the SDK package is already embedded in the project's <c>Packages/</c>.</summary>
    bool IsEmbedded(string projectPath);

    /// <summary>
    /// Resolves the SDK repo+ref (from <paramref name="manifestSourceDir"/>'s manifest, else the
    /// template for <paramref name="version"/> in the mirror, else the SDK's main branch) and clones
    /// it to a temporary staging directory. Throws if the clone/checkout fails. Returns the staging path.
    /// </summary>
    string StageClone(string manifestSourceDir, TemplateVersion version);

    /// <summary>
    /// Finalizes an embed: moves <paramref name="stagingDir"/> (when non-null) into
    /// <c>Packages/ksp2community.ksp2unitytools</c> and ensures <c>.gitignore</c> excludes it. A null
    /// staging directory means the SDK was already embedded, so only the ignore rule is (re)applied.
    /// </summary>
    void Commit(string projectPath, string? stagingDir);
}

public sealed class SdkEmbedService(
    IGitService gitService,
    ITemplateRepositoryCache cache,
    IFileSystem fileSystem,
    ILogService logService) : ISdkEmbedService
{
    public const string SdkPackageName = "ksp2community.ksp2unitytools";
    private const string DefaultSdkRepoUrl = "https://github.com/KSP2Redux/SDK.git";
    private const string DefaultSdkRef = "main";
    private const string ManifestRelativePath = "Packages/manifest.json";
    private static readonly string GitignoreEntry = $"/Packages/{SdkPackageName}/";

    public bool IsEmbedded(string projectPath)
        => fileSystem.Directory.Exists(fileSystem.Path.Combine(projectPath, "Packages", SdkPackageName));

    public string StageClone(string manifestSourceDir, TemplateVersion version)
    {
        var (url, reference) = ResolveSource(manifestSourceDir, version);
        var staging = fileSystem.Path.Combine(
            fileSystem.Path.GetTempPath(), "ReduxSdkManager", "sdk-embed", Guid.NewGuid().ToString("N"));

        logService.Info($"Embedding SDK: cloning {url} at {reference}.");
        gitService.CloneAndCheckout(url, reference, staging);
        return staging;
    }

    public void Commit(string projectPath, string? stagingDir)
    {
        if (stagingDir is not null)
        {
            var target = fileSystem.Path.Combine(projectPath, "Packages", SdkPackageName);
            MoveDirectory(stagingDir, target);
            logService.Info($"Embedded SDK into '{target}'.");
        }

        EnsureGitignore(projectPath);
    }

    // URL always comes from a manifest dependency when present; the ref falls back to the SDK's main
    // branch when the dependency has no '#ref'. Only when no dependency exists anywhere do we fall
    // back to the known SDK repo at main.
    private (string url, string reference) ResolveSource(string manifestSourceDir, TemplateVersion version)
    {
        var applying = fileSystem.Path.Combine(manifestSourceDir, ManifestRelativePath);
        if (fileSystem.File.Exists(applying) &&
            TryParseSdkDependency(fileSystem.File.ReadAllText(applying), out var url, out var reference))
        {
            return (url, reference ?? DefaultSdkRef);
        }

        cache.EnsureCloned();
        var mirrorManifest = gitService.ShowFile(cache.RepositoryPath, version.Raw, ManifestRelativePath);
        if (TryParseSdkDependency(mirrorManifest, out url, out reference))
        {
            return (url, reference ?? DefaultSdkRef);
        }

        logService.Warn($"No '{SdkPackageName}' dependency found; embedding {DefaultSdkRepoUrl} at {DefaultSdkRef}.");
        return (DefaultSdkRepoUrl, DefaultSdkRef);
    }

    // Reads dependencies["ksp2community.ksp2unitytools"], e.g. "https://.../SDK.git#beta-6", splitting
    // the git URL from its optional '#ref'.
    private static bool TryParseSdkDependency(string? manifestJson, out string url, out string? reference)
    {
        url = "";
        reference = null;
        if (string.IsNullOrWhiteSpace(manifestJson)) return false;

        try
        {
            using var doc = JsonDocument.Parse(manifestJson);
            if (!doc.RootElement.TryGetProperty("dependencies", out var deps) ||
                !deps.TryGetProperty(SdkPackageName, out var dep) ||
                dep.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var value = dep.GetString() ?? "";
            var hash = value.IndexOf('#');
            if (hash >= 0)
            {
                url = value[..hash];
                reference = value[(hash + 1)..];
            }
            else
            {
                url = value;
            }

            return url.Length > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void EnsureGitignore(string projectPath)
    {
        var gitignore = fileSystem.Path.Combine(projectPath, ".gitignore");
        var existing = fileSystem.File.Exists(gitignore) ? fileSystem.File.ReadAllText(gitignore) : "";

        var alreadyIgnored = existing
            .Split('\n')
            .Any(line => line.Trim().TrimEnd('/').Equals(GitignoreEntry.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
        if (alreadyIgnored) return;

        var separator = existing.Length == 0 || existing.EndsWith('\n') ? "" : "\n";
        fileSystem.File.WriteAllText(gitignore, $"{existing}{separator}{GitignoreEntry}\n");
    }

    // Directory.Move is a rename on the same volume; a cross-volume move throws, so fall back to a
    // recursive copy plus a best-effort delete of the staging directory.
    private void MoveDirectory(string source, string target)
    {
        var parent = fileSystem.Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent)) fileSystem.Directory.CreateDirectory(parent);

        try
        {
            fileSystem.Directory.Move(source, target);
        }
        catch (IOException)
        {
            CopyDirectory(source, target);
            DeleteBestEffort(source);
        }
    }

    private void CopyDirectory(string source, string target)
    {
        foreach (var file in fileSystem.Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = fileSystem.Path.GetRelativePath(source, file);
            var destination = fileSystem.Path.Combine(target, relative);
            var destinationDir = fileSystem.Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDir)) fileSystem.Directory.CreateDirectory(destinationDir);
            fileSystem.File.Copy(file, destination, overwrite: true);
        }
    }

    private void DeleteBestEffort(string dir)
    {
        if (!fileSystem.Directory.Exists(dir)) return;

        try
        {
            // git marks pack files read-only, which would otherwise block the delete on Windows.
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
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logService.Warn($"Could not clean up SDK staging directory '{dir}': {e.Message}");
        }
    }
}
