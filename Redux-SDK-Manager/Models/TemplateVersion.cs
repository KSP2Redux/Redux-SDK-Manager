using System.Text.RegularExpressions;

namespace Redux_SDK_Manager.Models;

/// <summary>
/// A template version stamp (e.g. <c>0.2.8.5</c> or <c>26w32a</c>) together with the channel it
/// belongs to, inferred from its shape.
/// </summary>
public sealed partial record TemplateVersion(string Raw, TemplateChannel Channel)
{
    public static TemplateVersion Parse(string raw)
    {
        var trimmed = raw.Trim();
        return new TemplateVersion(trimmed, Classify(trimmed));
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
