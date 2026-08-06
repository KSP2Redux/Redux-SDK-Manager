using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Services;

/// <summary>
/// GUI implementation of <see cref="IPromptService"/>. Core services call these synchronously from a
/// background thread (create/ingest/upgrade/open run off the UI thread), so each call is marshaled to
/// the UI thread as a themed modal dialog and blocks the caller until the user answers. It must not be
/// invoked on the UI thread, since blocking the UI thread on a UI dialog would deadlock.
/// </summary>
public sealed class DialogPromptService(IDialogService dialogService) : IPromptService
{
    public bool Confirm(string message, bool defaultAnswer)
        => RunBlocking(() => dialogService.ConfirmAsync("Confirm", message));

    public void Alert(string message)
        => RunBlocking(async () =>
        {
            await dialogService.AlertAsync("Heads up", message);
            return true;
        });

    public string Ask(string message, string defaultValue)
    {
        var entered = RunBlocking(() => dialogService.AskAsync("Project", message, defaultValue));
        return string.IsNullOrWhiteSpace(entered) ? defaultValue : entered;
    }

    // Posts the dialog onto the UI thread and blocks this (background) thread on the result.
    private static T RunBlocking<T>(Func<Task<T>> action)
    {
        var completion = new TaskCompletionSource<T>();
        Dispatcher.UIThread.Post(async void () =>
        {
            try
            {
                completion.SetResult(await action());
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        });
        return completion.Task.GetAwaiter().GetResult();
    }
}
