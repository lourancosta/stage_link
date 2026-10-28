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

    public void WriteUInt16(ushort value)
    {
        WriteByte((byte)(value >> 8)); // High byte
        WriteByte((byte)(value & 0xFF)); // Low byte
    }

    public void WriteInt16(short value)
    {
        WriteUInt16((ushort)value);
    }

    public void WriteFixedPoint(double value)
    {
        WriteInt16(FixedPoint.ToWire(value));
    }
}