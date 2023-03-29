namespace CarbonEconomy;

public class AzureRetailPricesApiResponseItem
{
    public string ArmRegionName { get; set; } = string.Empty;
    public string ArmSkuName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string EffectiveStartDate { get; set; } = string.Empty;
    public bool IsPrimaryMeterRegion { get; set; }
    public string Location { get; set; } = string.Empty;
    public string MeterId { get; set; } = string.Empty;
    public string MeterName { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public double RetailPrice { get; set; }
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceFamily { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string SkuId { get; set; } = string.Empty;
    public string SkuName { get; set; } = string.Empty;
    public double TierMinimumUnits { get; set; }
    public string Type { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public double UnitPrice { get; set; }
}

public class AzureRetailPricesApiResponse
{
    public string BillingCurrency { get; set; } = string.Empty;
    public int Count { get; set; }
    public string CustomerEntityId { get; set; } = string.Empty;
    public string CustomerEntityType { get; set; } = string.Empty;
    public List<AzureRetailPricesApiResponseItem> Items { get; set; } = new();
    public string NextPageLink { get; set; } = string.Empty;
}
