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
}