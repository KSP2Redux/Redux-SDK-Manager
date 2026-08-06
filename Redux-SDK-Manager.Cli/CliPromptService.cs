using System;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli;

/// <summary>
/// Prompts on the terminal. A forced answer from --yes/--no short-circuits the prompt. Otherwise the
/// question goes to stderr (stdout stays the data channel) and the answer is read from stdin. A
/// closed or empty stdin falls back to the caller's default, so piped invocations never hang.
/// </summary>
public sealed class CliPromptService : IPromptService
{
    /// <summary>Set by the verb from --yes/--no. Null means ask interactively.</summary>
    public bool? ForcedAnswer { get; set; }

    /// <summary>Set by the verb from --name (or similar). Null means ask interactively.</summary>
    public string? ForcedText { get; set; }

    public bool Confirm(string message, bool defaultAnswer)
    {
        if (ForcedAnswer is { } forced) return forced;

        Console.Error.Write($"{message} {(defaultAnswer ? "[Y/n]" : "[y/N]")} ");

        var line = Console.In.ReadLine();
        if (string.IsNullOrWhiteSpace(line)) return defaultAnswer;

        return line.Trim().ToLowerInvariant() switch
        {
            "y" or "yes" => true,
            "n" or "no" => false,
            _ => defaultAnswer
        };
    }

    public void Alert(string message)
    {
        Console.Error.WriteLine(message);
    }

    public string Ask(string message, string defaultValue)
    {
        if (ForcedText is not null) return ForcedText;

        Console.Error.Write($"{message} [{defaultValue}]: ");

        var line = Console.In.ReadLine();
        return string.IsNullOrWhiteSpace(line) ? defaultValue : line.Trim();
    }
}
