using System.Text;

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
        if (_position >= _data.Length)
        {
            throw new EndOfStreamException("No more bytes to read.");
        }

        byte value = _data[_position];
        _position++;
        return value;
    }

    public ushort ReadUInt16()
    {
        byte high = ReadByte();
        byte low = ReadByte();
        return (ushort)((high << 8) | low);
    }

    public short ReadInt16()
    {
        return (short)ReadUInt16();
    }

    public double ReadFixedPoint()
    {
        return FixedPoint.FromWire(ReadInt16());
    }

    public string ReadString()
    {
        byte length = ReadByte();

        byte[] characters = new byte[length];
        for (int i = 0; i < length; i++)
        {
            characters[i] = ReadByte();
        }

        if (!Ascii.IsValid(characters))
        {
            throw new InvalidDataException("Text contains non-ASCII bytes.");
        }

        return Encoding.ASCII.GetString(characters);
    }
}
