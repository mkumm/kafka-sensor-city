using System.Text.Json;
using Confluent.Kafka;
using SensorSimulator;

var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP")
                       ?? "localhost:9092";

Console.WriteLine($"Connecting to Kafka at {bootstrapServers}");

var config = new ProducerConfig
{
    BootstrapServers        = bootstrapServers,
    Acks                    = Acks.All,
    MessageSendMaxRetries   = 3,
    LingerMs                = 5
};

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
};

var cts  = new CancellationTokenSource();
var city = new CitySimulator(districts: 5, sensorsPerDistrict: 40);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("Shutting down...");
};

Console.WriteLine("Simulator started. Press Ctrl+C to stop.");
Console.WriteLine("Producing events to raw-sensor-events...\n");

long produced = 0;

using var producer = new ProducerBuilder<string, string>(config).Build();

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        var evt  = city.NextEvent();
        var json = JsonSerializer.Serialize(evt, jsonOptions);

        await producer.ProduceAsync(
            topic: "raw-sensor-events",
            message: new Message<string, string>
            {
                Key   = evt.DistrictId,
                Value = json
            },
            cancellationToken: cts.Token
        );

        produced++;

        if (produced % 10 == 0)
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Produced {produced} events. " +
                              $"Last: {evt.SensorType} in {evt.DistrictId} " +
                              $"(congested={evt.IsCongested})");

        await Task.Delay(50, cts.Token);
    }
}
catch (OperationCanceledException) { }
finally
{
    producer.Flush(TimeSpan.FromSeconds(5));
    Console.WriteLine($"Done. Total events produced: {produced}");
}
