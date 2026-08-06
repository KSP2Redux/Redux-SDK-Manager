using System;
using Microsoft.Extensions.DependencyInjection;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli;

/// <summary>The shared per-invocation context handed to every verb.</summary>
public sealed class CliContext(IServiceProvider services, CliOutput output)
{
    public IServiceProvider Services => services;
    public CliOutput Output => output;

    public T Get<T>() where T : notnull => services.GetRequiredService<T>();

    /// <summary>True if git is available, most template operations need it.</summary>
    public bool GitAvailable => Get<IGitService>().IsInstalled();
}
