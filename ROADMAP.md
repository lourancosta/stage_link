# StageLink Roadmap

## Phase 1: Protocol library (C#)
- [x] Solution scaffold (protocol library + xUnit tests)
- [x] `FixedPoint`: convert values to/from 16-bit wire format (x100)
- [x] `ByteWriter`: write bytes, 16-bit big-endian values and fixed-point values into a message buffer
- [ ] `ByteReader`: read values back from a buffer
- [ ] Message format and commands

## Phase 2: Speaker simulator (C#)
- [ ] Simulated speaker that receives and answers commands

## Phase 3: Desktop app (Electron + React + TypeScript)
- [ ] Connect to the simulator
- [ ] Control gain and other parameters from the UI
