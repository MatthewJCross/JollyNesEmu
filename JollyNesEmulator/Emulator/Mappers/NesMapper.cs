
using JollyNesEmulator.Nes.Mappers;

namespace JollyNesEmulator.Emulator.Mappers
{
    public enum NesNametableMirroring
    {
        Vertical,
        Horizontal,
        OneScreenLower,
        OneScreenUpper,
        FourScreen
    }

    public abstract class NesMapper
    {
        public virtual bool IrqPending => false;

        protected NesMapper(NesCartridge cartridge)
        {
            Cartridge = cartridge;
        }

        public NesCartridge Cartridge { get; }
        public virtual bool VerticalMirroring => Cartridge.VerticalMirroring;
        public virtual NesNametableMirroring NametableMirroring => Cartridge.HasFourScreenMirroring ? NesNametableMirroring.FourScreen : (Cartridge.VerticalMirroring ? NesNametableMirroring.Vertical : NesNametableMirroring.Horizontal);

        public abstract byte CpuRead(ushort address);

        public abstract void CpuWrite(ushort address, byte value);

        public abstract byte PpuRead(ushort address);

        public abstract void PpuWrite(ushort address, byte value);

        public virtual void Reset()
        {
        }

        public virtual void ClearIrq()
        {
        }

        public virtual void NotifyPpuAddress(ushort address)
        {
        }

        public virtual void NotifyPpuCycle()
        {
        }

        public virtual void ClockCpuCycle()
        {
        }

        public static NesMapper Create(NesCartridge cartridge)
        {
            return cartridge.MapperNumber switch
            {
                0 => new Mapper000(cartridge),
                1 => new Mapper001(cartridge),
                2 => new Mapper002(cartridge),
                3 => new Mapper003(cartridge),
                4 => new Mapper004(cartridge),
                67 => new Mapper067(cartridge),
                _ => throw new NotSupportedException($"NES mapper {cartridge.MapperNumber} is not supported yet.")
            };
        }
    }
}