namespace AggregatorService;

public enum SignalStatus
{
    Normal,
    Congested
}

public class SignalStateChange
{
    public string IntersectionId  { get; init; } = "";
    public string DistrictId      { get; init; } = "";
    public SignalStatus Status     { get; init; }
    public int VehicleCount       { get; init; }
    public double SpeedAvgKmh     { get; init; }
    public int OccupancyPct       { get; init; }
    public DateTime TimestampUtc  { get; init; }
    public string Reason          { get; init; } = "";
}
