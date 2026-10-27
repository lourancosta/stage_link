namespace StageLink.Protocol;

public class ByteWriter
{
    private readonly List<byte> _buffer = new();

    public byte[] ToArray()
    {
        return _buffer.ToArray();
    }

    public void WriteByte(byte value)
    {
        _buffer.Add(value);
    }
}