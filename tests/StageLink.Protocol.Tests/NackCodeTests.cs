using StageLink.Protocol;

namespace StageLink.Protocol.Tests;

public class NackCodeTests
{
    [Theory]
    [InlineData(NackCode.UnsupportedCommand, 0x01)]
    [InlineData(NackCode.InvalidPayload, 0x02)]
    [InlineData(NackCode.ValueOutOfRange, 0x03)]
    public void NackCode_HasExpectedWireCode(NackCode code, int expected)
    {
        Assert.Equal(expected, (byte)code);
    }
}