namespace Mira.Domain.Locations;

// TypicalMinutes is the usual drive time for this route (Mapbox only); null when unknown.
public sealed record Commute(string Name, int Minutes, double Km, int? TypicalMinutes = null);
