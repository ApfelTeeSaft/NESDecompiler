using NESDecompiler.Core.CPU;
using NESDecompiler.Core.Disassembly;

namespace NESDecompiler.Core.Decompilation;

public static class FunctionDecompiler
{
    /// <summary>
    /// Traces and decompiles a single function
    /// </summary>
    /// <param name="functionAddress">The CPU address of the entry point of the function to decompile</param>
    /// <param name="codeRegions">All available regions of bytes that could contain instructions for the function</param>
    public static DecompiledFunction Decompile(ushort functionAddress, IReadOnlyList<CodeRegion> codeRegions)
    {
        var instructions = new List<DisassembledInstruction>();
        var jumpAddresses = new HashSet<ushort>();
        var seenInstructions = new HashSet<ushort>();
        var addressQueue = new Queue<ushort>([functionAddress]);

        while (addressQueue.TryDequeue(out var nextAddress))
        {
            if (!seenInstructions.Add(nextAddress))
            {
                if (nextAddress == functionAddress)
                {
                    // This means a branch occurred that caused the flow to wrap around to instructions preceding
                    // the function entrance. This usually happens when there is a jump/branch to right before the
                    // entrypoint, usually due to decompiling in the middle of a loop. To fix this, we need to add
                    // a jump back to the function entrypoint
                    if (functionAddress == 0x00)
                    {
                        const string message = "Wrap around instruction detected for a function at 0000, but that " +
                                               "doesn't make sense";

                        throw new InvalidOperationException(message);
                    }

                    var addressHigh = (functionAddress & 0xFF00) >> 8;
                    var addressLow = functionAddress & 0x00FF;

                    var jumpInstruction = new DisassembledInstruction
                    {
                        Info = InstructionSet.GetInstruction(0x4C),
                        CPUAddress = (ushort)(nextAddress - 1),
                        Bytes = [0x4C, (byte)addressLow, (byte)addressHigh],
                        TargetAddress = functionAddress,

                        // Make sure they appear after any instruction that already occupies that address
                        SubAddressOrder = 1,
                    };

                    instructions.Add(jumpInstruction);
                }

                continue;
            }

            var instruction = GetNextInstruction(nextAddress, codeRegions);
            if (instruction == null)
            {
                // Consider no instruction the end of the function. This is usually the case
                // with an always taken branch
                continue;
            }

            instructions.Add(instruction);

            // Ensure the function entrypoint has a label
            if (instruction.CPUAddress == functionAddress && instruction.Label == null)
            {
                instruction.Label = $"sub_{functionAddress:X4}";
                jumpAddresses.Add(functionAddress);
            }

            if (IsEndOfFunction(instruction))
            {
                continue;
            }

            if (instruction.TargetAddress != null)
            {
                jumpAddresses.Add(instruction.TargetAddress.Value);
                addressQueue.Enqueue(instruction.TargetAddress.Value);
            }

            if (!instruction.IsJump)
            {
                addressQueue.Enqueue((ushort)(nextAddress + instruction.Info.Size));
            }
        }

        // Add labels for any jump targets
        foreach (var instruction in instructions)
        {
            if (jumpAddresses.Contains(instruction.CPUAddress))
            {
                instruction.Label = $"loc_{instruction.CPUAddress:X4}";
            }
        }

        return new DecompiledFunction(functionAddress, instructions, jumpAddresses);
    }

    private static DisassembledInstruction? GetNextInstruction(ushort address, IReadOnlyList<CodeRegion> regions)
    {
        var relevantRegion = regions
            .Where(x => x.BaseAddress <= address)
            .Where(x => x.BaseAddress + x.Bytes.Length > address)
            .FirstOrDefault();

        if (relevantRegion == null)
        {
            var message = $"No code region contained the address 0x{address:X4}";
            throw new InvalidOperationException(message);
        }

        var offset = address - relevantRegion.BaseAddress;
        var bytes = relevantRegion.Bytes.Span[offset..];
        var info = InstructionSet.GetInstruction(bytes[0]);
        if (!info.IsValid)
        {
            var message = $"Warning: encountered unknown op code 0x{bytes[0]:X2} at address 0x{address:X4}";
            Console.WriteLine(message);
            return null;
        }

        if (bytes.Length < info.Size)
        {
            var message = $"Opcode {info.Mnemonic} at address 0x{address:X4} requires {info.Size} bytes, but only " +
                          $"{bytes.Length} are available";

            throw new InvalidOperationException(message);
        }

        var instruction = new DisassembledInstruction
        {
            Address = (ushort)offset,
            CPUAddress = address,
            Info = info,
            Bytes = bytes[..info.Size].ToArray(),
        };

        Disassembler.CalculateTargetAddress(instruction);

        return instruction;
    }

    private static bool IsEndOfFunction(DisassembledInstruction instruction)
    {
        // RTI and RTS are obviously the end of a function. We consider BRK and JSR
        // to be the end of a function as well because an RTI or RTS will do a function
        // call into the next instruction. This is required because RTI/RTS could be
        // returning based on a modified stack, and therefore we are not guaranteed to
        // be returning to the expected spot.
        if (instruction.Info.Mnemonic is "JSR" or "BRK" or "RTI" or "RTS")
        {
            return true;
        }

        // Since we don't know where we are jumping at compile time, this will be treated
        // as a function call, thus we consider it the end of the function.
        if (instruction.Info.AddressingMode == AddressingMode.Indirect)
        {
            return true;
        }

        return false;
    }
}