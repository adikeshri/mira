using Mira.Application.Calendar;
using Mira.Application.Configuration;
using Mira.Domain.Calendar;
using Mira.Domain.Configuration;
using Mira.Infrastructure.Calendar;

namespace Mira.Tests;

public class CalendarTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    private const string Ics = """
        BEGIN:VCALENDAR
        VERSION:2.0
        PRODID:-//test//EN
        BEGIN:VEVENT
        UID:standup
        DTSTART:20260928T100000Z
        DTEND:20260928T101500Z
        RRULE:FREQ=DAILY
        SUMMARY:Standup
        END:VEVENT
        BEGIN:VEVENT
        UID:holiday
        DTSTART;VALUE=DATE:20261009
        DTEND;VALUE=DATE:20261010
        SUMMARY:Holiday
        END:VEVENT
        BEGIN:VEVENT
        UID:nope
        DTSTART:20261008T120000Z
        DTEND:20261008T130000Z
        STATUS:CANCELLED
        SUMMARY:Cancelled
        END:VEVENT
        END:VCALENDAR
        """;

    private sealed class Fake(Dictionary<string, IReadOnlyList<CalendarEvent>> byName) : ICalendarReader
    {
        public Task<IReadOnlyList<CalendarEvent>> ReadAsync(Feed c, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
            Task.FromResult(byName.GetValueOrDefault(c.Name) ?? []);
    }

    private sealed class Settings(MirrorSettings s) : ISettingsStore
    {
        public Task<MirrorSettings> GetAsync(CancellationToken ct) => Task.FromResult(s);
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void Ics_expands_recurrence_marks_all_day_and_skips_cancelled()
    {
        var events = IcsCalendarReader.Parse(Ics, "Work", Now, Now.AddDays(3));
        Assert.Equal(["Standup", "Standup", "Holiday", "Standup"], events.Select(e => e.Title).ToArray()); // Oct 7 and 8 10:00, Oct 9 all-day, Oct 9 10:00
        Assert.All(events, e => Assert.Equal("Work", e.Calendar));
        Assert.True(events.Single(e => e.Title == "Holiday").AllDay);
        Assert.All(events.Where(e => e.Title == "Standup"), e => Assert.Equal(TimeSpan.FromMinutes(15), e.End - e.Start));
        Assert.Equal(TimeSpan.FromDays(1), events.Single(e => e.Title == "Holiday").End - events.Single(e => e.Title == "Holiday").Start);
        Assert.Empty(IcsCalendarReader.Parse("not an ics", "Work", Now, Now.AddDays(3)));
    }

    [Fact]
    public async Task Only_today_in_the_configured_zone_is_returned_merged_and_deduped()
    {
        // Now is 09:00 UTC = 04:00 in UTC-5, so "today" there is Oct 7 05:00 UTC to Oct 8 05:00 UTC.
        CalendarEvent Timed(string cal, string title, DateTimeOffset start) => new(title, cal, start, start.AddHours(1), false, null);
        CalendarEvent Day(string title, int day) => new(title, "A", new(2026, 10, day, 0, 0, 0, TimeSpan.Zero), new(2026, 10, day + 1, 0, 0, 0, TimeSpan.Zero), true, null);
        var cfg = new MirrorSettings { TimeZone = "America/New_York", Calendars = [new("https://secret/a", "A"), new("https://secret/b", "B"), new("https://secret/c", "Down")] };
        var reader = new Fake(new()
        {
            ["A"] = [Timed("A", "Late", Now.AddHours(15)), Timed("A", "Shared", Now.AddHours(2)), Timed("A", "Yesterday", Now.AddHours(-10)),
                     Timed("A", "Tomorrow", Now.AddHours(21)), Day("Today all-day", 7), Day("Tomorrow all-day", 8)],
            ["B"] = [Timed("B", "Early", Now.AddHours(-1)), Timed("B", "Shared", Now.AddHours(2))],
        });

        var events = await new GetCalendarHandler(new Settings(cfg), reader, new Clock()).Handle(new GetCalendarQuery(), default);

        Assert.Equal(["Today all-day", "Early", "Shared", "Late"], events.Select(e => e.Title).ToArray());
        Assert.Equal([0, 1, 0, 0], events.Select(e => e.Source).ToArray()); // config position: A=0, B=1
        Assert.Empty((await new GetSettingsHandler(new Settings(cfg)).Handle(new(), default)).Calendars);
    }
}
