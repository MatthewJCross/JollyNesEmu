using JollyNesEmulator.Apu;
using JollyNesEmulator.Emulator.Mappers;
using JollyNesEmulator.Ppu;
using System.Diagnostics;

namespace JollyNesEmulator.Emulator
{
    public sealed class NesBus
    {
        private readonly byte[] _ram = new byte[2048];
        private NesCartridge? _cartridge;
        private NesMapper? _mapper;
        private NesPpu? _ppu;
        private NesApu? _apu;
        private NesController? _controller1;
        private NesController? _controller2;

        public NesCartridge? Cartridge => _cartridge;

        public NesMapper? Mapper => _mapper;

        public NesPpu? Ppu => _ppu;
        public NesApu? Apu => _apu;

        public NesController? Controller1 => _controller1;

        public NesController? Controller2 => _controller2;

        public void AttachCartridge(NesCartridge cartridge)
        {
            _cartridge = cartridge;
            _mapper = NesMapper.Create(cartridge);
            _mapper.Reset();
            _ppu?.AttachMapper(_mapper);
        }

        public void AttachPpu(NesPpu ppu)
        {
            _ppu = ppu;

            if (_mapper != null)
                _ppu.AttachMapper(_mapper);
        }

        public void AttachApu(NesApu apu)
        {
            _apu = apu;
        }

        public void AttachControllers(NesController controller1, NesController controller2)
        {
            _controller1 = controller1;
            _controller2 = controller2;
        }

        public byte Read(ushort address)
        {
            if (address <= 0x1FFF)
                return ReadRam(address);

            if (address <= 0x3FFF)
                return ReadPpuRegister(address);

            if (address <= 0x4017)
                return ReadIoRegister(address);

            if (address <= 0x401F)
                return 0;

            if (_mapper == null)
                return 0;

            return _mapper.CpuRead(address);
        }

        public void Write(ushort address, byte value)
        {
            if (address <= 0x1FFF)
            {
                WriteRam(address, value);
                return;
            }

            if (address <= 0x3FFF)
            {
                WritePpuRegister(address, value);
                return;
            }

            if (address <= 0x4017)
            {
                WriteIoRegister(address, value);
                return;
            }

            if (address <= 0x401F)
                return;

            _mapper?.CpuWrite(address, value);
        }

        private byte ReadRam(ushort address)
        {
            return _ram[address & 0x07FF];
        }

        private void WriteRam(ushort address, byte value)
        {
            _ram[address & 0x07FF] = value;
        }

        private byte ReadPpuRegister(ushort address)
        {
            if (_ppu == null)
                return 0;

            ushort registerAddress = (ushort)(0x2000 + (address & 0x0007));
            return _ppu.CpuReadRegister(registerAddress);
        }

        private void WritePpuRegister(ushort address, byte value)
        {
            if (_ppu == null)
                return;

            ushort registerAddress = (ushort)(0x2000 + (address & 0x0007));
            _ppu.CpuWriteRegister(registerAddress, value);
        }

        private byte ReadIoRegister(ushort address)
        {
            return address switch
            {
                0x4015 => _apu?.ReadRegister(address) ?? 0,
                0x4016 => _controller1?.Read() ?? 0,
                0x4017 => _controller2?.Read() ?? 0,
                _ => 0
            };
        }

        private void WriteIoRegister(ushort address, byte value)
        {
            switch (address)
            {
                case 0x4000:
                case 0x4001:
                case 0x4002:
                case 0x4003:
                case 0x4004:
                case 0x4005:
                case 0x4006:
                case 0x4007:
                case 0x4008:
                case 0x4009:
                case 0x400A:
                case 0x400B:
                case 0x400C:
                case 0x400D:
                case 0x400E:
                case 0x400F:
                case 0x4015:
                case 0x4017:
                    _apu?.WriteRegister(address, value);
                    break;

                case 0x4014:
                    PerformOamDma(value);
                    break;

                case 0x4016:
                    _controller1?.Write(value);
                    _controller2?.Write(value);
                    break;
            }
        }

        private void PerformOamDma(byte page)
        {
            if (_ppu == null)
                return;

            ushort sourceAddress = (ushort)(page << 8);

            for (int index = 0; index < 256; index++)
            {
                byte value = Read((ushort)(sourceAddress + index));
                _ppu.WriteOamByte((byte)index, value);
            }
        }

        public void Reset()
        {
            Array.Clear(_ram, 0, _ram.Length);
            _mapper?.Reset();
            _ppu?.Reset();
            _controller1?.Reset();
            _controller2?.Reset();
        }
    }
}