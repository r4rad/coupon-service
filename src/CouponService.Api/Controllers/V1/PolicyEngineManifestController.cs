using CouponService.Engine.Facts;
using CouponService.Engine.Manifest;
using Microsoft.AspNetCore.Mvc;

namespace CouponService.Api.Controllers.V1;

[ApiController]
[Route("v1/policy-engine")]
[Tags("Admin")]
public sealed class PolicyEngineManifestController(IFactRegistry factRegistry) : ControllerBase
{
    [HttpGet("manifest")]
    [EndpointSummary("Get the engine manifest")]
    [EndpointDescription(
        "Returns every registered fact with its type and cost, every condition operator, every effect operator, and the configured parse limits. Generated from the live registry so it cannot drift.")]
    [ProducesResponseType(typeof(EngineManifest), StatusCodes.Status200OK)]
    public ActionResult<EngineManifest> GetManifest()
    {
        // AC-6.2: manifest is generated, never hand-maintained.
        var manifest = EngineManifestGenerator.Generate(factRegistry, EngineLimits.Default);
        return Ok(manifest);
    }
}
