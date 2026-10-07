namespace Mira.Domain.Calendar;

// Source is the calendar's position in config.json, so a client can colour by calendar stably.
// All-day events carry midnight UTC of their start and (exclusive) end date.
public sealed record CalendarEvent(string Title, string Calendar, DateTimeOffset Start, DateTimeOffset End, bool AllDay, string? Location, int Source = 0);
