using System;
using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Redux_SDK_Manager.ViewModels;
using Redux_SDK_Manager.Wrappers;
using Testably.Abstractions;

namespace Redux_SDK_Manager.Services;

/// <summary>Registers the application's services and ViewModels with the DI container.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Infrastructure abstractions (real implementations; tests substitute mocks)
        services.AddSingleton<IFileSystem, RealFileSystem>();
        services.AddSingleton(SystemEnvironmentProvider.Instance);
        services.AddSingleton<IProcessRunner, ProcessRunner>();

        // Services
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ITemplateVersionService, TemplateVersionService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<ITemplateCatalogService, TemplateCatalogService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();

        return services;
    }
}
