using StageLink.Protocol;

namespace StageLink.Protocol.Tests;

public class FaultsTests
{
    [Theory]
    [InlineData(Faults.None, 0x00)]
    [InlineData(Faults.Overtemperature, 0x01)]
    [InlineData(Faults.AmplifierFault, 0x02)]
    [InlineData(Faults.PowerSupplyFault, 0x04)]
    public void Faults_HasExpectedBit(Faults fault, int expected)
    {
        Assert.Equal(expected, (byte)fault);
    }

    [Fact]
    public void Faults_CanBeCombined()
    {
        Faults faults = Faults.Overtemperature | Faults.PowerSupplyFault;

        Assert.Equal(0x05, (byte)faults);
        Assert.True(faults.HasFlag(Faults.PowerSupplyFault));
        Assert.False(faults.HasFlag(Faults.AmplifierFault));
    }
}
