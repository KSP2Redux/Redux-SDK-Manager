using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Redux_SDK_Manager.ViewModels;

namespace Redux_SDK_Manager.Services;

/// <summary>
/// Hosts the app's single modal dialog. <see cref="Current"/> is what the window overlay binds to,
/// so setting it shows a dialog and clearing it hides the overlay.
/// </summary>
public interface IDialogService
{
    /// <summary>The dialog currently shown, or null when none is open.</summary>
    DialogViewModel? Current { get; }

    Task AlertAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No");

    /// <summary>Asks for a line of text. Returns null when the user cancels.</summary>
    Task<string?> AskAsync(string title, string message, string defaultValue);
}

public partial class DialogService : ObservableObject, IDialogService
{
    [ObservableProperty]
    private DialogViewModel? _current;

    public async Task AlertAsync(string title, string message)
        => await ShowAsync(new DialogViewModel(DialogKind.Alert, title, message));

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        var result = await ShowAsync(new DialogViewModel(DialogKind.Confirm, title, message,
            confirmText: confirmText, cancelText: cancelText));
        return result.Confirmed;
    }

    public async Task<string?> AskAsync(string title, string message, string defaultValue)
    {
        var result = await ShowAsync(new DialogViewModel(DialogKind.Ask, title, message, input: defaultValue));
        return result.Confirmed ? result.Text : null;
    }

    private async Task<DialogResult> ShowAsync(DialogViewModel dialog)
    {
        Current = dialog;
        try
        {
            return await dialog.Completion;
        }
        finally
        {
            Current = null;
        }
    }
}
