namespace StageLink.Protocol;

public class Frame
{
    public const byte Header = 0xAA;

    public Command Command { get; }

    private readonly byte[] _payload;
    public byte[] Payload => [.. _payload];

    public Frame(Command command, byte[] payload)
    {
        if (payload.Length > byte.MaxValue)
        {
            throw new ArgumentException("Payload cannot be longer than 255 bytes", nameof(payload));
        }

        Command = command;
        _payload = [.. payload];
    }

    public byte[] Encode()
    {
        byte length = (byte)_payload.Length;

        var writer = new ByteWriter();
        writer.WriteByte(Header);
        writer.WriteByte((byte)Command);
        writer.WriteByte(length);

        foreach (byte value in _payload)
        {
            writer.WriteByte(value);
        }

        writer.WriteByte(ComputeChecksum(Command, _payload));

        return writer.ToArray();
    }

    public static Frame Decode(byte[] bytes)
    {
        try
        {
            var reader = new ByteReader(bytes);

            if (reader.ReadByte() != Header)
            {
                throw new InvalidFrameException("Frame does not start with the header byte");
            }

            var command = (Command)reader.ReadByte();
            if (!Enum.IsDefined(command))
            {
                throw new InvalidFrameException($"Unknown command: 0x{(byte)command:X2}.");
            }

            byte length = reader.ReadByte();
            byte[] payload = new byte[length];
            for (int i = 0; i < length; i++)
            {
                payload[i] = reader.ReadByte();
            }

            byte checksum = reader.ReadByte();
            if (checksum != ComputeChecksum(command, payload))
            {
                throw new InvalidFrameException("Checksum does not match");
            }

            return new Frame(command, payload);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidFrameException("Frame is imcomplete", ex);
        }
    }

    private static byte ComputeChecksum(Command command, byte[] payload)
    {
        return Checksum.Compute([(byte)command, (byte)payload.Length, .. payload]);
    }
}