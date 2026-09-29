using System.Globalization;
using System.Numerics;
using System.Text;

namespace Calculator.Core.Programmer;

/// <summary>
/// Formats word values the way the programmer mode shows them: signed decimal with digit grouping,
/// HEX in groups of 4, OCT in groups of 3 and BIN in groups of 4 (the raw two's complement bits).
/// </summary>
public static class ProgrammerFormatter
{
    public static string Format(ulong raw, Radix radix, WordSize size, string decimalGroupSeparator = ",")
    {
        if (radix == Radix.Decimal)
        {
            long signed = Word.ToSigned(raw, size);
            string digits = FormatMagnitude(BigInteger.Abs(new BigInteger(signed)), Radix.Decimal);
            return (signed < 0 ? "-" : string.Empty) + Group(digits, 3, decimalGroupSeparator);
        }

        string magnitude = FormatMagnitude(raw, radix);
        if (radix == Radix.Binary && magnitude != "0")
        {
            // Binary is shown in whole nibbles: 3 is "0011".
            int padded = (magnitude.Length + 3) / 4 * 4;
            magnitude = magnitude.PadLeft(padded, '0');
        }

        return Group(magnitude, GroupSize(radix), " ");
    }

    /// <summary>Formats the digits being typed, keeping the sign of a decimal number.</summary>
    public static string FormatInput(ProgrammerInput input, string decimalGroupSeparator = ",")
    {
        string grouped = Group(input.Digits, GroupSize(input.Radix), input.Radix == Radix.Decimal ? decimalGroupSeparator : " ");
        return input.IsNegative ? "-" + grouped : grouped;
    }

    public static string FormatMagnitude(BigInteger magnitude, Radix radix)
    {
        if (magnitude.IsZero)
        {
            return "0";
        }

        var digits = new StringBuilder();
        BigInteger value = BigInteger.Abs(magnitude);
        int radixValue = (int)radix;
        while (!value.IsZero)
        {
            int digit = (int)(value % radixValue);
            digits.Insert(0, digit < 10 ? (char)('0' + digit) : (char)('A' + digit - 10));
            value /= radixValue;
        }

        return digits.ToString();
    }

    private static int GroupSize(Radix radix) => radix switch
    {
        Radix.Octal => 3,
        Radix.Decimal => 3,
        _ => 4,
    };

    private static string Group(string digits, int size, string separator)
    {
        if (digits.Length <= size)
        {
            return digits;
        }

        var text = new StringBuilder();
        int first = digits.Length % size;
        if (first > 0)
        {
            text.Append(digits, 0, first);
        }

        for (int i = first; i < digits.Length; i += size)
        {
            if (text.Length > 0)
            {
                text.Append(separator);
            }

            text.Append(digits, i, size);
        }

        return text.ToString();
    }

    public static string ToInvariantDecimal(ulong raw, WordSize size) =>
        Word.ToSigned(raw, size).ToString(CultureInfo.InvariantCulture);
}
