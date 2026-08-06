using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.ViewModels;

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

    /// <summary>Shows the grouped, filterable version picker. Returns the chosen version's raw string, or null.</summary>
    Task<string?> SelectVersionAsync(string title, string message, IReadOnlyList<TemplateVersion> versions);
}

public partial class DialogService : ObservableObject, IDialogService
{
    [ObservableProperty]
    private ViewModelBase? _current;

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

    public async Task<string?> SelectVersionAsync(string title, string message, IReadOnlyList<TemplateVersion> versions)
    {
        var picker = new VersionPickerViewModel(title, message, versions);
        return await ShowAsync(picker, picker.Completion);
    }

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
