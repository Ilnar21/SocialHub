using Microsoft.AspNetCore.Mvc;
using SocialHub.Feed.Application.Abstractions;

namespace SocialHub.Feed.Api.Controllers;

/// <summary>
/// Healthcheck Feed Service. Проверяет доступность Redis.
/// </summary>
[ApiController]
[Route("feed")]
public sealed class HealthController : ControllerBase
{
    private readonly IFeedCache _cache;

    public HealthController(IFeedCache cache)
    {
        _cache = cache;
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var redisOk = await _cache.PingAsync(ct);
        return Ok(new
        {
            status = "ok",
            redis = redisOk ? "up" : "down",
        });
    }
}
