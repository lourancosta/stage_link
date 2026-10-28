# StageLink Roadmap

## Phase 1: Protocol library (C#)
- [x] Solution scaffold (protocol library + xUnit tests)
- [x] `FixedPoint`: convert values to/from 16-bit wire format (x100)
- [x] `ByteWriter`: write bytes, 16-bit big-endian values and fixed-point values into a message buffer
- [x] `ByteReader`: read bytes, 16-bit big-endian values and fixed-point values, with a clear error on truncated data
- [x] Message format and commands (see "Protocol format" below)
  - [x] `Command` enum
  - [x] `Checksum` (XOR)
  - [x] `Frame` encode / decode with header and checksum validation

## Phase 2: Speaker simulator (C#)
- [ ] Simulated speaker that receives and answers commands

## Phase 3: Desktop app (Electron + React + TypeScript)
- [ ] Connect to the simulator
- [ ] Control gain and other parameters from the UI

## Protocol format (v1)

Every message is a frame:

| Header | Command | Length | Payload      | Checksum |
|--------|---------|--------|--------------|----------|
| 1 byte | 1 byte  | 1 byte | Length bytes | 1 byte   |

- **Header:** always `0xAA` (marks the start of a frame)
- **Length:** number of payload bytes
- **Checksum:** XOR of Command, Length and every Payload byte
- Multi-byte values are big-endian. Gain is fixed-point (value x 100, 16-bit signed).

| Code   | Command | Payload                                  |
|--------|---------|------------------------------------------|
| `0x01` | SetGain | channel (1 byte) + gain (2 bytes)        |
| `0x02` | GetGain | channel (1 byte)                         |
| `0x03` | SetMute | channel (1 byte) + muted (1 byte: 0 / 1) |

Example: set channel 1 to +12.5 dB → `AA 01 03 01 04 E2 E5`
