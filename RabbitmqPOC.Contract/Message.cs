namespace RabbitmqPOC.Contract;

public class RequestMessage
{
    public required string TerminalId { get; init; }
    public int SystemTrace { get; init; }
}
public class ResponseMessage
{
    public required string TerminalId { get; init; }
    public int SystemTrace { get; init; }
    public required string ResponseCode { get; init; }
}
