using System.Text;

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

    // Text is sent as 1 length byte followed by the ASCII characters.
    public void WriteString(string value)
    {
        if (value.Length > byte.MaxValue)
        {
            throw new ArgumentException("Text cannot be longer than 255 characters.", nameof(value));
        }

        if (!Ascii.IsValid(value))
        {
            throw new ArgumentException("Text must contain only ASCII characters.", nameof(value));
        }

        WriteByte((byte)value.Length);

        foreach (byte character in Encoding.ASCII.GetBytes(value))
        {
            WriteByte(character);
        }
    }
}
