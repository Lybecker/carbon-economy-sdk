using CarbonAware.DataSources.ElectricityMaps.Client;
using GSF.CarbonAware.Handlers;
using GSF.CarbonAware.Models;
using Microsoft.Extensions.Logging;

namespace CarbonEconomy;

public class CloudRegionSelector : ICloudRegionSelector
{
    private readonly IForecastHandler _forecastHandler;
    private readonly IAzureRetailPricesApiClient _azureRetailsPricesApiClient;
    private readonly ILogger<CloudRegionSelector> _logger;

    public CloudRegionSelector(IForecastHandler forecastHandler, IAzureRetailPricesApiClient azureRetailsPricesApiClient, ILogger<CloudRegionSelector> logger)
    {
        _forecastHandler = forecastHandler;
        _azureRetailsPricesApiClient = azureRetailsPricesApiClient;
        _logger = logger;
    }

    public async Task<IEnumerable<EvaluatedCompute>> EvaluateComputeListAsync(IList<Compute> computeList, int emissionForecastWindow, double emissionWeight)
    {
        if (emissionWeight < 0)
        {
            _logger.LogWarning("Emission weight is expected to be in interval [0, 1], received value {EmissionWeight}, setting to 0", emissionWeight);
            emissionWeight = 0;
        }
        else if (emissionWeight > 1)
        {
            _logger.LogWarning("Emission weight is expected to be in interval [0, 1], received value {EmissionWeight}, setting to 1", emissionWeight);
            emissionWeight = 1;
        }

        double priceWeight = 1 - emissionWeight;
        IList<EvaluatedCompute> evaluatedComputeList = new List<EvaluatedCompute>();
        var locations = new HashSet<string>(from compute in computeList select compute.Location).ToArray();
        Dictionary<string, double> carbonRatings = await GetEmissionRatingsAsync(locations, emissionForecastWindow);

        foreach (Compute compute in computeList)
        {
            (double dedicated, double spot, double lowPriority) unitPrices = await GetVmPricesAsync(compute);

            evaluatedComputeList.Add(new EvaluatedCompute(compute)
            {
                Rating = carbonRatings[compute.Location],
                UnitPrice = compute.Type == ComputeType.Dedicated ? unitPrices.dedicated : compute.Type == ComputeType.Spot ? unitPrices.spot : unitPrices.lowPriority
            });
        }

        try
        {
            double minUnitPrice = evaluatedComputeList.Where(evaluatedCompute => evaluatedCompute.UnitPrice >= 0).Min(evaluatedCompute => evaluatedCompute.UnitPrice);
            double maxUnitPrice = evaluatedComputeList.Max(evaluatedCompute => evaluatedCompute.UnitPrice);
            double minRating = evaluatedComputeList.Where(evaluatedCompute => evaluatedCompute.Rating >= 0).Min(evaluatedCompute => evaluatedCompute.Rating);
            double maxRating = evaluatedComputeList.Max(evaluatedCompute => evaluatedCompute.Rating);

            foreach (EvaluatedCompute evaluatedCompute in evaluatedComputeList)
            {
                double normalizedPrice = (evaluatedCompute.UnitPrice == -1 || minUnitPrice == maxUnitPrice) ? 1
                    : (evaluatedCompute.UnitPrice - minUnitPrice) / (maxUnitPrice - minUnitPrice);
                double normalizedRating = (evaluatedCompute.Rating == -1 || minRating == maxRating) ? 1
                    : (evaluatedCompute.Rating - minRating) / (maxRating - minRating);

                evaluatedCompute.Cost = Math.Sqrt(Math.Pow(priceWeight * normalizedPrice, 2) + Math.Pow(emissionWeight * normalizedRating, 2));

                if (evaluatedCompute.OperatingSystem.ToLower().Contains("skynet"))
                {
                    // A little safety never hurt anyone
                    evaluatedCompute.Cost = double.MaxValue;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to evaluate the cost of compute");
        }

        return evaluatedComputeList.OrderBy(evaluatedCompute => evaluatedCompute.Cost);
    }

    public async Task<Dictionary<string, double>> GetEmissionRatingsAsync(string[] locations, int windowSize)
    {
        Dictionary<string, double> ratings = new();

        foreach (string location in locations)
        {
            ratings.Add(location, -1);
        }

        IEnumerable<EmissionsForecast>? emissionsForecasts = null;

        // Because the window size is exactly the time specified by the start and end times, we will get only one
        // rating per location that is the average for that span
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMinutes(windowSize);

        try
        {
            emissionsForecasts = await _forecastHandler.GetCurrentForecastAsync(locations, startTime, endTime, windowSize);
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

        if (emissionsForecasts != null)
        {
            foreach (EmissionsForecast emissionsForecast in emissionsForecasts)
            {
                foreach (EmissionsData emissionsDataPoint in emissionsForecast.EmissionsDataPoints)
                {
                    if (!string.IsNullOrEmpty(emissionsDataPoint.Location) // TODO: Fix when CA-SDK bug has been fixed: https://github.com/microsoft/carbon-aware-sdk/issues/234
                        && ratings[emissionsDataPoint.Location] == -1)
                    {
                        ratings[emissionsDataPoint.Location] = emissionsDataPoint.Rating;
                    }
                }
            }
        }

        foreach (string location in ratings.Keys)
        {
            if (ratings[location] == -1)
            {
                _logger.LogError("Failed to get emission rating for location {Location}", location);
            }
        }

        return ratings;
    }

    public async Task<(double, double, double)> GetVmPricesAsync(Compute compute)
    {
        AzureRetailPricesApiResponse response;

        try
        {
            response = await _azureRetailsPricesApiClient.GetVmPricesAsync(compute.Location, compute.VmSku, "Consumption");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get prices for virtual machines in location {Location} with SKU {VmSku}", compute.Location, compute.VmSku);
            return (-1, -1, -1);
        }

        if (response.Count == 0)
        {
            _logger.LogError("Failed to get prices for virtual machines in location {Location} with SKU {VmSku}", compute.Location, compute.VmSku);
            return (-1, -1, -1);
        }

        double unitPriceDedicated = -1;
        double unitPriceSpot = -1;
        double unitPriceLowPriority = -1;

        foreach (AzureRetailPricesApiResponseItem item in response.Items)
        {
            if ((compute.OperatingSystem.ToLower().Contains("windows") && item.ProductName.ToLower().Contains("windows"))
                    || (!compute.OperatingSystem.ToLower().Contains("windows") && !item.ProductName.ToLower().Contains("windows")))
            {
                _logger.LogDebug("Resolved unit price of {ProductName} {SkuName}: {UnitPrice}", item.ProductName, item.SkuName, item.UnitPrice);

                if (item.SkuName.ToLower().Contains("low priority"))
                {
                    unitPriceLowPriority = item.UnitPrice;
                }
                else if (item.SkuName.ToLower().Contains("spot"))
                {
                    unitPriceSpot = item.UnitPrice;
                }
                else
                {
                    unitPriceDedicated = item.UnitPrice;
                }
            }
        }

        return (unitPriceDedicated, unitPriceSpot, unitPriceLowPriority);
    }
}
