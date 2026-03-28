using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using DashboardService;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<IntersectionStateStore>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<TrafficHub>("/traffic");

var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP")
                       ?? "localhost:9092";

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() }
};

// Run Kafka consumer in background
_ = Task.Run(async () =>
{
    var store  = app.Services.GetRequiredService<IntersectionStateStore>();
    var hub    = app.Services.GetRequiredService<IHubContext<TrafficHub>>();

    var config = new ConsumerConfig
    {
        BootstrapServers = bootstrapServers,
        GroupId          = "dashboard-service-group",
        AutoOffsetReset  = AutoOffsetReset.Earliest,
        EnableAutoCommit = true
    };

    using var consumer = new ConsumerBuilder<string, string>(config).Build();
    consumer.Subscribe("signal-state-changes");

    Console.WriteLine("Dashboard Kafka consumer started...");

    while (true)
    {
        try
        {
            var result = consumer.Consume(TimeSpan.FromMilliseconds(100));
            if (result == null) continue;

            using var doc  = JsonDocument.Parse(result.Message.Value);
            var root       = doc.RootElement;

            var state = new IntersectionState
            {
                IntersectionId = root.GetProperty("intersectionId").GetString()!,
                DistrictId     = root.GetProperty("districtId").GetString()!,
                Status         = root.GetProperty("status").GetString()!,
                VehicleCount   = root.GetProperty("vehicleCount").GetInt32(),
                SpeedAvgKmh    = root.GetProperty("speedAvgKmh").GetDouble(),
                OccupancyPct   = root.GetProperty("occupancyPct").GetInt32(),
                TimestampUtc   = root.GetProperty("timestampUtc").GetDateTime(),
                Reason         = root.GetProperty("reason").GetString()!
            };

            store.Update(state);

            // Broadcast to all connected browsers
            await hub.Clients.All.SendAsync("StateChange", state);

            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] " +
                              $"Broadcast: {state.IntersectionId} → {state.Status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Consumer error: {ex.Message}");
            await Task.Delay(1000);
        }
    }
});

app.Run();
