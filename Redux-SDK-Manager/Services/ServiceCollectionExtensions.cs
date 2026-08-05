using Microsoft.Extensions.DependencyInjection;
using Redux_SDK_Manager.ViewModels;

namespace Redux_SDK_Manager.Services;

/// <summary>Registers the application's services and ViewModels with the DI container.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Services
        // services.AddSingleton<IExampleService, ExampleService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();

        return services;
    }
}
