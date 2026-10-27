using StageLink.Protocol;

namespace StageLink.Protocol.Tests;

public class ByteReaderTests
{
    [Fact]
    public void ReadByte_ReadsBytesInOrder()
    {
        // Arrange
        var reader = new ByteReader([0xAA, 0x01]);

        // Act + Assert
        Assert.Equal(0xAA, reader.ReadByte());
        Assert.Equal(0x01, reader.ReadByte());
    }
}