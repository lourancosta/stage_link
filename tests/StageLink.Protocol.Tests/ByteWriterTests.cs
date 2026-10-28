using StageLink.Protocol;

namespace StageLink.Protocol.Tests;

public class ByteWriterTests
{
    [Fact]
    public void ToArray_IsEmptyForNewWriter()
    {
        // Arrange
        var writer = new ByteWriter();

        // Act
        byte[] bytes = writer.ToArray();
        // Assert
        Assert.Empty(bytes);
    }

    [Fact]
    public void WriteByte_AppendsBytesInOrder()
    {
        // Arrange
        var writer = new ByteWriter();

        // Act
        writer.WriteByte(0xAA);
        writer.WriteByte(0x01);

        // Assert
        Assert.Equal([0xAA, 0X01], writer.ToArray());
    }

    [Fact]
    public void WriteInt16_WritesBigEndian()
    {
        // Arrange
        var writer = new ByteWriter();

        // Act
        writer.WriteInt16(0x1234);

        // Assert
        Assert.Equal([0x12, 0x34], writer.ToArray());
    }

    [Fact]
    public void WriteInt16_handleNegativeValues()
    {
        // Arrange
        var writer = new ByteWriter();

        // Act
        writer.WriteInt16(-350); // -350 in hex is 0xFEAA, so the bytes should be [0xFE, 0xAA]

        // Assert
        Assert.Equal([0xFE, 0xA2], writer.ToArray());
    }

    [Fact]
    public void WriteFixedPoint_ConvertsAndWritesBigEndian()
    {
        // Arrange
        var writer = new ByteWriter();

        // Act
        writer.WriteFixedPoint(12.5); // 12.5 in fixed-point representation (assuming 8.8 format) is 0x0C80, so the bytes should be [0x0C, 0x80]

        // Assert
        Assert.Equal([0x04, 0xE2], writer.ToArray());
    }

    [Fact]
    public void WriteUInt16_WritesValuesAboveShortRange()
    {
        var writer = new ByteWriter();

        writer.WriteUInt16(65000);

        Assert.Equal([0xFD, 0xE8], writer.ToArray());
    }
}