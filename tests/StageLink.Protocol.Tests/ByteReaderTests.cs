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

    [Fact]
    public void ReadByte_ThrowsWhenNoDataLeft()
    {
        // Arrange
        var reader = new ByteReader([]);

        // Act + Assert
        Assert.Throws<EndOfStreamException>(() => reader.ReadByte());
    }

    [Fact]
    public void ReadInt16_ThrowsWhenOnlyOneByteLeft()
    {
        // Arrange
        var reader = new ByteReader([0x12]);

        // Act + Assert
        Assert.Throws<EndOfStreamException>(() => reader.ReadInt16());
    }

    [Fact]
    public void ReadUInt16_ReadsValuesAboveShortRange()
    {
        var reader = new ByteReader([0xFD, 0xE8]);

        Assert.Equal(65000, reader.ReadUInt16());
    }

    [Fact]
    public void ReadString_ReadsLengthThenAscii()
    {
        var reader = new ByteReader([0x02, 0x4C, 0x31]);

        Assert.Equal("L1", reader.ReadString());
    }

    [Fact]
    public void ReadString_ThrowsWhenTextIsTruncated()
    {
        // Length says 3 characters, but only 2 follow
        var reader = new ByteReader([0x03, 0x4C, 0x31]);

        Assert.Throws<EndOfStreamException>(() => reader.ReadString());
    }

    [Fact]
    public void ReadString_ThrowsOnNonAsciiBytes()
    {
        var reader = new ByteReader([0x01, 0xE9]);

        Assert.Throws<InvalidDataException>(() => reader.ReadString());
    }
}
