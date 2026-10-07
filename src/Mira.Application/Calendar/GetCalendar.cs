using MediatR;
using Mira.Application.Configuration;
using Mira.Domain.Calendar;
using Mira.Domain.Configuration;

namespace Mira.Application.Calendar;

public interface ICalendarReader
{
    // Events of one configured calendar that overlap [from, to), recurrences expanded; empty when it is unavailable.
    Task<IReadOnlyList<CalendarEvent>> ReadAsync(Feed calendar, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}

public sealed record GetCalendarQuery : IRequest<IReadOnlyList<CalendarEvent>>;

// Today only, in the configured time zone: the whole day, including events that already ended.
public sealed class GetCalendarHandler(ISettingsStore settings, ICalendarReader reader, TimeProvider clock)
    : IRequestHandler<GetCalendarQuery, IReadOnlyList<CalendarEvent>>
{
    public const int MaxItems = 100;

    public async Task<IReadOnlyList<CalendarEvent>> Handle(GetCalendarQuery q, CancellationToken ct)
    {
        var cfg = await settings.GetAsync(ct);
        var tz = ZoneOf(cfg.TimeZone) ?? clock.LocalTimeZone;
        var today = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), tz).Date;
        var dayStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(today, tz), TimeSpan.Zero);
        var dayEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(today.AddDays(1), tz), TimeSpan.Zero);

        // Ask for a day either side so all-day events (stored as UTC dates) are never missed at the edges, then cut exactly.
        var events = await Task.WhenAll(cfg.Calendars.Select(async (c, i) =>
            (await reader.ReadAsync(c, dayStart.AddDays(-1), dayEnd.AddDays(1), ct)).Select(e => e with { Source = i })));
        return events
            .SelectMany(e => e)
            .Where(e => e.AllDay ? e.Start.Date <= today && today < e.End.Date : e.Start < dayEnd && e.End > dayStart)
            .DistinctBy(e => (e.Title, e.Start, e.End)) // the same event invited on two calendars
            .OrderByDescending(e => e.AllDay).ThenBy(e => e.Start).ThenBy(e => e.Title)
            .Take(MaxItems)
            .ToList();
    }

    private static TimeZoneInfo? ZoneOf(string id)
    {
        try { return string.IsNullOrWhiteSpace(id) ? null : TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }
}
