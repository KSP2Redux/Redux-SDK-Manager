using System;
using Microsoft.Extensions.DependencyInjection;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli;

/// <summary>
/// Builds the service container the CLI verbs run against. These are the same Core services the GUI
/// uses, so a CLI command exercises exactly the same code paths.
/// </summary>
public static class CliServiceProvider
{
    public static IServiceProvider Build()
        => new ServiceCollection()
            .AddReduxSdkManagerCore()
            // Override Core's non-interactive default so verbs can prompt on the terminal. The verb
            // resolves the concrete type to set its forced --yes/--no answer. The service resolves
            // the same singleton, so the answer reaches it.
            .AddSingleton<CliPromptService>()
            .AddSingleton<IPromptService>(sp => sp.GetRequiredService<CliPromptService>())
            .BuildServiceProvider();
}
