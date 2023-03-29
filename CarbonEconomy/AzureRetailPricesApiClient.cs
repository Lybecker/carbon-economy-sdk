using System.Net.Http.Headers;

namespace CarbonEconomy;

public class AzureRetailPricesApiClient : IAzureRetailPricesApiClient
{
    private const string AzureRetailPricesApiBaseUrl = "https://prices.azure.com/api/retail/prices?api-version=2021-10-01-preview";

    public async Task<AzureRetailPricesApiResponse> GetVmPricesAsync(string location, string priceType = "Consumption")
    {
        string query = $"serviceName eq 'Virtual Machines' and armRegionName eq '{location}' and priceType eq '{priceType}'";
        return await SendQueryAsync(query);
    }

    public async Task<AzureRetailPricesApiResponse> GetVmPricesAsync(string location, string armSkuName, string priceType = "Consumption")
    {
        string query = $"serviceName eq 'Virtual Machines' and armRegionName eq '{location}' and armSkuName eq '{armSkuName}' and priceType eq '{priceType}'";
        return await SendQueryAsync(query);
    }

    private static async Task<AzureRetailPricesApiResponse> SendRequestAsync(string url)
    {
        using HttpClient httpClient = new();
        httpClient.BaseAddress = new Uri(AzureRetailPricesApiBaseUrl);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(url);

        if (httpResponseMessage.IsSuccessStatusCode)
        {
            return await httpResponseMessage.Content.ReadAsAsync<AzureRetailPricesApiResponse>();
        }
        else
        {
            throw new InvalidOperationException($"Azure Retail Prices API returned response with error code {(int)httpResponseMessage.StatusCode} {httpResponseMessage.StatusCode}");
        }
    }

    private static async Task<AzureRetailPricesApiResponse> SendQueryAsync(string query)
    {
        AzureRetailPricesApiResponse response = await SendRequestAsync($"{AzureRetailPricesApiBaseUrl}&$filter={query}");
        string nextPageLink = response.NextPageLink;

        while (!string.IsNullOrEmpty(nextPageLink))
        {
            AzureRetailPricesApiResponse nextResponse = await SendRequestAsync(nextPageLink);
            response.Items.AddRange(nextResponse.Items);
            response.Count += nextResponse.Count;
            nextPageLink = nextResponse.NextPageLink;
        }

        return response;
    }
}
