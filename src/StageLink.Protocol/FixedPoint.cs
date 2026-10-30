namespace StageLink.Protocol;

public static class FixedPoint
{
    public static short ToWire(double value)
    {
        double scaled = Math.Round(value * 100);

        // Casting a too-large double to short doesn't fail in C#: it silently gives a
        // wrong (even negative) number. Check first. Written this way so NaN fails too,
        // because every comparison with NaN is false.
        if (!(scaled >= short.MinValue && scaled <= short.MaxValue))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be between -327.68 and 327.67.");
        }

        return (short)scaled;
    }

    public static double FromWire(short wire)
    {
        return wire / 100.0;
    }
}
