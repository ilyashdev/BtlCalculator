using System.Numerics;
using Calculator.Core.Numerics;

namespace Calculator.Core.Programmer;

/// <summary>
/// Fixed-width two's complement integer operations. Values are kept as the raw bit pattern in a <see cref="ulong"/>,
/// masked to the word size; <see cref="ToSigned"/> gives the signed interpretation shown in decimal.
/// </summary>
public static class Word
{
    public static int Bits(WordSize size) => (int)size;

    public static ulong Mask(WordSize size) => size == WordSize.QWord ? ulong.MaxValue : (1UL << Bits(size)) - 1;

    public static ulong Truncate(ulong raw, WordSize size) => raw & Mask(size);

    public static ulong FromSigned(long value, WordSize size) => Truncate(unchecked((ulong)value), size);

    public static long ToSigned(ulong raw, WordSize size)
    {
        int bits = Bits(size);
        if (bits == 64)
        {
            return unchecked((long)raw);
        }

        // Sign extension: move the sign bit to bit 63 and shift back arithmetically.
        int unused = 64 - bits;
        return unchecked((long)(raw << unused)) >> unused;
    }

    public static bool GetBit(ulong raw, int index) => ((raw >> index) & 1) == 1;

    public static ulong ToggleBit(ulong raw, int index, WordSize size) =>
        index < Bits(size) ? raw ^ (1UL << index) : raw;

    /// <summary>Converts a decimal value to the word, truncating the fraction and wrapping around like the original.</summary>
    public static ulong FromDecimal(BigDecimal value, WordSize size)
    {
        BigInteger integer = value.Truncate().ToBigInteger();
        BigInteger modulus = BigInteger.One << Bits(size);
        BigInteger wrapped = ((integer % modulus) + modulus) % modulus;
        return (ulong)wrapped;
    }

    public static ulong Apply(ProgrammerOperator op, ulong left, ulong right, WordSize size, ShiftMode shiftMode)
    {
        long a = ToSigned(left, size);
        long b = ToSigned(right, size);

        ulong result = op switch
        {
            ProgrammerOperator.Add => unchecked((ulong)(a + b)),
            ProgrammerOperator.Subtract => unchecked((ulong)(a - b)),
            ProgrammerOperator.Multiply => unchecked((ulong)(a * b)),
            ProgrammerOperator.Divide => unchecked((ulong)Divide(a, b)),
            ProgrammerOperator.Modulo => unchecked((ulong)Remainder(a, b)),
            ProgrammerOperator.And => left & right,
            ProgrammerOperator.Or => left | right,
            ProgrammerOperator.Xor => left ^ right,
            ProgrammerOperator.Nand => ~(left & right),
            ProgrammerOperator.Nor => ~(left | right),
            ProgrammerOperator.LeftShift => ShiftLeft(left, b, size),
            ProgrammerOperator.RightShift => ShiftRight(left, b, size, shiftMode),
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
        };

        return Truncate(result, size);
    }

    /// <summary>Applies a function; rotations through carry read and update the carry bit.</summary>
    public static ulong Apply(ProgrammerFunction function, ulong value, WordSize size, ref bool carry)
    {
        int bits = Bits(size);
        ulong topBit = 1UL << (bits - 1);

        ulong result;
        switch (function)
        {
            case ProgrammerFunction.Negate:
                result = unchecked((ulong)-ToSigned(value, size));
                break;
            case ProgrammerFunction.Not:
                result = ~value;
                break;
            case ProgrammerFunction.RotateLeft:
                result = (value << 1) | ((value & topBit) != 0 ? 1UL : 0UL);
                break;
            case ProgrammerFunction.RotateRight:
                result = (value >> 1) | ((value & 1) != 0 ? topBit : 0UL);
                break;
            case ProgrammerFunction.RotateLeftThroughCarry:
            {
                bool newCarry = (value & topBit) != 0;
                result = (value << 1) | (carry ? 1UL : 0UL);
                carry = newCarry;
                break;
            }

            case ProgrammerFunction.RotateRightThroughCarry:
            {
                bool newCarry = (value & 1) != 0;
                result = (value >> 1) | (carry ? topBit : 0UL);
                carry = newCarry;
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(function), function, null);
        }

        return Truncate(result, size);
    }

    private static long Divide(long a, long b)
    {
        if (b == 0)
        {
            throw new CalculationException(a == 0 ? CalculationError.Undefined : CalculationError.DivideByZero);
        }

        // long.MinValue / -1 does not fit: two's complement wraps around to long.MinValue.
        return b == -1 ? unchecked(-a) : a / b;
    }

    private static long Remainder(long a, long b)
    {
        if (b == 0)
        {
            throw new CalculationException(a == 0 ? CalculationError.Undefined : CalculationError.DivideByZero);
        }

        return b == -1 ? 0 : a % b;
    }

    private static ulong ShiftLeft(ulong value, long amount, WordSize size)
    {
        if (amount < 0)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        return amount >= Bits(size) ? 0 : value << (int)amount;
    }

    private static ulong ShiftRight(ulong value, long amount, WordSize size, ShiftMode mode)
    {
        if (amount < 0)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        int bits = Bits(size);
        if (mode == ShiftMode.Logical)
        {
            return amount >= bits ? 0 : value >> (int)amount;
        }

        // Arithmetic: the sign bit is copied into the vacated bits.
        long signed = ToSigned(value, size);
        int shift = (int)Math.Min(amount, 63);
        return unchecked((ulong)(signed >> shift));
    }
}
