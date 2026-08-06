namespace Redux_SDK_Manager.Services;

/// <summary>
/// Asks the user a yes/no question. The frontend decides how it's asked: the CLI prints a y/n prompt
/// on the terminal, a GUI would show a dialog. A non-interactive host uses
/// <see cref="DefaultPromptService"/>, which never asks and returns the caller's default.
/// </summary>
public interface IPromptService
{
    /// <summary>
    /// Returns the user's yes/no answer to <paramref name="message"/>, or
    /// <paramref name="defaultAnswer"/> when there is no one to ask.
    /// </summary>
    bool Confirm(string message, bool defaultAnswer);

    /// <summary>
    /// Alerts the user about something
    /// </summary>
    /// <param name="message">What to alert about</param>
    void Alert(string message);
}

/// <summary>
/// Non-interactive fallback: returns the default without asking. Core registers this so every
/// container resolves an <see cref="IPromptService"/>. Interactive frontends override it.
/// </summary>
public sealed class DefaultPromptService : IPromptService
{
    public bool Confirm(string message, bool defaultAnswer) => defaultAnswer;
    public void Alert(string message)
    {
    }
}
