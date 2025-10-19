using AspireShop.ServiceDefaults.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspireShop.ServiceDefaults;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChatThrottling(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new ChatThrottleOptions();
        configuration.GetSection("ChatThrottle").Bind(options);
        
        services.AddSingleton(options);
        services.AddSingleton<IChatThrottle, InMemoryChatThrottle>();
        
        return services;
    }
}
