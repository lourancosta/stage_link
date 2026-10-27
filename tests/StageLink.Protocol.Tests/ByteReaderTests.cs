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

    [Fact]
    public void ReadInt16_ReadsBigEndian()
    {
        // Arrange
        var reader = new ByteReader([0x12, 0x34]);

        // Act
        short value = reader.ReadInt16();

        // Assert
        Assert.Equal(0x1234, value);
    }

    [Fact]
    public void ReadInt16_HandlesNegativeValues()
    {
        // Arrange
        var reader = new ByteReader([0xFE, 0xA2]);

        // Act
        short value = reader.ReadInt16();

        // Assert
        Assert.Equal(-350, value);
    }

    [Fact]
    public void ReadFixedPoint_ReadsAndConverts()
    {
        // Arrange
        var reader = new ByteReader([0x04, 0xE2]);

        // Act
        double gain = reader.ReadFixedPoint();

        // Assert
        Assert.Equal(12.5, gain);
    }
}