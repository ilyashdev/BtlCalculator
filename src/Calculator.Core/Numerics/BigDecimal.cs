using System.Globalization;
using System.Numerics;
using System.Text;

namespace Calculator.Core.Numerics;

/// <summary>
/// An arbitrary precision decimal number: <c>Coefficient × 10^Exponent</c>.
/// Addition, subtraction and multiplication are exact. Division and all transcendental functions
/// take an explicit number of significant digits (see <see cref="BigMath"/>).
/// Values are always normalized: the coefficient has no trailing zeros and zero is stored as 0 × 10^0.
/// </summary>
public readonly struct BigDecimal : IEquatable<BigDecimal>, IComparable<BigDecimal>
{
    public static readonly BigDecimal Zero = new(BigInteger.Zero, 0);
    public static readonly BigDecimal One = new(BigInteger.One, 0);
    public static readonly BigDecimal Two = new(2, 0);
    public static readonly BigDecimal Ten = new(10, 0);
    public static readonly BigDecimal Half = new(5, -1);

    public BigDecimal(BigInteger coefficient, int exponent)
    {
        if (coefficient.IsZero)
        {
            Coefficient = BigInteger.Zero;
            Exponent = 0;
            return;
        }

        while (coefficient % 10 == 0)
        {
            coefficient /= 10;
            exponent++;
        }

        Coefficient = coefficient;
        Exponent = exponent;
    }

    public BigInteger Coefficient { get; }

    public int Exponent { get; }

    public int Sign => Coefficient.Sign;

    public bool IsZero => Coefficient.IsZero;

    public bool IsNegative => Coefficient.Sign < 0;

    public bool IsInteger => Exponent >= 0;

    /// <summary>Number of decimal digits in the coefficient (1 for zero).</summary>
    public int DigitCount => Powers.DigitCount(Coefficient);

    /// <summary>floor(log10(|value|)), i.e. the position of the most significant digit. 0 for zero.</summary>
    public int Magnitude => IsZero ? 0 : Exponent + DigitCount - 1;

    public static BigDecimal FromInteger(BigInteger value) => new(value, 0);

    public static implicit operator BigDecimal(int value) => new(value, 0);

    public static implicit operator BigDecimal(long value) => new(value, 0);

    public static implicit operator BigDecimal(BigInteger value) => new(value, 0);

    /// <summary>
    /// Parses an invariant-culture number: optional sign, digits, optional fraction, optional exponent ("-12.5e+3").
    /// </summary>
    public static BigDecimal Parse(string text)
    {
        if (!TryParse(text, out BigDecimal result))
        {
            throw new FormatException($"'{text}' is not a valid number.");
        }

        return result;
    }

    public static bool TryParse(string text, out BigDecimal result)
    {
        result = Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string s = text.Trim();
        int position = 0;
        bool negative = false;
        if (s[position] is '-' or '+')
        {
            negative = s[position] == '-';
            position++;
        }

        var digits = new StringBuilder();
        int fractionDigits = 0;
        bool seenPoint = false;
        bool seenDigit = false;
        for (; position < s.Length; position++)
        {
            char c = s[position];
            if (char.IsAsciiDigit(c))
            {
                digits.Append(c);
                seenDigit = true;
                if (seenPoint)
                {
                    fractionDigits++;
                }
            }
            else if (c == '.' && !seenPoint)
            {
                seenPoint = true;
            }
            else
            {
                break;
            }
        }

        if (!seenDigit)
        {
            return false;
        }

        int exponent = 0;
        if (position < s.Length)
        {
            if (s[position] is not ('e' or 'E'))
            {
                return false;
            }

            if (!int.TryParse(s.AsSpan(position + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
            {
                return false;
            }
        }

        BigInteger coefficient = BigInteger.Parse(digits.ToString(), CultureInfo.InvariantCulture);
        result = new BigDecimal(negative ? -coefficient : coefficient, exponent - fractionDigits);
        return true;
    }

    /// <summary>Converts a double using its shortest round-trip representation. Intended for initial estimates only.</summary>
    public static BigDecimal FromDouble(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Only finite values can be converted.");
        }

        return Parse(value.ToString("R", CultureInfo.InvariantCulture));
    }

    /// <summary>Approximate conversion to double, used for initial estimates of iterative algorithms.</summary>
    public double ToDouble()
    {
        if (IsZero)
        {
            return 0;
        }

        // Keep at most 17 significant digits so the coefficient converts without overflow.
        BigDecimal rounded = RoundToSignificantDigits(17);
        double coefficient = (double)rounded.Coefficient;
        return coefficient * Math.Pow(10, rounded.Exponent);
    }

    public BigDecimal Negate() => new(-Coefficient, Exponent);

    public BigDecimal Abs() => IsNegative ? Negate() : this;

    /// <summary>Rounds half away from zero to the given number of significant digits.</summary>
    public BigDecimal RoundToSignificantDigits(int digits)
    {
        if (digits < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(digits));
        }

        int excess = DigitCount - digits;
        return excess <= 0 ? this : DropDigits(excess);
    }

    /// <summary>Rounds half away from zero so that no digits remain below 10^-decimalPlaces.</summary>
    public BigDecimal RoundToDecimalPlaces(int decimalPlaces)
    {
        int excess = -decimalPlaces - Exponent;
        return excess <= 0 ? this : DropDigits(excess);
    }

    /// <summary>Removes the fractional part (rounds toward zero).</summary>
    public BigDecimal Truncate()
    {
        if (IsInteger)
        {
            return this;
        }

        BigInteger divisor = Powers.Of10(-Exponent);
        return new BigDecimal(BigInteger.Divide(Coefficient, divisor), 0);
    }

    public BigDecimal Floor()
    {
        BigDecimal truncated = Truncate();
        return IsNegative && truncated != this ? truncated - One : truncated;
    }

    public BigDecimal Ceiling()
    {
        BigDecimal truncated = Truncate();
        return !IsNegative && truncated != this ? truncated + One : truncated;
    }

    /// <summary>Rounds half away from zero to an integer.</summary>
    public BigDecimal RoundToInteger() => RoundToDecimalPlaces(0);

    /// <summary>Returns the value as an integer. The value must be an integer.</summary>
    public BigInteger ToBigInteger()
    {
        if (!IsInteger)
        {
            throw new InvalidOperationException("The value is not an integer.");
        }

        return Coefficient * Powers.Of10(Exponent);
    }

    /// <summary>Returns this value with the decimal point moved: this × 10^power (exact).</summary>
    public BigDecimal ScaleByPowerOfTen(int power) => new(Coefficient, Exponent + power);

    public static BigDecimal operator -(BigDecimal value) => value.Negate();

    public static BigDecimal operator +(BigDecimal left, BigDecimal right)
    {
        if (left.IsZero)
        {
            return right;
        }

        if (right.IsZero)
        {
            return left;
        }

        int exponent = Math.Min(left.Exponent, right.Exponent);
        BigInteger a = left.Coefficient * Powers.Of10(left.Exponent - exponent);
        BigInteger b = right.Coefficient * Powers.Of10(right.Exponent - exponent);
        return new BigDecimal(a + b, exponent);
    }

    public static BigDecimal operator -(BigDecimal left, BigDecimal right) => left + right.Negate();

    public static BigDecimal operator *(BigDecimal left, BigDecimal right) =>
        new(left.Coefficient * right.Coefficient, left.Exponent + right.Exponent);

    public static bool operator ==(BigDecimal left, BigDecimal right) => left.Equals(right);

    public static bool operator !=(BigDecimal left, BigDecimal right) => !left.Equals(right);

    public static bool operator <(BigDecimal left, BigDecimal right) => left.CompareTo(right) < 0;

    public static bool operator >(BigDecimal left, BigDecimal right) => left.CompareTo(right) > 0;

    public static bool operator <=(BigDecimal left, BigDecimal right) => left.CompareTo(right) <= 0;

    public static bool operator >=(BigDecimal left, BigDecimal right) => left.CompareTo(right) >= 0;

    public int CompareTo(BigDecimal other)
    {
        if (Sign != other.Sign)
        {
            return Sign.CompareTo(other.Sign);
        }

        return (this - other).Sign;
    }

    // Normalization makes the representation unique, so structural equality is value equality.
    public bool Equals(BigDecimal other) => Coefficient == other.Coefficient && Exponent == other.Exponent;

    public override bool Equals(object? obj) => obj is BigDecimal other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Coefficient, Exponent);

    /// <summary>Invariant plain or scientific representation, for diagnostics and tests.</summary>
    public override string ToString() => ToInvariantString();

    public string ToInvariantString()
    {
        if (IsZero)
        {
            return "0";
        }

        string digits = BigInteger.Abs(Coefficient).ToString(CultureInfo.InvariantCulture);
        string sign = IsNegative ? "-" : string.Empty;
        int magnitude = Magnitude;

        if (magnitude is < -7 or > 40)
        {
            string mantissa = digits.Length == 1 ? digits : digits[0] + "." + digits[1..];
            return $"{sign}{mantissa}e{(magnitude >= 0 ? "+" : "-")}{Math.Abs(magnitude)}";
        }

        if (Exponent >= 0)
        {
            return sign + digits + new string('0', Exponent);
        }

        int integerDigits = digits.Length + Exponent;
        if (integerDigits > 0)
        {
            return sign + digits[..integerDigits] + "." + digits[integerDigits..];
        }

        return sign + "0." + new string('0', -integerDigits) + digits;
    }

    private BigDecimal DropDigits(int count)
    {
        BigInteger divisor = Powers.Of10(count);
        BigInteger quotient = BigInteger.DivRem(Coefficient, divisor, out BigInteger remainder);
        if (BigInteger.Abs(remainder) * 2 >= divisor)
        {
            quotient += Coefficient.Sign;
        }

        return new BigDecimal(quotient, Exponent + count);
    }
}
