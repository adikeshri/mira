using MediatR;
using Mira.Application.Calendar;

namespace Mira.Api.Endpoints;

public static class CalendarEndpoints
{
    // Today's events from every calendar in config.json, merged: all-day first, then by start time.
    public static void MapCalendar(this IEndpointRouteBuilder api) =>
        api.MapGet("/calendar", (ISender mediator, CancellationToken ct) => mediator.Send(new GetCalendarQuery(), ct));
}
