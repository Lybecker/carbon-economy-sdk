# Carbon Economy SDK

You can reduce the carbon footprint of your application by just running things at different times and in different locations. That is because not all electricity is produced in the same way. Most is produced through burning fossil fuels, some is produced using cleaner sources like wind and solar.

Reducing carbon emissions might incur a cost making it a business decision if the reduction is worth the cost. This SDK helps you to make informed decisions, enabling your system to answer challenges like below:

* 5% increase in cost, but 50% reduction in carbon emissions is worth it.
* 50% increase in cost, but 5% reduction in carbon emissions is not worth it.

> Note: The SDK builds upon the [Carbon Aware SDK](https://github.com/Green-Software-Foundation/carbon-aware-sdk) from [Green Software Foundation](https://greensoftware.foundation/). To compile and use this SDK, you need to compile and reference the [Carbon Aware SDK](https://github.com/Green-Software-Foundation/carbon-aware-sdk).

## Background story

This SDK was built in collaboration with [Microsoft](https://www.microsoft.com/) and [Vestas Wind Systems](https://www.vestas.com/). Vestas needed to run very large wind turbine simulations in the cloud. For Vestas to meet their sustainability goals and become carbon neutral by 2030 without carbon offsets, they need a way to run their simulations in the most carbon efficient way.

Using the Carbon Economy SDK, Vestas can now run their simulations in the most carbon efficient way. This is done by running the simulations in the cloud at the time of day when the electricity is produced using the least carbon intensive sources. This is done by using the Carbon Economy SDK to evaluate the carbon emissions of the electricity in the different cloud regions at different times of the day. The simulation is then run in the cloud region with the lowest carbon emissions at the time of day when the electricity is produced using the least carbon intensive sources.

Two approaches was used:

* *Time shifting*: delaying the simulation to a time when the electricity is produced using the least carbon intensive sources. Resulting in a 8-12% reduction in carbon emissions.
* *Location shifting*: running the simulation in a cloud region with the lowest carbon emissions. Resulting in up to 90% reduction in carbon emissions.

## Introduction

Carbon Economy SDK takes compute unit information as input, resolves the unit price of the virtual machine, using [Azure Retail Prices API](https://learn.microsoft.com/rest/api/cost-management/retail-prices/azure-retail-prices), and the emission forecast in its location and calculates a normalized cost for each unit.

The input compute unit information contains:

* [Virtual machine size (SKU)](https://learn.microsoft.com/azure/virtual-machines/sizes) e.g., "`Standard_D5_v2`"
* Operating system e.g., "`windows-server-2022-datacenter`"
* Type: Dedicated, [spot](https://learn.microsoft.com/azure/virtual-machines/spot-vms) or [low priority](https://azure.microsoft.com/blog/low-priority-scale-sets/)
* Location: [Azure location (region)](https://azure.microsoft.com/explore/global-infrastructure/geographies) e.g., `"westeurope"`

All of the information above affect the price, but only the location is relevant to the emission forecast.

The output contains the evaluated cost for every given unit in the input, and is calculated based on the following formula:

```plaintext
cost = sqrt((weight_p * price)^2 + (weight_e * emission)^2)
```

Where:

* `weight_p` (calculated: 1 - `weight_e`) is the weight towards the importance of the price factor (in closed interval [0,1])
* `weight_e` is the weight towards the importance of emission factor (in closed interval [0,1])
* `price` is the price normalized (1 for the highest in the group, 0 for the lowest)
* `emission` is the emission ranking normalized (1 for the highest in the group, 0 for the lowest)

> Note, that with the given the constraints of the input in this formula, the value of the `cost` too is normalized, in closed interval [0,1]. However, the maximum value in a set of results is not guaranteed to be 1 e.g., consider the case: `sqrt((0.5*1)^2 + (0.5*1)^2) = sqrt(0.5)` (approx. 0.707).

The normalization of price and emission data to the closed interval [0,1] is done by the following basic equation:

```plaintext
        x_i - min(x)
z_i = ───────────────
      max(x) - min(x)
```

Where:

* `z_i` is the normalized value ("`_i`" represents the subscript of the index *i* of a value in a set)
* `x_i` is the value to normalize
* `min(x)` is the minimum value in the set
* `max(x)` is the maximum value in the set

## Example

The code:

```csharp
IList<Compute> computeList = new List<Compute>()
{
    new Compute("westeurope", "Standard_D5_v2", "windows-server-2022-datacenter") { Id = "Tellar Prime" },
    new Compute("swedencentral", "Standard_D5_v2", "windows-server-2022-datacenter") { Id = "Altair IV" },
    new Compute("norwayeast", "Standard_D5_v2", "windows-server-2022-datacenter") { Id = "Nimbus III" },
    new Compute("uksouth", "Standard_D5_v2", "windows-server-2022-datacenter") { Id = "Vulcan" }
};

int emissionForecastWindow = 30; // In minutes
double emissionWeight = 0.5; // Emission and price weighed equally

var results = await cloudRegionSelector.EvaluateComputeListAsync(computeList, emissionForecastWindow, emissionWeight);
```

Contents in the results:

| Index | ID | Location | Emission rating | Unit price | Norm. rating | Norm. price | Cost |
| ----- | -- | -------- | --------------- | ---------- | ------------ | ----------- | ---- |
| **0** | `Altair IV` | Sweden Central | 29 | $1.824 | 0 | 0 | **0** |
| **1** | `Nimbus III` | Norway East | 69 | $1.932 | 0.117 | 0.205 | **0.118** |
| **1** | `Tellar Prime` | West Europe | 367 | $2.131 | 0.988 | 0.583 | **0.574** |
| **2** | `Vulcan` | UK South | 371 | $2.351 | 1 | 1 | **0.707** |

> Note: Virtual machine SKUs and operating system details omitted from the table above as they are the same for all locations in the query.

## Caveats

As the saying goes: you shouldn't mix apples and oranges. Combining different node types (e.g., dedicated and spot virtual machines) or different virtual machines SKUs into a single query (i.e., one `EvaluateComputeListAsync` method call) will yield results, but they will be skewed. The recommended approach is to have only one type of nodes and VMs in a single query, and execute several queries, if cost analysis of many types of machines is needed. If you for some reason still want to mix VMs in one query, be very careful when interpreting the results.

Mixing, let's say, spot and dedicated virtual machines will naturally be biased towards spot VMs given that they are considerably cheaper. The best and the worst item in the results will be reasonable, but anything in between will not likely make sense.

Mixing different VM SKUs should also be avoided, unless their performance with respect to the intended workload is the same or very close. For example, the best item in the results can be cheaper in terms of the unit price, but if the performance is poor, the virtual machine will be running longer to complete the job and the price therefore can be higher.
