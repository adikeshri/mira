using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Mira.Application.Configuration;
using Mira.Domain.Configuration;

namespace Mira.Infrastructure.Configuration;

// Re-read on every call so edits to config.json apply without a restart.
// A relative path is looked up in the working directory and then each parent, so
// `dotnet run` (which starts in src/Mira.Api) still finds the repo-root config.json.
public sealed class FileSettingsStore(string configPath, ILogger<FileSettingsStore> log, string? startDir = null) : ISettingsStore
{
    private string? _reported;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public async Task<MirrorSettings> GetAsync(CancellationToken ct)
    {
        var path = Resolve();
        if (path is null)
        {
            Report($"no config file found for '{configPath}', using defaults");
            return new MirrorSettings();
        }
        Report($"using config {path}");
        try
        {
            await using var file = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<MirrorSettings>(file, Json, ct) ?? new MirrorSettings();
        }
        catch (JsonException e)
        {
            // ponytail: one bad value drops the whole file to defaults; mirror-magic's zod fell back per field.
            log.LogError("{Path} is not valid, using defaults: {Message}", path, e.Message);
            return new MirrorSettings();
        }
    }

    private string? Resolve()
    {
        if (Path.IsPathRooted(configPath)) return File.Exists(configPath) ? configPath : null;
        for (var dir = new DirectoryInfo(startDir ?? Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, configPath);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    // Log only when the outcome changes, not on every request.
    private void Report(string message)
    {
        if (message == _reported) return;
        _reported = message;
        log.LogInformation("{Message}", message);
    }
}
