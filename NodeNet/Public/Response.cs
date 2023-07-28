public class Response
{
    public Response(ResponseStatus status, byte[] data)
    {
        Status = status;
        Data = data;
    }

    public ResponseStatus Status { get; private set; }
    public byte[] Data { get; private set; }

}


public enum ResponseStatus
{
    Success,
    Timeout,
    ResolveFailure,
    UnknownError
}
