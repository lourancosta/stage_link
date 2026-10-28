namespace StageLink.Protocol;

public static class Crc16
{
    private const ushort Polynomial = 0x1021;
    private const ushort InitialValue = 0xFFFF;

    public static ushort Compute(byte[] data)
    {
        ushort crc = InitialValue;

        foreach (byte value in data)
        {
            crc ^= (ushort)(value << 8);

            for (int bit = 0; bit < 8; bit++)
            {
                if ((crc & 0x8000) != 0)
                {
                    crc = (ushort)((crc << 1) ^ Polynomial);
                }
                else
                {
                    crc <<= 1;
                }
            }
        }

        return crc;
    }
}