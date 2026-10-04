using System;
using System.Collections.Generic;
using System.Text;

namespace JollyNesEmulator.Emulator.Mappers
{
    public sealed class Mapper001 : NesMapper
    {
        private readonly byte[] _prgRam;

        private byte _shiftRegister;
        private byte _control;
        private byte _chrBank0;
        private byte _chrBank1;
        private byte _prgBank;

        public Mapper001(NesCartridge cartridge) : base(cartridge)
        {
            _prgRam = new byte[Math.Max(8192, cartridge.PrgRamSize)];
            Reset();
        }

        public override bool VerticalMirroring => (_control & 0x03) == 2;

        public override NesNametableMirroring NametableMirroring
        {
            get
            {
                if (Cartridge.HasFourScreenMirroring)
                    return NesNametableMirroring.FourScreen;

                return (_control & 0x03) switch
                {
                    0 => NesNametableMirroring.OneScreenLower,
                    1 => NesNametableMirroring.OneScreenUpper,
                    2 => NesNametableMirroring.Vertical,
                    _ => NesNametableMirroring.Horizontal
                };
            }
        }

        public override byte CpuRead(ushort address)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
                return _prgRam[address - 0x6000];

            if (address < 0x8000)
                return 0;

            int offset = GetPrgRomOffset(address);

            if (offset < 0 || offset >= Cartridge.PrgRom.Length)
                return 0;

            return Cartridge.PrgRom[offset];
        }

        public override void CpuWrite(ushort address, byte value)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                _prgRam[address - 0x6000] = value;
                return;
            }

            if (address < 0x8000)
                return;

            if ((value & 0x80) != 0)
            {
                _shiftRegister = 0x10;
                _control |= 0x0C;
                return;
            }

            bool complete = (_shiftRegister & 0x01) != 0;
            _shiftRegister >>= 1;
            _shiftRegister |= (byte)((value & 0x01) << 4);

            if (!complete)
                return;

            byte registerValue = (byte)(_shiftRegister & 0x1F);
            _shiftRegister = 0x10;

            int registerIndex = (address >> 13) & 0x03;

            switch (registerIndex)
            {
                case 0:
                    _control = registerValue;
                    break;

                case 1:
                    _chrBank0 = registerValue;
                    break;

                case 2:
                    _chrBank1 = registerValue;
                    break;

                case 3:
                    _prgBank = registerValue;
                    break;
            }
        }

        public override byte PpuRead(ushort address)
        {
            if (address >= 0x2000)
                return 0;

            int offset = GetChrRomOffset(address);

            if (offset < 0 || offset >= Cartridge.ChrRom.Length)
                return 0;

            return Cartridge.ChrRom[offset];
        }

        public override void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000)
                return;

            if (Cartridge.ChrRom.Length == 0)
                return;
        }

        public override void Reset()
        {
            _shiftRegister = 0x10;
            _control = 0x0C;
            _chrBank0 = 0;
            _chrBank1 = 0;
            _prgBank = 0;
        }

        private int GetPrgRomOffset(ushort address)
        {
            int bankCount16K = Cartridge.PrgRom.Length / 0x4000;

            if (bankCount16K <= 0)
                return 0;

            int mode = (_control >> 2) & 0x03;
            int bank = _prgBank & 0x0F;

            if (mode <= 1)
            {
                int bank32K = (bank & 0x0E) >> 1;
                int bankCount32K = Math.Max(1, Cartridge.PrgRom.Length / 0x8000);
                bank32K %= bankCount32K;
                return (bank32K * 0x8000) + (address - 0x8000);
            }

            if (mode == 2)
            {
                if (address < 0xC000)
                    return address - 0x8000;

                bank %= bankCount16K;
                return (bank * 0x4000) + (address - 0xC000);
            }

            if (address < 0xC000)
            {
                bank %= bankCount16K;
                return (bank * 0x4000) + (address - 0x8000);
            }

            return ((bankCount16K - 1) * 0x4000) + (address - 0xC000);
        }

        private int GetChrRomOffset(ushort address)
        {
            if (Cartridge.ChrRom.Length == 0)
                return 0;

            int mode = (_control >> 4) & 0x01;

            if (mode == 0)
            {
                int bank = (_chrBank0 & 0x1E) >> 1;
                int offset = (bank * 0x2000) + address;
                return offset % Cartridge.ChrRom.Length;
            }

            if (address < 0x1000)
            {
                int offset = (_chrBank0 * 0x1000) + address;
                return offset % Cartridge.ChrRom.Length;
            }

            int chrOffset = (_chrBank1 * 0x1000) + (address - 0x1000);
            return chrOffset % Cartridge.ChrRom.Length;
        }
    }
}
