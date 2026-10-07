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

    [Fact]
    public async Task Display_settings_default_to_auto_and_read_from_config()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var none = await new FileSettingsStore(Path.Combine(dir.FullName, "missing.json"), NullLogger<FileSettingsStore>.Instance).GetAsync(default);
            Assert.Equal((Mira.Domain.Configuration.LayoutMode.Auto, 0.6), (none.Display.Layout, none.Display.NightDim));

            await File.WriteAllTextAsync(Path.Combine(dir.FullName, "c.json"), """{ "display": { "layout": "portrait", "nightDim": 0.4 } }""");
            var set = await new FileSettingsStore(Path.Combine(dir.FullName, "c.json"), NullLogger<FileSettingsStore>.Instance).GetAsync(default);
            Assert.Equal((Mira.Domain.Configuration.LayoutMode.Portrait, 0.4), (set.Display.Layout, set.Display.NightDim));
        }
        finally { dir.Delete(true); }
    }
}
