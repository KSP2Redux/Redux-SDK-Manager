using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Redux_SDK_Manager.Services.Merging;

/// <summary>
/// Merges a Unity <c>Packages/manifest.json</c> so a template version bump applies the template's
/// package add/remove/version changes while keeping the packages the user added themselves.
/// </summary>
public static class ManifestMerge
{
    // Relaxed escaping keeps git URLs (https://...#branch) readable instead of \u-escaped.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Three way merge a Packages/manifest.json
    /// </summary>
    /// <param name="baseJson">The old template's manifest, or null for ingest (= union, no removals).</param>
    /// <param name="theirsJson">The target template's manifest.</param>
    /// <param name="mineJson">The project's current manifest.</param>
    /// <returns>The merged json</returns>
    public static string Merge(string? baseJson, string theirsJson, string mineJson)
    {
        JsonObject mine;
        try
        {
            mine = JsonNode.Parse(mineJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            // Project manifest unreadable - fall back to the template's manifest.
            return theirsJson;
        }

        var result = ToDependencyDict(mine);
        var theirs = Dependencies(theirsJson);
        var baseDeps = baseJson is null ? null : Dependencies(baseJson);

        if (baseDeps is null)
        {
            // Ingest: union - template wins on shared ids, adds new, removes nothing.
            foreach (var (id, version) in theirs) result[id] = version;
        }
        else
        {
            // Upgrade: apply what the template changed between base and theirs.
            foreach (var (id, version) in theirs)
            {
                if (!baseDeps.TryGetValue(id, out var baseVersion) || baseVersion != version)
                {
                    result[id] = version; // template added or changed it (template-wins on conflict)
                }
            }

            foreach (var id in baseDeps.Keys.Where(id => !theirs.ContainsKey(id)))
            {
                result.Remove(id); // template removed it
            }
        }

        var dependencies = new JsonObject();
        foreach (var (id, version) in result.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            dependencies[id] = version;
        }

        mine["dependencies"] = dependencies;
        return mine.ToJsonString(WriteOptions);
    }

    private static Dictionary<string, string> Dependencies(string json)
        => JsonNode.Parse(json) is JsonObject obj ? ToDependencyDict(obj) : new(StringComparer.Ordinal);

    private static Dictionary<string, string> ToDependencyDict(JsonObject obj)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (obj["dependencies"] is not JsonObject deps) return dict;
        
        foreach (var (id, value) in deps)
        {
            if (value is not null) dict[id] = value.GetValue<string>();
        }

        return dict;
    }
}
