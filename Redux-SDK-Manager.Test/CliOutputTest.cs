using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Redux_SDK_Manager.Cli;

namespace Redux_SDK_Manager.Test;

public class CliOutputTest
{
    [Test]
    public void Result_TextMode_WritesLine()
    {
        var sw = new StringWriter();
        new CliOutput(sw, isJson: false).Result("hello");
        Assert.That(sw.ToString().Trim(), Is.EqualTo("hello"));
    }

    [Test]
    public void Result_JsonMode_WritesNothing()
    {
        var sw = new StringWriter();
        new CliOutput(sw, isJson: true).Result("hello");
        Assert.That(sw.ToString(), Is.Empty);
    }

    [Test]
    public void Payload_JsonMode_SerializesToResults_AndSkipsTextFallback()
    {
        var sw = new StringWriter();
        new CliOutput(sw, isJson: true).Payload(new { a = 1 }, () => Assert.Fail("text fallback must not run in JSON mode"));
        Assert.That(sw.ToString(), Does.Contain("\"a\": 1"));
    }

    [Test]
    public void Payload_TextMode_RunsFallback_AndWritesNoJson()
    {
        var sw = new StringWriter();
        var ran = false;
        new CliOutput(sw, isJson: false).Payload(new { a = 1 }, () => ran = true);
        Assert.That(ran, Is.True);
        Assert.That(sw.ToString(), Is.Empty);
    }

    [Test]
    public void Table_FormatsAlignedColumns()
    {
        var sw = new StringWriter();
        new CliOutput(sw, isJson: false).Table(
            ["VERSION", "CHANNEL"],
            [["0.2.8.5", "Release"], ["26w32a", "Snapshot"]]);

        var lines = sw.ToString().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
        Assert.That(lines[0], Does.StartWith("VERSION"));
        Assert.That(lines[1], Does.Contain("---"));
        Assert.That(lines.Any(l => l.Contains("0.2.8.5") && l.Contains("Release")), Is.True);
    }

    [Test]
    public void Fail_ReturnsExitCode_AndWritesErrorToStderr()
    {
        var results = new StringWriter();
        var stderr = new StringWriter();
        var original = Console.Error;
        Console.SetError(stderr);
        try
        {
            var code = new CliOutput(results, isJson: false).Fail(3, "boom");
            Assert.That(code, Is.EqualTo(3));
            Assert.That(stderr.ToString(), Does.Contain("boom"));
        }
        finally
        {
            Console.SetError(original);
        }
    }
}
