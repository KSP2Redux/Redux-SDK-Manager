using System;
using Microsoft.Extensions.DependencyInjection;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli;

/// <summary>
/// Builds the service container the CLI verbs run against — the same Core services the GUI uses, so
/// a CLI command exercises exactly the same code paths.
/// </summary>
public static class CliServiceProvider
{
    public static IServiceProvider Build()
        => new ServiceCollection().AddReduxSdkManagerCore().BuildServiceProvider();
}
