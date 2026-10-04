using JollyNesEmulator.Emulator;
using JollyNesEmulator.Emulator.Mappers;

namespace JollyNesEmulator.Nes.Mappers
{
    public sealed class Mapper002 : NesMapper
    {
        private int _prgBank;

        private readonly byte[] _chrRam;

        public Mapper002(NesCartridge cartridge)
            : base(cartridge)
        {
            _prgBank = 0;
            _chrRam = cartridge.ChrRom.Length == 0 ? new byte[8192] : System.Array.Empty<byte>();
        }

        public override byte CpuRead(ushort address)
        {
            if (address < 0x8000)
                return 0;

            if (address < 0xC000)
            {
                int bankCount = Cartridge.PrgRom.Length / 0x4000;
                int bank = _prgBank % bankCount;
                int offset = (bank * 0x4000) + (address - 0x8000);

                return Cartridge.PrgRom[offset];
            }

            int lastBank = (Cartridge.PrgRom.Length / 0x4000) - 1;
            int lastOffset = (lastBank * 0x4000) + (address - 0xC000);

            return Cartridge.PrgRom[lastOffset];
        }

        public override void CpuWrite(ushort address, byte value)
        {
            if (address < 0x8000)
                return;

            int bankCount = Cartridge.PrgRom.Length / 0x4000;

            if (bankCount <= 1)
            {
                _prgBank = 0;
                return;
            }

            _prgBank = value % bankCount;
        }

        public override byte PpuRead(ushort address)
        {
            if (address > 0x1FFF)
                return 0;

            if (Cartridge.ChrRom.Length > 0)
                return Cartridge.ChrRom[address];

            return _chrRam[address];
        }

        public override void PpuWrite(ushort address, byte value)
        {
            if (address > 0x1FFF)
                return;

            if (Cartridge.ChrRom.Length == 0)
                _chrRam[address] = value;
        }
    }
}