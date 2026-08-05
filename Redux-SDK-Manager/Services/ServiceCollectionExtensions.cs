using Microsoft.Extensions.DependencyInjection;
using Redux_SDK_Manager.ViewModels;

namespace Redux_SDK_Manager.Services;

/// <summary>Registers the application's services (shared Core + UI) with the DI container.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddReduxSdkManagerCore();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();

        return services;
    }
}
