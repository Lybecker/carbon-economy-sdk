namespace CarbonEconomy;

public interface ICloudRegionSelector
{
    /// <summary>
    /// Evaluates the given compute list based on the current price and expected emissions.
    /// </summary>
    /// <param name="computeList">The compute list to evaluate. Note that the Azure Pricing API expects the exact capitalization of the virtual machine SKU name e.g., "Standard_D5_v2".</param>
    /// <param name="emissionForecastWindow">The window size of the emission forecast in minutes e.g., 30 for the next thirty minutes.</param>
    /// <param name="emissionWeight">The weight given to the emissions in terms of evaluation: 0.0 to have no impact at all and only focus on price, 1.0 to only focus on emissions regardless of price.</param>
    /// <returns>A list of <see cref="EvaluatedCompute"/> with evaluation results.</returns>
    public Task<IEnumerable<EvaluatedCompute>> EvaluateComputeListAsync(IList<Compute> computeList, int emissionForecastWindow, double emissionWeight);

    /// <summary>
    /// Gets emission forecast for the given locations.
    /// </summary>
    /// <param name="locations">The locations to get emission forecast for.</param>
    /// <param name="windowSize">The forecast window size in minutes.</param>
    /// <returns>The emission forecast in a dictionary, where the key is the location and the value the emission rating.</returns>
    Task<Dictionary<string, double>> GetEmissionRatingsAsync(string[] locations, int windowSize);

    /// <summary>
    /// Gets the dedicated, spot and low priority unit prices for the given virtual machine.
    /// </summary>
    /// <param name="compute">The virtual machine information.</param>
    /// <returns>The unit prices in order: dedicated, spot and low priority.</returns>
    Task<(double, double, double)> GetVmPricesAsync(Compute compute);
}
