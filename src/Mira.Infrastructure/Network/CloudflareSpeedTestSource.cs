using Mira.Application.Network;

namespace Mira.Infrastructure.Network;

// Relays a fixed 2.5 MB download so the mirror's browser can time it without calling Cloudflare itself.
// It measures the slower of mira's internet link and the mirror-to-mira link; fine when both sit on one LAN.
public sealed class CloudflareSpeedTestSource(HttpClient http) : ISpeedTestSource
{
    public const long Bytes = 2_500_000;

    public async Task<Stream> OpenAsync(CancellationToken ct)
    {
        var res = await http.GetAsync($"https://speed.cloudflare.com/__down?bytes={Bytes}", HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStreamAsync(ct);
    }
}
