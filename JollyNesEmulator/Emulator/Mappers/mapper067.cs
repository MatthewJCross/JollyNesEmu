using JollyNesEmulator.Emulator;
using JollyNesEmulator.Emulator.Mappers;
using JollyNesEmulator.Nes;

namespace JollyNesEmulator.Nes.Mappers
{
    public sealed class Mapper067 : NesMapper
    {
        private readonly byte[] _chrBanks = new byte[4];

        private int _prgBank;
        private ushort _irqCounter;
        private bool _irqEnabled;
        private bool _irqPending;
        private bool _irqWriteHigh;

        private NesNametableMirroring _nametableMirroring;

        public Mapper067(NesCartridge cartridge) : base(cartridge)
        {
            Reset();
        }

        public override bool IrqPending => _irqPending;

        public override bool VerticalMirroring => _nametableMirroring == NesNametableMirroring.Vertical;

        public override NesNametableMirroring NametableMirroring => _nametableMirroring;

        public override byte CpuRead(ushort address)
        {
            if (address < 0x8000)
                return 0;

            int offset;

            if (address < 0xC000)
            {
                int bankCount = Math.Max(1, Cartridge.PrgRom.Length / 0x4000);
                int bank = _prgBank % bankCount;
                offset = (bank * 0x4000) + (address - 0x8000);
            }
            else
            {
                int bankCount = Math.Max(1, Cartridge.PrgRom.Length / 0x4000);
                int bank = bankCount - 1;
                offset = (bank * 0x4000) + (address - 0xC000);
            }

            if (offset < 0 || offset >= Cartridge.PrgRom.Length)
                return 0;

            return Cartridge.PrgRom[offset];
        }

        public override void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000)
                return;

            switch (address & 0xF800)
            {
                case 0x8000:
                    _irqPending = false;
                    break;

                case 0x8800:
                    _chrBanks[0] = value;
                    break;

                case 0x9800:
                    _chrBanks[1] = value;
                    break;

                case 0xA800:
                    _chrBanks[2] = value;
                    break;

                case 0xB800:
                    _chrBanks[3] = value;
                    break;

                case 0xC800:
                    if (!_irqWriteHigh)
                    {
                        _irqCounter = (ushort)((_irqCounter & 0x00FF) | (value << 8));
                        _irqWriteHigh = true;
                    }
                    else
                    {
                        _irqCounter = (ushort)((_irqCounter & 0xFF00) | value);
                        _irqWriteHigh = false;
                    }

                    break;

                case 0xD800:
                    _irqEnabled = (value & 0x10) != 0;
                    _irqWriteHigh = false;

                    if (!_irqEnabled)
                        _irqPending = false;

                    break;

                case 0xE800:
                    _nametableMirroring = (value & 0x03) switch
                    {
                        0 => NesNametableMirroring.Vertical,
                        1 => NesNametableMirroring.Horizontal,
                        2 => NesNametableMirroring.OneScreenLower,
                        _ => NesNametableMirroring.OneScreenUpper
                    };
                    break;

                case 0xF800:
                    _prgBank = value & 0x0F;
                    break;
            }
        }

        public override byte PpuRead(ushort address)
        {
            if (address >= 0x2000)
                return 0;

            int bank = _chrBanks[address / 0x0800] & 0x3F;
            int offset = (bank * 0x0800) + (address & 0x07FF);

            if (Cartridge.ChrRom.Length == 0)
                return 0;

            offset %= Cartridge.ChrRom.Length;

            return Cartridge.ChrRom[offset];
        }

        public override void PpuWrite(ushort address, byte value)
        {
            if (address >= 0x2000)
                return;
        }

        public override void ClearIrq()
        {
            _irqPending = false;
        }

        public override void Reset()
        {
            Array.Clear(_chrBanks, 0, _chrBanks.Length);

            _prgBank = 0;
            _irqCounter = 0;
            _irqEnabled = false;
            _irqPending = false;
            _irqWriteHigh = false;

            _nametableMirroring = Cartridge.HasFourScreenMirroring ? NesNametableMirroring.FourScreen : (Cartridge.VerticalMirroring ? NesNametableMirroring.Vertical : NesNametableMirroring.Horizontal);
        }

        public override void ClockCpuCycle()
        {
            if (!_irqEnabled)
                return;

            if (_irqCounter == 0)
            {
                _irqCounter = 0xFFFF;
                _irqEnabled = false;
                _irqPending = true;
                return;
            }

            _irqCounter--;
        }
    }
}