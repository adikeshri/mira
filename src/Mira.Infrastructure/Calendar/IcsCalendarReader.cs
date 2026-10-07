using Ical.Net;
using Ical.Net.DataTypes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Mira.Application.Calendar;
using Event = Mira.Domain.Calendar.CalendarEvent;
using IcalEvent = Ical.Net.CalendarComponents.CalendarEvent;
using Mira.Domain.Configuration;

namespace Mira.Infrastructure.Calendar;

// Fetches only calendars listed in config.json. The URL is a secret: it is never logged or returned.
public sealed class IcsCalendarReader(HttpClient http, IMemoryCache cache, ILogger<IcsCalendarReader> log) : ICalendarReader
{
    private const int MaxBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5), StaleFor = TimeSpan.FromDays(1);

    public async Task<IReadOnlyList<Event>> ReadAsync(Feed calendar, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var key = $"ics:{calendar.Url}";
        try
        {
            var ics = await cache.Cached(key, Ttl, async () =>
            {
                using var res = await http.GetAsync(calendar.Url, HttpCompletionOption.ResponseHeadersRead, ct);
                res.EnsureSuccessStatusCode();
                var text = await ReadCapped(res, ct);
                cache.Set($"{key}:stale", text, StaleFor);
                return text;
            });
            return Parse(ics, calendar.Name, from, to);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            // HttpRequestException.Message can embed the URL, so only the calendar name is logged.
            log.LogWarning("calendar {Name} failed: {Type}", calendar.Name, e.GetType().Name);
            return cache.TryGetValue($"{key}:stale", out string? stale) && stale is not null ? Parse(stale, calendar.Name, from, to) : [];
        }
    }

    private static async Task<string> ReadCapped(HttpResponseMessage res, CancellationToken ct)
    {
        await using var body = await res.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int n;
        while ((n = await body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + n > MaxBytes) throw new InvalidDataException($"calendar larger than {MaxBytes} bytes");
            buffer.Write(chunk, 0, n);
        }
        return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    public static IReadOnlyList<Event> Parse(string ics, string calendarName, DateTimeOffset from, DateTimeOffset to)
    {
        Ical.Net.Calendar cal;
        try { cal = Ical.Net.Calendar.Load(ics); }
        catch (Exception e) when (e is not OperationCanceledException) { return []; }
        if (cal is null) return [];

        return cal.GetOccurrences<IcalEvent>(new CalDateTime(from.UtcDateTime))
            .TakeWhile(o => o.Period.StartTime.AsUtc < to.UtcDateTime)
            .Where(o => o.Source is IcalEvent { Status: not "CANCELLED" })
            .Select(o =>
            {
                var ev = (IcalEvent)o.Source;
                var start = o.Period.StartTime;
                var end = o.Period.EffectiveEndTime ?? start;
                return new Event(
                    string.IsNullOrWhiteSpace(ev.Summary) ? "(no title)" : ev.Summary.Trim(),
                    calendarName,
                    ToUtc(start), ToUtc(end), !start.HasTime,
                    string.IsNullOrWhiteSpace(ev.Location) ? null : ev.Location.Trim());
            })
            .ToList();

        // Date-only values become midnight UTC so the UI can show the date without a timezone shift.
        static DateTimeOffset ToUtc(CalDateTime d) => new(DateTime.SpecifyKind(d.HasTime ? d.AsUtc : d.Value, DateTimeKind.Utc));
    }
}
