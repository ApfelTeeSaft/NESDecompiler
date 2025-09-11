using System;
using System.Collections.Generic;
using System.Text;
using NESDecompiler.Core.CPU;
using NESDecompiler.Core.Exceptions;
using NESDecompiler.Core.ROM;

namespace NESDecompiler.Core.Disassembly
{
    /// <summary>
    /// Represents a disassembled instruction with its address and operands
    /// </summary>
    public class DisassembledInstruction
    {
        /// <summary>
        /// The address of this instruction in the ROM
        /// </summary>
        public ushort Address { get; set; }

        /// <summary>
        /// The CPU memory address this instruction maps to
        /// </summary>
        public ushort CPUAddress { get; set; }

        /// <summary>
        /// Information about this instruction's opcode
        /// </summary>
        public required InstructionInfo Info { get; init; }

        /// <summary>
        /// The raw bytes of this instruction (including operands)
        /// </summary>
        public byte[]? Bytes { get; set; }

        /// <summary>
        /// The operand bytes of this instruction
        /// </summary>
        public byte[] Operands => Bytes!.Length > 1 ? Bytes[1..] : Array.Empty<byte>();

        /// <summary>
        /// The target address for branch and jump instructions
        /// </summary>
        public ushort? TargetAddress { get; set; }

        /// <summary>
        /// Potential label for this instruction
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Potential comment for this instruction
        /// </summary>
        public string? Comment { get; set; }

        /// <summary>
        /// Whether this instruction is a potential function entry point
        /// </summary>
        public bool IsFunctionEntry { get; set; }

        /// <summary>
        /// Whether this instruction is a potential function exit point
        /// </summary>
        public bool IsFunctionExit => Info.Mnemonic == "RTS" || Info.Mnemonic == "RTI";

        /// <summary>
        /// Whether this instruction is a branch instruction
        /// </summary>
        public bool IsBranch => Info.Type == InstructionType.Branch;

        /// <summary>
        /// Whether this instruction is a jump instruction
        /// </summary>
        public bool IsJump => Info.Mnemonic == "JMP" || Info.Mnemonic == "JSR";

        /// <summary>
        /// Returns a string representation of this instruction
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(Label))
            {
                sb.AppendLine($"{Label}:");
            }

            sb.Append($"{CPUAddress:X4}  ");
            foreach (var b in Bytes!)
            {
                sb.Append($"{b:X2} ");
            }

            sb.Append(new string(' ', (3 - Bytes.Length) * 3 + 2));

            sb.Append(Info.Mnemonic);

            if (Info.AddressingMode != AddressingMode.Implied &&
                Info.AddressingMode != AddressingMode.Accumulator)
            {
                sb.Append(' ');

                if (Info.AddressingMode == AddressingMode.Relative && TargetAddress.HasValue)
                {
                    sb.Append($"${TargetAddress.Value:X4}");
                }
                else if (Operands.Length == 1)
                {
                    string operandFormat = Info.GetOperandFormat();
                    sb.Append(string.Format(operandFormat, Operands[0]));
                }
                else if (Operands.Length == 2)
                {
                    string operandFormat = Info.GetOperandFormat();
                    ushort value = (ushort)((Operands[1] << 8) | Operands[0]);
                    sb.Append(string.Format(operandFormat, value));
                }
            }

            if (!string.IsNullOrEmpty(Comment))
            {
                sb.Append($" ; {Comment}");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Disassembles 6502 machine code into assembly language
    /// </summary>
    public class Disassembler
    {
        private ROMInfo romInfo;
        private byte[] codeData;
        private List<DisassembledInstruction> instructions;
        private Dictionary<ushort, DisassembledInstruction> addressToInstruction;
        private HashSet<ushort> entryPoints;
        private HashSet<ushort> referencedAddresses;
        private Dictionary<ushort, string> labels;
        private int labelCounter;

        /// <summary>
        /// The list of disassembled instructions
        /// </summary>
        public IReadOnlyList<DisassembledInstruction> Instructions => instructions;

        /// <summary>
        /// Maps CPU addresses to disassembled instructions
        /// </summary>
        public IReadOnlyDictionary<ushort, DisassembledInstruction> AddressToInstruction => addressToInstruction;

        /// <summary>
        /// The list of entry points (e.g., reset vector, NMI vector)
        /// </summary>
        public IReadOnlySet<ushort> EntryPoints => entryPoints;

        /// <summary>
        /// The list of addresses referenced by the code
        /// </summary>
        public IReadOnlySet<ushort> ReferencedAddresses => referencedAddresses;

        /// <summary>
        /// Maps CPU addresses to labels
        /// </summary>
        public IReadOnlyDictionary<ushort, string> Labels => labels;

        /// <summary>
        /// Creates a new disassembler for the specified ROM
        /// </summary>
        /// <param name="romInfo">Information about the ROM</param>
        /// <param name="codeData">The code data to disassemble</param>
        public Disassembler(ROMInfo romInfo, byte[] codeData)
        {
            this.romInfo = romInfo ?? throw new ArgumentNullException(nameof(romInfo));
            this.codeData = codeData ?? throw new ArgumentNullException(nameof(codeData));

            instructions = new List<DisassembledInstruction>();
            addressToInstruction = new Dictionary<ushort, DisassembledInstruction>();
            entryPoints = new HashSet<ushort>();
            referencedAddresses = new HashSet<ushort>();
            labels = new Dictionary<ushort, string>();
            labelCounter = 0;

            if (romInfo.ResetVector != 0)
            {
                entryPoints.Add(romInfo.ResetVector);
            }
        }

        /// <summary>
        /// Disassembles the code data
        /// </summary>
        public void Disassemble()
        {
            LinearDisassembly();
            TraceExecution();
            IdentifyFunctions();
            GenerateLabels();
        }

        /// <summary>
        /// Gets the disassembled instruction at the specified address
        /// </summary>
        /// <param name="address">The CPU address</param>
        /// <returns>The disassembled instruction, or null if not found</returns>
        public DisassembledInstruction? GetInstructionAt(ushort address)
        {
            addressToInstruction.TryGetValue(address, out var instruction);
            return instruction;
        }

        /// <summary>
        /// Performs a linear disassembly of the code data
        /// </summary>
        private void LinearDisassembly()
        {
            try
            {
                int offset = 0;

                ushort baseAddress = 0x8000;

                while (offset < codeData.Length)
                {
                    ushort cpuAddress = (ushort)(baseAddress + offset);

                    byte opcode = codeData[offset];

                    var instructionInfo = InstructionSet.GetInstruction(opcode);

                    if (!instructionInfo.IsValid)
                    {
                        offset++;
                        continue;
                    }

                    if (offset + instructionInfo.Size > codeData.Length)
                    {
                        offset++;
                        continue;
                    }

                    byte[] bytes = new byte[instructionInfo.Size];
                    Array.Copy(codeData, offset, bytes, 0, instructionInfo.Size);

                    var instruction = new DisassembledInstruction
                    {
                        Address = (ushort)offset,
                        CPUAddress = cpuAddress,
                        Info = instructionInfo,
                        Bytes = bytes
                    };

                    CalculateTargetAddress(instruction);

                    instructions.Add(instruction);
                    addressToInstruction[cpuAddress] = instruction;
                    offset += instructionInfo.Size;
                }
            }
            catch (Exception ex)
            {
                throw new DisassemblyException($"Error during linear disassembly: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Traces execution from known entry points
        /// </summary>
        private void TraceExecution()
        {
            try
            {
                var toTrace = new Queue<ushort>(entryPoints);
                var traced = new HashSet<ushort>();

                while (toTrace.Count > 0)
                {
                    ushort address = toTrace.Dequeue();

                    if (traced.Contains(address))
                    {
                        continue;
                    }

                    traced.Add(address);

                    if (!addressToInstruction.TryGetValue(address, out var instruction))
                    {
                        continue;
                    }

                    if (entryPoints.Contains(address))
                    {
                        instruction.IsFunctionEntry = true;
                    }

                    if (instruction.IsJump)
                    {
                        if (instruction.TargetAddress.HasValue)
                        {
                            ushort target = instruction.TargetAddress.Value;

                            referencedAddresses.Add(target);

                            if (instruction.Info.Mnemonic == "JSR")
                            {
                                entryPoints.Add(target);

                                ushort returnAddress = (ushort)(address + instruction.Info.Size);
                                toTrace.Enqueue(returnAddress);
                            }

                            toTrace.Enqueue(target);

                            if (instruction.Info.Mnemonic == "JMP")
                            {
                                continue;
                            }
                        }
                    }
                    else if (instruction.IsBranch)
                    {
                        if (instruction.TargetAddress.HasValue)
                        {
                            ushort target = instruction.TargetAddress.Value;

                            referencedAddresses.Add(target);

                            toTrace.Enqueue(target);
                        }
                    }
                    else if (instruction.IsFunctionExit)
                    {
                        continue;
                    }

                    ushort nextAddress = (ushort)(address + instruction.Info.Size);
                    toTrace.Enqueue(nextAddress);
                }
            }
            catch (Exception ex)
            {
                throw new DisassemblyException($"Error during execution tracing: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Identifies functions and their boundaries
        /// </summary>
        private void IdentifyFunctions()
        {
            try
            {
                foreach (ushort entryPoint in entryPoints)
                {
                    if (addressToInstruction.TryGetValue(entryPoint, out var instruction))
                    {
                        instruction.IsFunctionEntry = true;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new DisassemblyException($"Error during function identification: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generates labels for referenced addresses
        /// </summary>
        private void GenerateLabels()
        {
            try
            {
                foreach (ushort entryPoint in entryPoints)
                {
                    if (addressToInstruction.TryGetValue(entryPoint, out var instruction))
                    {
                        string label = $"sub_{entryPoint:X4}";
                        instruction.Label = label;
                        labels[entryPoint] = label;
                    }
                }

                foreach (ushort address in referencedAddresses)
                {
                    if (!labels.ContainsKey(address) && addressToInstruction.TryGetValue(address, out var instruction))
                    {
                        string label = $"loc_{labelCounter++:X4}";
                        instruction.Label = label;
                        labels[address] = label;
                    }
                }

                foreach (var instruction in instructions)
                {
                    if (instruction.TargetAddress.HasValue)
                    {
                        ushort target = instruction.TargetAddress.Value;
                        if (labels.TryGetValue(target, out string? label))
                        {
                            instruction.Comment = $"-> {label}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new DisassemblyException($"Error during label generation: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Calculates the target address for branch and jump instructions
        /// </summary>
        /// <param name="instruction">The instruction to process</param>
        private void CalculateTargetAddress(DisassembledInstruction instruction)
        {
            if (instruction.Info.AddressingMode == AddressingMode.Relative)
            {
                // Branch instructions use relative addressing
                // The offset is signed and relative to the next instruction
                sbyte offset = (sbyte)instruction.Operands[0];
                ushort nextAddress = (ushort)(instruction.CPUAddress + instruction.Info.Size);
                instruction.TargetAddress = (ushort)(nextAddress + offset);
            }
            else if (instruction.IsJump &&
                     (instruction.Info.AddressingMode == AddressingMode.Absolute ||
                      instruction.Info.AddressingMode == AddressingMode.Indirect))
            {
                if (instruction.Operands.Length == 2)
                {
                    ushort target = (ushort)((instruction.Operands[1] << 8) | instruction.Operands[0]);
                    instruction.TargetAddress = target;
                }
            }
        }

        /// <summary>
        /// Returns the disassembly as a formatted string
        /// </summary>
        public string ToAssemblyString()
        {
            var sb = new StringBuilder();

            sb.AppendLine("; 6502 Disassembly");
            sb.AppendLine($"; ROM: {romInfo}");
            sb.AppendLine();

            foreach (var instruction in instructions)
            {
                sb.AppendLine(instruction.ToString());
            }

            return sb.ToString();
        }
    }
}