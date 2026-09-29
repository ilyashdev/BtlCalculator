using System.Numerics;

namespace Calculator.Core.Programmer;

/// <summary>
/// The integer being typed in the current radix. Digits that would not fit into the word are rejected:
/// in decimal the signed range applies, in other radixes all bits of the word can be set.
/// </summary>
public sealed class ProgrammerInput(Radix radix, WordSize size)
{
    private BigInteger _magnitude = BigInteger.Zero;

    public Radix Radix { get; } = radix;

    public bool IsNegative { get; private set; }

    /// <summary>The typed digits in the input radix, "0" when nothing was typed.</summary>
    public string Digits => ProgrammerFormatter.FormatMagnitude(_magnitude, Radix);

    public bool TryAppendDigit(int digit)
    {
        if (digit < 0 || digit >= (int)Radix)
        {
            return false;
        }

        BigInteger candidate = (_magnitude * (int)Radix) + digit;
        if (candidate > MaxMagnitude())
        {
            return false;
        }

        _magnitude = candidate;
        return true;
    }

    public void Backspace()
    {
        _magnitude /= (int)Radix;
        if (_magnitude.IsZero)
        {
            IsNegative = false;
        }
    }

    /// <summary>+/− while typing a decimal number changes its sign.</summary>
    public void ToggleSign()
    {
        IsNegative = !IsNegative;
        if (_magnitude > MaxMagnitude())
        {
            IsNegative = !IsNegative;
        }
    }

    public ulong ToRaw()
    {
        BigInteger value = IsNegative ? -_magnitude : _magnitude;
        BigInteger modulus = BigInteger.One << Word.Bits(size);
        return (ulong)(((value % modulus) + modulus) % modulus);
    }

    private BigInteger MaxMagnitude()
    {
        int bits = Word.Bits(size);
        if (Radix != Radix.Decimal)
        {
            return (BigInteger.One << bits) - 1;
        }

        // Signed range: up to 2^(bits-1) − 1, or 2^(bits-1) for a negative number.
        BigInteger half = BigInteger.One << (bits - 1);
        return IsNegative ? half : half - 1;
    }
}
