using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitmqPOC.Contract;
using System.Text;
using System.Text.Json;

Console.Title = $"Switch";
Console.WriteLine($"Switch started.");


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

    var receiveTask = ReceiveAsync(channel, cancellationTokenSource.Token);
    await Task.WhenAll(receiveTask, Task.Run(() =>
    {
        Console.ReadLine();
        cancellationTokenSource.Cancel();
    }));
}
catch (Exception ex)
{
    Console.WriteLine($"Error in Siwtch: {ex.Message}");
}


Console.WriteLine($"Switch stopped.");


async Task ReceiveAsync(IChannel channel, CancellationToken cancellationToken)
{
    var queue = await channel.QueueDeclareAsync($"Siwtch_Pos_Incoming", durable: true, exclusive: true, autoDelete: true, cancellationToken: cancellationToken);

    await channel.QueueBindAsync(queue.QueueName, "Pos_Incoming", "", cancellationToken: cancellationToken);

    await channel
       .ExchangeDeclareAsync("Pos_Outgoing", RabbitMQ.Client.ExchangeType.Direct, durable: true, autoDelete: false, arguments: null, cancellationToken: cancellationToken);


    var consumer = new AsyncEventingBasicConsumer(channel);
    consumer.ReceivedAsync += async (ch, ea) =>
    {
        var body = ea.Body.ToArray();
        Console.WriteLine($"Received message: {System.Text.Encoding.UTF8.GetString(body)}");
        await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);

        var props = new BasicProperties
        {
            CorrelationId = ea.BasicProperties.CorrelationId,
        };

        var request = JsonSerializer.Deserialize<RequestMessage>(body);
        


        var response = new ResponseMessage()
        {
            TerminalId = request!.TerminalId,
            SystemTrace = request.SystemTrace,
            ResponseCode = "00" // Assuming success response code
        };
        var responseBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
        await channel
                .BasicPublishAsync(exchange: "Pos_Outgoing", routingKey: ea.BasicProperties.CorrelationId!, mandatory: true, basicProperties: props, body: responseBody, cancellationToken: cancellationToken);


    };
    // this consumer tag identifies the subscription
    // when it has to be cancelled
    string consumerTag = await channel.BasicConsumeAsync(queue.QueueName, false, consumer, cancellationToken: cancellationToken);

    await Task.WhenAny(Task.Delay(Timeout.Infinite, cancellationToken));
}