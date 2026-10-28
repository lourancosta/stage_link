namespace StageLink.Protocol;

public enum NackCode : byte
{
    UnsupportedCommand = 0x01,
    InvalidPayload = 0x02,
    ValueOutOfRange = 0x03,
}