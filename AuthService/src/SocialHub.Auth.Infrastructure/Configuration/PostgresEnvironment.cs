namespace SocialHub.Auth.Infrastructure.Configuration;

public static class PostgresEnvironment
{
    public static void ApplyConnectionString()
    {
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB");
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER");
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";

        if (string.IsNullOrWhiteSpace(database)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
        Environment.SetEnvironmentVariable("ConnectionStrings__AuthDb", connectionString);
    }

    public static void ApplyJwtSettings()
    {
        Map("JWT_ISSUER", "Jwt__Issuer");
        Map("JWT_AUDIENCE", "Jwt__Audience");
        Map("JWT_SECRET", "Jwt__Secret");
        Map("JWT_ACCESS_TOKEN_LIFETIME_MINUTES", "Jwt__AccessTokenLifetimeMinutes");
    }

    private static void Map(string source, string target)
    {
        var value = Environment.GetEnvironmentVariable(source);
        if (!string.IsNullOrWhiteSpace(value))
        {
            Environment.SetEnvironmentVariable(target, value);
        }
    }
}
