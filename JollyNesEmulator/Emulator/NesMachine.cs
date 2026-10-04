using JollyNesEmulator.Apu;
using JollyNesEmulator.CPU;
using JollyNesEmulator.Nes;
using JollyNesEmulator.Ppu;
using System;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace JollyNesEmulator.Emulator
{
    public sealed class NesMachine
    {
        private bool _lastVBlank;

        public NesBus Bus { get; }

        public Cpu6502 Cpu { get; }

        public NesPpu Ppu { get; }
        private readonly NesAudio _audio;
        public NesApu Apu { get; }

        public NesCartridge? Cartridge => Bus.Cartridge;

        public bool IsRomLoaded => Cartridge != null;

        public bool IsFrameReady => Ppu.FrameReady;

        public NesMachine()
        {
            Bus = new NesBus();
            Ppu = new NesPpu();
            _audio = new NesAudio();
            Apu = new NesApu(_audio);

            NesController controller1 = new NesController();
            NesController controller2 = new NesController();

            Bus.AttachPpu(Ppu);
            Bus.AttachApu(Apu);
            Bus.AttachControllers(controller1, controller2);

            Cpu = new Cpu6502(Bus);
        }

        public void LoadRom(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A ROM file path is required.", nameof(filePath));

            NesCartridge cartridge = NesCartridge.Load(filePath);
            Bus.AttachCartridge(cartridge);
            Reset();
        }

        public void Reset()
        {
            if (!IsRomLoaded)
                return;

            Bus.Reset();
            Apu.Reset();

            _lastVBlank = Ppu.VBlank;

            Ppu.ClearFrameReady();

            Cpu.Reset();
        }

        public int StepInstruction()
        {
            if (!IsRomLoaded)
                return 0;

            if (Cpu.IsHalted)
                return 0;

            int cpuCycles = Cpu.ExecuteInstruction();

            if (cpuCycles <= 0)
                return 0;

            for (int index = 0; index < cpuCycles; index++)
                Bus.Mapper?.ClockCpuCycle();

            StepPpu(cpuCycles);
            Apu.Step(cpuCycles);

            return cpuCycles;
        }

        public void RunFrame()
        {
            if (!IsRomLoaded)
                return;

            Ppu.ClearFrameReady();

            while (!Ppu.FrameReady && !Cpu.IsHalted)
                StepInstruction();
        }

        public void RunInstructions(int instructionCount)
        {
            if (!IsRomLoaded)
                return;

            if (instructionCount < 1)
                return;

            for (int index = 0; index < instructionCount; index++)
            {
                if (Cpu.IsHalted)
                    break;

                StepInstruction();
            }
        }

        private void StepPpu(int cpuCycles)
        {
            for (int index = 0; index < cpuCycles; index++)
            {
                Ppu.Step(3);
                CheckVBlank();
                CheckMapperIrq();
            }
        }

        private void CheckMapperIrq()
        {
            if (Bus.Mapper?.IrqPending != true)
                return;

            Cpu.RequestInterrupt(InterruptTypeEnum.IRQ);
            Bus.Mapper.ClearIrq();
        }

        private void CheckVBlank()
        {
            bool currentVBlank = Ppu.VBlank;
            if (currentVBlank && !_lastVBlank && Ppu.NmiEnabled)
            {
                Cpu.RequestInterrupt(InterruptTypeEnum.NMI);
            }
            _lastVBlank = currentVBlank;
        }
    }
}


