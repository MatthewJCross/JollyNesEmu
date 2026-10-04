using JollyNesEmulator.Emulator.Mappers;
using System.Diagnostics;

namespace JollyNesEmulator.Ppu
{
    public sealed class NesPpu
    {
        public const int ScreenWidth = 256;
        public const int ScreenHeight = 240;

        private readonly byte[] _nametableRam = new byte[4096];
        private readonly byte[] _paletteRam = new byte[32];
        private readonly byte[] _oam = new byte[256];
        private readonly byte[] _frameBuffer = new byte[ScreenWidth * ScreenHeight];
        private readonly byte[] _spritePixelBuffer = new byte[ScreenWidth];
        private readonly byte[] _spritePriorityBuffer = new byte[ScreenWidth];
        private readonly bool[] _spriteOpaqueBuffer = new bool[ScreenWidth];

        private int _spriteBufferScanline = -1;

        private NesMapper? _mapper;

        private byte _ppuCtrl;
        private byte _ppuMask;
        private byte _ppuStatus;
        private byte _oamAddress;
        private byte _ppuDataBuffer;

        private ushort _vramAddress;
        private ushort _tempAddress;
        private byte _fineX;
        private bool _writeToggle;

        private int _cycle;
        private int _scanline;
        private bool _frameReady;

        private int _scrollX;
        private int _scrollY;
        
        public byte[] FrameBuffer => _frameBuffer;

        public bool FrameReady => _frameReady;

        public byte[] PaletteRam => _paletteRam;

        public int Cycle => _cycle;

        public int Scanline => _scanline;

        public bool VBlank => (_ppuStatus & 0x80) != 0;

        public bool NmiEnabled => (_ppuCtrl & 0x80) != 0;

        public byte Mask => _ppuMask;

        public NesPpu()
        {
            Reset();
        }

        public void AttachMapper(NesMapper mapper)
        {
            _mapper = mapper;
        }

        public void Reset()
        {
            _ppuCtrl = 0;
            _ppuMask = 0;
            _ppuStatus = 0;
            _oamAddress = 0;
            _ppuDataBuffer = 0;
            _vramAddress = 0;
            _tempAddress = 0;
            _fineX = 0;
            _writeToggle = false;
            _cycle = 0;
            _scanline = 0;
            _frameReady = false;
            _spriteBufferScanline = -1;
            _scrollX = 0;
            _scrollY = 0;

            Array.Clear(_nametableRam, 0, _nametableRam.Length);
            Array.Clear(_paletteRam, 0, _paletteRam.Length);
            Array.Clear(_oam, 0, _oam.Length);
            Array.Clear(_frameBuffer, 0, _frameBuffer.Length);
            Array.Clear(_spritePixelBuffer, 0, _spritePixelBuffer.Length);
            Array.Clear(_spritePriorityBuffer, 0, _spritePriorityBuffer.Length);
            Array.Clear(_spriteOpaqueBuffer, 0, _spriteOpaqueBuffer.Length);
        }

        public void Step(int ppuCycles)
        {
            for (int index = 0; index < ppuCycles; index++)
                StepCycle();
        }

        private void StepCycle()
        {
            _cycle++;
            _mapper?.NotifyPpuCycle();

            if (_scanline >= 0 && _scanline < 240)
                RenderScanlinePixel();

            if (_scanline == 241 && _cycle == 1)
            {
                _ppuStatus |= 0x80;
                _frameReady = true;
            }

            if (_scanline == 261 && _cycle == 1)
                _ppuStatus &= 0x1F;

            if (_cycle >= 341)
            {
                _cycle = 0;
                _scanline++;

                if (_scanline >= 262)
                    _scanline = 0;
            }
        }

        private void RenderScanlinePixel()
        {
            if (_cycle < 1 || _cycle > 256)
                return;

            int x = _cycle - 1;
            int y = _scanline;

            byte backgroundColor = GetPaletteColor(0);
            bool backgroundOpaque = false;

            if ((_ppuMask & 0x08) != 0)
                backgroundColor = RenderBackgroundPixel(x, y, out backgroundOpaque);

            if (_spriteBufferScanline != y)
            {
                RenderSpritesForScanline(y);
                _spriteBufferScanline = y;
            }

            int frameIndex = (y * ScreenWidth) + x;

            _frameBuffer[frameIndex] = backgroundColor;

            if ((_ppuMask & 0x10) == 0)
                return;

            if (!_spriteOpaqueBuffer[x])
                return;

            if (backgroundOpaque && x < 255 && (_ppuStatus & 0x40) == 0)
            {
                if (x >= 8 || (_ppuMask & 0x04) != 0)
                    _ppuStatus |= 0x40;
            }

            if (_spritePriorityBuffer[x] != 0 && backgroundOpaque)
                return;

            _frameBuffer[frameIndex] = _spritePixelBuffer[x];
        }

        private byte RenderBackgroundPixel(int x, int y, out bool opaque)
        {
            opaque = false;

            int scrollX = _scrollX;
            int scrollY = _scrollY;

            int worldX = scrollX + x;
            int worldY = scrollY + y;

            int nametableX = (_tempAddress >> 10) & 0x01;
            int nametableY = (_tempAddress >> 11) & 0x01;

            int nametableOffsetX = worldX / 256;
            int nametableOffsetY = worldY / 240;

            int pixelX = worldX % 256;
            int pixelY = worldY % 240;

            if (pixelX < 0)
                pixelX += 256;

            if (pixelY < 0)
                pixelY += 240;

            int currentNametableX = (nametableX + nametableOffsetX) & 0x01;
            int currentNametableY = (nametableY + nametableOffsetY) & 0x01;

            int coarsePixelX = pixelX >> 3;
            int coarsePixelY = pixelY >> 3;

            int fineX = pixelX & 0x07;
            int fineY = pixelY & 0x07;

            int nametableIndex = currentNametableX | (currentNametableY << 1);
            ushort nametableBase = (ushort)(0x2000 + (nametableIndex * 0x400));

            ushort tileAddress = (ushort)(nametableBase + (coarsePixelY * 32) + coarsePixelX);
            byte tileIndex = ReadPpuMemory(tileAddress);

            int patternTableBase = (_ppuCtrl & 0x10) != 0 ? 0x1000 : 0x0000;
            ushort patternAddress = (ushort)(patternTableBase + (tileIndex * 16) + fineY);

            byte lowPlane = ReadPpuPatternMemory(patternAddress);
            byte highPlane = ReadPpuPatternMemory((ushort)(patternAddress + 8));

            int bit = 7 - fineX;
            int pixel = ((highPlane >> bit) & 1) << 1;
            pixel |= (lowPlane >> bit) & 1;

            if (pixel == 0)
                return GetPaletteColor(0);

            opaque = true;

            int attributeX = coarsePixelX >> 2;
            int attributeY = coarsePixelY >> 2;
            int attributeIndex = (attributeY * 8) + attributeX;

            ushort attributeAddress = (ushort)(nametableBase + 0x03C0 + attributeIndex);
            byte attribute = ReadPpuMemory(attributeAddress);

            int quadrantX = (coarsePixelX >> 1) & 0x01;
            int quadrantY = (coarsePixelY >> 1) & 0x01;
            int quadrant = quadrantX | (quadrantY << 1);

            int paletteIndex = quadrant switch
            {
                0 => attribute & 0x03,
                1 => (attribute >> 2) & 0x03,
                2 => (attribute >> 4) & 0x03,
                _ => (attribute >> 6) & 0x03
            };

            int paletteAddress = (paletteIndex * 4) + pixel;

            return GetPaletteColor(paletteAddress);
        }

        //private byte RenderBackgroundPixel(int x, int y, out bool opaque)
        //{
        //    opaque = false;

        //    int coarseX = _tempAddress & 0x001F;
        //    int coarseY = (_tempAddress >> 5) & 0x001F;
        //    int nametableX = (_tempAddress >> 10) & 0x01;
        //    int nametableY = (_tempAddress >> 11) & 0x01;

        //    int scrollX = (coarseX << 3) | _fineX;
        //    int scrollY = (coarseY << 3) | ((_tempAddress >> 12) & 0x07);

        //    int worldX = scrollX + x;
        //    int worldY = scrollY + y;

        //    int nametableOffsetX = worldX / 256;
        //    int nametableOffsetY = worldY / 240;

        //    int pixelX = worldX & 0xFF;
        //    int pixelY = worldY % 240;

        //    if (pixelY < 0)
        //        pixelY += 240;

        //    int currentNametableX = nametableX ^ (nametableOffsetX & 0x01);
        //    int currentNametableY = nametableY ^ (nametableOffsetY & 0x01);

        //    int coarsePixelX = pixelX >> 3;
        //    int coarsePixelY = pixelY >> 3;
        //    int fineX = pixelX & 0x07;
        //    int fineY = pixelY & 0x07;

        //    int nametableIndex = currentNametableX | (currentNametableY << 1);
        //    ushort nametableBase = (ushort)(0x2000 + (nametableIndex * 0x400));

        //    ushort tileAddress = (ushort)(nametableBase + (coarsePixelY * 32) + coarsePixelX);
        //    byte tileIndex = ReadPpuMemory(tileAddress);

        //    int patternTableBase = (_ppuCtrl & 0x10) != 0 ? 0x1000 : 0x0000;
        //    ushort patternAddress = (ushort)(patternTableBase + (tileIndex * 16) + fineY);

        //    byte lowPlane = ReadPpuPatternMemory(patternAddress);
        //    byte highPlane = ReadPpuPatternMemory((ushort)(patternAddress + 8));

        //    int bit = 7 - fineX;
        //    int pixel = ((highPlane >> bit) & 1) << 1;
        //    pixel |= (lowPlane >> bit) & 1;

        //    if (pixel == 0)
        //        return GetPaletteColor(0);

        //    opaque = true;

        //    int attributeX = coarsePixelX >> 2;
        //    int attributeY = coarsePixelY >> 2;
        //    int attributeIndex = (attributeY * 8) + attributeX;
        //    ushort attributeAddress = (ushort)(nametableBase + 0x03C0 + attributeIndex);

        //    byte attribute = ReadPpuMemory(attributeAddress);

        //    int quadrantX = (coarsePixelX & 0x02) != 0 ? 1 : 0;
        //    int quadrantY = (coarsePixelY & 0x02) != 0 ? 1 : 0;
        //    int quadrant = quadrantX | (quadrantY << 1);

        //    int paletteIndex = quadrant switch
        //    {
        //        0 => attribute & 0x03,
        //        1 => (attribute >> 2) & 0x03,
        //        2 => (attribute >> 4) & 0x03,
        //        _ => (attribute >> 6) & 0x03
        //    };

        //    int paletteAddress = 1 + (paletteIndex * 4) + pixel;

        //    return GetPaletteColor(paletteAddress);
        //}

        private void RenderSpritesForScanline(int y)
        {
            Array.Clear(_spritePixelBuffer, 0, _spritePixelBuffer.Length);
            Array.Clear(_spritePriorityBuffer, 0, _spritePriorityBuffer.Length);
            Array.Clear(_spriteOpaqueBuffer, 0, _spriteOpaqueBuffer.Length);

            if ((_ppuMask & 0x10) == 0)
                return;

            int spriteHeight = (_ppuCtrl & 0x20) != 0 ? 16 : 8;

            for (int spriteIndex = 0; spriteIndex < 64; spriteIndex++)
            {
                int oamIndex = spriteIndex * 4;
                int spriteY = _oam[oamIndex];
                int spriteX = _oam[oamIndex + 3];
                int spriteRow = y - spriteY - 1;

                if (spriteRow < 0 || spriteRow >= spriteHeight)
                    continue;

                byte tileIndex = _oam[oamIndex + 1];
                byte attributes = _oam[oamIndex + 2];

                bool flipHorizontal = (attributes & 0x40) != 0;
                bool flipVertical = (attributes & 0x80) != 0;
                bool behindBackground = (attributes & 0x20) != 0;

                if (flipVertical)
                    spriteRow = spriteHeight - 1 - spriteRow;

                int tileAddress;

                if (spriteHeight == 16)
                {
                    int patternTableBase = (tileIndex & 0x01) != 0 ? 0x1000 : 0x0000;
                    tileIndex &= 0xFE;

                    if (spriteRow >= 8)
                    {
                        tileIndex++;
                        spriteRow -= 8;
                    }

                    tileAddress = patternTableBase + (tileIndex * 16);
                }
                else
                {
                    int patternTableBase = (_ppuCtrl & 0x08) != 0 ? 0x1000 : 0x0000;
                    tileAddress = patternTableBase + (tileIndex * 16);
                }

                byte lowPlane = ReadPpuPatternMemory((ushort)(tileAddress + spriteRow));
                byte highPlane = ReadPpuPatternMemory((ushort)(tileAddress + spriteRow + 8));

                for (int column = 0; column < 8; column++)
                {
                    int x = spriteX + column;

                    if (x < 0 || x >= ScreenWidth)
                        continue;

                    if (x < 8 && (_ppuMask & 0x04) == 0)
                        continue;

                    int spriteColumn = flipHorizontal ? 7 - column : column;
                    int bit = 7 - spriteColumn;

                    int pixel = ((highPlane >> bit) & 1) << 1;
                    pixel |= (lowPlane >> bit) & 1;

                    if (pixel == 0)
                        continue;

                    if (_spriteOpaqueBuffer[x])
                        continue;

                    int paletteIndex = 0x10 + ((attributes & 0x03) * 4) + pixel;

                    _spritePixelBuffer[x] = GetPaletteColor(paletteIndex);
                    _spritePriorityBuffer[x] = behindBackground ? (byte)1 : (byte)0;
                    _spriteOpaqueBuffer[x] = true;
                }
            }
        }

        private readonly int[] _paletteRequestCounts = new int[32];
        private byte GetPaletteColor(int paletteIndex)
        {
            paletteIndex &= 0x1F;

            _paletteRequestCounts[paletteIndex]++;

            if (paletteIndex == 0x10)
                paletteIndex = 0x00;

            if (paletteIndex == 0x14)
                paletteIndex = 0x04;

            if (paletteIndex == 0x18)
                paletteIndex = 0x08;

            if (paletteIndex == 0x1C)
                paletteIndex = 0x0C;

            return (byte)(_paletteRam[paletteIndex] & 0x3F);
        }        

        public byte CpuReadRegister(ushort address)
        {
            address = (ushort)(0x2000 + (address & 0x0007));

            return address switch
            {
                0x2002 => ReadStatus(),
                0x2004 => ReadOamData(),
                0x2007 => ReadData(),
                _ => 0
            };
        }

        public void CpuWriteRegister(ushort address, byte value)
        {
            address = (ushort)(0x2000 + (address & 0x0007));

            switch (address)
            {
                case 0x2000:
                    WriteControl(value);
                    break;

                case 0x2001:
                    WriteMask(value);
                    break;

                case 0x2003:
                    WriteOamAddress(value);
                    break;

                case 0x2004:
                    WriteOamData(value);
                    break;

                case 0x2005:
                    WriteScroll(value);
                    break;

                case 0x2006:
                    WriteAddress(value);
                    break;

                case 0x2007:
                    WriteData(value);
                    break;
            }
        }

        private byte ReadStatus()
        {
            byte result = _ppuStatus;
            _ppuStatus &= 0x7F;
            _writeToggle = false;
            return result;
        }

        private byte ReadOamData()
        {
            return _oam[_oamAddress];
        }

        private byte ReadData()
        {
            ushort address = (ushort)(_vramAddress & 0x3FFF);
            byte value = ReadPpuMemory(address);

            if (address < 0x3F00)
            {
                byte bufferedValue = _ppuDataBuffer;
                _ppuDataBuffer = value;
                value = bufferedValue;
            }
            else
            {
                _ppuDataBuffer = ReadPpuMemory((ushort)(address - 0x1000));
            }

            IncrementVramAddress();

            return value;
        }

        private void WriteControl(byte value)
        {
            _ppuCtrl = value;
            _tempAddress = (ushort)((_tempAddress & 0xF3FF) | ((value & 0x03) << 10));
        }

        private void WriteMask(byte value)
        {
            _ppuMask = value;
        }

        private void WriteOamAddress(byte value)
        {
            _oamAddress = value;
        }

        private void WriteOamData(byte value)
        {
            byte index = _oamAddress;
            _oam[index] = value;
            _oamAddress++;
        }

        private void WriteScroll(byte value)
        {
            if (!_writeToggle)
            {
                _fineX = (byte)(value & 0x07);
                _scrollX = value;
                _tempAddress = (ushort)((_tempAddress & 0xFFE0) | (value >> 3));
                _writeToggle = true;
                return;
            }

            _scrollY = value;
            _tempAddress = (ushort)((_tempAddress & 0x8C1F) | ((value & 0x07) << 12));
            _tempAddress = (ushort)((_tempAddress & 0xFC1F) | ((value & 0xF8) << 2));
            _writeToggle = false;
        }
        
        //private void WriteScroll(byte value)
        //{
        //    if (!_writeToggle)
        //    {
        //        _fineX = (byte)(value & 0x07);
        //        _tempAddress = (ushort)((_tempAddress & 0xFFE0) | (value >> 3));
        //        _writeToggle = true;
        //        return;
        //    }

        //    _tempAddress = (ushort)((_tempAddress & 0x8C1F) | ((value & 0x07) << 12));
        //    _tempAddress = (ushort)((_tempAddress & 0xFC1F) | ((value & 0xF8) << 2));
        //    _writeToggle = false;
        //}

        private void WriteAddress(byte value)
        {
            if (!_writeToggle)
            {
                _tempAddress = (ushort)((_tempAddress & 0x00FF) | ((value & 0x3F) << 8));
                _writeToggle = true;
                return;
            }

            _tempAddress = (ushort)((_tempAddress & 0xFF00) | value);
            _vramAddress = _tempAddress;
            _writeToggle = false;
        }

        private void WriteData(byte value)
        {
            WritePpuMemory((ushort)(_vramAddress & 0x3FFF), value);
            IncrementVramAddress();
        }

        private void IncrementVramAddress()
        {
            int increment = (_ppuCtrl & 0x04) != 0 ? 32 : 1;
            _vramAddress = (ushort)((_vramAddress + increment) & 0x3FFF);
        }

        public void WriteOamByte(byte index, byte value)
        {
            _oam[index] = value;
        }

        public byte ReadOamByte(byte index)
        {
            return _oam[index];
        }

        private byte ReadPpuMemory(ushort address)
        {
            address &= 0x3FFF;

            if (address <= 0x1FFF)
                return _mapper?.PpuRead(address) ?? 0;

            if (address <= 0x2FFF)
                return ReadNametable(address);

            if (address <= 0x3EFF)
                return ReadNametable((ushort)(address - 0x1000));

            return _paletteRam[NormalizePaletteAddress(address)];
        }

        private byte ReadPpuPatternMemory(ushort address)
        {
            address &= 0x1FFF;
            _mapper?.NotifyPpuAddress(address);
            return _mapper?.PpuRead(address) ?? 0;
        }

        private void WritePpuMemory(ushort address, byte value)
        {
            address &= 0x3FFF;

            if (address <= 0x1FFF)
            {
                _mapper?.PpuWrite(address, value);
                return;
            }

            if (address <= 0x2FFF)
            {
                WriteNametable(address, value);
                return;
            }

            if (address <= 0x3EFF)
            {
                WriteNametable((ushort)(address - 0x1000), value);
                return;
            }

            int paletteIndex = NormalizePaletteAddress(address);
            _paletteRam[paletteIndex] = (byte)(value & 0x3F);
        }

        private byte ReadNametable(ushort address)
        {
            int index = GetNametableIndex(address);
            return _nametableRam[index];
        }

        private void WriteNametable(ushort address, byte value)
        {
            int index = GetNametableIndex(address);
            _nametableRam[index] = value;
        }

        private int GetNametableIndex(ushort address)
        {
            int relativeAddress = (address - 0x2000) & 0x0FFF;
            int table = relativeAddress / 0x0400;
            int offset = relativeAddress & 0x03FF;

            NesNametableMirroring mirroring = _mapper?.NametableMirroring ?? (_mapper?.VerticalMirroring == true ? NesNametableMirroring.Vertical : NesNametableMirroring.Horizontal);

            int physicalTable = mirroring switch
            {
                NesNametableMirroring.Vertical => table & 1,
                NesNametableMirroring.Horizontal => table < 2 ? 0 : 1,
                NesNametableMirroring.OneScreenLower => 0,
                NesNametableMirroring.OneScreenUpper => 1,
                NesNametableMirroring.FourScreen => table,
                _ => 0
            };

            return (physicalTable * 0x0400) + offset;
        }

        private static int NormalizePaletteAddress(ushort address)
        {
            int index = (address - 0x3F00) & 0x1F;

            if (index == 0x10)
                index = 0x00;

            if (index == 0x14)
                index = 0x04;

            if (index == 0x18)
                index = 0x08;

            if (index == 0x1C)
                index = 0x0C;

            return index;
        }

        public void ClearFrameReady()
        {
            _frameReady = false;
        }
    }
}