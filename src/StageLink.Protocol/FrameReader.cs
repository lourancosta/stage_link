namespace StageLink.Protocol;

// Reads complete frames, one at a time, from a byte stream such as a TCP connection.
// TCP keeps bytes in order but can split or merge messages, so the reader uses the
// start marker to find where a frame begins and the length field to know where it ends.
public class FrameReader
{
    private const byte MarkerHigh = (byte)(Frame.StartMarker >> 8); // 0x53 'S'
    private const byte MarkerLow = (byte)(Frame.StartMarker & 0xFF); // 0x4C 'L'
    private const int LengthOffset = 6; // marker (2) + version (1) + command (1) + message number (2)

    private readonly Stream _stream;
    private readonly byte[] _oneByte = new byte[1];

    public FrameReader(Stream stream)
    {
        _stream = stream;
    }

    // Returns null when the stream ends cleanly (the other side disconnected).
    public async Task<Frame?> ReadFrameAsync(CancellationToken cancellationToken = default)
    {
        if (!await FindStartMarkerAsync(cancellationToken))
        {
            return null;
        }

        byte[] header = new byte[Frame.HeaderSize];
        header[0] = MarkerHigh;
        header[1] = MarkerLow;
        await _stream.ReadExactlyAsync(header.AsMemory(2), cancellationToken);

        ushort length = new ByteReader(header[LengthOffset..]).ReadUInt16();

        byte[] frame = new byte[Frame.HeaderSize + length + Frame.CrcSize];
        header.CopyTo(frame, 0);
        await _stream.ReadExactlyAsync(frame.AsMemory(Frame.HeaderSize), cancellationToken);

        return Frame.Decode(frame);
    }

    // Reads byte by byte until it sees 0x53 followed by 0x4C.
    // Returns false if the stream ends first.
    private async Task<bool> FindStartMarkerAsync(CancellationToken cancellationToken)
    {
        int previous = -1;

        while (true)
        {
            int current = await ReadByteAsync(cancellationToken);
            if (current == -1)
            {
                return false;
            }

            if (previous == MarkerHigh && current == MarkerLow)
            {
                return true;
            }

            previous = current;
        }
    }

    // Returns the next byte, or -1 at the end of the stream.
    private async Task<int> ReadByteAsync(CancellationToken cancellationToken)
    {
        int bytesRead = await _stream.ReadAsync(_oneByte, cancellationToken);
        return bytesRead == 0 ? -1 : _oneByte[0];
    }
}
