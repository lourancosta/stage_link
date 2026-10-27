namespace StageLink.Protocol;

public class InvalidFrameException : Exception
{
    public InvalidFrameException(string message) : base(message)
    {
    }
}