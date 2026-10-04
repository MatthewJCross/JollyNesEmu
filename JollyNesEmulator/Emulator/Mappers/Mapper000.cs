using System.Diagnostics;

namespace JollyNesEmulator.Emulator.Mappers
{
    public sealed class Mapper000 : NesMapper
    {
        private readonly byte[] _chrRam;

        public Mapper000(NesCartridge cartridge)
            : base(cartridge)
        {
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
        }

        public override byte PpuRead(ushort address)
        {
            if (address > 0x1FFF)
                return 0;

            byte value;

            if (Cartridge.ChrRom.Length > 0)
                value = Cartridge.ChrRom[address];
            else
                value = _chrRam[address];

            return value;
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

