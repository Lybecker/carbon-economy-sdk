using GSF.CarbonAware.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonEconomy;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add services needed in order to use an Carbon Economy SDK.
    /// </summary>
    public static IServiceCollection AddCarbonEconomyServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAzureRetailPricesApiClient, AzureRetailPricesApiClient>();
        services.AddScoped<ICloudRegionSelector, CloudRegionSelector>();
        services.AddScoped<ICloudRegionCarbonIntensity, CloudRegionCarbonIntensity>();
        services.AddEmissionsServices(configuration);
        services.AddForecastServices(configuration);
        return services;
    }
}
