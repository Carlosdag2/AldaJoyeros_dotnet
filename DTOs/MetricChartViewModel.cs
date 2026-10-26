namespace AldaJoyeros.DTOs;

public sealed record MetricChartPoint(string Label, string AxisLabel, double Value);
public sealed record MetricChartViewModel(string Id, string Title, string Unit, IReadOnlyList<MetricChartPoint> Points, bool Compact = false);
