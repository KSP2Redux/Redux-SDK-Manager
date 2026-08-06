using System;
using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Redux_SDK_Manager.Wrappers;
using Testably.Abstractions;

namespace Redux_SDK_Manager.Services;

/// <summary>Registers the UI-agnostic core services shared by the app and CLI frontends.</summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddReduxSdkManagerCore(this IServiceCollection services)
    {
        // Infrastructure abstractions (real implementations; tests substitute mocks)
        services.AddSingleton<IFileSystem, RealFileSystem>();
        services.AddSingleton(SystemEnvironmentProvider.Instance);
        services.AddSingleton<IProcessRunner, ProcessRunner>();

        // Services
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ITemplateVersionService, TemplateVersionService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<ITemplateCatalogService, TemplateCatalogService>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IUnityService, UnityService>();

        return services;
    }
}
