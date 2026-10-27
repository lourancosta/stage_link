namespace StageLink.Protocol;

public static class Checksum
{
    public static byte Compute(byte[] data)
    {
        byte result = 0;

        foreach (byte value in data)
        {
            result ^= value; // result = (byte)(result ^ value).
        }

        return result;
    }
}