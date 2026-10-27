using StageLink.Protocol;

namespace StageLink.Protocol.Tests;

public class ChecksumTests
{
    [Fact]
    public void Compute_ReturnsZeroForEmptyData()
    {
        Assert.Equal(0, Checksum.Compute([]));
    }

    [Fact]
    public void Compute_ReturnsSameByteForSingleByte()
    {
        Assert.Equal(0x42, Checksum.Compute([0x42]));
    }

    [Fact]
    public void Compute_XorsAllBytes()
    {
        Assert.Equal(0xE5, Checksum.Compute([0x01, 0x03, 0x01, 0x04, 0xE2]));
    }
}