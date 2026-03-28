namespace SensorSimulator;

public enum SensorType
{
    Intersection,
    Highway,
    Parking,
    Pedestrian
}

public class SensorEvent
{
    public string SensorId       { get; init; } = "";
    public SensorType SensorType { get; init; }
    public string DistrictId     { get; init; } = "";
    public string? IntersectionId { get; init; }
    public int VehicleCount      { get; init; }
    public double SpeedAvgKmh    { get; init; }
    public int OccupancyPct      { get; init; }
    public bool IsCongested      { get; init; }
    public DateTime TimestampUtc { get; init; }
    public long SequenceNum      { get; init; }
}
