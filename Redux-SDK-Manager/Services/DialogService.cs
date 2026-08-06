using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.ViewModels;
using Redux_SDK_Manager.Wrappers;

namespace Redux_SDK_Manager.Services;

/// <summary>
/// Hosts the app's single modal dialog. <see cref="Current"/> is what the window overlay binds to,
/// so setting it shows a dialog (resolved to its view by the ViewLocator) and clearing it hides the
/// overlay.
/// </summary>
public interface IDialogService
{
    /// <summary>The dialog view model currently shown, or null when none is open.</summary>
    ViewModelBase? Current { get; }

    Task AlertAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No");

    /// <summary>Asks for a line of text. Returns null when the user cancels.</summary>
    Task<string?> AskAsync(string title, string message, string defaultValue);

    /// <summary>
    /// Shows the grouped, filterable version picker. When <paramref name="showEmbedOption"/> is set it
    /// also offers the "embed the SDK for development" checkbox. Returns the choice, or null on cancel.
    /// </summary>
    Task<VersionChoice?> SelectVersionAsync(
        string title, string message, IReadOnlyList<TemplateVersion> versions, bool showEmbedOption = false);

    /// <summary>
    /// Prompts with an action button that opens <paramref name="url"/> in the browser (e.g. a
    /// "download" page for a missing prerequisite) and a cancel button. No-op if the user cancels.
    /// </summary>
    Task OfferLinkAsync(string title, string message, string actionText, string url);

    /// <summary>Whether the full-window blocking overlay is shown (locks the UI, e.g. while updating).</summary>
    bool IsBlocking { get; }

    /// <summary>The message shown on the blocking overlay.</summary>
    string BlockingMessage { get; }

    /// <summary>
    /// Shows a full-window scrim that locks the whole UI until <see cref="ClearBlocking"/> (or the
    /// process exits). Used while an update downloads and restarts.
    /// </summary>
    void ShowBlocking(string message);

    /// <summary>Dismisses the blocking overlay shown by <see cref="ShowBlocking"/>.</summary>
    void ClearBlocking();
}

public partial class DialogService(IProcessRunner processRunner) : ObservableObject, IDialogService
{
    [ObservableProperty]
    private ViewModelBase? _current;

    [ObservableProperty]
    private bool _isBlocking;

    [ObservableProperty]
    private string _blockingMessage = "";

    public async Task AlertAsync(string title, string message)
    {
        var dialog = new DialogViewModel(DialogKind.Alert, title, message);
        await ShowAsync(dialog, dialog.Completion);
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        var dialog = new DialogViewModel(DialogKind.Confirm, title, message, confirmText: confirmText, cancelText: cancelText);
        var result = await ShowAsync(dialog, dialog.Completion);
        return result.Confirmed;
    }

    public async Task<string?> AskAsync(string title, string message, string defaultValue)
    {
        var dialog = new DialogViewModel(DialogKind.Ask, title, message, input: defaultValue);
        var result = await ShowAsync(dialog, dialog.Completion);
        return result.Confirmed ? result.Text : null;
    }

    public async Task<VersionChoice?> SelectVersionAsync(
        string title, string message, IReadOnlyList<TemplateVersion> versions, bool showEmbedOption = false)
    {
        var picker = new VersionPickerViewModel(title, message, versions, showEmbedOption);
        return await ShowAsync(picker, picker.Completion);
    }

    public async Task OfferLinkAsync(string title, string message, string actionText, string url)
    {
        if (!await ConfirmAsync(title, message, actionText, "Cancel")) return;

        try
        {
            processRunner.OpenUrl(url);
        }
        catch
        {
            // Falling back to showing the link is better than silently doing nothing if no browser
            // could be launched.
            await AlertAsync(title, $"Could not open your browser. Visit this page to download:\n{url}");
        }
    }

    public void ShowBlocking(string message)
    {
        BlockingMessage = message;
        IsBlocking = true;
    }

    public void ClearBlocking() => IsBlocking = false;

    private async Task<TResult> ShowAsync<TResult>(ViewModelBase dialog, Task<TResult> completion)
    {
        Current = dialog;
        try
        {
            return await completion;
        }
        finally
        {
            Current = null;
        }
    }
}
