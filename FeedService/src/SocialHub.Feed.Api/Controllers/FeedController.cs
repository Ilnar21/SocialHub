using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SocialHub.Feed.Application.Abstractions;
using SocialHub.Feed.Application.Models.Feed;
using SocialHub.Feed.Domain.Constants;

namespace SocialHub.Feed.Api.Controllers;

/// <summary>
/// Публичные операции над лентой пользователя.
/// Health-check вынесен в отдельный HealthController.
/// </summary>
[ApiController]
[Route("feed")]
public sealed class FeedController : ControllerBase
{
    private readonly IFeedService _feedService;
    private readonly ICurrentUserContext _userContext;

    public FeedController(IFeedService feedService, ICurrentUserContext userContext)
    {
        _feedService = feedService;
        _userContext = userContext;
    }

    /// <summary>
    /// GET /feed?page=1&amp;limit=20
    /// Лента текущего пользователя, отсортированная по убыванию score.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(FeedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FeedResponse>> GetFeed(
        [FromQuery] int page = 1,
        [FromQuery] int limit = FeedLimits.DefaultPageSize,
        CancellationToken ct = default)
    {
        if (!_userContext.IsAuthenticated || _userContext.UserId is not { } userId)
        {
            return Unauthorized(new { error = "A valid JWT bearer token is required" });
        }

        var feed = await _feedService.GetFeedAsync(userId, page, limit, ct);
        return Ok(feed);
    }

    /// <summary>
    /// POST /feed/refresh
    /// Пользователь принудительно сбрасывает кэш своей ленты — следующий GET /feed
    /// пройдёт мимо Redis и пересоберёт страницы из источников.
    /// </summary>
    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!_userContext.IsAuthenticated || _userContext.UserId is not { } userId)
        {
            return Unauthorized(new { error = "A valid JWT bearer token is required" });
        }

        await _feedService.RefreshAsync(userId, ct);
        return Accepted();
    }

    /// <summary>
    /// POST /feed/invalidate { "userId": "..." }
    /// Внутренний endpoint, который вызывает Post Service при появлении нового поста
    /// в подписке: сбрасывает кэш ленты конкретного подписчика.
    /// Позже сюда же можно подписать Kafka-consumer на топик feed.cache.invalidate.
    /// </summary>
    [HttpPost("invalidate")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Invalidate(
        [FromBody] InvalidateFeedRequest request,
        CancellationToken ct)
    {
        await _feedService.InvalidateAsync(request.UserId, ct);
        return Accepted();
    }
}
