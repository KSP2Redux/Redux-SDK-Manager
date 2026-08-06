using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Redux_SDK_Manager.Cli;

/// <summary>
/// Writer for everything the verbs print. Results go to the result stream (normally stdout) and
/// progress/warnings/errors go to stderr, so a script can capture one without the other and the
/// JSON document on stdout stays parseable even while a command logs its steps to stderr.
/// </summary>
public sealed class CliOutput
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly TextWriter _results;

    public CliOutput(TextWriter results, bool isJson)
    {
        _results = results;
        IsJson = isJson;
    }

    /// <summary>True when the result stream carries a JSON document rather than text.</summary>
    public bool IsJson { get; }

    /// <summary>Writes a line of text to stdout, or nothing at all in JSON mode.</summary>
    public void Result(string line)
    {
        if (IsJson) return;
        _results.WriteLine(line);
    }

    /// <summary>Serializes a payload to stdout in JSON mode, otherwise runs the text fallback.</summary>
    public void Payload(object payload, Action writeText)
    {
        if (IsJson)
        {
            _results.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return;
        }

        writeText();
    }

    /// <summary>Writes a progress line to stderr in both modes.</summary>
    public void Progress(string line) => Console.Error.WriteLine(line);

    /// <summary>Writes a warning to stderr.</summary>
    public void Warn(string line) => Console.Error.WriteLine($"warning: {line}");

    /// <summary>Writes an error to stderr.</summary>
    public void Error(string line) => Console.Error.WriteLine($"error: {line}");

    /// <summary>
    /// Reports a failure and returns the exit code that describes it. In JSON mode the failure also
    /// goes to stdout so a script parsing stdout sees the error rather than an empty document.
    /// </summary>
    public int Fail(int exitCode, string message)
    {
        Error(message);
        if (IsJson)
        {
            _results.WriteLine(JsonSerializer.Serialize(new { ok = false, exitCode, error = message }, JsonOptions));
        }

        return exitCode;
    }

    /// <summary>Writes rows to stdout as aligned columns.</summary>
    public void Table(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var widths = new int[headers.Count];
        for (var column = 0; column < headers.Count; column++)
        {
            widths[column] = headers[column].Length;
        }

        foreach (var row in rows)
        {
            for (var column = 0; column < headers.Count && column < row.Count; column++)
            {
                widths[column] = Math.Max(widths[column], row[column]?.Length ?? 0);
            }
        }

        _results.WriteLine(string.Join("  ", PadToWidths(headers, widths)).TrimEnd());
        _results.WriteLine(string.Join("  ", DashesForWidths(widths)));
        foreach (var row in rows)
        {
            _results.WriteLine(string.Join("  ", PadToWidths(row, widths)).TrimEnd());
        }
    }

    private static IEnumerable<string> PadToWidths(IReadOnlyList<string> cells, int[] widths)
    {
        for (var column = 0; column < widths.Length; column++)
        {
            var cell = column < cells.Count ? cells[column] ?? "" : "";
            yield return cell.PadRight(widths[column]);
        }
    }

    private static IEnumerable<string> DashesForWidths(int[] widths)
    {
        foreach (var width in widths)
        {
            yield return new string('-', width);
        }
    }
}
