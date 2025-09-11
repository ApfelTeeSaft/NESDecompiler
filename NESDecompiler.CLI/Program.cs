using System;
using System.IO;
using CommandLine;
using NESDecompiler.Core.ROM;
using NESDecompiler.Core.Disassembly;
using NESDecompiler.Core.Decompilation;
using NESDecompiler.Core.Exceptions;
using System.Runtime.InteropServices;

namespace NESDecompiler.CLI
{
    /// <summary>
    /// Command-line options for the decompiler
    /// </summary>
    class Options
    {
        [Option('i', "input", Required = true, HelpText = "Input NES ROM file path")]
        public string InputFile { get; set; } = string.Empty;

        [Option('o', "output", Required = false, HelpText = "Output directory for generated files")]
        public string? OutputDirectory { get; set; }

        [Option('d', "disassemble", Required = false, Default = false, HelpText = "Generate disassembly output")]
        public bool GenerateDisassembly { get; set; }

        [Option('c', "decompile", Required = false, Default = true, HelpText = "Generate C code output")]
        public bool GenerateCCode { get; set; }

        [Option('v', "verbose", Required = false, Default = false, HelpText = "Set output to verbose messages")]
        public bool Verbose { get; set; }
    }

    /// <summary>
    /// Main program class for the CLI interface
    /// </summary>
    class Program
    {
        /// <summary>
        /// Entry point for the CLI application
        /// </summary>
        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<Options>(args)
                .MapResult(
                    options => RunDecompiler(options),
                    errors => 1
                );
        }

        /// <summary>
        /// Runs the decompiler with the specified options
        /// </summary>
        /// <param name="options">Command-line options</param>
        /// <returns>0 for success, non-zero for failure</returns>
        static int RunDecompiler(Options options)
        {
            try
            {
                Console.WriteLine($"NES ROM Decompiler");
                Console.WriteLine($"=================");
                Console.WriteLine();

                if (!File.Exists(options.InputFile))
                {
                    Console.Error.WriteLine($"Error: Input file '{options.InputFile}' does not exist");
                    return 1;
                }

                string outputDirectory = options.OutputDirectory ?? Path.GetDirectoryName(options.InputFile) ?? ".";
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                Console.WriteLine($"Loading ROM: {options.InputFile}");
                var romLoader = new ROMLoader();
                var romInfo = romLoader.LoadFromFile(options.InputFile);

                if (options.Verbose)
                {
                    Console.WriteLine(romInfo.ToString());
                }

                byte[] prgRomData = romLoader.GetPRGROMData();

                Console.WriteLine("Disassembling code...");
                var disassembler = new Disassembler(romInfo, prgRomData);
                disassembler.Disassemble();

                if (options.Verbose)
                {
                    Console.WriteLine($"Disassembled {disassembler.Instructions.Count} instructions");
                }

                if (options.GenerateDisassembly)
                {
                    string disassemblyFile = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(options.InputFile) + ".asm");
                    Console.WriteLine($"Generating disassembly: {disassemblyFile}");

                    string disassembly = disassembler.ToAssemblyString();
                    File.WriteAllText(disassemblyFile, disassembly);
                }

                if (options.GenerateCCode)
                {
                    Console.WriteLine("Decompiling to C code...");
                    var decompiler = new Decompiler(romInfo, disassembler);
                    decompiler.Decompile();

                    if (options.Verbose)
                    {
                        Console.WriteLine($"Identified {decompiler.Variables.Count} variables and {decompiler.Functions.Count} functions");
                    }

                    string cCodeFile = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(options.InputFile) + ".c");
                    Console.WriteLine($"Generating C code: {cCodeFile}");

                    string cCode = decompiler.GenerateCCode();
                    File.WriteAllText(cCodeFile, cCode);

                    string headerFile = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(options.InputFile) + ".h");
                    Console.WriteLine($"Generating header file: {headerFile}");

                    string headerCode = GenerateHeaderFile(decompiler);
                    File.WriteAllText(headerFile, headerCode);
                }

                Console.WriteLine("Decompilation completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                Exception currentEx = ex;
                Console.Error.WriteLine($"Error: {currentEx.Message}");

                if (options.Verbose)
                {
                    while (currentEx.InnerException != null)
                    {
                        currentEx = currentEx.InnerException;
                        Console.Error.WriteLine($"  Caused by: {currentEx.Message}");
                    }

                    Console.Error.WriteLine();
                    Console.Error.WriteLine("Stack trace:");
                    Console.Error.WriteLine(ex.StackTrace);
                }

                return 1;
            }
        }

        /// <summary>
        /// Generates a C header file for the decompiled ROM
        /// </summary>
        /// <param name="decompiler">The decompiler instance</param>
        /// <returns>The generated header code</returns>
        static string GenerateHeaderFile(Decompiler decompiler)
        {
            StringWriter writer = new StringWriter();

            writer.WriteLine("/*");
            writer.WriteLine(" * Decompiled NES ROM");
            writer.WriteLine($" * ROM: {decompiler.ROMInfo}");
            writer.WriteLine(" */");
            writer.WriteLine();

            string guardName = Path.GetFileNameWithoutExtension(decompiler.ROMInfo.RawData![0].ToString()).ToUpper() + "_H";
            writer.WriteLine($"#ifndef {guardName}");
            writer.WriteLine($"#define {guardName}");
            writer.WriteLine();

            writer.WriteLine("#include <stdint.h>");
            writer.WriteLine("#include <stdbool.h>");
            writer.WriteLine();

            writer.WriteLine("// NES Hardware Registers");
            writer.WriteLine("#define PPUCTRL   (*((volatile uint8_t*)0x2000))");
            writer.WriteLine("#define PPUMASK   (*((volatile uint8_t*)0x2001))");
            writer.WriteLine("#define PPUSTATUS (*((volatile uint8_t*)0x2002))");
            writer.WriteLine("#define OAMADDR   (*((volatile uint8_t*)0x2003))");
            writer.WriteLine("#define OAMDATA   (*((volatile uint8_t*)0x2004))");
            writer.WriteLine("#define PPUSCROLL (*((volatile uint8_t*)0x2005))");
            writer.WriteLine("#define PPUADDR   (*((volatile uint8_t*)0x2006))");
            writer.WriteLine("#define PPUDATA   (*((volatile uint8_t*)0x2007))");
            writer.WriteLine("#define OAMDMA    (*((volatile uint8_t*)0x4014))");
            writer.WriteLine("#define SND_CHN   (*((volatile uint8_t*)0x4015))");
            writer.WriteLine("#define JOY1      (*((volatile uint8_t*)0x4016))");
            writer.WriteLine("#define JOY2      (*((volatile uint8_t*)0x4017))");
            writer.WriteLine();

            writer.WriteLine("// Variables");
            foreach (var variable in decompiler.Variables.Values)
            {
                if (variable.Address < 0x2000 || variable.Address >= 0x8000)
                {
                    writer.WriteLine($"extern {variable.GetCType()} {variable.Name};");
                }
            }
            writer.WriteLine();

            writer.WriteLine("// Functions");
            foreach (var function in decompiler.Functions.Values)
            {
                writer.WriteLine($"void {function.Name}();");
            }
            writer.WriteLine();

            writer.WriteLine($"#endif // {guardName}");

            return writer.ToString();
        }
    }
}