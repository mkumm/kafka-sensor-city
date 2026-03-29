namespace SensorSimulator;

public class CitySimulator
{
    private record Sensor(
        string Id,
        SensorType Type,
        string DistrictId,
        string? IntersectionId
    );

    private readonly List<Sensor> _sensors = [];
    private long _seq = 0;
    private readonly Random _rng = new();

    // Fixed count so the dashboard can pre-populate all dots without waiting
    // for the first state change from each intersection
    public const int IntersectionsPerDistrict = 12;

    private static readonly (SensorType Type, int Weight)[] NonIntersectionWeights =
    [
        (SensorType.Highway,    35),
        (SensorType.Parking,    20),
        (SensorType.Pedestrian, 15)
    ];

    public CitySimulator(int districts = 5, int sensorsPerDistrict = 40)
    {
        for (int d = 1; d <= districts; d++)
        {
            string districtId = $"district-{d}";

            for (int i = 1; i <= IntersectionsPerDistrict; i++)
            {
                _sensors.Add(new Sensor(
                    Id:             $"sensor-{districtId}-intersection-{i:D3}",
                    Type:           SensorType.Intersection,
                    DistrictId:     districtId,
                    IntersectionId: $"int-{d:D2}{i:D2}"
                ));
            }

            int remaining = sensorsPerDistrict - IntersectionsPerDistrict;
            for (int s = 1; s <= remaining; s++)
            {
                var type = PickWeightedNonIntersectionType();
                _sensors.Add(new Sensor(
                    Id:             $"sensor-{districtId}-{type.ToString().ToLower()}-{s:D3}",
                    Type:           type,
                    DistrictId:     districtId,
                    IntersectionId: null
                ));
            }
        }
    }

    public SensorEvent NextEvent()
    {
        var sensor  = _sensors[_rng.Next(_sensors.Count)];
        var traffic = SimulateTraffic(sensor.Type);

        return new SensorEvent
        {
            SensorId        = sensor.Id,
            SensorType      = sensor.Type,
            DistrictId      = sensor.DistrictId,
            IntersectionId  = sensor.IntersectionId,
            VehicleCount    = traffic.vehicleCount,
            SpeedAvgKmh     = traffic.speedAvgKmh,
            OccupancyPct    = traffic.occupancyPct,
            IsCongested     = traffic.occupancyPct > 75 && traffic.speedAvgKmh < 25,
            TimestampUtc    = DateTime.UtcNow,
            SequenceNum     = Interlocked.Increment(ref _seq)
        };
    }

    private (int vehicleCount, double speedAvgKmh, int occupancyPct) SimulateTraffic(SensorType type)
    {
        // Intersection sensors occasionally spike into congestion
        // Other sensor types stay in normal ranges
        bool congested = type == SensorType.Intersection && _rng.Next(100) < 15;

        int vehicleCount = congested
            ? _rng.Next(18, 35)
            : _rng.Next(1, 18);

        double speedAvgKmh = congested
            ? Math.Round(_rng.NextDouble() * 20 + 5, 1)   // 5–25 km/h
            : Math.Round(_rng.NextDouble() * 60 + 30, 1); // 30–90 km/h

        int occupancyPct = congested
            ? _rng.Next(76, 100)
            : _rng.Next(5, 75);

        return (vehicleCount, speedAvgKmh, occupancyPct);
    }

    private SensorType PickWeightedNonIntersectionType()
    {
        int total = NonIntersectionWeights.Sum(x => x.Weight);
        int roll  = _rng.Next(total);
        int cum   = 0;

        foreach (var (type, weight) in NonIntersectionWeights)
        {
            cum += weight;
            if (roll < cum) return type;
        }

        return SensorType.Highway;
    }
}
