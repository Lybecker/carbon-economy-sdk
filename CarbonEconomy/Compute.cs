namespace CarbonEconomy;

public enum ComputeType
{
    /// <summary>
    /// Allocated VMs dedicated to the customer including Reserved Instances.
    /// </summary>
    Dedicated = 0,
    /// <summary>
    /// Spot VMs discounted but can be evicted.
    /// </summary>
    Spot = 1,
    LowPriority = 2,
}

public class Compute
{
    /// <summary>
    /// Optional.
    /// ID unique for a compute so that it can be easily tracked through evaluation.
    /// Not used by the Carbon Economy library.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    public string Location { get; set; }

    public string VmSku { get; init; }

    public string OperatingSystem { get; init; }

    public ComputeType Type { get; init; }

    public Compute(string location, string vmSku, string operatingSystem, ComputeType type = ComputeType.Dedicated)
    {
        Location = location;
        VmSku = vmSku;
        OperatingSystem = operatingSystem;
        Type = type;
    }

    public override string ToString()
    {
        return $"{Id} {Location} {VmSku} {OperatingSystem} {Type}";
    }
}
