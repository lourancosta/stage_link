# StageLink Roadmap

## Phase 1: Protocol library (C#)
- [x] Solution scaffold (protocol library + xUnit tests)
- [x] `FixedPoint`: convert values to/from 16-bit wire format (x100)
- [x] `ByteWriter`: write bytes, 16-bit big-endian values and fixed-point values into a message buffer
- [x] `ByteReader`: read bytes, 16-bit big-endian values and fixed-point values, with a clear error on truncated data
- [x] Message format v1 (simple prototype: `AA` header, XOR checksum)
- [x] Message format v2, the full design (see "Protocol format" below)
  - [x] `WriteUInt16` / `ReadUInt16` for unsigned 16-bit fields
  - [x] `Crc16` (CRC-16/CCITT-FALSE)
  - [x] `Command` enum with the full message list
  - [x] `Frame` v2: start marker, version, message number, 16-bit length, CRC
  - [x] Remove the v1 `Checksum`

## Phase 2: Speaker simulator (C#)
- [x] Project setup: `StageLink.Simulator` library + `StageLink.Simulator.Tests`
- [x] `SpeakerState`: name, gain, mute, tilt, with range validation
- [x] `NackCode` enum + `Speaker.Handle(Frame)`: Ping → Pong, unsupported → Nack
- [x] SetGain / SetMute / SetTilt → Ack, or Nack on bad payload or out-of-range value
- [ ] GetState → State (adds `WriteString` / `ReadString` to the protocol)
- [ ] TCP server: accept the app's connection and read frames from the byte stream
- [ ] Subscribe / Unsubscribe / Telemetry (timed updates)
- [ ] Announce over UDP (so the app can discover speakers)

## Phase 3: Desktop app (Electron + React + TypeScript)
- [ ] Connect to the simulator
- [ ] Control gain and other parameters from the UI

## Protocol format

Every message is a frame. Multi-byte values are big-endian.

| Field          | Bytes | Value / meaning |
|----------------|-------|-----------------|
| Start marker   | 2     | `53 4C` ("SL"). Lets the receiver find the start of a frame. |
| Version        | 1     | Protocol version, currently `01`. |
| Command        | 1     | Which message this is (table below). |
| Message number | 2     | Set by the app; the speaker copies it into its reply. |
| Length         | 2     | Number of data bytes that follow. |
| Data           | N     | The command's values. |
| CRC            | 2     | CRC-16/CCITT-FALSE (poly `0x1021`, init `0xFFFF`) over every byte before it, start marker included. |

Gain and tilt are fixed-point (value x 100, 16-bit signed).

| Code          | Message                 | Direction           | Data |
|---------------|-------------------------|---------------------|------|
| `0x01`        | Announce                | speaker → network (UDP) | id, name, model, firmware, TCP port |
| `0x02`/`0x03` | Ping / Pong             | both                | none |
| `0x10`/`0x11` | GetState / State        | app → speaker / reply | name + gain, mute, tilt |
| `0x20`        | SetGain                 | app → speaker       | gain (2 bytes, signed) |
| `0x21`        | SetMute                 | app → speaker       | 1 byte: 1 = on, 0 = off |
| `0x22`        | SetTilt                 | app → speaker       | angle (2 bytes, signed) |
| `0x30`/`0x31` | Subscribe / Unsubscribe | app → speaker       | interval in ms |
| `0x32`        | Telemetry               | speaker → app       | temperature, level, limiter, gain, mute, tilt, faults |
| `0x7E`/`0x7F` | Ack / Nack              | speaker → app       | Nack carries an error code |

Example: SetGain −6.5 dB, message number 1 →
`53 4C 01 20 00 01 00 02 FD 76 CA 6B`

## Simulator rules

| Setting | Range | Default |
|---------|-------|---------|
| Gain    | −60.00 to +12.00 dB | 0.00 |
| Tilt    | −15.00 to +15.00 degrees | 0.00 |
| Mute    | on / off | off |

Every reply copies the request's message number.

| Reply | Data |
|-------|------|
| Pong  | none |
| Ack   | none |
| Nack  | 1 byte error code |
| State | name (1 byte length + ASCII characters), gain (2), mute (1), tilt (2) |

| Nack code | Meaning |
|-----------|---------|
| `0x01` | Unsupported command (the speaker doesn't accept this message) |
| `0x02` | Invalid payload (wrong number of data bytes, or mute not 0/1) |
| `0x03` | Value out of range |
