using CarbonAware.DataSources.ElectricityMaps.Client;
using GSF.CarbonAware.Handlers;
using GSF.CarbonAware.Models;
using Microsoft.Extensions.Logging;

namespace CarbonEconomy;

public class CloudRegionCarbonIntensity : ICloudRegionCarbonIntensity
{
    private readonly IEmissionsHandler _emissionsHandler;
    private readonly ILogger<CloudRegionSelector> _logger;

    public CloudRegionCarbonIntensity(IEmissionsHandler emissionsHandler, ILogger<CloudRegionSelector> logger)
    {
        _emissionsHandler = emissionsHandler;
        _logger = logger;
    }

    public async Task<Dictionary<string, double>> GetEmissionsAsync(string[] locations, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var emissions = new Dictionary<string, double>();

        foreach (string location in locations)
        {
            if (!emissions.ContainsKey(location))
            {
                emissions.Add(location, -1);
            }
        }

        IEnumerable<EmissionsData>? emissionsData = null;

        try
        {
            emissionsData = await _emissionsHandler.GetEmissionsDataAsync(locations, startTime, endTime);
        }
        catch (Exception ex)
        {
            if (ex.InnerException != null && ex.InnerException is ElectricityMapsClientHttpException)
            {
                var httpException = (ElectricityMapsClientHttpException)ex.InnerException;
                _logger.LogError(ex, "The request to Carbon Aware data source failed with message '{Message}' (Status code: {StatusCode})", httpException.Message, httpException.Status);
            }
            else
            {
                _logger.LogError(ex, "The request to Carbon Aware data source failed");
            }
        }

        if (emissionsData != null)
        {
            foreach (EmissionsData emissionData in emissionsData)
            {
                if (!string.IsNullOrEmpty(emissionData.Location) // TODO: Fix when CA-SDK bug has been fixed: https://github.com/microsoft/carbon-aware-sdk/issues/234
                    && emissions[emissionData.Location] == -1)
                {
                    emissions[emissionData.Location] = emissionData.Rating;
                }
            }
        }

        foreach (string location in emissions.Keys)
        {
            if (emissions[location] == -1)
            {
                _logger.LogError("Failed to get emissions for location {Location}", location);
            }
        }

        return emissions;
    }
}
