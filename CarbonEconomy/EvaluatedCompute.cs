namespace CarbonEconomy;

public class EvaluatedCompute : Compute
{
    /// <summary>
    /// The carbon intensity.
    /// </summary>
    public double Rating { get; init; }

    /// <summary>
    /// Unit price in dollars.
    /// </summary>
    public double UnitPrice { get; init; }

    /// <summary>
    /// Value calculated based on the emissions and the price. Lower is better.
    /// </summary>
    public double Cost { get; set; }

    public EvaluatedCompute(string location, string vmSku, string operatingSystem, ComputeType type) : base(location, vmSku, operatingSystem, type)
    {
    }

    public EvaluatedCompute(Compute compute) : base(compute.Location, compute.VmSku, compute.OperatingSystem, compute.Type)
    {
        Id = compute.Id;
    }

    public override string ToString()
    {
        return $"{base.ToString()} ({Rating}, ${UnitPrice}, {Cost})";
    }
}
