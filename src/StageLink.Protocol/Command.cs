namespace StageLink.Protocol;

public enum Command : byte
{
    SetGain = 0x01,
    GetGain = 0x02,
    SetMute = 0x03,
}