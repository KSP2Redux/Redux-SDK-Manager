using Microsoft.Extensions.DependencyInjection;
using Redux_SDK_Manager.ViewModels;

namespace Redux_SDK_Manager.Services;

/// <summary>Registers the application's services (shared Core + UI) with the DI container.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds this applications services with the DI container
    /// </summary>
    /// <param name="services">The DI container</param>
    /// <returns>The DI container for method chaining</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddReduxSdkManagerCore();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<VersionsViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
