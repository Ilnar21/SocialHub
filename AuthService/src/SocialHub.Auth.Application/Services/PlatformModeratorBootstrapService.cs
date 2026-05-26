using SocialHub.Auth.Application.Abstractions;
using SocialHub.Auth.Domain.Enums;

namespace SocialHub.Auth.Application.Services;

public sealed class PlatformModeratorBootstrapService(
    IUserRepository users,
    IUnitOfWork unitOfWork)
{
    public async Task<PlatformModeratorBootstrapResult> ApplyAsync(
        string? configuredModerators,
        CancellationToken cancellationToken)
    {
        var entries = ParseEntries(configuredModerators);
        if (entries.Count == 0)
        {
            return new PlatformModeratorBootstrapResult(0, 0, []);
        }

        var promotedCount = 0;
        var alreadyModeratorCount = 0;
        var skippedEntries = new List<string>();

        foreach (var entry in entries)
        {
            var user = await users.FindByUsernameAsync(entry.Username, cancellationToken);
            if (user is null)
            {
                skippedEntries.Add($"{entry.Username}: user was not found");
                continue;
            }

            if (!user.Email.Equals(entry.Email, StringComparison.OrdinalIgnoreCase))
            {
                skippedEntries.Add($"{entry.Username}: email does not match configured value");
                continue;
            }

            if (user.Role == UserRole.PlatformModerator)
            {
                alreadyModeratorCount++;
                continue;
            }

            user.Role = UserRole.PlatformModerator;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            promotedCount++;
        }

        if (promotedCount > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new PlatformModeratorBootstrapResult(promotedCount, alreadyModeratorCount, skippedEntries);
    }

    private static IReadOnlyCollection<PlatformModeratorBootstrapEntry> ParseEntries(string? configuredModerators)
    {
        if (string.IsNullOrWhiteSpace(configuredModerators))
        {
            return [];
        }

        var entries = new List<PlatformModeratorBootstrapEntry>();
        var uniqueEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawEntry in configuredModerators.Split(
                     [',', ';', '\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = rawEntry.IndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == rawEntry.Length - 1)
            {
                continue;
            }

            var username = rawEntry[..separatorIndex].Trim();
            var email = rawEntry[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            var uniqueKey = $"{username}:{email}";
            if (uniqueEntries.Add(uniqueKey))
            {
                entries.Add(new PlatformModeratorBootstrapEntry(username, email));
            }
        }

        return entries;
    }
}

public sealed record PlatformModeratorBootstrapResult(
    int PromotedCount,
    int AlreadyModeratorCount,
    IReadOnlyCollection<string> SkippedEntries);

internal sealed record PlatformModeratorBootstrapEntry(string Username, string Email);
