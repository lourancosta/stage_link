namespace StageLink.Protocol;

// A bit field: each fault is one bit, so several can be active at once
// (Overtemperature | PowerSupplyFault = 0x05).
[Flags]
public enum Faults : byte
{
    None = 0,
    Overtemperature = 1 << 0,
    AmplifierFault = 1 << 1,
    PowerSupplyFault = 1 << 2,
}
