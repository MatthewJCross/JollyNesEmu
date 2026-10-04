using System;
using System.IO;

namespace JollyNesEmulator.Emulator
{
    public sealed class NesCartridge
    {
        public string FilePath { get; }
        public string FileName => Path.GetFileName(FilePath);
        public byte[] PrgRom { get; }
        public byte[] ChrRom { get; }
        public byte[] ChrRam { get; }
        public int MapperNumber { get; }
        public int PrgRomBanks { get; }
        public int ChrRomBanks { get; }
        public bool HasBatteryBackedRam { get; }
        public bool HasTrainer { get; }
        public bool HasFourScreenMirroring { get; }
        public bool VerticalMirroring { get; }
        public int PrgRamSize { get; }

        private NesCartridge(string filePath, byte[] prgRom, byte[] chrRom, byte[] chrRam, int mapperNumber, int prgRomBanks, int chrRomBanks, bool hasBatteryBackedRam, bool hasTrainer, bool hasFourScreenMirroring, bool verticalMirroring, int prgRamSize)
        {
            FilePath = filePath;
            PrgRom = prgRom;
            ChrRom = chrRom;
            ChrRam = chrRam;
            MapperNumber = mapperNumber;
            PrgRomBanks = prgRomBanks;
            ChrRomBanks = chrRomBanks;
            HasBatteryBackedRam = hasBatteryBackedRam;
            HasTrainer = hasTrainer;
            HasFourScreenMirroring = hasFourScreenMirroring;
            VerticalMirroring = verticalMirroring;
            PrgRamSize = prgRamSize;
        }

        public static NesCartridge Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A ROM file path is required.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("The NES ROM file could not be found.", filePath);

            byte[] data = File.ReadAllBytes(filePath);

            if (data.Length < 16)
                throw new InvalidDataException("The file is too small to contain a valid NES header.");

            if (data[0] != 'N' || data[1] != 'E' || data[2] != 'S' || data[3] != 0x1A)
                throw new InvalidDataException("The file does not contain a valid iNES header.");

            int prgRomBanks = data[4];
            int chrRomBanks = data[5];
            byte flags6 = data[6];
            byte flags7 = data[7];
            byte flags8 = data[8];

            bool hasTrainer = (flags6 & 0x04) != 0;
            bool hasBatteryBackedRam = (flags6 & 0x02) != 0;
            bool verticalMirroring = (flags6 & 0x01) != 0;
            bool hasFourScreenMirroring = (flags6 & 0x08) != 0;

            int mapperNumber = (flags6 >> 4) | (flags7 & 0xF0);
            bool nes2Header = (flags7 & 0x0C) == 0x08;

            if (nes2Header)
                throw new NotSupportedException("NES 2.0 ROM headers are not supported yet.");

            int prgRamSize = flags8 == 0 ? 8192 : flags8 * 8192;
            int offset = 16;

            if (hasTrainer)
            {
                if (data.Length < offset + 512)
                    throw new InvalidDataException("The ROM declares a trainer, but the trainer data is missing.");

                offset += 512;
            }

            int prgRomSize = prgRomBanks * 16384;
            int chrRomSize = chrRomBanks * 8192;

            if (prgRomBanks == 0)
                throw new InvalidDataException("The ROM does not contain any PRG ROM.");

            if (data.Length < offset + prgRomSize)
                throw new InvalidDataException("The ROM does not contain the complete PRG ROM.");

            byte[] prgRom = new byte[prgRomSize];
            Buffer.BlockCopy(data, offset, prgRom, 0, prgRomSize);
            offset += prgRomSize;

            byte[] chrRom;
            byte[] chrRam;

            if (chrRomSize > 0)
            {
                if (data.Length < offset + chrRomSize)
                    throw new InvalidDataException("The ROM does not contain the complete CHR ROM.");

                chrRom = new byte[chrRomSize];
                Buffer.BlockCopy(data, offset, chrRom, 0, chrRomSize);
                chrRam = Array.Empty<byte>();
            }
            else
            {
                chrRom = Array.Empty<byte>();
                chrRam = new byte[8192];
            }

            return new NesCartridge(filePath, prgRom, chrRom, chrRam, mapperNumber, prgRomBanks, chrRomBanks, hasBatteryBackedRam, hasTrainer, hasFourScreenMirroring, verticalMirroring, prgRamSize);
        }

        public override string ToString()
        {
            string chrDescription = ChrRom.Length > 0 ? $"CHR {ChrRom.Length / 1024} KB ROM" : $"CHR {ChrRam.Length / 1024} KB RAM";
            return $"{FileName} - Mapper {MapperNumber}, PRG {PrgRom.Length / 1024} KB, {chrDescription}";
        }
    }
}