using System.Collections.Concurrent;

namespace DashboardService;

public class IntersectionState
{
    public string IntersectionId { get; init; } = "";
    public string DistrictId     { get; init; } = "";
    public string Status         { get; init; } = "Normal";
    public int VehicleCount      { get; init; }
    public double SpeedAvgKmh    { get; init; }
    public int OccupancyPct      { get; init; }
    public DateTime TimestampUtc { get; init; }
    public string Reason         { get; init; } = "";
}

// Singleton that holds the current state of every intersection
// Acts as the source of truth for new browser connections
public class IntersectionStateStore
{
    private readonly ConcurrentDictionary<string, IntersectionState> _states = new();

    public void Update(IntersectionState state) =>
        _states[state.IntersectionId] = state;

    public IEnumerable<IntersectionState> GetAll() =>
        _states.Values;
}
