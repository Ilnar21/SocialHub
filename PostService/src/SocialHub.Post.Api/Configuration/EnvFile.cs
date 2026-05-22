namespace SocialHub.Post.Api.Configuration;

public static class EnvFile
{
    public static void Load(string fileName = ".env")
    {
        var path = FindFile(fileName);
        if (path is null)
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string? FindFile(string fileName)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, fileName);
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
