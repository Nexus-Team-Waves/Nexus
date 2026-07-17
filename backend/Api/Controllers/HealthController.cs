using Microsoft.AspNetCore.Mvc;

namespace Mems.Api.Controllers;

/// <summary>
/// Liveness endpoint. Deliberately trivial and unauthenticated: it proves the API process
/// is up and nothing more. It does not touch the MEMS DB or SAP B1, so it stays green even
/// when those are down — that separation is what makes it useful to a load balancer.
/// A future readiness check (DB + SAP reachable) should be a *separate* endpoint.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly IWebHostEnvironment _environment;

    public HealthController(ILogger<HealthController> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    [HttpGet]
    public ActionResult<HealthResponse> Get()
    {
        _logger.LogInformation("Health probe served.");

        return Ok(new HealthResponse(
            Status: "healthy",
            Service: "MEMS API",
            Environment: _environment.EnvironmentName,
            UtcTime: DateTime.UtcNow));
    }
}

public sealed record HealthResponse(
    string Status,
    string Service,
    string Environment,
    DateTime UtcTime);
