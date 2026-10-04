using JollyNesEmulator.Emulator.Mappers;

namespace JollyNesEmulator.Emulator
{
    public sealed class NesMemory
    {
        private readonly byte[] _ram = new byte[2048];
        private NesCartridge? _cartridge;
        private NesMapper? _mapper;

        public void InsertCartridge(NesCartridge cartridge)
        {
            _cartridge = cartridge;
            _mapper = NesMapper.Create(cartridge);
        }

        public byte Read(ushort address)
        {
            if (address <= 0x1FFF)
                return _ram[address & 0x07FF];

            if (address <= 0x3FFF)
                return ReadPpuRegister((ushort)(0x2000 | (address & 0x0007)));

            if (address <= 0x401F)
                return ReadIoRegister(address);

            if (address >= 0x4020)
                return ReadCartridge(address);

            return 0;
        }

        public void Write(ushort address, byte value)
        {
            if (address <= 0x1FFF)
            {
                _ram[address & 0x07FF] = value;
                return;
            }

            if (address <= 0x3FFF)
            {
                WritePpuRegister((ushort)(0x2000 | (address & 0x0007)), value);
                return;
            }

            if (address <= 0x401F)
            {
                WriteIoRegister(address, value);
                return;
            }

            if (address >= 0x4020)
            {
                WriteCartridge(address, value);
            }
        }

        public ushort ReadWord(ushort address)
        {
            byte low = Read(address);
            byte high = Read((ushort)(address + 1));
            return (ushort)(low | (high << 8));
        }

        public void Reset()
        {
            System.Array.Clear(_ram, 0, _ram.Length);
            _mapper?.Reset();
        }

        private byte ReadPpuRegister(ushort address)
        {
            return 0;
        }

        private void WritePpuRegister(ushort address, byte value)
        {
        }

        private byte ReadIoRegister(ushort address)
        {
            return 0;
        }

        private void WriteIoRegister(ushort address, byte value)
        {
            int a = 0;
        }

        private byte ReadCartridge(ushort address)
        {
            return _mapper?.CpuRead(address) ?? 0;
        }

        private void WriteCartridge(ushort address, byte value)
        {
            _mapper?.CpuWrite(address, value);
        }
    }
}

