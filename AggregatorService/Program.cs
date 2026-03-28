using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using AggregatorService;

var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP")
                       ?? "localhost:9092";

Console.WriteLine($"Connecting to Kafka at {bootstrapServers}");

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() }
};

var consumerConfig = new ConsumerConfig
{
    BootstrapServers = bootstrapServers,
    GroupId          = "aggregator-service-group",
    AutoOffsetReset  = AutoOffsetReset.Latest,
    EnableAutoCommit = false
};

var producerConfig = new ProducerConfig
{
    BootstrapServers      = bootstrapServers,
    Acks                  = Acks.All,
    MessageSendMaxRetries = 3
};

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("Shutting down...");
};

// Rolling window: track last 30 seconds of events per intersection
var windows = new Dictionary<string, Queue<(DateTime time, bool congested)>>();
var currentStatus = new Dictionary<string, SignalStatus>();
var windowDuration = TimeSpan.FromSeconds(30);
const int CongestionThreshold = 3; // congested events in window to trigger state change

long received  = 0;
long published = 0;

using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

consumer.Subscribe("intersection-events");
Console.WriteLine("Aggregator started. Listening on intersection-events...\n");

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        var result = consumer.Consume(cts.Token);
        received++;

        using var doc  = JsonDocument.Parse(result.Message.Value);
        var root       = doc.RootElement;
        var intId      = root.GetProperty("intersectionId").GetString()!;
        var districtId = root.GetProperty("districtId").GetString()!;
        var congested  = root.GetProperty("isCongested").GetBoolean();
        var eventTime  = root.GetProperty("timestampUtc").GetDateTime();
        var vehicles   = root.GetProperty("vehicleCount").GetInt32();
        var speed      = root.GetProperty("speedAvgKmh").GetDouble();
        var occupancy  = root.GetProperty("occupancyPct").GetInt32();

        // Maintain rolling window per intersection
        if (!windows.ContainsKey(intId))
        {
            windows[intId]       = new Queue<(DateTime, bool)>();
            currentStatus[intId] = SignalStatus.Normal;
        }

        var window = windows[intId];
        window.Enqueue((eventTime, congested));

        // Evict events older than 30 seconds
        while (window.Count > 0 && eventTime - window.Peek().time > windowDuration)
            window.Dequeue();

        // Count congested events in current window
        var congestedCount = window.Count(e => e.congested);
        var newStatus = congestedCount >= CongestionThreshold
            ? SignalStatus.Congested
            : SignalStatus.Normal;

        // Only publish when status changes
        if (newStatus != currentStatus[intId])
        {
            currentStatus[intId] = newStatus;

            var stateChange = new SignalStateChange
            {
                IntersectionId = intId,
                DistrictId     = districtId,
                Status         = newStatus,
                VehicleCount   = vehicles,
                SpeedAvgKmh    = speed,
                OccupancyPct   = occupancy,
                TimestampUtc   = DateTime.UtcNow,
                Reason         = $"{congestedCount} congested events in last 30s window"
            };

            await producer.ProduceAsync(
                topic: "signal-state-changes",
                message: new Message<string, string>
                {
                    Key   = districtId,
                    Value = JsonSerializer.Serialize(stateChange, jsonOptions)
                },
                cancellationToken: cts.Token
            );

            published++;
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] STATE CHANGE: {intId} → {newStatus} " +
                              $"({congestedCount} congested events in window) " +
                              $"[total published={published}]");
        }

        consumer.Commit(result);

        if (received % 200 == 0)
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Processed={received} " +
                              $"Tracking {windows.Count} intersections " +
                              $"StateChanges={published}");
    }
}
catch (OperationCanceledException) { }
finally
{
    consumer.Close();
    producer.Flush(TimeSpan.FromSeconds(5));
    Console.WriteLine($"Done. Received={received} StateChanges={published}");
}
