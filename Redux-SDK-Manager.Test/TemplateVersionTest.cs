using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Test;

public class TemplateVersionTest
{
    [TestCase("0.2.8.5", TemplateChannel.Release)]
    [TestCase("1.0", TemplateChannel.Release)]
    [TestCase("26w32a", TemplateChannel.Snapshot)]
    [TestCase("00w01z", TemplateChannel.Snapshot)]
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
}
