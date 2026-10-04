using JollyNesEmulator.Emulator;

namespace JollyNesEmulator.Emulator.Mappers
{
    public sealed class Mapper004 : NesMapper
    {
        private readonly byte[] _prgRam;
        private readonly int[] _bankRegisters = new int[8];

        private int _command;
        private bool _prgMode;
        private bool _chrMode;

        private bool _verticalMirroring;
        private bool _prgRamEnabled;
        private bool _prgRamWriteProtected;

        private byte _irqLatch;
        private byte _irqCounter;
        private bool _irqReload;
        private bool _irqEnabled;
        private bool _irqPending;

        private bool _lastPpuA12;
        private int _ppuA12LowCycles;

        public override bool IrqPending => _irqPending;

        public override bool VerticalMirroring => Cartridge.HasFourScreenMirroring ? Cartridge.VerticalMirroring : _verticalMirroring;

        public Mapper004(NesCartridge cartridge) : base(cartridge)
        {
            _prgRam = new byte[Math.Max(8192, cartridge.PrgRamSize)];
            Reset();
        }

        public override byte CpuRead(ushort address)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                if (!_prgRamEnabled)
                    return 0;

                return _prgRam[address - 0x6000];
            }

            if (address < 0x8000)
                return 0;

            int bank = GetPrgBank(address);
            int offset = GetPrgOffset(address, bank);

            return Cartridge.PrgRom[offset];
        }

        public override void CpuWrite(ushort address, byte value)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                if (_prgRamEnabled && !_prgRamWriteProtected)
                    _prgRam[address - 0x6000] = value;

                return;
            }

            if (address < 0x8000)
                return;

            if ((address & 0xE001) == 0x8000)
            {
                _command = value & 0x07;
                _prgMode = (value & 0x40) != 0;
                _chrMode = (value & 0x80) != 0;
                return;
            }

            if ((address & 0xE001) == 0x8001)
            {
                _bankRegisters[_command] = value;
                return;
            }

            if ((address & 0xE001) == 0xA000)
            {
                if (!Cartridge.HasFourScreenMirroring)
                    _verticalMirroring = (value & 0x01) != 0;

                return;
            }

            if ((address & 0xE001) == 0xA001)
            {
                _prgRamEnabled = (value & 0x80) != 0;
                _prgRamWriteProtected = (value & 0x40) != 0;
                return;
            }

            if ((address & 0xE001) == 0xC000)
            {
                _irqLatch = value;
                return;
            }

            if ((address & 0xE001) == 0xC001)
            {
                _irqReload = true;
                return;
            }

            if ((address & 0xE001) == 0xE000)
            {
                _irqEnabled = false;
                _irqPending = false;
                return;
            }

            if ((address & 0xE001) == 0xE001)
            {
                _irqEnabled = true;
            }
        }

        public override byte PpuRead(ushort address)
        {
            if (address >= 0x2000)
                return 0;

            int bank = GetChrBank(address);
            int offset = (bank * 0x0400) + (address & 0x03FF);

            if (Cartridge.ChrRom.Length > 0)
            {
                offset %= Cartridge.ChrRom.Length;
                return Cartridge.ChrRom[offset];
            }

            if (Cartridge.ChrRam.Length > 0)
            {
                offset %= Cartridge.ChrRam.Length;
                return Cartridge.ChrRam[offset];
            }

            return 0;
        }

        public override void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000)
                return;

            if (Cartridge.ChrRom.Length > 0)
                return;

            if (Cartridge.ChrRam.Length == 0)
                return;

            int bank = GetChrBank(address);
            int offset = (bank * 0x0400) + (address & 0x03FF);
            offset %= Cartridge.ChrRam.Length;

            Cartridge.ChrRam[offset] = value;
        }

        public override void NotifyPpuAddress(ushort address)
        {
            bool currentA12 = (address & 0x1000) != 0;

            if (currentA12 && !_lastPpuA12 && _ppuA12LowCycles >= 8)
                ClockIrqCounter();

            _lastPpuA12 = currentA12;

            if (currentA12)
                _ppuA12LowCycles = 0;
        }

        public override void NotifyPpuCycle()
        {
            if (!_lastPpuA12 && _ppuA12LowCycles < 16)
                _ppuA12LowCycles++;
        }

        public override void ClearIrq()
        {
            _irqPending = false;
        }

        public override void Reset()
        {
            Array.Clear(_bankRegisters, 0, _bankRegisters.Length);

            _command = 0;
            _prgMode = false;
            _chrMode = false;

            _verticalMirroring = Cartridge.VerticalMirroring;
            _prgRamEnabled = true;
            _prgRamWriteProtected = false;

            _irqLatch = 0;
            _irqCounter = 0;
            _irqReload = false;
            _irqEnabled = false;
            _irqPending = false;

            _lastPpuA12 = false;
            _ppuA12LowCycles = 0;
        }

        private void ClockIrqCounter()
        {
            if (_irqCounter == 0 || _irqReload)
            {
                _irqCounter = _irqLatch;
                _irqReload = false;
            }
            else
            {
                _irqCounter--;
            }

            if (_irqCounter == 0 && _irqEnabled)
                _irqPending = true;
        }

        private int GetPrgBank(ushort address)
        {
            int bankCount = Cartridge.PrgRom.Length / 0x2000;

            if (bankCount <= 0)
                return 0;

            int secondLast = Math.Max(0, bankCount - 2);
            int last = Math.Max(0, bankCount - 1);

            int bank;

            switch ((address - 0x8000) / 0x2000)
            {
                case 0:
                    bank = _prgMode ? secondLast : _bankRegisters[6];
                    break;

                case 1:
                    bank = _bankRegisters[7];
                    break;

                case 2:
                    bank = _prgMode ? _bankRegisters[6] : secondLast;
                    break;

                default:
                    bank = last;
                    break;
            }

            bank %= bankCount;

            if (bank < 0)
                bank += bankCount;

            return bank;
        }

        private int GetPrgOffset(ushort address, int bank)
        {
            return (bank * 0x2000) + (address & 0x1FFF);
        }

        private int GetChrBank(ushort address)
        {
            int bank;

            if (!_chrMode)
            {
                if (address < 0x0800)
                    bank = (_bankRegisters[0] & 0xFE) + ((address >> 10) & 1);
                else if (address < 0x1000)
                    bank = (_bankRegisters[1] & 0xFE) + ((address >> 10) & 1);
                else if (address < 0x1400)
                    bank = _bankRegisters[2];
                else if (address < 0x1800)
                    bank = _bankRegisters[3];
                else if (address < 0x1C00)
                    bank = _bankRegisters[4];
                else
                    bank = _bankRegisters[5];
            }
            else
            {
                if (address < 0x0400)
                    bank = _bankRegisters[2];
                else if (address < 0x0800)
                    bank = _bankRegisters[3];
                else if (address < 0x0C00)
                    bank = _bankRegisters[4];
                else if (address < 0x1000)
                    bank = _bankRegisters[5];
                else if (address < 0x1800)
                    bank = (_bankRegisters[0] & 0xFE) + ((address - 0x1000) >> 10);
                else
                    bank = (_bankRegisters[1] & 0xFE) + ((address - 0x1800) >> 10);
            }

            return bank;
        }
    }
}