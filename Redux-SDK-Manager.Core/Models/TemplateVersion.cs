using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Redux_SDK_Manager.Models;

/// <summary>
/// A template version stamp (e.g. <c>0.2.8.5</c> or <c>26w32a</c>) together with the channel it
/// belongs to, inferred from its shape.
/// </summary>
public sealed partial record TemplateVersion(string Raw, TemplateChannel Channel)
{
    /// <summary>Orders versions newest first. Releases compare numerically per segment; the
    /// fixed-width snapshot stamp (26w32a) sorts correctly as an ordinal string.</summary>
    public static IComparer<TemplateVersion> NewestFirst { get; } =
        Comparer<TemplateVersion>.Create((a, b) => CompareOldestFirst(b, a));

    public static TemplateVersion Parse(string raw)
    {
        var trimmed = raw.Trim();
        return new TemplateVersion(trimmed, Classify(trimmed));
    }

    private static int CompareOldestFirst(TemplateVersion a, TemplateVersion b)
        => a.Channel == TemplateChannel.Release && b.Channel == TemplateChannel.Release
            ? CompareRelease(a.Raw, b.Raw)
            : string.CompareOrdinal(a.Raw, b.Raw);

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

    // Minecraft-style snapshot stamp, e.g. 26w32a
    [GeneratedRegex(@"^\d{2}w\d{2}[a-z]$")]
    private static partial Regex SnapshotRegex();

    // Dotted numeric release, e.g. 0.2.8.5
    [GeneratedRegex(@"^\d+(\.\d+)+$")]
    private static partial Regex ReleaseRegex();
}
