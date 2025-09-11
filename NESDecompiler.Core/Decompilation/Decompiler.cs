using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NESDecompiler.Core.CPU;
using NESDecompiler.Core.Disassembly;
using NESDecompiler.Core.Exceptions;
using NESDecompiler.Core.ROM;

namespace NESDecompiler.Core.Decompilation
{
    /// <summary>
    /// Type of variable in the decompiled code
    /// </summary>
    public enum VariableType
    {
        Byte,
        Word,
        Array,
        Pointer,
        Unknown
    }

    /// <summary>
    /// A variable identified during decompilation
    /// </summary>
    public class Variable
    {
        /// <summary>
        /// The CPU address of this variable
        /// </summary>
        public ushort Address { get; set; }

        /// <summary>
        /// The name of this variable
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of this variable
        /// </summary>
        public VariableType Type { get; set; }

        /// <summary>
        /// The size of this variable in bytes
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Whether this variable is accessed for reading
        /// </summary>
        public bool IsRead { get; set; }

        /// <summary>
        /// Whether this variable is accessed for writing
        /// </summary>
        public bool IsWritten { get; set; }

        /// <summary>
        /// Creates a new variable
        /// </summary>
        public Variable(ushort address, string name, VariableType type, int size = 1)
        {
            Address = address;
            Name = name;
            Type = type;
            Size = size;
        }

        /// <summary>
        /// Returns the C type of this variable
        /// </summary>
        public string GetCType()
        {
            return Type switch
            {
                VariableType.Byte => "uint8_t",
                VariableType.Word => "uint16_t",
                VariableType.Pointer => "uint8_t*",
                VariableType.Array => "uint8_t",
                _ => "uint8_t"
            };
        }
    }

    /// <summary>
    /// Represents a decompiled function
    /// </summary>
    public class Function
    {
        /// <summary>
        /// The address of this function
        /// </summary>
        public ushort Address { get; set; }

        /// <summary>
        /// The name of this function
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The addresses of instructions in this function
        /// </summary>
        public HashSet<ushort> Instructions { get; } = new HashSet<ushort>();

        /// <summary>
        /// The variables accessed by this function
        /// </summary>
        public HashSet<ushort> VariablesAccessed { get; } = new HashSet<ushort>();

        /// <summary>
        /// The functions called by this function
        /// </summary>
        public HashSet<ushort> CalledFunctions { get; } = new HashSet<ushort>();

        /// <summary>
        /// Creates a new function
        /// </summary>
        public Function(ushort address, string name)
        {
            Address = address;
            Name = name;
        }
    }

    /// <summary>
    /// A block of code representing a logical unit
    /// </summary>
    public class CodeBlock
    {
        /// <summary>
        /// The starting address of this block
        /// </summary>
        public ushort StartAddress { get; set; }

        /// <summary>
        /// The ending address of this block
        /// </summary>
        public ushort EndAddress { get; set; }

        /// <summary>
        /// The instructions in this block
        /// </summary>
        public List<DisassembledInstruction> Instructions { get; } = new List<DisassembledInstruction>();

        /// <summary>
        /// The blocks that can follow this one
        /// </summary>
        public List<CodeBlock> Successors { get; } = new List<CodeBlock>();

        /// <summary>
        /// Creates a new code block
        /// </summary>
        public CodeBlock(ushort startAddress)
        {
            StartAddress = startAddress;
            EndAddress = startAddress;
        }
    }

    public class WorkspaceFile
    {
        public string CurrentFilePath { get; set; } = string.Empty;
        public List<string> RecentFiles { get; set; } = new List<string>();
        public bool IsDisassembled { get; set; }
        public bool IsDecompiled { get; set; }
        public Dictionary<string, VariableWorkspaceData> Variables { get; set; } = new Dictionary<string, VariableWorkspaceData>();
        public Dictionary<string, FunctionWorkspaceData> Functions { get; set; } = new Dictionary<string, FunctionWorkspaceData>();
    }

    public class VariableWorkspaceData
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class FunctionWorkspaceData
    {
        public string Name { get; set; } = string.Empty;
        public string ReturnType { get; set; } = string.Empty;
        public List<string> Parameters { get; set; } = new List<string>();
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Decompiles 6502 assembly code to C code
    /// </summary>
    public class Decompiler
    {
        private readonly ROMInfo romInfo;
        private readonly Disassembler disassembler;
        private readonly Dictionary<ushort, Variable> variables;
        private readonly Dictionary<ushort, Function> functions;
        private readonly Dictionary<ushort, CodeBlock> codeBlocks;
        private int variableCounter;

        /// <summary>
        /// Information about the ROM being decompiled
        /// </summary>
        public ROMInfo ROMInfo => romInfo;

        /// <summary>
        /// The disassembler used by this decompiler
        /// </summary>
        public Disassembler Disassembler => disassembler;

        /// <summary>
        /// The variables identified during decompilation
        /// </summary>
        public IReadOnlyDictionary<ushort, Variable> Variables => variables;

        /// <summary>
        /// The functions identified during decompilation
        /// </summary>
        public IReadOnlyDictionary<ushort, Function> Functions => functions;

        /// <summary>
        /// The code blocks identified during decompilation
        /// </summary>
        public IReadOnlyDictionary<ushort, CodeBlock> CodeBlocks => codeBlocks;

        /// <summary>
        /// Creates a new decompiler for the specified ROM
        /// </summary>
        /// <param name="romInfo">Information about the ROM</param>
        /// <param name="disassembler">The disassembler to use</param>
        public Decompiler(ROMInfo romInfo, Disassembler disassembler)
        {
            this.romInfo = romInfo ?? throw new ArgumentNullException(nameof(romInfo));
            this.disassembler = disassembler ?? throw new ArgumentNullException(nameof(disassembler));

            variables = new Dictionary<ushort, Variable>();
            functions = new Dictionary<ushort, Function>();
            codeBlocks = new Dictionary<ushort, CodeBlock>();
            variableCounter = 0;
        }

        /// <summary>
        /// Decompiles the ROM
        /// </summary>
        public void Decompile()
        {
            try
            {
                AnalyzeControlFlow();
                IdentifyVariables();
                IdentifyFunctions();
                AnalyzeDataDependencies();
            }
            catch (Exception ex)
            {
                throw new DecompilationException($"Error during decompilation: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Analyzes the control flow of the program
        /// </summary>
        private void AnalyzeControlFlow()
        {
            try
            {
                IdentifyBasicBlocks();
                BuildControlFlowGraph();
            }
            catch (Exception ex)
            {
                throw new DecompilationException($"Error during control flow analysis: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Identifies basic blocks in the code
        /// </summary>
        private void IdentifyBasicBlocks()
        {
            try
            {
                // A basic block is a sequence of instructions with no branches in or out
                // except at the beginning and end

                var leaders = new HashSet<ushort>();

                if (disassembler.Instructions.Count > 0)
                {
                    leaders.Add(disassembler.Instructions[0].CPUAddress);
                }

                foreach (var entryPoint in disassembler.EntryPoints)
                {
                    leaders.Add(entryPoint);
                }

                foreach (var instruction in disassembler.Instructions)
                {
                    if (instruction.IsBranch || instruction.IsJump)
                    {
                        if (instruction.TargetAddress.HasValue)
                        {
                            leaders.Add(instruction.TargetAddress.Value);
                        }

                        if (instruction.Info.Mnemonic != "JMP")
                        {
                            ushort nextAddress = (ushort)(instruction.CPUAddress + instruction.Info.Size);
                            leaders.Add(nextAddress);
                        }
                    }
                }

                ushort currentStart = 0;

                foreach (var instruction in disassembler.Instructions.OrderBy(i => i.CPUAddress))
                {
                    ushort address = instruction.CPUAddress;

                    if (leaders.Contains(address))
                    {
                        var newBlock = new CodeBlock(address);
                        codeBlocks[address] = newBlock;
                        currentStart = address;
                    }

                    if (codeBlocks.TryGetValue(currentStart, out var currentBlock))
                    {
                        currentBlock.Instructions.Add(instruction);
                        currentBlock.EndAddress = address;
                    }

                    if (instruction.IsBranch || instruction.IsJump || instruction.IsFunctionExit)
                    {
                        currentStart = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new DecompilationException($"Error during basic block identification: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Builds the control flow graph
        /// </summary>
        private void BuildControlFlowGraph()
        {
            try
            {
                foreach (var block in codeBlocks.Values)
                {
                    if (block.Instructions.Count == 0)
                    {
                        continue;
                    }

                    var lastInstruction = block.Instructions[^1];

                    if (lastInstruction.IsBranch)
                    {
                        if (lastInstruction.TargetAddress.HasValue)
                        {
                            ushort targetAddress = lastInstruction.TargetAddress.Value;
                            if (codeBlocks.TryGetValue(targetAddress, out var targetBlock))
                            {
                                block.Successors.Add(targetBlock);
                            }
                        }
                        ushort nextAddress = (ushort)(lastInstruction.CPUAddress + lastInstruction.Info.Size);
                        if (codeBlocks.TryGetValue(nextAddress, out var nextBlock))
                        {
                            block.Successors.Add(nextBlock);
                        }
                    }
                    else if (lastInstruction.Info.Mnemonic == "JMP")
                    {
                        if (lastInstruction.TargetAddress.HasValue)
                        {
                            ushort targetAddress = lastInstruction.TargetAddress.Value;
                            if (codeBlocks.TryGetValue(targetAddress, out var targetBlock))
                            {
                                block.Successors.Add(targetBlock);
                            }
                        }
                    }
                    else if (lastInstruction.Info.Mnemonic == "JSR")
                    {
                        ushort nextAddress = (ushort)(lastInstruction.CPUAddress + lastInstruction.Info.Size);
                        if (codeBlocks.TryGetValue(nextAddress, out var nextBlock))
                        {
                            block.Successors.Add(nextBlock);
                        }
                    }
                    else if (!lastInstruction.IsFunctionExit)
                    {
                        ushort nextAddress = (ushort)(lastInstruction.CPUAddress + lastInstruction.Info.Size);
                        if (codeBlocks.TryGetValue(nextAddress, out var nextBlock))
                        {
                            block.Successors.Add(nextBlock);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new DecompilationException($"Error during control flow graph construction: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Identifies variables accessed by the code
        /// </summary>
        private void IdentifyVariables()
        {
            try
            {
                // NES memory map
                Dictionary<ushort, string> knownAddresses = new Dictionary<ushort, string>
                {
                    { 0x2000, "PPUCTRL" },
                    { 0x2001, "PPUMASK" },
                    { 0x2002, "PPUSTATUS" },
                    { 0x2003, "OAMADDR" },
                    { 0x2004, "OAMDATA" },
                    { 0x2005, "PPUSCROLL" },
                    { 0x2006, "PPUADDR" },
                    { 0x2007, "PPUDATA" },
                    { 0x4000, "SQ1_VOL" },
                    { 0x4001, "SQ1_SWEEP" },
                    { 0x4002, "SQ1_LO" },
                    { 0x4003, "SQ1_HI" },
                    { 0x4004, "SQ2_VOL" },
                    { 0x4005, "SQ2_SWEEP" },
                    { 0x4006, "SQ2_LO" },
                    { 0x4007, "SQ2_HI" },
                    { 0x4008, "TRI_LINEAR" },
                    { 0x400A, "TRI_LO" },
                    { 0x400B, "TRI_HI" },
                    { 0x400C, "NOISE_VOL" },
                    { 0x400E, "NOISE_LO" },
                    { 0x400F, "NOISE_HI" },
                    { 0x4010, "DMC_FREQ" },
                    { 0x4011, "DMC_RAW" },
                    { 0x4012, "DMC_START" },
                    { 0x4013, "DMC_LEN" },
                    { 0x4014, "OAMDMA" },
                    { 0x4015, "SND_CHN" },
                    { 0x4016, "JOY1" },
                    { 0x4017, "JOY2" }
                };

                foreach (var instruction in disassembler.Instructions)
                {
                    if (instruction.Info.AddressingMode == AddressingMode.Implied ||
                        instruction.Info.AddressingMode == AddressingMode.Accumulator ||
                        instruction.Info.AddressingMode == AddressingMode.Immediate ||
                        instruction.Info.AddressingMode == AddressingMode.Relative)
                    {
                        continue;
                    }

                    ushort? address = null;

                    if (instruction.Info.AddressingMode == AddressingMode.ZeroPage ||
                        instruction.Info.AddressingMode == AddressingMode.ZeroPageX ||
                        instruction.Info.AddressingMode == AddressingMode.ZeroPageY)
                    {
                        address = instruction.Operands[0];
                    }
                    else if (instruction.Info.AddressingMode == AddressingMode.Absolute ||
                             instruction.Info.AddressingMode == AddressingMode.AbsoluteX ||
                             instruction.Info.AddressingMode == AddressingMode.AbsoluteY)
                    {
                        address = (ushort)((instruction.Operands[1] << 8) | instruction.Operands[0]);
                    }
                    else if (instruction.Info.AddressingMode == AddressingMode.IndexedIndirect ||
                             instruction.Info.AddressingMode == AddressingMode.IndirectIndexed)
                    {
                        address = instruction.Operands[0];
                    }

                    if (address.HasValue)
                    {
                        ushort addr = address.Value;

                        if (!variables.TryGetValue(addr, out var variable))
                        {
                            string name;

                            if (knownAddresses.TryGetValue(addr, out var knownName))
                            {
                                name = knownName;
                            }
                            else if (addr < 0x100)
                            {
                                // Zero page
                                name = $"zp_{addr:X2}";
                            }
                            else if (addr < 0x800)
                            {
                                // RAM
                                name = $"ram_{addr:X4}";
                            }
                            else if (addr >= 0x8000)
                            {
                                // ROM
                                name = $"rom_{addr:X4}";
                            }
                            else
                            {
                                // Other memory
                                name = $"var_{variableCounter++:X4}";
                            }

                            VariableType type = VariableType.Byte;

                            if (instruction.Info.AddressingMode == AddressingMode.IndexedIndirect ||
                                instruction.Info.AddressingMode == AddressingMode.IndirectIndexed)
                            {
                                type = VariableType.Pointer;
                            }

                            variable = new Variable(addr, name, type);
                            variables[addr] = variable;
                        }

                        if (instruction.Info.Type == InstructionType.Store)
                        {
                            variable.IsWritten = true;
                        }
                        else
                        {
                            variable.IsRead = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new DecompilationException($"Error during variable identification: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Identifies functions in the code
        /// </summary>
        private void IdentifyFunctions()
        {
            try
            {
                foreach (var entryPoint in disassembler.EntryPoints)
                {
                    if (!functions.ContainsKey(entryPoint))
                    {
                        string name = disassembler.Labels.TryGetValue(entryPoint, out var label) ?
                            label : $"func_{entryPoint:X4}";

                        functions[entryPoint] = new Function(entryPoint, name);
                    }
                }

                foreach (var function in functions.Values.ToList())
                {
                    AnalyzeFunction(function);
                }
            }
            catch (Exception ex)
            {
                throw new DecompilationException($"Error during function identification: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Analyzes a function to identify its instructions, variables, and called functions
        /// </summary>
        /// <param name="function">The function to analyze</param>
        private void AnalyzeFunction(Function function)
        {
            try
            {
                var toProcess = new Queue<ushort>();
                toProcess.Enqueue(function.Address);

                while (toProcess.Count > 0)
                {
                    ushort address = toProcess.Dequeue();

                    if (function.Instructions.Contains(address))
                    {
                        continue;
                    }

                    if (!disassembler.AddressToInstruction.TryGetValue(address, out var instruction))
                    {
                        continue;
                    }

                    function.Instructions.Add(address);

                    if (instruction.Info.AddressingMode != AddressingMode.Implied &&
                        instruction.Info.AddressingMode != AddressingMode.Accumulator &&
                        instruction.Info.AddressingMode != AddressingMode.Immediate &&
                        instruction.Info.AddressingMode != AddressingMode.Relative)
                    {
                        ushort? varAddress = null;

                        if (instruction.Info.AddressingMode == AddressingMode.ZeroPage ||
                            instruction.Info.AddressingMode == AddressingMode.ZeroPageX ||
                            instruction.Info.AddressingMode == AddressingMode.ZeroPageY)
                        {
                            varAddress = instruction.Operands[0];
                        }
                        else if (instruction.Info.AddressingMode == AddressingMode.Absolute ||
                                 instruction.Info.AddressingMode == AddressingMode.AbsoluteX ||
                                 instruction.Info.AddressingMode == AddressingMode.AbsoluteY)
                        {
                            varAddress = (ushort)((instruction.Operands[1] << 8) | instruction.Operands[0]);
                        }
                        else if (instruction.Info.AddressingMode == AddressingMode.IndexedIndirect ||
                                 instruction.Info.AddressingMode == AddressingMode.IndirectIndexed)
                        {
                            varAddress = instruction.Operands[0];
                        }

                        if (varAddress.HasValue)
                        {
                            function.VariablesAccessed.Add(varAddress.Value);
                        }
                    }

                    if (instruction.Info.Mnemonic == "JSR" && instruction.TargetAddress.HasValue)
                    {
                        ushort target = instruction.TargetAddress.Value;

                        function.CalledFunctions.Add(target);

                        if (!functions.ContainsKey(target))
                        {
                            string name = disassembler.Labels.TryGetValue(target, out var label) ?
                                label : $"func_{target:X4}";

                            functions[target] = new Function(target, name);

                            AnalyzeFunction(functions[target]);
                        }

                        ushort nextAddress = (ushort)(address + instruction.Info.Size);
                        toProcess.Enqueue(nextAddress);
                    }
                    else if (instruction.Info.Mnemonic == "JMP" && instruction.TargetAddress.HasValue)
                    {
                        ushort target = instruction.TargetAddress.Value;
                        toProcess.Enqueue(target);
                    }
                    else if (instruction.IsBranch && instruction.TargetAddress.HasValue)
                    {
                        ushort target = instruction.TargetAddress.Value;
                        ushort nextAddress = (ushort)(address + instruction.Info.Size);

                        toProcess.Enqueue(target);
                        toProcess.Enqueue(nextAddress);
                    }
                    else if (!instruction.IsFunctionExit)
                    {
                        ushort nextAddress = (ushort)(address + instruction.Info.Size);
                        toProcess.Enqueue(nextAddress);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new DecompilationException($"Error during function analysis: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Analyzes data dependencies between functions
        /// </summary>
        private void AnalyzeDataDependencies()
        {
            try
            {
                foreach (var function in functions.Values)
                {
                    foreach (var varAddress in function.VariablesAccessed)
                    {
                        if (variables.TryGetValue(varAddress, out var variable))
                        {
                            foreach (var instructionAddress in function.Instructions)
                            {
                                var instruction = disassembler.AddressToInstruction[instructionAddress];

                                ushort? accessedAddress = null;

                                if (instruction.Info.AddressingMode == AddressingMode.ZeroPage ||
                                    instruction.Info.AddressingMode == AddressingMode.ZeroPageX ||
                                    instruction.Info.AddressingMode == AddressingMode.ZeroPageY)
                                {
                                    accessedAddress = instruction.Operands[0];
                                }
                                else if (instruction.Info.AddressingMode == AddressingMode.Absolute ||
                                         instruction.Info.AddressingMode == AddressingMode.AbsoluteX ||
                                         instruction.Info.AddressingMode == AddressingMode.AbsoluteY)
                                {
                                    accessedAddress = (ushort)((instruction.Operands[1] << 8) | instruction.Operands[0]);
                                }
                                else if (instruction.Info.AddressingMode == AddressingMode.IndexedIndirect ||
                                         instruction.Info.AddressingMode == AddressingMode.IndirectIndexed)
                                {
                                    accessedAddress = instruction.Operands[0];
                                }

                                if (accessedAddress == varAddress)
                                {
                                    if (instruction.Info.Type == InstructionType.Store)
                                    {
                                        variable.IsWritten = true;
                                    }
                                    else
                                    {
                                        variable.IsRead = true;
                                    }

                                    if (instruction.Info.AddressingMode == AddressingMode.AbsoluteX ||
                                        instruction.Info.AddressingMode == AddressingMode.AbsoluteY ||
                                        instruction.Info.AddressingMode == AddressingMode.ZeroPageX ||
                                        instruction.Info.AddressingMode == AddressingMode.ZeroPageY ||
                                        instruction.Info.AddressingMode == AddressingMode.IndexedIndirect ||
                                        instruction.Info.AddressingMode == AddressingMode.IndirectIndexed)
                                    {
                                        if (variable.Type != VariableType.Pointer)
                                        {
                                            variable.Type = VariableType.Array;
                                            variable.Size = 256;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new DecompilationException($"Error during data dependency analysis: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generates C code for the decompiled ROM
        /// </summary>
        /// <returns>The generated C code</returns>
        public string GenerateCCode()
        {
            try
            {
                var sb = new StringBuilder();

                sb.AppendLine("/*");
                sb.AppendLine(" * Decompiled NES ROM");
                sb.AppendLine($" * ROM: {romInfo}");
                sb.AppendLine(" */");
                sb.AppendLine();

                sb.AppendLine("#include <stdint.h>");
                sb.AppendLine("#include <stdbool.h>");
                sb.AppendLine("#include <stdlib.h>");
                sb.AppendLine("#include <string.h>");
                sb.AppendLine();

                sb.AppendLine("// 6502 CPU Status Flag Constants");
                sb.AppendLine("#define CARRY_FLAG     0x01");
                sb.AppendLine("#define ZERO_FLAG      0x02");
                sb.AppendLine("#define INTERRUPT_FLAG 0x04");
                sb.AppendLine("#define DECIMAL_FLAG   0x08");
                sb.AppendLine("#define BREAK_FLAG     0x10");
                sb.AppendLine("#define UNUSED_FLAG    0x20");
                sb.AppendLine("#define OVERFLOW_FLAG  0x40");
                sb.AppendLine("#define NEGATIVE_FLAG  0x80");
                sb.AppendLine();

                sb.AppendLine("// CPU Registers");
                sb.AppendLine("static uint8_t a;      // Accumulator");
                sb.AppendLine("static uint8_t x;      // X Register");
                sb.AppendLine("static uint8_t y;      // Y Register");
                sb.AppendLine("static uint8_t status; // Status Register");
                sb.AppendLine("static uint16_t pc;    // Program Counter");
                sb.AppendLine("static uint8_t sp;     // Stack Pointer");
                sb.AppendLine();

                sb.AppendLine("// Memory");
                sb.AppendLine("static uint8_t memory[0x10000]; // 64KB memory");
                sb.AppendLine("static uint8_t stack[0x100];    // Stack (0x0100-0x01FF)");
                sb.AppendLine();

                sb.AppendLine("// NES Hardware Registers");
                sb.AppendLine("#define PPUCTRL   (*((volatile uint8_t*)0x2000))");
                sb.AppendLine("#define PPUMASK   (*((volatile uint8_t*)0x2001))");
                sb.AppendLine("#define PPUSTATUS (*((volatile uint8_t*)0x2002))");
                sb.AppendLine("#define OAMADDR   (*((volatile uint8_t*)0x2003))");
                sb.AppendLine("#define OAMDATA   (*((volatile uint8_t*)0x2004))");
                sb.AppendLine("#define PPUSCROLL (*((volatile uint8_t*)0x2005))");
                sb.AppendLine("#define PPUADDR   (*((volatile uint8_t*)0x2006))");
                sb.AppendLine("#define PPUDATA   (*((volatile uint8_t*)0x2007))");
                sb.AppendLine("#define OAMDMA    (*((volatile uint8_t*)0x4014))");
                sb.AppendLine("#define SND_CHN   (*((volatile uint8_t*)0x4015))");
                sb.AppendLine("#define JOY1      (*((volatile uint8_t*)0x4016))");
                sb.AppendLine("#define JOY2      (*((volatile uint8_t*)0x4017))");
                sb.AppendLine();

                sb.AppendLine("// Variables");
                foreach (var variable in variables.Values)
                {
                    if (variable.Address < 0x2000 || variable.Address >= 0x8000)
                    {
                        sb.AppendLine($"static {variable.GetCType()} {variable.Name}[256];");
                    }
                }
                sb.AppendLine();

                sb.AppendLine("// Function prototypes");
                foreach (var function in functions.Values)
                {
                    sb.AppendLine($"void {function.Name}();");
                }
                sb.AppendLine();

                foreach (var function in functions.Values)
                {
                    sb.AppendLine($"void {function.Name}() {{");

                    GenerateFunctionBody(function, sb);

                    sb.AppendLine("}");
                    sb.AppendLine();
                }

                sb.AppendLine("int main() {");
                sb.AppendLine("    // Initialize CPU state");
                sb.AppendLine("    a = 0;");
                sb.AppendLine("    x = 0;");
                sb.AppendLine("    y = 0;");
                sb.AppendLine("    status = UNUSED_FLAG; // Bit 5 is always set");
                sb.AppendLine("    sp = 0xFF;");
                sb.AppendLine("    pc = 0x8000; // Start of PRG ROM");
                sb.AppendLine();
                sb.AppendLine("    // Initialize memory");
                sb.AppendLine("    memset(memory, 0, sizeof(memory));");
                sb.AppendLine("    memset(stack, 0, sizeof(stack));");
                sb.AppendLine();

                if (functions.TryGetValue(romInfo.ResetVector, out var resetFunction))
                {
                    sb.AppendLine($"    // Call the reset function");
                    sb.AppendLine($"    {resetFunction.Name}();");
                    sb.AppendLine();
                }

                sb.AppendLine("    // Main loop");
                sb.AppendLine("    while (1) {");
                sb.AppendLine("        // Handle NMI, IRQ, etc.");
                sb.AppendLine("        // Read input");
                sb.AppendLine("        // Update game state");
                sb.AppendLine("        // Render graphics");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    return 0;");
                sb.AppendLine("}");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new DecompilationException($"Error during C code generation: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generates C code for a function
        /// </summary>
        /// <param name="function">The function to generate code for</param>
        /// <param name="sb">The string builder to append to</param>
        private void GenerateFunctionBody(Function function, StringBuilder sb)
        {
            try
            {
                var functionBlocks = new List<CodeBlock>();

                foreach (var block in codeBlocks.Values)
                {
                    if (function.Instructions.Contains(block.StartAddress))
                    {
                        functionBlocks.Add(block);
                    }
                }

                functionBlocks.Sort((a, b) => a.StartAddress.CompareTo(b.StartAddress));

                sb.AppendLine("    // Function labels");
                var usedLabels = new HashSet<string>();

                foreach (var block in functionBlocks)
                {
                    if (disassembler.Labels.TryGetValue(block.StartAddress, out var label))
                    {
                        usedLabels.Add(label);
                    }

                    foreach (var instruction in block.Instructions)
                    {
                        if ((instruction.IsBranch || instruction.IsJump) && instruction.TargetAddress.HasValue)
                        {
                            ushort target = instruction.TargetAddress.Value;
                            if (disassembler.Labels.TryGetValue(target, out var targetLabel))
                            {
                                usedLabels.Add(targetLabel);
                            }
                        }
                    }
                }

                foreach (var label in usedLabels)
                {
                    sb.AppendLine($"    static void* {label} = &&{label}_impl;  // Forward declaration for computed goto");
                }
                sb.AppendLine();

                foreach (var block in functionBlocks)
                {
                    if (disassembler.Labels.TryGetValue(block.StartAddress, out var label))
                    {
                        sb.AppendLine($"{label}_impl:  // Address: 0x{block.StartAddress:X4}");
                    }

                    foreach (var instruction in block.Instructions)
                    {
                        string asmComment = instruction.ToString()
                            .Replace("#", "0x")  // Replace # with 0x to avoid preprocessor issues
                            .Replace("$", "0x")  // Replace $ with 0x for hex values
                            .Replace("%", "");   // Remove % format specifiers

                        sb.AppendLine($"    // {asmComment}");

                        GenerateInstructionCode(instruction, sb);
                    }

                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                throw new DecompilationException($"Error during function body generation: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generates C code for an instruction
        /// </summary>
        /// <param name="instruction">The instruction to generate code for</param>
        /// <param name="sb">The string builder to append to</param>
        private void GenerateInstructionCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            try
            {
                switch (instruction.Info.Type)
                {
                    case InstructionType.Load:
                        GenerateLoadCode(instruction, sb);
                        break;
                    case InstructionType.Store:
                        GenerateStoreCode(instruction, sb);
                        break;
                    case InstructionType.Transfer:
                        GenerateTransferCode(instruction, sb);
                        break;
                    case InstructionType.Stack:
                        GenerateStackCode(instruction, sb);
                        break;
                    case InstructionType.Arithmetic:
                        GenerateArithmeticCode(instruction, sb);
                        break;
                    case InstructionType.Increment:
                        GenerateIncrementCode(instruction, sb);
                        break;
                    case InstructionType.Decrement:
                        GenerateDecrementCode(instruction, sb);
                        break;
                    case InstructionType.Shift:
                        GenerateShiftCode(instruction, sb);
                        break;
                    case InstructionType.Logic:
                        GenerateLogicCode(instruction, sb);
                        break;
                    case InstructionType.Compare:
                        GenerateCompareCode(instruction, sb);
                        break;
                    case InstructionType.Branch:
                        GenerateBranchCode(instruction, sb);
                        break;
                    case InstructionType.Jump:
                        GenerateJumpCode(instruction, sb);
                        break;
                    case InstructionType.Return:
                        GenerateReturnCode(instruction, sb);
                        break;
                    case InstructionType.Set:
                    case InstructionType.Clear:
                        GenerateFlagCode(instruction, sb);
                        break;
                    case InstructionType.Interrupt:
                        GenerateInterruptCode(instruction, sb);
                        break;
                    case InstructionType.Other:
                        GenerateOtherCode(instruction, sb);
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new DecompilationException($"Error during instruction code generation: {ex.Message}", ex);
            }
        }


        private void GenerateLoadCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Process LDA, LDX, LDY
            string register = instruction.Info.Mnemonic.Substring(2);
            string? variableName = GetVariableName(instruction);

            if (variableName != null)
            {
                sb.AppendLine($"    {register.ToLower()} = {variableName};");
            }
            else if (instruction.Info.AddressingMode == AddressingMode.Immediate)
            {
                sb.AppendLine($"    {register.ToLower()} = 0x{instruction.Operands[0]:X2};");
            }
            else
            {
                sb.AppendLine($"    // TODO: Load {register} with value at {GetAddressString(instruction)}");
            }
        }

        private void GenerateStoreCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Process STA, STX, STY
            string register = instruction.Info.Mnemonic.Substring(2);
            string? variableName = GetVariableName(instruction);

            if (variableName != null)
            {
                sb.AppendLine($"    {variableName} = {register.ToLower()};");
            }
            else
            {
                sb.AppendLine($"    // TODO: Store {register} to {GetAddressString(instruction)}");
            }
        }

        private void GenerateTransferCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Process TAX, TXA, TAY, TYA, TSX, TXS
            string source = instruction.Info.Mnemonic.Substring(1, 1).ToLower();
            string dest = instruction.Info.Mnemonic.Substring(2, 1).ToLower();

            sb.AppendLine($"    {dest} = {source};");
        }

        private void GenerateJumpCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Handle jump operations: JMP, JSR
            if (instruction.TargetAddress.HasValue)
            {
                ushort target = instruction.TargetAddress.Value;

                if (disassembler.Labels.TryGetValue(target, out var label))
                {
                    if (instruction.Info.Mnemonic == "JSR")
                    {
                        sb.AppendLine($"    {label}();  // Call subroutine");
                    }
                    else  // JMP
                    {
                        sb.AppendLine($"    goto *{label};  // Unconditional jump using computed goto");
                    }
                }
                else
                {
                    string dynamicLabel = $"dynamic_label_0x{target:X4}";

                    if (instruction.Info.Mnemonic == "JSR")
                    {
                        // Try to find a corresponding function by pattern matching
                        string potentialFuncName = $"sub_{target:X4}";

                        sb.AppendLine($"    // Call function at target address 0x{target:X4}");
                        sb.AppendLine($"    // Try to call function if it exists, otherwise use inline code");
                        sb.AppendLine($"    #ifdef {potentialFuncName}");
                        sb.AppendLine($"    {potentialFuncName}();");
                        sb.AppendLine($"    #else");
                        sb.AppendLine($"    // Warning: No function definition found for JSR target 0x{target:X4}");
                        sb.AppendLine($"    // Treating as inline code section - may need manual adjustment");
                        sb.AppendLine($"    {{ // Create scope for local variables");
                        sb.AppendLine($"        // First save return address");
                        sb.AppendLine($"        uint16_t return_addr = pc + 3; // 3 bytes for JSR instruction");
                        sb.AppendLine($"        stack[sp--] = (return_addr >> 8) & 0xFF;   // Push high byte");
                        sb.AppendLine($"        stack[sp--] = return_addr & 0xFF;          // Push low byte");
                        sb.AppendLine($"        // Jump to target");
                        sb.AppendLine($"        pc = 0x{target:X4};");
                        sb.AppendLine($"        // Note: You may need to manually implement the called function here");
                        sb.AppendLine($"    }}");
                        sb.AppendLine($"    #endif");
                    }
                    else // JMP
                    {
                        sb.AppendLine($"    // Jump to target address 0x{target:X4}");
                        sb.AppendLine($"    // Define a local label for the jump target");
                        sb.AppendLine($"    #ifdef {dynamicLabel}");
                        sb.AppendLine($"    goto *{dynamicLabel}; // Jump to predefined label if available");
                        sb.AppendLine($"    #else");
                        sb.AppendLine($"    // Warning: No label found for JMP target 0x{target:X4}");
                        sb.AppendLine($"    // Using direct PC assignment instead");
                        sb.AppendLine($"    pc = 0x{target:X4};");
                        sb.AppendLine($"    // Note: You may need to implement a dynamic jump target here");
                        sb.AppendLine($"    #endif");
                    }
                }
            }
            else if (instruction.Info.AddressingMode == AddressingMode.Indirect)
            {
                // Handle JMP indirect - this is commonly used for tables and vectors
                if (instruction.Operands.Length == 2)
                {
                    ushort indirectAddr = (ushort)((instruction.Operands[1] << 8) | instruction.Operands[0]);

                    sb.AppendLine($"    // Indirect jump via address 0x{indirectAddr:X4}");
                    sb.AppendLine($"    {{");
                    sb.AppendLine($"        // Read jump target from the indirect address (little-endian)");
                    sb.AppendLine($"        uint16_t target_addr = memory[0x{indirectAddr:X4}] | (memory[0x{indirectAddr:X4} + 1] << 8);");
                    sb.AppendLine($"        pc = target_addr;  // Jump to the target address");
                    sb.AppendLine($"        // Note: This might be a vector table jump, consider adding special handling");
                    sb.AppendLine($"    }}");
                }
                else
                {
                    sb.AppendLine($"    // Indirect jump with unrecognized operand format");
                    sb.AppendLine($"    // Note: This needs manual investigation");
                }
            }
            else
            {
                // Handle other complex jump situations - such as indexed indirect
                sb.AppendLine($"    // {instruction.Info.Mnemonic} with dynamic or complex target");
                sb.AppendLine($"    // Complex addressing mode: {instruction.Info.AddressingMode}");
                sb.AppendLine($"    // This requires special handling - review the original assembly code");

                // Add implementation guidance based on addressing mode
                switch (instruction.Info.AddressingMode)
                {
                    case AddressingMode.IndexedIndirect:
                        sb.AppendLine($"    // This is likely a jump table indexed by X");
                        sb.AppendLine($"    // Example implementation:");
                        sb.AppendLine($"    // uint16_t addr = memory[x + {GetImmediateValue(instruction)}] | (memory[x + {GetImmediateValue(instruction)} + 1] << 8);");
                        sb.AppendLine($"    // pc = addr;");
                        break;
                    case AddressingMode.IndirectIndexed:
                        sb.AppendLine($"    // This is likely a jump to an address indexed by Y");
                        sb.AppendLine($"    // Example implementation:");
                        sb.AppendLine($"    // uint16_t base_addr = memory[{GetImmediateValue(instruction)}] | (memory[{GetImmediateValue(instruction)} + 1] << 8);");
                        sb.AppendLine($"    // pc = base_addr + y;");
                        break;
                    default:
                        sb.AppendLine($"    // Unsupported addressing mode for jump instruction");
                        sb.AppendLine($"    // Review original assembly and implement manually");
                        break;
                }
            }
        }

        private string GetImmediateValue(DisassembledInstruction instruction)
        {
            if (instruction.Operands.Length > 0)
            {
                return $"0x{instruction.Operands[0]:X2}";
            }
            return "0x00";
        }

        private void GenerateReturnCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Process RTS, RTI
            sb.AppendLine("    return;");
        }


        private string? GetVariableName(DisassembledInstruction instruction)
        {
            if (instruction.Info.AddressingMode == AddressingMode.ZeroPage ||
                instruction.Info.AddressingMode == AddressingMode.ZeroPageX ||
                instruction.Info.AddressingMode == AddressingMode.ZeroPageY)
            {
                // Zero page addressing
                ushort address = instruction.Operands[0];

                if (variables.TryGetValue(address, out var variable))
                {
                    return GetIndexedVariableName(variable, instruction.Info.AddressingMode);
                }
            }
            else if (instruction.Info.AddressingMode == AddressingMode.Absolute ||
                     instruction.Info.AddressingMode == AddressingMode.AbsoluteX ||
                     instruction.Info.AddressingMode == AddressingMode.AbsoluteY)
            {
                // Absolute addressing
                ushort address = (ushort)((instruction.Operands[1] << 8) | instruction.Operands[0]);

                if (variables.TryGetValue(address, out var variable))
                {
                    return GetIndexedVariableName(variable, instruction.Info.AddressingMode);
                }
            }

            return null;
        }

        private string GetIndexedVariableName(Variable variable, AddressingMode addressingMode)
        {
            if (addressingMode == AddressingMode.ZeroPageX || addressingMode == AddressingMode.AbsoluteX)
            {
                if (variable.Type == VariableType.Array)
                {
                    return $"{variable.Name}[x]";
                }
                else
                {
                    return $"{variable.Name} + x";
                }
            }
            else if (addressingMode == AddressingMode.ZeroPageY || addressingMode == AddressingMode.AbsoluteY)
            {
                if (variable.Type == VariableType.Array)
                {
                    return $"{variable.Name}[y]";
                }
                else
                {
                    return $"{variable.Name} + y";
                }
            }
            else
            {
                return variable.Name;
            }
        }

        private string GetAddressString(DisassembledInstruction instruction)
        {
            if (instruction.Info.AddressingMode == AddressingMode.ZeroPage ||
                instruction.Info.AddressingMode == AddressingMode.ZeroPageX ||
                instruction.Info.AddressingMode == AddressingMode.ZeroPageY)
            {
                // Zero page addressing
                ushort address = instruction.Operands[0];
                return $"0x{address:X2}";
            }
            else if (instruction.Info.AddressingMode == AddressingMode.Absolute ||
                     instruction.Info.AddressingMode == AddressingMode.AbsoluteX ||
                     instruction.Info.AddressingMode == AddressingMode.AbsoluteY)
            {
                // Absolute addressing
                ushort address = (ushort)((instruction.Operands[1] << 8) | instruction.Operands[0]);
                return $"0x{address:X4}";
            }
            else if (instruction.Info.AddressingMode == AddressingMode.IndexedIndirect)
            {
                // Indexed indirect addressing (X-indexed)
                return $"*(0x{instruction.Operands[0]:X2} + X)";
            }
            else if (instruction.Info.AddressingMode == AddressingMode.IndirectIndexed)
            {
                // Indirect indexed addressing (Y-indexed)
                return $"*(0x{instruction.Operands[0]:X2}) + Y";
            }

            return "unknown";
        }

        private void GenerateStackCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Handle stack operations: PHA, PLA, PHP, PLP, etc.
            switch (instruction.Info.Mnemonic)
            {
                case "PHA":
                    sb.AppendLine("    stack[sp--] = a;  // Push accumulator to stack");
                    break;
                case "PHP":
                    sb.AppendLine("    stack[sp--] = status;  // Push status register to stack");
                    break;
                case "PLA":
                    sb.AppendLine("    a = stack[++sp];  // Pull accumulator from stack");
                    sb.AppendLine("    // Update zero and negative flags");
                    sb.AppendLine("    status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | (a == 0 ? ZERO_FLAG : 0) | (a & 0x80);");
                    break;
                case "PLP":
                    sb.AppendLine("    status = stack[++sp];  // Pull status register from stack");
                    break;
                default:
                    sb.AppendLine($"    // TODO: Implement {instruction.Info.Mnemonic} instruction");
                    break;
            }
        }

        private void GenerateArithmeticCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Handle arithmetic operations: ADC, SBC
            string? operand = GetOperandString(instruction);
            if (operand == null)
                return;

            switch (instruction.Info.Mnemonic)
            {
                case "ADC":
                    sb.AppendLine($"    // ADC - Add with carry");
                    sb.AppendLine($"    {{");
                    sb.AppendLine($"        uint16_t result = a + {operand} + (status & CARRY_FLAG ? 1 : 0);");
                    sb.AppendLine($"        // Set carry flag if result > 255");
                    sb.AppendLine($"        status = (status & ~CARRY_FLAG) | (result > 0xFF ? CARRY_FLAG : 0);");
                    sb.AppendLine($"        // Set overflow flag if sign bit changes in an unexpected way");
                    sb.AppendLine($"        uint8_t overflow = (~(a ^ {operand}) & (a ^ (uint8_t)result) & 0x80) ? OVERFLOW_FLAG : 0;");
                    sb.AppendLine($"        status = (status & ~OVERFLOW_FLAG) | overflow;");
                    sb.AppendLine($"        a = (uint8_t)result;");
                    sb.AppendLine($"        // Update zero and negative flags");
                    sb.AppendLine($"        status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | (a == 0 ? ZERO_FLAG : 0) | (a & 0x80);");
                    sb.AppendLine($"    }}");
                    break;

                case "SBC":
                    sb.AppendLine($"    // SBC - Subtract with carry");
                    sb.AppendLine($"    {{");
                    sb.AppendLine($"        uint16_t result = a - {operand} - (status & CARRY_FLAG ? 0 : 1);");
                    sb.AppendLine($"        // Set carry flag if no borrow required (result >= 0)");
                    sb.AppendLine($"        status = (status & ~CARRY_FLAG) | (result < 0x100 ? CARRY_FLAG : 0);");
                    sb.AppendLine($"        // Set overflow flag if sign bit changes in an unexpected way");
                    sb.AppendLine($"        uint8_t overflow = ((a ^ {operand}) & (a ^ (uint8_t)result) & 0x80) ? OVERFLOW_FLAG : 0;");
                    sb.AppendLine($"        status = (status & ~OVERFLOW_FLAG) | overflow;");
                    sb.AppendLine($"        a = (uint8_t)result;");
                    sb.AppendLine($"        // Update zero and negative flags");
                    sb.AppendLine($"        status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | (a == 0 ? ZERO_FLAG : 0) | (a & 0x80);");
                    sb.AppendLine($"    }}");
                    break;

                default:
                    sb.AppendLine($"    // TODO: Implement {instruction.Info.Mnemonic} instruction");
                    break;
            }
        }

        private void GenerateIncrementCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Handle increment operations: INC, INX, INY
            string? operand = GetOperandString(instruction);

            switch (instruction.Info.Mnemonic)
            {
                case "INC":
                    sb.AppendLine($"    // Increment memory location");
                    sb.AppendLine($"    {operand} = ({operand} + 1) & 0xFF;");
                    sb.AppendLine($"    // Update zero and negative flags");
                    sb.AppendLine($"    status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | ({operand} == 0 ? ZERO_FLAG : 0) | ({operand} & 0x80);");
                    break;

                case "INX":
                    sb.AppendLine($"    // Increment X register");
                    sb.AppendLine($"    x = (x + 1) & 0xFF;");
                    sb.AppendLine($"    // Update zero and negative flags");
                    sb.AppendLine($"    status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | (x == 0 ? ZERO_FLAG : 0) | (x & 0x80);");
                    break;

                case "INY":
                    sb.AppendLine($"    // Increment Y register");
                    sb.AppendLine($"    y = (y + 1) & 0xFF;");
                    sb.AppendLine($"    // Update zero and negative flags");
                    sb.AppendLine($"    status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | (y == 0 ? ZERO_FLAG : 0) | (y & 0x80);");
                    break;

                default:
                    sb.AppendLine($"    // TODO: Implement {instruction.Info.Mnemonic} instruction");
                    break;
            }
        }

        private void GenerateDecrementCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Handle decrement operations: DEC, DEX, DEY
            string? operand = GetOperandString(instruction);

            switch (instruction.Info.Mnemonic)
            {
                case "DEC":
                    sb.AppendLine($"    // Decrement memory location");
                    sb.AppendLine($"    {operand} = ({operand} - 1) & 0xFF;");
                    sb.AppendLine($"    // Update zero and negative flags");
                    sb.AppendLine($"    status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | ({operand} == 0 ? ZERO_FLAG : 0) | ({operand} & 0x80);");
                    break;

                case "DEX":
                    sb.AppendLine($"    // Decrement X register");
                    sb.AppendLine($"    x = (x - 1) & 0xFF;");
                    sb.AppendLine($"    // Update zero and negative flags");
                    sb.AppendLine($"    status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | (x == 0 ? ZERO_FLAG : 0) | (x & 0x80);");
                    break;

                case "DEY":
                    sb.AppendLine($"    // Decrement Y register");
                    sb.AppendLine($"    y = (y - 1) & 0xFF;");
                    sb.AppendLine($"    // Update zero and negative flags");
                    sb.AppendLine($"    status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | (y == 0 ? ZERO_FLAG : 0) | (y & 0x80);");
                    break;

                default:
                    sb.AppendLine($"    // TODO: Implement {instruction.Info.Mnemonic} instruction");
                    break;
            }
        }

        private void GenerateShiftCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Handle shift operations: ASL, LSR, ROL, ROR
            string? operand;

            if (instruction.Info.AddressingMode == AddressingMode.Accumulator)
            {
                operand = "a";
            }
            else
            {
                operand = GetOperandString(instruction);
                if (operand == null)
                    return;
            }

            switch (instruction.Info.Mnemonic)
            {
                case "ASL":
                    sb.AppendLine($"    // Arithmetic shift left");
                    sb.AppendLine($"    status = (status & ~CARRY_FLAG) | (({operand} & 0x80) ? CARRY_FLAG : 0);");
                    sb.AppendLine($"    {operand} = ({operand} << 1) & 0xFF;");
                    sb.AppendLine($"    // Update zero and negative flags");
                    sb.AppendLine($"    status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | ({operand} == 0 ? ZERO_FLAG : 0) | ({operand} & 0x80);");
                    break;

                case "LSR":
                    sb.AppendLine($"    // Logical shift right");
                    sb.AppendLine($"    status = (status & ~CARRY_FLAG) | (({operand} & 0x01) ? CARRY_FLAG : 0);");
                    sb.AppendLine($"    {operand} = {operand} >> 1;");
                    sb.AppendLine($"    // Update zero and negative flags (negative always clear)");
                    sb.AppendLine($"    status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | ({operand} == 0 ? ZERO_FLAG : 0);");
                    break;

                case "ROL":
                    sb.AppendLine($"    // Rotate left");
                    sb.AppendLine($"    {{");
                    sb.AppendLine($"        uint8_t oldCarry = (status & CARRY_FLAG) ? 1 : 0;");
                    sb.AppendLine($"        status = (status & ~CARRY_FLAG) | (({operand} & 0x80) ? CARRY_FLAG : 0);");
                    sb.AppendLine($"        {operand} = (({operand} << 1) | oldCarry) & 0xFF;");
                    sb.AppendLine($"        // Update zero and negative flags");
                    sb.AppendLine($"        status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | ({operand} == 0 ? ZERO_FLAG : 0) | ({operand} & 0x80);");
                    sb.AppendLine($"    }}");
                    break;

                case "ROR":
                    sb.AppendLine($"    // Rotate right");
                    sb.AppendLine($"    {{");
                    sb.AppendLine($"        uint8_t oldCarry = (status & CARRY_FLAG) ? 0x80 : 0;");
                    sb.AppendLine($"        status = (status & ~CARRY_FLAG) | (({operand} & 0x01) ? CARRY_FLAG : 0);");
                    sb.AppendLine($"        {operand} = ({operand} >> 1) | oldCarry;");
                    sb.AppendLine($"        // Update zero and negative flags");
                    sb.AppendLine($"        status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | ({operand} == 0 ? ZERO_FLAG : 0) | ({operand} & 0x80);");
                    sb.AppendLine($"    }}");
                    break;

                default:
                    sb.AppendLine($"    // TODO: Implement {instruction.Info.Mnemonic} instruction");
                    break;
            }
        }

        private void GenerateLogicCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Handle logic operations: AND, ORA, EOR, BIT
            string? operand = GetOperandString(instruction);
            if (operand == null)
                return;

            switch (instruction.Info.Mnemonic)
            {
                case "AND":
                    sb.AppendLine($"    // Logical AND");
                    sb.AppendLine($"    a &= {operand};");
                    sb.AppendLine($"    // Update zero and negative flags");
                    sb.AppendLine($"    status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | (a == 0 ? ZERO_FLAG : 0) | (a & 0x80);");
                    break;

                case "ORA":
                    sb.AppendLine($"    // Logical OR");
                    sb.AppendLine($"    a |= {operand};");
                    sb.AppendLine($"    // Update zero and negative flags");
                    sb.AppendLine($"    status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | (a == 0 ? ZERO_FLAG : 0) | (a & 0x80);");
                    break;

                case "EOR":
                    sb.AppendLine($"    // Logical exclusive OR");
                    sb.AppendLine($"    a ^= {operand};");
                    sb.AppendLine($"    // Update zero and negative flags");
                    sb.AppendLine($"    status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | (a == 0 ? ZERO_FLAG : 0) | (a & 0x80);");
                    break;

                case "BIT":
                    sb.AppendLine($"    // Bit test");
                    sb.AppendLine($"    // Set zero flag based on AND result");
                    sb.AppendLine($"    status = (status & ~ZERO_FLAG) | ((a & {operand}) == 0 ? ZERO_FLAG : 0);");
                    sb.AppendLine($"    // Copy bits 6 and 7 of operand to overflow and negative flags");
                    sb.AppendLine($"    status = (status & ~(OVERFLOW_FLAG | NEGATIVE_FLAG)) | ({operand} & 0x40) | ({operand} & 0x80);");
                    break;

                default:
                    sb.AppendLine($"    // TODO: Implement {instruction.Info.Mnemonic} instruction");
                    break;
            }
        }

        private void GenerateCompareCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Handle compare operations: CMP, CPX, CPY
            string? operand = GetOperandString(instruction);
            if (operand == null)
                return;

            string register = "";

            switch (instruction.Info.Mnemonic)
            {
                case "CMP":
                    register = "a";
                    break;
                case "CPX":
                    register = "x";
                    break;
                case "CPY":
                    register = "y";
                    break;
                default:
                    sb.AppendLine($"    // TODO: Implement {instruction.Info.Mnemonic} instruction");
                    return;
            }

            sb.AppendLine($"    // Compare {register} with memory");
            sb.AppendLine($"    {{");
            sb.AppendLine($"        uint8_t result = {register} - {operand};");
            sb.AppendLine($"        // Set carry flag if {register} >= memory");
            sb.AppendLine($"        status = (status & ~CARRY_FLAG) | ({register} >= {operand} ? CARRY_FLAG : 0);");
            sb.AppendLine($"        // Update zero and negative flags");
            sb.AppendLine($"        status = (status & ~(ZERO_FLAG | NEGATIVE_FLAG)) | (result == 0 ? ZERO_FLAG : 0) | (result & 0x80);");
            sb.AppendLine($"    }}");
        }

        private void GenerateBranchCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Handle branch operations: BCC, BCS, BEQ, BMI, BNE, BPL, BVC, BVS
            if (!instruction.TargetAddress.HasValue)
            {
                sb.AppendLine($"    // TODO: Implement {instruction.Info.Mnemonic} with unknown target");
                return;
            }

            string condition = "";
            string comment = "";

            switch (instruction.Info.Mnemonic)
            {
                case "BCC":
                    condition = "(status & CARRY_FLAG) == 0";
                    comment = "Branch if carry clear";
                    break;
                case "BCS":
                    condition = "(status & CARRY_FLAG) != 0";
                    comment = "Branch if carry set";
                    break;
                case "BEQ":
                    condition = "(status & ZERO_FLAG) != 0";
                    comment = "Branch if equal (zero set)";
                    break;
                case "BMI":
                    condition = "(status & NEGATIVE_FLAG) != 0";
                    comment = "Branch if minus (negative set)";
                    break;
                case "BNE":
                    condition = "(status & ZERO_FLAG) == 0";
                    comment = "Branch if not equal (zero clear)";
                    break;
                case "BPL":
                    condition = "(status & NEGATIVE_FLAG) == 0";
                    comment = "Branch if plus (negative clear)";
                    break;
                case "BVC":
                    condition = "(status & OVERFLOW_FLAG) == 0";
                    comment = "Branch if overflow clear";
                    break;
                case "BVS":
                    condition = "(status & OVERFLOW_FLAG) != 0";
                    comment = "Branch if overflow set";
                    break;
                default:
                    sb.AppendLine($"    // TODO: Implement {instruction.Info.Mnemonic} instruction");
                    return;
            }

            string label = disassembler.Labels.TryGetValue(instruction.TargetAddress.Value, out var targetLabel) ?
                targetLabel : $"loc_{instruction.TargetAddress.Value:X4}";

            sb.AppendLine($"    // {comment}");
            sb.AppendLine($"    if ({condition}) {{");
            sb.AppendLine($"        goto *{label};  // Use computed goto for better branch handling");
            sb.AppendLine($"    }}");
        }

        private void GenerateFlagCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Handle flag operations: SEC, CLC, SEI, CLI, SED, CLD, CLV
            switch (instruction.Info.Mnemonic)
            {
                case "SEC":
                    sb.AppendLine("    // Set carry flag");
                    sb.AppendLine("    status |= CARRY_FLAG;");
                    break;
                case "CLC":
                    sb.AppendLine("    // Clear carry flag");
                    sb.AppendLine("    status &= ~CARRY_FLAG;");
                    break;
                case "SEI":
                    sb.AppendLine("    // Set interrupt disable flag");
                    sb.AppendLine("    status |= INTERRUPT_FLAG;");
                    break;
                case "CLI":
                    sb.AppendLine("    // Clear interrupt disable flag");
                    sb.AppendLine("    status &= ~INTERRUPT_FLAG;");
                    break;
                case "SED":
                    sb.AppendLine("    // Set decimal flag");
                    sb.AppendLine("    status |= DECIMAL_FLAG;");
                    break;
                case "CLD":
                    sb.AppendLine("    // Clear decimal flag");
                    sb.AppendLine("    status &= ~DECIMAL_FLAG;");
                    break;
                case "CLV":
                    sb.AppendLine("    // Clear overflow flag");
                    sb.AppendLine("    status &= ~OVERFLOW_FLAG;");
                    break;
                default:
                    sb.AppendLine($"    // TODO: Implement {instruction.Info.Mnemonic} instruction");
                    break;
            }
        }

        private void GenerateInterruptCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Handle interrupt operations: BRK, RTI
            switch (instruction.Info.Mnemonic)
            {
                case "BRK":
                    sb.AppendLine("    // Software interrupt (BRK)");
                    sb.AppendLine("    // Push program counter + 2");
                    sb.AppendLine("    stack[sp--] = (pc + 2) >> 8;");  // Push high byte
                    sb.AppendLine("    stack[sp--] = (pc + 2) & 0xFF;"); // Push low byte
                    sb.AppendLine("    // Push status register with break flag set");
                    sb.AppendLine("    stack[sp--] = status | BREAK_FLAG;");
                    sb.AppendLine("    // Set interrupt disable flag");
                    sb.AppendLine("    status |= INTERRUPT_FLAG;");
                    sb.AppendLine("    // Load interrupt vector from $FFFE-$FFFF");
                    sb.AppendLine("    pc = memory[0xFFFE] | (memory[0xFFFF] << 8);");
                    break;
                case "RTI":
                    sb.AppendLine("    // Return from interrupt");
                    sb.AppendLine("    // Pull status register");
                    sb.AppendLine("    status = stack[++sp];");
                    sb.AppendLine("    // Pull program counter");
                    sb.AppendLine("    pc = stack[++sp];"); // Pull low byte
                    sb.AppendLine("    pc |= stack[++sp] << 8;"); // Pull high byte
                    break;
                default:
                    sb.AppendLine($"    // TODO: Implement {instruction.Info.Mnemonic} instruction");
                    break;
            }
        }

        private void GenerateOtherCode(DisassembledInstruction instruction, StringBuilder sb)
        {
            // Handle other operations: NOP
            switch (instruction.Info.Mnemonic)
            {
                case "NOP":
                    sb.AppendLine("    // No operation");
                    sb.AppendLine("    // Do nothing");
                    break;
                default:
                    sb.AppendLine($"    // TODO: Implement {instruction.Info.Mnemonic} instruction");
                    break;
            }
        }

        private string? GetOperandString(DisassembledInstruction instruction)
        {
            if (instruction.Info.AddressingMode == AddressingMode.Immediate)
            {
                if (instruction.Operands.Length > 0)
                {
                    return $"0x{instruction.Operands[0]:X2}";
                }
                return null;
            }

            string? variableName = GetVariableName(instruction);
            if (variableName != null)
            {
                return variableName;
            }

            return GetAddressString(instruction);
        }
    }
}