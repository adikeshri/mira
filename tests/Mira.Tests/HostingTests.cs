using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mira.Tests;

public class HostingTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly DirectoryInfo _ui = Directory.CreateTempSubdirectory();
    private readonly WebApplicationFactory<Program> _factory;

    public HostingTests(WebApplicationFactory<Program> factory)
    {
        File.WriteAllText(Path.Combine(_ui.FullName, "index.html"), "<html>mirror</html>");
        _factory = factory.WithWebHostBuilder(b => b
            .UseSetting("Mira:ConfigPath", "/nonexistent/config.json")
            .UseSetting("Mira:UiPath", _ui.FullName));
    }

    public void Dispose() => _ui.Delete(true);

    [Fact]
    public async Task Serves_the_ui_with_spa_fallback_and_security_headers_but_keeps_api_404s_json()
    {
        var http = _factory.CreateClient();
        var root = await http.GetAsync("/");
        Assert.Contains("mirror", await root.Content.ReadAsStringAsync());
        Assert.Contains("connect-src 'self'", root.Headers.GetValues("Content-Security-Policy").Single());

        Assert.Contains("mirror", await http.GetStringAsync("/some/client/route")); // SPA fallback
        var missing = await http.GetAsync("/api/nope");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.DoesNotContain("mirror", await missing.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cors_allows_configured_origins_only()
    {
        var http = _factory.CreateClient();
        async Task<bool> Allowed(string origin)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/config");
            req.Headers.Add("Origin", origin);
            return (await http.SendAsync(req)).Headers.Contains("Access-Control-Allow-Origin");
        }
        Assert.True(await Allowed("http://127.0.0.1:8080"));
        Assert.False(await Allowed("https://evil.test"));
    }
}
