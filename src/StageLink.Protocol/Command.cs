namespace StageLink.Protocol;

public enum Command : byte
{
    // Discovery and connection
    Announce = 0x01,
    Ping = 0x02,
    Pong = 0x03,

    // State
    GetState = 0x10,
    State = 0x11,

    // Settings
    SetGain = 0x20,
    SetMute = 0x21,
    SetTilt = 0x22,

    // Live data
    Subscribe = 0x30,
    Unsubscribe = 0x31,
    Telemetry = 0x32,

    // Replies
    Ack = 0x7E,
    Nack = 0x7F,
}