using JollyNesEmulator.Emulator;
using JollyNesEmulator.Emulator.Mappers;

namespace JollyNesEmulator.Nes.Mappers
{
    public sealed class Mapper003 : NesMapper
    {
        private int _chrBank;
        private readonly byte[] _chrRam;

        public Mapper003(NesCartridge cartridge)
            : base(cartridge)
        {
            _chrBank = 0;
            _chrRam = cartridge.ChrRom.Length == 0 ? new byte[8192] : System.Array.Empty<byte>();
        }

        public override byte CpuRead(ushort address)
        {
            if (address < 0x8000)
                return 0;

            int offset = address - 0x8000;

            if (Cartridge.PrgRom.Length == 16384)
                offset &= 0x3FFF;
            else
                offset %= Cartridge.PrgRom.Length;

            return Cartridge.PrgRom[offset];
        }

        public override void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000)
                return;

            if (Cartridge.ChrRom.Length == 0)
                return;

            int bankCount = Cartridge.ChrRom.Length / 0x2000;

            if (bankCount <= 1)
            {
                _chrBank = 0;
                return;
            }

            _chrBank = value % bankCount;
        }

        public override byte PpuRead(ushort address)
        {
            if (address > 0x1FFF)
                return 0;

            if (Cartridge.ChrRom.Length == 0)
                return _chrRam[address];

            int bankCount = Cartridge.ChrRom.Length / 0x2000;
            int bank = _chrBank % bankCount;
            int offset = (bank * 0x2000) + address;

            return Cartridge.ChrRom[offset];
        }

        public override void PpuWrite(ushort address, byte value)
        {
            if (address > 0x1FFF)
                return;

            if (Cartridge.ChrRom.Length == 0)
                _chrRam[address] = value;
        }

        public override void Reset()
        {
            _chrBank = 0;
        }
    }
}
