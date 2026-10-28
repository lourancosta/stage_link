namespace StageLink.Protocol;

public class InvalidFrameException : Exception
{
    public InvalidFrameException(string message) : base(message)
    {
    }

    public InvalidFrameException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}