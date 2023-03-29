namespace CarbonEconomy;

public interface ICloudRegionCarbonIntensity
{
    /// <summary>
    /// Gets emission data for the given locations.
    /// </summary>
    /// <param name="locations">The locations to get emissions for.</param>
    /// <param name="startTime">The start time.</param>
    /// <param name="endTime">The end time.</param>
    /// <returns>The emission data in a dictionary, where the key is the location and the value the emission rating.</returns>
    public Task<Dictionary<string, double>> GetEmissionsAsync(string[] locations, DateTimeOffset startTime, DateTimeOffset endTime);
}
