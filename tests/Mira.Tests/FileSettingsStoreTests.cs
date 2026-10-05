using Microsoft.Extensions.Logging.Abstractions;
using Mira.Infrastructure.Configuration;

namespace Mira.Tests;

public class FileSettingsStoreTests
{
    [Fact]
    public async Task Relative_config_is_found_in_a_parent_of_the_working_directory()
    {
        var root = Directory.CreateTempSubdirectory();
        var child = Directory.CreateDirectory(Path.Combine(root.FullName, "src", "Api"));
        await File.WriteAllTextAsync(Path.Combine(root.FullName, "mira-test.json"),
            """{ "name": "Ada", "news": { "local": [ { "url": "https://x.test/rss", "name": "Local" } ] } }""");
        try
        {
            var cfg = await new FileSettingsStore("mira-test.json", NullLogger<FileSettingsStore>.Instance, child.FullName).GetAsync(default);
            Assert.Equal("Ada", cfg.Name);
            Assert.Equal("Local", Assert.Single(cfg.News.Local).Name);
        }
        finally
        {
            root.Delete(true);
        }
    }
}
