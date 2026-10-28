
namespace StageLink.Protocol.Tests;

public class Crc16Tests
{
    [Fact]
    public void Compute_MatchesStandardCheckValue()
    {
        byte[] data = "123456789"u8.ToArray();

        Assert.Equal(0x29B1, Crc16.Compute(data));
    }

    [Fact]
    public void Compute_ReturnsInitialValueForEmptyData()
    {
        Assert.Equal(0xFFFF, Crc16.Compute([]));
    }

    [Fact]
    public void Compute_MatchesStudyNotesExample()
    {
        byte[] frameWithoutCrc = [0x53, 0x4C, 0x01, 0x20, 0x00, 0x01, 0x00, 0x02, 0xFD, 0x76];

        Assert.Equal(0xCA6B, Crc16.Compute(frameWithoutCrc));
    }
}