namespace CarbonEconomy;

public interface IAzureRetailPricesApiClient
{
    /// <summary>
    /// Retrieves prices for all the virtual machines in the given location.
    /// Note that this method call will require many queries and will take some time.
    /// </summary>
    /// <param name="location">The Azure location e.g., "westeurope".</param>
    /// <param name="priceType">The price type e.g., "Consumption".</param>
    /// <returns><see cref="AzureRetailPricesApiResponse"/> instance containing the price data.</returns>
    public Task<AzureRetailPricesApiResponse> GetVmPricesAsync(string location, string priceType);

    /// <summary>
    /// Retrieves prices of virtual machines with the specified SKU in the given location.
    /// Note that the SKU name is case sensitive.
    /// </summary>
    /// <param name="location">The Azure location e.g., "westeurope".</param>
    /// <param name="armSkuName">The case sensitive virtual machine SKU name e.g., "Standard_D5_v2".</param>
    /// <param name="priceType">The price type e.g., "Consumption".</param>
    /// <returns><see cref="AzureRetailPricesApiResponse"/> instance containing the price data.</returns>
    public Task<AzureRetailPricesApiResponse> GetVmPricesAsync(string location, string armSkuName, string priceType);
}
