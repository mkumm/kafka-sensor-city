using System.Text.Json;
using Confluent.Kafka;

var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP")
                       ?? "localhost:9092";

Console.WriteLine($"Connecting to Kafka at {bootstrapServers}");

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

var consumerConfig = new ConsumerConfig
{
    BootstrapServers  = bootstrapServers,
    GroupId           = "filter-service-group",
    AutoOffsetReset   = AutoOffsetReset.Latest,
    EnableAutoCommit  = false
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

long received  = 0;
long forwarded = 0;
long dropped   = 0;

using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

consumer.Subscribe("raw-sensor-events");
Console.WriteLine("Filter service started. Listening on raw-sensor-events...\n");

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        var result = consumer.Consume(cts.Token);
        received++;

        var json = result.Message.Value;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var sensorType = root.GetProperty("sensorType").GetString();

        if (sensorType != "Intersection")
        {
            dropped++;
            consumer.Commit(result);
            continue;
        }

        // Forward intersection events to next topic
        await producer.ProduceAsync(
            topic: "intersection-events",
            message: new Message<string, string>
            {
                Key   = result.Message.Key,
                Value = json
            },
            cancellationToken: cts.Token
        );

        forwarded++;
        consumer.Commit(result);

        if (received % 100 == 0)
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] " +
                              $"Received={received} " +
                              $"Forwarded={forwarded} " +
                              $"Dropped={dropped} " +
                              $"({(double)forwarded/received*100:F1}% pass rate)");
    }
}
catch (OperationCanceledException) { }
finally
{
    consumer.Close();
    producer.Flush(TimeSpan.FromSeconds(5));
    Console.WriteLine($"Done. Received={received} Forwarded={forwarded} Dropped={dropped}");
}
