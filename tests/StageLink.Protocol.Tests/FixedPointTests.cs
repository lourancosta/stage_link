using StageLink.Protocol;

namespace StageLink.Protocol.Tests;

public class FixedPointTests
{
    [Fact]
    public void ToWire_MultipliesBy100()
    {
        short wire = FixedPoint.ToWire(3.5);
        Assert.Equal(350, wire);
    }

    [Fact]
    public void FromWire_DividesBy100()
    {
        double gain = FixedPoint.FromWire(1200);
        Assert.Equal(12, gain);
    }

    [Fact]
    public void FromWire_KeepsDecimals()
    {
        double gain = FixedPoint.FromWire(1250);
        Assert.Equal(12.5, gain);
    }

    [Fact]
    public void ToWire_RoundsToNearest()
    {
        short wire = FixedPoint.ToWire(0.29);
        Assert.Equal(29, wire);
    }
}