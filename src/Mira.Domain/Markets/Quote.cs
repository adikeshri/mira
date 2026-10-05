namespace Mira.Domain.Markets;

public sealed record Quote(double Value, double? ChangePct, IReadOnlyList<double> Trend)
{
    // Change is measured against the previous print; unknown or non-positive means no change figure.
    public static Quote Of(double value, double? previous, IReadOnlyList<double> trend) =>
        new(value, previous is > 0 ? (value - previous.Value) / previous.Value * 100 : null, trend);
}

// One line of the markets panel. InvertColor: for fx a rising rate reads as bad news to the viewer.
public sealed record MarketRow(string Key, string Label, string? Currency, bool InvertColor, Quote? Quote);
