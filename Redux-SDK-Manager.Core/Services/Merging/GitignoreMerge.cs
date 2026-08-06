using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Redux_SDK_Manager.Services.Merging;

/// <summary>
/// Merges a <c>.gitignore</c> so a template bump adopts the new template's rules while keeping the
/// lines the user added themselves. Result = the target template's content + the user-only lines.
/// </summary>
public static class GitignoreMerge
{
    private const string Marker = "# --- added in this project ---";

    /// <summary>Three way merge a .gitignore file</summary>
    /// <param name="baseText">The old template's .gitignore, or null for ingest.</param>
    /// <param name="theirsText">The target template's .gitignore.</param>
    /// <param name="mineText">The project's current .gitignore.</param>
    /// <returns>A three way merged .gitignore</returns>
    public static string Merge(string? baseText, string theirsText, string mineText)
    {
        var theirsSet = LineSet(theirsText);
        var baseSet = baseText is null ? new HashSet<string>(StringComparer.Ordinal) : LineSet(baseText);

        // Lines the user added that were never in the template - not in the target template, and (for
        // an upgrade) not in the old template either, so a template-removed rule isn't resurrected.
        // Our own marker line is skipped so repeated merges stay idempotent.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var userAdditions = (from line in Lines(mineText)
            let trimmed = line.Trim()
            where trimmed.Length != 0 && trimmed != Marker
            where !theirsSet.Contains(trimmed) && !baseSet.Contains(trimmed)
            where seen.Add(trimmed)
            select line).ToList();

        if (userAdditions.Count == 0) return theirsText;

        var builder = new StringBuilder(theirsText);
        if (!theirsText.EndsWith('\n')) builder.Append('\n');
        builder.Append('\n').Append(Marker).Append('\n');
        foreach (var line in userAdditions)
        {
            builder.Append(line).Append('\n');
        }

        return builder.ToString();
    }

    private static IEnumerable<string> Lines(string text) => text.Replace("\r\n", "\n").Split('\n');

    private static HashSet<string> LineSet(string text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in Lines(text))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0) set.Add(trimmed);
        }

        return set;
    }
}