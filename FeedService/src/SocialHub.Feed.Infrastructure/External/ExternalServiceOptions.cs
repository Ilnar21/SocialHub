namespace SocialHub.Feed.Infrastructure.External;

/// <summary>
/// Адреса соседних микросервисов. Привязывается к секции "Services" в appsettings.
/// В контейнере переопределяется переменными окружения:
///   Services__CommunityServiceUrl, Services__PostServiceUrl, Services__TimeoutSeconds.
/// </summary>
public sealed class ExternalServiceOptions
{
    public const string SectionName = "Services";

    public string CommunityServiceUrl { get; set; } = "http://community-service:8080";

    public string CommunityGrpcUrl { get; set; } = "http://community-service:8081";

    public string PostServiceUrl { get; set; } = "http://post-service:8080";

    public string? InternalToken { get; set; }

    /// <summary>Таймаут одного исходящего запроса. Часть бюджета NFR-2 (5 секунд).</summary>
    public int TimeoutSeconds { get; set; } = 4;
}
