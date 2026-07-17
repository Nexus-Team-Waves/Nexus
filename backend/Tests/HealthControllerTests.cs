using Mems.Api.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mems.Tests;

/// <summary>
/// Smoke test proving the test harness runs end-to-end. Replace/extend as real
/// behaviour lands — the entitlement calculator is the thing that will need real coverage.
/// </summary>
public class HealthControllerTests
{
    [Fact]
    public void Get_ReportsHealthy()
    {
        var controller = new HealthController(
            NullLogger<HealthController>.Instance,
            new FakeWebHostEnvironment("Testing"));

        var result = controller.Get().Result as OkObjectResult;
        var body = Assert.IsType<HealthResponse>(result!.Value);

        Assert.Equal("healthy", body.Status);
        Assert.Equal("Testing", body.Environment);
    }

    /// <summary>Minimal stand-in so the controller can be constructed without a host.</summary>
    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Mems.Api";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
