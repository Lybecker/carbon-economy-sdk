using GSF.CarbonAware.Handlers;
using GSF.CarbonAware.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarbonEconomy.Tests
{
    public class CarbonEconomyTests
    {
        private readonly string[] Locations = { "low_carbon_location", "medium_carbon_location", "high_carbon_location" };
        private readonly double[] SamplePrices = new double[] { 2.131, 0.317, 0.852, 1.087, 0.129, 0.217 }; // Dedicated, spot, low priority in order Windows, Linux
        private const string VmSkuName = "Standard_D5_v2";
        private const string OperatingSystemWindows = "windows-server-2022-datacenter";
        private const string OperatingSystemLinux = "ubuntu";
        private const string OperatingSystemSkynet = "skynet-genisys";
        private const int EstimatedJobLengthInMinutes = 30;

        private readonly NullLogger<CloudRegionSelector> _logger = NullLogger<CloudRegionSelector>.Instance;
        private Mock<IForecastHandler> _mockForecastHandler = new Mock<IForecastHandler>(MockBehavior.Strict);
        private Mock<IAzureRetailPricesApiClient> _mockAzureRetailPricesApiClient = new Mock<IAzureRetailPricesApiClient>(MockBehavior.Strict);
        private IList<Compute> _computeList = new List<Compute>();
        private IList<EmissionsForecast> _forecasts = new List<EmissionsForecast>();

        /// <summary>
        /// Constructor with a common setup (arrange) step for all test cases.
        /// </summary>
        public CarbonEconomyTests()
        {
        }

        [Fact]
        public async void EvaluateComputeListAsync_DoesNotFail_IfPriceAndForecastQueryFail()
        {
            // arrange
            ArrangeCompute(ComputeType.Dedicated, OperatingSystemLinux);
            var mockForecastHandler = new Mock<IForecastHandler>(MockBehavior.Strict);
            var mockAzureRetailPricesApiClient = new Mock<IAzureRetailPricesApiClient>(MockBehavior.Strict);

            AzureRetailPricesApiResponse azureRetailPricesApiResponse = new()
            {
                Count = 0,
                Items = new List<AzureRetailPricesApiResponseItem>()
            };

            mockForecastHandler.Setup(forecastHandler =>
                forecastHandler.GetCurrentForecastAsync(Locations, It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), EstimatedJobLengthInMinutes))
                .Returns(Task.FromResult<IEnumerable<EmissionsForecast>>(_forecasts));

            mockAzureRetailPricesApiClient.Setup(azureRetailPricesApiClient =>
                azureRetailPricesApiClient.GetVmPricesAsync(It.IsAny<string>(), "Standard_D5_v2", It.IsAny<string>()))
                .Returns(Task.FromResult(azureRetailPricesApiResponse));

            var carbonEconomySdk = new CloudRegionSelector(mockForecastHandler.Object, mockAzureRetailPricesApiClient.Object, _logger);

            // act
            var result = (await carbonEconomySdk.EvaluateComputeListAsync(_computeList, EstimatedJobLengthInMinutes, 0.5)).ToList();

            // assert
            Assert.Equal(_computeList.Count, result.Count);

            // The compute order in the list is meaningless, hence not checked

            foreach (EvaluatedCompute evaluatedCompute in result)
            {
                Assert.Equal(-1, evaluatedCompute.Rating);
                Assert.Equal(-1, evaluatedCompute.UnitPrice);
                Assert.Equal(result[0].Cost, evaluatedCompute.Cost); // All cost values should be the same
            }
        }

        [Fact]
        public async void EvaluateComputeListAsync_DoesNotFail_IfNormalizationGetsSameMinAndMaxValues()
        {
            // arrange
            ArrangeUniformForecasts();
            ArrangePrices(new double[] { 1, 1, 1, 1, 1, 1 });

            var carbonEconomySdk = new CloudRegionSelector(_mockForecastHandler.Object, _mockAzureRetailPricesApiClient.Object, _logger);

            // act
            var result = (await carbonEconomySdk.EvaluateComputeListAsync(_computeList, EstimatedJobLengthInMinutes, 0.5)).ToList();

            // assert
            Assert.Equal(_computeList.Count, result.Count);

            // The compute order in the list is meaningless, hence not checked

            foreach (EvaluatedCompute evaluatedCompute in result)
            {
                Assert.Equal(result[0].Cost, evaluatedCompute.Cost); // All cost values should be the same
            }
        }

        [Theory]
        [InlineData(ComputeType.Dedicated)]
        [InlineData(ComputeType.Spot)]
        [InlineData(ComputeType.LowPriority)]
        public async void EvaluateComputeListAsync_ReturnsCorrectUnitPrices(ComputeType computeType)
        {
            // arrange
            ArrangeCompute(computeType, OperatingSystemWindows);
            ArrangeUniformForecasts();
            ArrangePrices(SamplePrices);

            var carbonEconomySdk = new CloudRegionSelector(_mockForecastHandler.Object, _mockAzureRetailPricesApiClient.Object, _logger);

            // act
            var result = (await carbonEconomySdk.EvaluateComputeListAsync(_computeList, EstimatedJobLengthInMinutes, 0.5)).ToList();

            // assert
            Assert.Equal(_computeList.Count, result.Count);

            foreach (EvaluatedCompute evaluatedCompute in result)
            {
                switch (computeType)
                {
                    case ComputeType.Dedicated:
                        Assert.Equal(SamplePrices[0], evaluatedCompute.UnitPrice);
                        break;
                    case ComputeType.Spot:
                        Assert.Equal(SamplePrices[1], evaluatedCompute.UnitPrice);
                        break;
                    case ComputeType.LowPriority:
                        Assert.Equal(SamplePrices[2], evaluatedCompute.UnitPrice);
                        break;
                    default:
                        Assert.Fail($"Unexpected compute type in result {evaluatedCompute}");
                        break;
                }
            }
        }

        [Theory]
        [InlineData(0.0, "high_carbon_location")]
        [InlineData(0.5, "medium_carbon_location")]
        [InlineData(1.0, "low_carbon_location")]
        public async void EvaluateComputeListAsync_ReturnsPreferredCompute_BasedOnEmissionWeight(double emissionWeight, string expectedLocation)
        {
            // arrange
            ArrangeCompute(ComputeType.Dedicated, OperatingSystemWindows);
            ArrangeForecasts(new double[] { 50, 100, 150 });
            ArrangePricesPerLocation(new double[] { 5, 2.5, 1 });

            var carbonEconomySdk = new CloudRegionSelector(_mockForecastHandler.Object, _mockAzureRetailPricesApiClient.Object, _logger);

            // act
            var result = (await carbonEconomySdk.EvaluateComputeListAsync(_computeList, EstimatedJobLengthInMinutes, emissionWeight)).ToList();

            // assert
            Assert.Equal(_computeList.Count, result.Count);
            Assert.Equal(expectedLocation, result[0].Location);
        }

        [Fact]
        public async void EvaluateComputeListAsync_ReturnsMaxCost_IfOperatingSystemIsEvilAi()
        {
            // arrange
            ArrangeCompute(ComputeType.Dedicated, OperatingSystemSkynet);
            ArrangeUniformForecasts();
            ArrangePrices(SamplePrices);

            var carbonEconomySdk = new CloudRegionSelector(_mockForecastHandler.Object, _mockAzureRetailPricesApiClient.Object, _logger);

            // act
            var result = (await carbonEconomySdk.EvaluateComputeListAsync(_computeList, EstimatedJobLengthInMinutes, 0.5)).ToList();

            // assert
            Assert.Equal(double.MaxValue, result[0].Cost);
        }

        public void ArrangeCompute(ComputeType computeType, string operatingSystem)
        {
            foreach (string location in Locations)
            {
                _computeList.Add(new Compute(location, VmSkuName, operatingSystem, computeType)
                {
                    Id = location
                });
            }
        }

        /// <param name="ratings">Rating for each location in the same order than in <see cref="Locations"/>.</param>
        private void ArrangeForecasts(double[] ratings)
        {
            for (int i = 0; i < ratings.Length; ++i)
            {
                _forecasts.Add(new EmissionsForecast
                {
                    EmissionsDataPoints = new List<EmissionsData>
                    {
                        new EmissionsData { Location = Locations[i], Rating = ratings[i] }
                    }
                });
            }

            _mockForecastHandler.Setup(forecastHandler =>
                forecastHandler.GetCurrentForecastAsync(Locations, It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), EstimatedJobLengthInMinutes))
                .Returns(Task.FromResult<IEnumerable<EmissionsForecast>>(_forecasts));
        }

        private void ArrangeUniformForecasts()
        {
            double[] ratings = new double[] { 50, 50, 50 };
            ArrangeForecasts(ratings);
        }

        private void ArrangePrices(double[] prices)
        {
            AzureRetailPricesApiResponse azureRetailPricesApiResponse = new()
            {
                Count = 6,
                Items = new List<AzureRetailPricesApiResponseItem>()
                {
                    new AzureRetailPricesApiResponseItem() { ProductName = "Virtual Machines Dv2 Series Windows", SkuName = "D5 v2", UnitPrice = prices[0] },
                    new AzureRetailPricesApiResponseItem() { ProductName = "Virtual Machines Dv2 Series Windows", SkuName = "D5 v2 Spot", UnitPrice = prices[1] },
                    new AzureRetailPricesApiResponseItem() { ProductName = "Virtual Machines Dv2 Series Windows", SkuName = "D5 v2 Low Priority", UnitPrice = prices[2] },
                    new AzureRetailPricesApiResponseItem() { ProductName = "Virtual Machines Dv2 Series", SkuName = "D5 v2", UnitPrice = prices[3] },
                    new AzureRetailPricesApiResponseItem() { ProductName = "Virtual Machines Dv2 Series", SkuName = "D5 v2 Spot", UnitPrice = prices[4] },
                    new AzureRetailPricesApiResponseItem() { ProductName = "Virtual Machines Dv2 Series", SkuName = "D5 v2 Low Priority", UnitPrice = prices[5] }
                }
            };

            _mockAzureRetailPricesApiClient.Setup(azureRetailPricesApiClient =>
                azureRetailPricesApiClient.GetVmPricesAsync(It.IsAny<string>(), VmSkuName, It.IsAny<string>()))
                .Returns(Task.FromResult(azureRetailPricesApiResponse));
        }

        /// <param name="ratings">Prices of dedicated VMs for each location in the same order than in <see cref="Locations"/>.</param>
        private void ArrangePricesPerLocation(double[] dedicatedComputePrices)
        {
            Dictionary<string, AzureRetailPricesApiResponse> prices = new Dictionary<string, AzureRetailPricesApiResponse>();

            for (int i = 0; i < dedicatedComputePrices.Length; ++i)
            {
                prices[Locations[i]] = new()
                {
                    Count = 6,
                    Items = new List<AzureRetailPricesApiResponseItem>()
                    {
                        new AzureRetailPricesApiResponseItem() { ProductName = "Virtual Machines Dv2 Series Windows", SkuName = "D5 v2", UnitPrice = dedicatedComputePrices[i] },
                        new AzureRetailPricesApiResponseItem() { ProductName = "Virtual Machines Dv2 Series Windows", SkuName = "D5 v2 Spot", UnitPrice = dedicatedComputePrices[i] / 10 },
                        new AzureRetailPricesApiResponseItem() { ProductName = "Virtual Machines Dv2 Series Windows", SkuName = "D5 v2 Low Priority", UnitPrice = dedicatedComputePrices[i] / 2 },
                        new AzureRetailPricesApiResponseItem() { ProductName = "Virtual Machines Dv2 Series", SkuName = "D5 v2", UnitPrice = dedicatedComputePrices[i] },
                        new AzureRetailPricesApiResponseItem() { ProductName = "Virtual Machines Dv2 Series", SkuName = "D5 v2 Spot", UnitPrice = dedicatedComputePrices[i] / 10 },
                        new AzureRetailPricesApiResponseItem() { ProductName = "Virtual Machines Dv2 Series", SkuName = "D5 v2 Low Priority", UnitPrice = dedicatedComputePrices[i] / 2 }
                    }
                };

                _mockAzureRetailPricesApiClient.Setup(azureRetailPricesApiClient =>
                    azureRetailPricesApiClient.GetVmPricesAsync(Locations[i], VmSkuName, It.IsAny<string>()))
                    .Returns(Task.FromResult(prices[Locations[i]]));
            }
        }
    }
}
