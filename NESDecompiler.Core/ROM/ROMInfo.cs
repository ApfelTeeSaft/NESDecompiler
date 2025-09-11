using System;
using System.Collections.Generic;

namespace NESDecompiler.Core.ROM
{
    /// <summary>
    /// Mirroring types for NES hardware
    /// </summary>
    public enum MirroringType
    {
        Horizontal,
        Vertical,
        FourScreen
    }

    /// <summary>
    /// Contains information about a loaded NES ROM
    /// </summary>
    public class ROMInfo
    {
        /// <summary>
        /// Size of the PRG ROM in bytes
        /// </summary>
        public int PRGROMSize { get; set; }

        /// <summary>
        /// Size of the CHR ROM in bytes
        /// </summary>
        public int CHRROMSize { get; set; }

        /// <summary>
        /// Mapper number
        /// </summary>
        public byte MapperNumber { get; set; }

        /// <summary>
        /// Type of mirroring used
        /// </summary>
        public MirroringType MirroringType { get; set; }

        /// <summary>
        /// Whether the ROM has battery-backed RAM
        /// </summary>
        public bool HasBatteryBackedRAM { get; set; }

        /// <summary>
        /// Whether the ROM has a trainer
        /// </summary>
        public bool HasTrainer { get; set; }

        /// <summary>
        /// Whether the ROM uses four-screen VRAM
        /// </summary>
        public bool HasFourScreenVRAM { get; set; }

        /// <summary>
        /// Whether the ROM is a VS System cartridge
        /// </summary>
        public bool IsVSSystemCart { get; set; }

        /// <summary>
        /// Offset of the PRG ROM in the file
        /// </summary>
        public int PRGROMOffset { get; set; }

        /// <summary>
        /// Offset of the CHR ROM in the file
        /// </summary>
        public int CHRROMOffset { get; set; }

        /// <summary>
        /// The Reset Vector (entry point) address
        /// </summary>
        public ushort ResetVector { get; set; }

        /// <summary>
        /// The raw ROM data for reference
        /// </summary>
        public byte[]? RawData { get; set; }

        /// <summary>
        /// List of identified entry points (including reset vector and NMI)
        /// </summary>
        public HashSet<ushort> EntryPoints { get; } = new HashSet<ushort>();

        /// <summary>
        /// Returns a string representation of the ROM information
        /// </summary>
        public override string ToString()
        {
            return $"ROM Info:\n" +
                   $"  PRG ROM: {PRGROMSize} bytes\n" +
                   $"  CHR ROM: {CHRROMSize} bytes\n" +
                   $"  Mapper: {MapperNumber}\n" +
                   $"  Mirroring: {MirroringType}\n" +
                   $"  Battery-Backed RAM: {HasBatteryBackedRAM}\n" +
                   $"  Reset Vector: ${ResetVector:X4}";
        }
    }
}