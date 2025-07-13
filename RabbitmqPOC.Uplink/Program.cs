using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitmqPOC.Contract;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

string currentPath = Environment.ProcessPath!;
string currentProcessName = Process.GetCurrentProcess().ProcessName;

var matchingProcesses = Process.GetProcessesByName(currentProcessName)
    .Where(p =>
    {
        try
        {
            return p.MainModule?.FileName == currentPath;
        }
        catch
        {
            return false; // access denied to some system processes
        }
    });

int count = matchingProcesses.Count();

var instanceId = $"Uplick-{count}";
Console.Title = $"Client - {instanceId}";
Console.WriteLine($"Uplink {instanceId} started.");


RabbitMQ.Client.ConnectionFactory factory = new RabbitMQ.Client.ConnectionFactory()
{
    HostName = "localhost",
    UserName = "guest",
    Password = "guest",
};

CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();


try
{
    await using var connection = await factory.CreateConnectionAsync();
    using var channel = await connection.CreateChannelAsync();

    var publishTask = PublishAsync(channel, cancellationTokenSource.Token);
    var receiveTask = ReceiveAsync(channel, cancellationTokenSource.Token);


    await Task.WhenAll(publishTask, receiveTask);
}
catch (Exception ex)
{
    Console.WriteLine($"Error in Uplink {instanceId}: {ex.Message}");
}


Console.WriteLine($"Uplink {instanceId} stopped.");


async Task PublishAsync(IChannel channel, CancellationToken cancellationToken)
{
    await channel
        .ExchangeDeclareAsync("Pos_Incoming", ExchangeType.Fanout, durable: true, autoDelete: false, arguments: null, cancellationToken: cancellationToken);

    var props = new BasicProperties
    {
        CorrelationId = $"{instanceId}"
    };
    int systemTrace = 1;
    while (true)
    {
        try
        {
            var message = new RequestMessage()
            {
                SystemTrace = systemTrace++,
                TerminalId = $"Terminal_{instanceId}"
            };
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            await channel
                .BasicPublishAsync(exchange: "Pos_Incoming", routingKey: "", mandatory: true, basicProperties: props, body: body, cancellationToken: cancellationToken);

            Console.WriteLine($"Sent: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Uplink {instanceId}: {ex.Message}");
        }
        finally
        {
            await Task.Delay(1000, cancellationToken: cancellationToken); // Simulate work
        }
    }
}

async Task ReceiveAsync(IChannel channel, CancellationToken cancellationToken)
{
    var queue = await channel
        .QueueDeclareAsync($"Pos_Outgoing_{instanceId}", durable: true, exclusive: true, autoDelete: true, cancellationToken: cancellationToken);

    await channel
        .QueueBindAsync(queue.QueueName, "Pos_Outgoing", routingKey: instanceId, cancellationToken: cancellationToken);

    var consumer = new AsyncEventingBasicConsumer(channel);
    consumer.ReceivedAsync += async (ch, ea) =>
    {
        var body = ea.Body.ToArray();
        Console.WriteLine($"Received message: {Encoding.UTF8.GetString(body)}");
        await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
    };
    // this consumer tag identifies the subscription
    // when it has to be cancelled
    string consumerTag = await channel.BasicConsumeAsync(queue.QueueName, false, consumer, cancellationToken: cancellationToken);
    await Task.WhenAny(Task.Delay(Timeout.Infinite, cancellationToken));
}