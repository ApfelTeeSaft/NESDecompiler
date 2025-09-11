using System;
using System.IO;
using System.Text;
using NESDecompiler.Core.Exceptions;

namespace NESDecompiler.Core.ROM
{
    /// <summary>
    /// Handles loading and parsing NES ROM files
    /// </summary>
    public class ROMLoader
    {
        // iNES header constants
        private const int HEADER_SIZE = 16;
        private const int PRG_ROM_SIZE_OFFSET = 4;
        private const int CHR_ROM_SIZE_OFFSET = 5;
        private const int FLAGS_6_OFFSET = 6;
        private const int FLAGS_7_OFFSET = 7;
        private const int PRG_RAM_SIZE_OFFSET = 8;
        private const int FLAGS_9_OFFSET = 9;
        private const int FLAGS_10_OFFSET = 10;

        // ROM data
        private byte[]? romData;
        private ROMInfo? romInfo;

        /// <summary>
        /// Information about the loaded ROM
        /// </summary>
        public ROMInfo? ROMInfo => romInfo;

        /// <summary>
        /// Loads a NES ROM file from disk
        /// </summary>
        /// <param name="filePath">Path to the ROM file</param>
        /// <returns>Information about the loaded ROM</returns>
        public ROMInfo LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("ROM file not found", filePath);
            }

            try
            {
                romData = File.ReadAllBytes(filePath);
                return ParseROMHeader();
            }
            catch (Exception ex) when (ex is not ROMException)
            {
                throw new ROMException($"Failed to load ROM file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads a NES ROM from a byte array
        /// </summary>
        /// <param name="data">The ROM data</param>
        /// <returns>Information about the loaded ROM</returns>
        public ROMInfo LoadFromBytes(byte[] data)
        {
            if (data == null || data.Length < HEADER_SIZE)
            {
                throw new ROMException("Invalid ROM data: too short");
            }

            romData = data;
            return ParseROMHeader();
        }

        /// <summary>
        /// Parses the iNES ROM header
        /// </summary>
        /// <returns>Information about the ROM</returns>
        private ROMInfo ParseROMHeader()
        {
            // Verify iNES header signature "NES" followed by MS-DOS EOF
            if (romData!.Length < HEADER_SIZE ||
                romData[0] != 0x4E || romData[1] != 0x45 || romData[2] != 0x53 || romData[3] != 0x1A)
            {
                throw new InvalidROMFormatException("Invalid iNES ROM header");
            }

            romInfo = new ROMInfo
            {
                PRGROMSize = romData[PRG_ROM_SIZE_OFFSET] * 16384, // 16KB units
                CHRROMSize = romData[CHR_ROM_SIZE_OFFSET] * 8192,  // 8KB units

                MapperNumber = (byte)((romData[FLAGS_7_OFFSET] & 0xF0) | ((romData[FLAGS_6_OFFSET] & 0xF0) >> 4)),

                MirroringType = (romData[FLAGS_6_OFFSET] & 0x01) == 0
                    ? MirroringType.Horizontal
                    : MirroringType.Vertical,

                HasBatteryBackedRAM = (romData[FLAGS_6_OFFSET] & 0x02) != 0,

                HasTrainer = (romData[FLAGS_6_OFFSET] & 0x04) != 0,

                HasFourScreenVRAM = (romData[FLAGS_6_OFFSET] & 0x08) != 0,

                IsVSSystemCart = (romData[FLAGS_7_OFFSET] & 0x01) != 0,

                PRGROMOffset = HEADER_SIZE + (((romData[FLAGS_6_OFFSET] & 0x04) != 0) ? 512 : 0),

                RawData = romData
            };

            romInfo.CHRROMOffset = romInfo.PRGROMOffset + romInfo.PRGROMSize;

            // Identify entry points (reset vector)
            if (romInfo.PRGROMSize > 0)
            {
                // In 6502, reset vector is at 0xFFFC-0xFFFD
                // For NES, this is mapped to the end of the first PRG ROM bank
                int resetVectorOffset = romInfo.PRGROMOffset + romInfo.PRGROMSize - 4;
                if (resetVectorOffset >= 0 && resetVectorOffset < romData.Length - 1)
                {
                    romInfo.ResetVector = (ushort)(romData[resetVectorOffset] | (romData[resetVectorOffset + 1] << 8));
                }
            }

            return romInfo;
        }

        /// <summary>
        /// Gets a segment of the ROM data
        /// </summary>
        /// <param name="offset">Starting offset</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>The requested data segment</returns>
        public byte[] GetROMSegment(int offset, int length)
        {
            if (romData == null)
                throw new ROMException("No ROM data loaded");

            if (offset < 0 || offset + length > romData.Length)
                throw new ROMException("Requested segment is out of bounds");

            byte[] segment = new byte[length];
            Array.Copy(romData, offset, segment, 0, length);
            return segment;
        }

        /// <summary>
        /// Gets the PRG ROM data
        /// </summary>
        /// <returns>The PRG ROM data</returns>
        public byte[] GetPRGROMData()
        {
            if (romInfo == null)
                throw new ROMException("No ROM data loaded");

            return GetROMSegment(romInfo.PRGROMOffset, romInfo.PRGROMSize);
        }

        /// <summary>
        /// Gets the CHR ROM data
        /// </summary>
        /// <returns>The CHR ROM data</returns>
        public byte[] GetCHRROMData()
        {
            if (romInfo == null)
                throw new ROMException("No ROM data loaded");

            if (romInfo.CHRROMSize == 0)
                return Array.Empty<byte>();

            return GetROMSegment(romInfo.CHRROMOffset, romInfo.CHRROMSize);
        }
    }
}