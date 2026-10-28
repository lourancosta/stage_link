namespace StageLink.Protocol;

public class Frame
{
    public const ushort StartMarker = 0x534C; // "SL"
    public const byte Version = 1;

    // Start marker (2) + version (1) + command (1) + message number (2) + length (2)
    public const int HeaderSize = 8;
    public const int CrcSize = 2;

    private readonly byte[] _payload;

    public Command Command { get; }
    public ushort MessageNumber { get; }
    public byte[] Payload => [.. _payload];

    public Frame(Command command, ushort messageNumber, byte[] payload)
    {
        if (payload.Length > ushort.MaxValue)
        {
            throw new ArgumentException("Payload cannot be longer than 65535 bytes.", nameof(payload));
        }

        Command = command;
        MessageNumber = messageNumber;
        _payload = [.. payload];
    }

    public byte[] Encode()
    {
        var writer = new ByteWriter();
        writer.WriteUInt16(StartMarker);
        writer.WriteByte(Version);
        writer.WriteByte((byte)Command);
        writer.WriteUInt16(MessageNumber);
        writer.WriteUInt16((ushort)_payload.Length);

        foreach (byte value in _payload)
        {
            writer.WriteByte(value);
        }

        writer.WriteUInt16(Crc16.Compute(writer.ToArray()));

        return writer.ToArray();
    }

    public static Frame Decode(byte[] bytes)
    {
        try
        {
            var reader = new ByteReader(bytes);

            if (reader.ReadUInt16() != StartMarker)
            {
                throw new InvalidFrameException("Frame does not start with the start marker.");
            }

            byte version = reader.ReadByte();
            if (version != Version)
            {
                throw new InvalidFrameException($"Unsupported protocol version: {version}.");
            }

            var command = (Command)reader.ReadByte();
            if (!Enum.IsDefined(command))
            {
                throw new InvalidFrameException($"Unknown command: 0x{(byte)command:X2}.");
            }

            ushort messageNumber = reader.ReadUInt16();
            ushort length = reader.ReadUInt16();

            byte[] payload = new byte[length];
            for (int i = 0; i < length; i++)
            {
                payload[i] = reader.ReadByte();
            }

            ushort crc = reader.ReadUInt16();
            if (crc != Crc16.Compute(bytes[..(HeaderSize + length)]))
            {
                throw new InvalidFrameException("CRC does not match.");
            }

            return new Frame(command, messageNumber, payload);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidFrameException("Frame is incomplete.", ex);
        }
    }
}