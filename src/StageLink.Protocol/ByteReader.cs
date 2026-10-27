namespace StageLink.Protocol;

public class ByteReader
{
    private readonly byte[] _data;
    private int _position;

    public ByteReader(byte[] data)
    {
        _data = data;
    }

    public byte ReadByte()
    {
        byte value = _data[_position];
        _position++;
        return value;
    }

    public short ReadInt16()
    {
        byte high = ReadByte();
        byte low = ReadByte();
        return (short)((high << 8) | low);
    }

    public double ReadFixedPoint()
    {
        return FixedPoint.FromWire(ReadInt16());
    }
}