using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Redux_SDK_Manager.ViewModels;

namespace Redux_SDK_Manager;

/// <summary>
/// Resolves a View for a given ViewModel by convention: a ViewModel type named
/// <c>*ViewModel</c> in the <c>ViewModels</c> namespace maps to a <c>*View</c> in the
/// <c>Views</c> namespace.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!
            .Replace("ViewModel", "View", StringComparison.Ordinal)
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
