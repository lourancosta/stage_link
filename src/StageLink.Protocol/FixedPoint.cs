namespace StageLink.Protocol;

public static class FixedPoint
{
    public static short ToWire(double value)
    {
        return (short)Math.Round(value * 100);
    }

    public static double FromWire(short wire)
    {
        return wire / 100.0;
    }
}