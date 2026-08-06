using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Redux_SDK_Manager.ViewModels;

public enum DialogKind
{
    Alert,
    Confirm,
    Ask
}

/// <summary>The outcome of a modal dialog: whether it was confirmed, and any entered text.</summary>
public readonly record struct DialogResult(bool Confirmed, string Text);

/// <summary>
/// Backs one modal dialog. The buttons complete <see cref="Completion"/>, which the dialog service
/// awaits, so a caller can show a dialog and get the result back as a task.
/// </summary>
public partial class DialogViewModel(
    DialogKind kind,
    string title,
    string message,
    string input = "",
    string confirmText = "OK",
    string cancelText = "Cancel")
    : ViewModelBase
{
    private readonly TaskCompletionSource<DialogResult> _completion = new();

    public DialogKind Kind { get; } = kind;
    public string Title { get; } = title;
    public string Message { get; } = message;
    public string ConfirmText { get; } = confirmText;
    public string CancelText { get; } = cancelText;

    public bool ShowCancel => Kind is DialogKind.Confirm or DialogKind.Ask;
    public bool ShowInput => Kind is DialogKind.Ask;

    [ObservableProperty]
    private string _input = input;

    /// <summary>Completes when the user answers the dialog.</summary>
    public Task<DialogResult> Completion => _completion.Task;

    [RelayCommand]
    private void Confirm() => _completion.TrySetResult(new DialogResult(true, Input));

    [RelayCommand]
    private void Cancel() => _completion.TrySetResult(new DialogResult(false, Input));
}
