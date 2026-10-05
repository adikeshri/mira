namespace Mira.Domain.Locations;

public readonly record struct Location(double Lat, double Lon, string? Name = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => Math.Abs(Lat) <= 90 && Math.Abs(Lon) <= 180;
}
