using System.Linq;
using Redux_SDK_Manager.Services.Merging;

namespace Redux_SDK_Manager.Test;

public class ManifestMergeTest
{
    private static string Manifest(params (string id, string version)[] deps)
    {
        var lines = deps.Select(d => $"    \"{d.id}\": \"{d.version}\"");
        return "{\n  \"dependencies\": {\n" + string.Join(",\n", lines) + "\n  }\n}";
    }

    [Test]
    public void Upgrade_AppliesTemplateAddChangeRemove_KeepsUserPackages()
    {
        var baseM = Manifest(("com.unity.burst", "1.8.28"), ("com.unity.entities", "6.4.0"), ("old.pkg", "1.0.0"));
        var theirs = Manifest(("com.unity.burst", "1.8.29"), ("com.unity.entities", "6.4.0"), ("new.pkg", "2.0.0"));
        var mine = Manifest(("com.unity.burst", "1.8.28"), ("com.unity.entities", "6.4.0"), ("old.pkg", "1.0.0"), ("user.pkg", "9.9.9"));

        var merged = ManifestMerge.Merge(baseM, theirs, mine);

        Assert.Multiple(() =>
        {
            Assert.That(merged, Does.Contain("\"com.unity.burst\": \"1.8.29\""));   // changed -> updated
            Assert.That(merged, Does.Contain("\"new.pkg\": \"2.0.0\""));            // added
            Assert.That(merged, Does.Not.Contain("old.pkg"));                       // removed
            Assert.That(merged, Does.Contain("\"user.pkg\": \"9.9.9\""));           // user package kept
            Assert.That(merged, Does.Contain("\"com.unity.entities\": \"6.4.0\"")); // unchanged kept
        });
    }

    [Test]
    public void Upgrade_TemplateWins_OnSharedVersionConflict()
    {
        var merged = ManifestMerge.Merge(
            Manifest(("com.unity.burst", "1.8.28")),
            Manifest(("com.unity.burst", "1.8.29")),
            Manifest(("com.unity.burst", "1.8.20"))); // user override

        Assert.That(merged, Does.Contain("\"com.unity.burst\": \"1.8.29\""));
    }

    [Test]
    public void Upgrade_KeepsUserOverride_WhenTemplateLeftThatPackageUnchanged()
    {
        var merged = ManifestMerge.Merge(
            Manifest(("com.unity.burst", "1.8.28")),
            Manifest(("com.unity.burst", "1.8.28")), // unchanged by template
            Manifest(("com.unity.burst", "1.8.20"))); // user override

        Assert.That(merged, Does.Contain("\"com.unity.burst\": \"1.8.20\""));
    }

    [Test]
    public void Ingest_Union_KeepsUserPackages_AddsTemplate_NoRemovals()
    {
        var merged = ManifestMerge.Merge(
            null,
            Manifest(("com.unity.burst", "1.8.29"), ("new.pkg", "2.0.0")),
            Manifest(("com.unity.burst", "1.8.0"), ("user.pkg", "9.9.9")));

        Assert.Multiple(() =>
        {
            Assert.That(merged, Does.Contain("\"com.unity.burst\": \"1.8.29\"")); // template wins
            Assert.That(merged, Does.Contain("\"new.pkg\": \"2.0.0\""));          // added
            Assert.That(merged, Does.Contain("\"user.pkg\": \"9.9.9\""));         // user kept
        });
    }
}
