using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Test;

public class TemplateVersionTest
{
    [TestCase("0.2.8.5", TemplateChannel.Release)]
    [TestCase("1.0", TemplateChannel.Release)]
    [TestCase("26w32a", TemplateChannel.Snapshot)]
    [TestCase("00w01z", TemplateChannel.Snapshot)]
    [TestCase("0.2.9-26w43a", TemplateChannel.Snapshot)]
    [TestCase("0.2.10-26w44a", TemplateChannel.Snapshot)]
    [TestCase("0.3.0-27w01a", TemplateChannel.Snapshot)]
    [TestCase("0.10.0-26w41a", TemplateChannel.Snapshot)]
    [TestCase("beta-6", TemplateChannel.Unknown)]
    [TestCase("v1.0", TemplateChannel.Unknown)]
    [TestCase("", TemplateChannel.Unknown)]
    [TestCase("garbage", TemplateChannel.Unknown)]
    public void Parse_ClassifiesChannel(string raw, TemplateChannel expected)
    {
        Assert.That(TemplateVersion.Parse(raw).Channel, Is.EqualTo(expected));
    }

    [Test]
    public void Parse_TrimsSurroundingWhitespace()
    {
        var version = TemplateVersion.Parse("  0.2.8.5\r\n");

        Assert.That(version.Raw, Is.EqualTo("0.2.8.5"));
    }

    [Test]
    public void NewestFirst_ComparesGameVersionNumericallyBeforeSnapshot()
    {
        var versions = new[] { "0.2.9-26w44b", "0.2.10-26w44a", "0.3.0-27w01a" }
            .Select(TemplateVersion.Parse).OrderBy(v => v, TemplateVersion.NewestFirst);

        Assert.That(versions.Select(v => v.Raw),
            Is.EqualTo(new[] { "0.3.0-27w01a", "0.2.10-26w44a", "0.2.9-26w44b" }));
    }

    [Test]
    public void NewestFirst_KeepsBareHistoricalSnapshotsInChronologicalOrder()
    {
        var versions = new[] { "26w43a", "0.2.9-26w44a", "26w42c" }
            .Select(TemplateVersion.Parse).OrderBy(v => v, TemplateVersion.NewestFirst);

        Assert.That(versions.Select(v => v.Raw),
            Is.EqualTo(new[] { "0.2.9-26w44a", "26w43a", "26w42c" }));
    }
}
