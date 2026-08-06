using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Redux_SDK_Manager.Services;

public interface IFilePickerService
{
    /// <summary>
    /// Shows a folder picker and returns the chosen local path, or null when cancelled. When
    /// <paramref name="suggestedStartPath"/> is an existing folder, the picker opens there.
    /// </summary>
    Task<string?> PickFolderAsync(string title, string? suggestedStartPath = null);

    /// <summary>
    /// Shows a file picker limited to the given extensions (without the dot, e.g. "exe") and returns the
    /// chosen local path, or null when cancelled.
    /// </summary>
    Task<string?> PickFileAsync(string title, string filterName, params string[] extensions);
}

public sealed class FilePickerService : IFilePickerService
{
    public async Task<string?> PickFolderAsync(string title, string? suggestedStartPath = null)
    {
        if (!TryGetWindow(out var window)) return null;

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        };

        if (!string.IsNullOrEmpty(suggestedStartPath))
        {
            options.SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(suggestedStartPath);
        }

        var folders = await window.StorageProvider.OpenFolderPickerAsync(options);

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickFileAsync(string title, string filterName, params string[] extensions)
    {
        if (!TryGetWindow(out var window)) return null;

        var fileType = new FilePickerFileType(filterName)
        {
            Patterns = extensions.Select(e => $"*.{e}").ToList()
        };

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [fileType]
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private static bool TryGetWindow(out Avalonia.Controls.Window window)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } main })
        {
            window = main;
            return true;
        }

        window = null!;
        return false;
    }
}
