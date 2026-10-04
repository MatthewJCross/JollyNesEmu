# JollyNesEmu

A Nintendo Entertainment System (NES) emulator written entirely in C# and built with modern .NET.

JollyNesEmu is a from-scratch NES emulator project focused on learning, experimentation, accuracy, and building a complete understanding of the NES hardware architecture.

> **Project status:** Active development — the emulator is playable with NES software, but several areas are still being improved toward greater hardware accuracy and compatibility.

---

## Features

JollyNesEmu currently contains implementations for the major components required to emulate an NES:

* MOS 6502-compatible CPU
* NES memory bus
* PPU (Picture Processing Unit)
* APU (Audio Processing Unit)
* Cartridge loading
* Mapper support
* Controller input
* Background rendering
* Sprite rendering
* NES palette handling
* VBlank and NMI handling
* IRQ support
* CPU/PPU timing
* Audio output using NAudio
* Frame-based emulation
* Debugging and stepping support

The emulator is being developed incrementally, with accuracy and compatibility improving as individual hardware components are implemented and tested.

---

## Screenshots

Add screenshots here as the emulator develops.

```text
docs/
└── images/
    ├── emulator.png
    └── super-mario-bros.png
```

Example:

```markdown
![JollyNesEmu running Super Mario Bros.](docs/images/super-mario-bros.png)
```

---

## Technology

JollyNesEmu is written in:

* **C#**
* **.NET 10**
* **WPF**
* **NAudio**

The emulator runs as a native .NET application on Windows.

---

## Architecture

The emulator is divided into hardware-oriented components.

```text
                    ┌─────────────────────┐
                    │    JollyNesEmu      │
                    │    Emulator UI      │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │     NesMachine      │
                    │   Main Emulator     │
                    │      Control        │
                    └──────────┬──────────┘
                               │
              ┌────────────────┼────────────────┐
              │                │                │
              ▼                ▼                ▼
       ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
       │   CPU 6502  │  │     PPU     │  │     APU     │
       │             │  │             │  │             │
       │ Instructions│  │ Graphics    │  │ Audio       │
       │ Registers   │  │ VRAM        │  │ Pulse       │
       │ Interrupts  │  │ Sprites     │  │ Triangle    │
       └──────┬──────┘  │ Background  │  │ Noise       │
              │         └──────┬──────┘  └──────┬──────┘
              │                │                │
              └────────────────┼────────────────┘
                               ▼
                    ┌─────────────────────┐
                    │       NesBus        │
                    │                     │
                    │ CPU/PPU/APU/Mapper  │
                    │ Memory Mapping      │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │    NesCartridge     │
                    │                     │
                    │ PRG ROM             │
                    │ CHR ROM / RAM       │
                    │ Mapper              │
                    └─────────────────────┘
```

---

## CPU

The CPU emulates the NES's Ricoh 2A03/2A07 processor, based on the MOS 6502 architecture.

The CPU implementation includes:

* Registers A, X and Y
* Program Counter
* Stack Pointer
* Processor status flags
* Memory addressing modes
* Instruction execution
* Branching
* Stack operations
* Interrupt handling
* NMI handling
* IRQ handling
* BRK/RTI processing

CPU execution is coordinated with PPU and APU timing by the emulator machine.

---

## PPU

The Picture Processing Unit is responsible for generating the NES video output.

The PPU implementation includes:

* Pattern tables
* Name tables
* Attribute tables
* Palette RAM
* Background rendering
* Sprite rendering
* Sprite attributes
* Sprite flipping
* PPU registers
* VRAM addressing
* VBlank
* NMI generation
* PPU scrolling
* NES colour palette

The renderer operates using the NES's tile and palette architecture rather than treating the cartridge graphics as conventional bitmap images.

---

## APU

The Audio Processing Unit provides NES audio generation.

Current audio emulation includes:

* Pulse channel 1
* Pulse channel 2
* Triangle channel
* Noise channel
* Length counters
* Envelope processing
* Sweep processing
* Frame sequencer
* NES audio mixing
* PCM output
* NAudio audio playback

Audio output is currently being refined for improved timing, stability and accuracy.

---

## Cartridges and Mappers

JollyNesEmu loads NES ROM images and exposes their PRG and CHR data to the emulator.

The cartridge system is designed around mapper support so that additional cartridge hardware can be implemented without rewriting the CPU or PPU.

Mapper support is an ongoing area of development.

---

## Controllers

NES controller input is emulated through the emulator's input system.

The controller implementation supports the standard NES button layout:

```text
        ┌───────────────┐
        │      D-Pad    │
        │   ↑           │
        │ ←   ┼   →     │
        │   ↓           │
        │               │
        │  SELECT START │
        │               │
        │    B     A    │
        └───────────────┘
```

---

## Timing

NES hardware is highly timing dependent.

JollyNesEmu therefore does not treat the CPU, PPU and APU as completely independent systems.

CPU execution advances the other hardware components as part of the emulation process.

The current timing model coordinates:

```text
CPU cycle
   │
   ├── PPU cycles
   │
   ├── APU clocks
   │
   └── Mapper clocks
```

Timing accuracy is an ongoing development target.

---

## Running the Emulator

### Requirements

* Windows 10 or later
* .NET 10 SDK
* Visual Studio 2022/2026 or another .NET-compatible IDE
* A legally obtained NES ROM

### Build

Clone the repository:

```bash
git clone https://github.com/<your-username>/JollyNesEmu.git
cd JollyNesEmu
```

Build the project:

```bash
dotnet build
```

Run the application:

```bash
dotnet run
```

Alternatively, open the solution in Visual Studio and run the application normally.

---

## Loading a ROM

Use the emulator's ROM loading functionality to select a `.nes` file.

For example:

```text
Super Mario Bros.nes
```

JollyNesEmu reads the cartridge header and loads the appropriate PRG and CHR data before resetting the emulated hardware.

---

## Supported ROMs

Compatibility is currently under development.

Games using simpler cartridge configurations are generally the first target while mapper and timing support is expanded.

Testing currently includes:

* Super Mario Bros.
* Additional NES ROMs during development

Compatibility should be considered experimental until the relevant mapper, CPU, PPU and timing behaviour has been verified.

---

## Debugging

JollyNesEmu is being developed with debugging in mind.

Useful areas for debugging include:

* CPU instruction stepping
* CPU registers
* Memory accesses
* PPU state
* PPU registers
* VRAM
* Palette RAM
* Sprite data
* APU state
* Mapper state
* Frame execution
* VBlank/NMI behaviour

The emulator is intentionally being built as separate hardware components so individual systems can be tested and corrected independently.

---

## Project Structure

The project is organised around the major NES hardware components.

```text
JollyNesEmu/
│
├── Apu/
│   └── NesApu.cs
│
├── CPU/
│   └── Cpu6502.cs
│
├── Emulator/
│   └── NesMachine.cs
│
├── Nes/
│   ├── NesBus.cs
│   ├── NesCartridge.cs
│   ├── NesController.cs
│   └── NesAudio.cs
│
├── Ppu/
│   └── NesPpu.cs
│
├── Mappers/
│
├── Graphics/
│
└── README.md
```

The exact structure may change as the emulator develops.

---

## Development Philosophy

JollyNesEmu is not intended to be a wrapper around an existing emulator core.

The goal is to implement the NES hardware from the ground up and understand how the individual components interact.

Development therefore follows the hardware architecture:

```text
6502
 ↓
Bus
 ↓
Cartridge / Mapper

PPU
 ↓
VRAM / CHR / Palette
 ↓
Video Output

APU
 ↓
Audio Channels
 ↓
Audio Mixer
 ↓
NAudio
```

When something does not behave correctly, the goal is to identify the underlying hardware behaviour rather than work around the problem at the application level.

---

## Current Development Status

### Completed / Working

* [x] NES cartridge loading
* [x] CPU core
* [x] CPU addressing modes
* [x] CPU interrupts
* [x] NES bus
* [x] PPU registers
* [x] PPU memory access
* [x] Pattern table decoding
* [x] Name table rendering
* [x] Attribute table handling
* [x] Palette handling
* [x] Background rendering
* [x] Sprite rendering
* [x] NES colour palette
* [x] VBlank handling
* [x] NMI generation
* [x] Basic APU channels
* [x] Audio output
* [x] Controller input
* [x] Frame execution

### In Progress

* [ ] Improved CPU timing accuracy
* [ ] Improved PPU timing accuracy
* [ ] Improved APU timing accuracy
* [ ] Improved audio stability
* [ ] Expanded mapper support
* [ ] Improved sprite accuracy
* [ ] More extensive ROM compatibility testing
* [ ] Automated emulator tests
* [ ] Debugging tools
* [ ] More accurate hardware behaviour

---

## Roadmap

The long-term goal is to progress from a functional emulator toward a much more accurate NES hardware simulation.

### CPU

* [ ] Cycle-accurate instruction timing
* [ ] Hardware edge cases
* [ ] Decimal mode behaviour verification
* [ ] Interrupt timing verification

### PPU

* [ ] Cycle-level rendering
* [ ] Sprite evaluation accuracy
* [ ] Sprite overflow behaviour
* [ ] Sprite zero hit accuracy
* [ ] Scroll timing
* [ ] Rendering edge cases

### APU

* [ ] More accurate channel timing
* [ ] Frame counter accuracy
* [ ] Envelope timing
* [ ] Sweep timing
* [ ] Mixer accuracy
* [ ] Improved audio buffering
* [ ] APU test ROM validation

### Cartridge / Mapper

* [ ] Additional mappers
* [ ] Mapper IRQ accuracy
* [ ] CHR-RAM support
* [ ] Battery-backed RAM
* [ ] More cartridge compatibility

### Testing

* [ ] CPU instruction test ROMs
* [ ] PPU test ROMs
* [ ] APU test ROMs
* [ ] Mapper test ROMs
* [ ] Automated regression tests
* [ ] Compatibility test suite

---

## Legal Notice

JollyNesEmu does **not** include copyrighted commercial NES ROMs.

You are responsible for obtaining and using ROM images legally.

Do not distribute copyrighted ROM files with this project.

---

## Contributing

Contributions, bug reports and suggestions are welcome.

If you find an emulation problem:

1. Open an issue.
2. Describe the game and ROM version being tested.
3. Explain what the emulator does.
4. Explain what you expected to happen.
5. Include relevant logs or screenshots where possible.
6. If possible, identify the hardware component involved.

For code contributions, please keep changes focused and avoid introducing unnecessary dependencies or architectural complexity.

---

## Acknowledgements

JollyNesEmu is inspired by the extensive documentation and reverse-engineering work produced by the NES emulation community.

Particular areas of reference include:

* NES CPU documentation
* NES PPU documentation
* NES APU documentation
* NES mapper documentation
* NES test ROM projects
* Community hardware research

---

## Disclaimer

JollyNesEmu is an independent emulator project.

Nintendo and the NES are trademarks of Nintendo.

JollyNesEmu is not affiliated with or endorsed by Nintendo.

---

## License

This project is licensed under the terms specified in the repository's `LICENSE` file.

If you are redistributing or modifying JollyNesEmu, please review the license before doing so.

---

## Project Status

**JollyNesEmu is actively developed.**

The project is currently focused on improving hardware accuracy, compatibility, audio stability and mapper support while continuing to build out the emulator's debugging and testing infrastructure.

The ultimate goal is simple:

> **Build an NES emulator from scratch and understand the hardware well enough to make it accurate.**

