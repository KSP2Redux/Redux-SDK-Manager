using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Redux_SDK_Manager.Models;

/// <summary>
/// A template version stamp (e.g. <c>0.2.8.5</c> or <c>0.2.10-26w44a</c>) together with the channel it
/// belongs to, inferred from its shape.
/// </summary>
public sealed partial record TemplateVersion(string Raw, TemplateChannel Channel)
{
    /// <summary>Orders game versions numerically and snapshot stamps chronologically.</summary>
    public static IComparer<TemplateVersion> NewestFirst { get; } =
        Comparer<TemplateVersion>.Create((a, b) => CompareOldestFirst(b, a));

    public static TemplateVersion Parse(string raw)
    {
        var trimmed = raw.Trim();
        return new TemplateVersion(trimmed, Classify(trimmed));
    }

    /// <summary>The weekly snapshot label, including for older tags without a game version.</summary>
    public string? SnapshotStamp => SnapshotRegex().Match(Raw) is { Success: true } match
        ? match.Groups["stamp"].Value : null;

    /// <summary>The game version prefix when this is a versioned snapshot.</summary>
    public string? GameVersion => SnapshotRegex().Match(Raw) is { Success: true } match
        && match.Groups["game"].Success ? match.Groups["game"].Value : null;

    private static int CompareOldestFirst(TemplateVersion a, TemplateVersion b)
    {
        if (a.Channel == TemplateChannel.Release && b.Channel == TemplateChannel.Release)
            return CompareRelease(a.Raw, b.Raw);

        if (a.Channel == TemplateChannel.Snapshot && b.Channel == TemplateChannel.Snapshot)
        {
            if (a.GameVersion is not null && b.GameVersion is not null)
            {
                var game = CompareRelease(a.GameVersion, b.GameVersion);
                if (game != 0) return game;
            }

            var stamp = string.CompareOrdinal(a.SnapshotStamp, b.SnapshotStamp);
            if (stamp != 0) return stamp;
            return (a.GameVersion is not null).CompareTo(b.GameVersion is not null);
        }

        return string.CompareOrdinal(a.Raw, b.Raw);
    }

    private static int CompareRelease(string a, string b)
    {
        var pa = a.Split('.').Select(ToInt).ToArray();
        var pb = b.Split('.').Select(ToInt).ToArray();
        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var x = i < pa.Length ? pa[i] : 0;
            var y = i < pb.Length ? pb[i] : 0;
            if (x != y) return x.CompareTo(y);
        }
        return 0;

        static int ToInt(string s) => int.TryParse(s, out var n) ? n : 0;
    }

    private static TemplateChannel Classify(string version)
    {
        if (SnapshotRegex().IsMatch(version)) return TemplateChannel.Snapshot;
        if (ReleaseRegex().IsMatch(version)) return TemplateChannel.Release;
        return TemplateChannel.Unknown;
    }

    // Keep older bare weekly tags usable alongside game-versioned snapshots.
    [GeneratedRegex(@"^(?:(?<game>\d+\.\d+\.\d+)-)?(?<stamp>\d{2}w\d{2}[a-z])$")]
    private static partial Regex SnapshotRegex();

    // Dotted numeric release, e.g. 0.2.8.5
    [GeneratedRegex(@"^\d+(\.\d+)+$")]
    private static partial Regex ReleaseRegex();
}
