using Redux_SDK_Manager.Services.Merging;

namespace Redux_SDK_Manager.Test;

public class GitignoreMergeTest
{
    [Test]
    public void Upgrade_AdoptsNewRules_DropsRemoved_KeepsUserLines()
    {
        var baseG = "bin/\nobj/\nold-rule/\n";
        var theirs = "bin/\nobj/\nnew-rule/\n";                  // added new-rule, removed old-rule
        var mine = "bin/\nobj/\nold-rule/\nMyLocalStuff/\n";      // user added MyLocalStuff, still has old-rule

        var merged = GitignoreMerge.Merge(baseG, theirs, mine);

        Assert.Multiple(() =>
        {
            Assert.That(merged, Does.Contain("new-rule/"));     // template added
            Assert.That(merged, Does.Contain("MyLocalStuff/")); // user line kept
            Assert.That(merged, Does.Not.Contain("old-rule/")); // template-removed rule dropped
        });
    }

    [Test]
    public void Ingest_Union_KeepsUserLines()
    {
        var merged = GitignoreMerge.Merge(null, "bin/\nobj/\n", "bin/\nMyLocalStuff/\n");

        Assert.Multiple(() =>
        {
            Assert.That(merged, Does.Contain("bin/"));
            Assert.That(merged, Does.Contain("obj/"));
            Assert.That(merged, Does.Contain("MyLocalStuff/"));
        });
    }

    [Test]
    public void Merge_IsIdempotent()
    {
        const string theirs = "bin/\nobj/\n";
        const string mine = "bin/\nMyLocalStuff/\n";

        var once = GitignoreMerge.Merge(null, theirs, mine);
        var twice = GitignoreMerge.Merge(null, theirs, once);

        Assert.That(twice, Is.EqualTo(once));
    }
}
