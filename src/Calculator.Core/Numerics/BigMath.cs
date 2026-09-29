using System.Numerics;

namespace Calculator.Core.Numerics;

/// <summary>
/// Arbitrary precision math on <see cref="BigDecimal"/>. Every function takes the number of significant digits
/// the result must be correct to; intermediate steps use extra guard digits.
/// Domain errors are reported with <see cref="CalculationException"/>.
/// </summary>
public static class BigMath
{
    /// <summary>Largest supported decimal exponent. Larger results overflow, smaller ones underflow to zero.</summary>
    public const int MaxMagnitude = 9999;

    /// <summary>Largest integer whose factorial is below 10^10000.</summary>
    public const int MaxFactorialArgument = 3248;

    private const int GuardDigits = 10;

    // ln(10^10000) ≈ 23025.85: arguments of exp beyond this certainly overflow.
    private static readonly BigDecimal MaxExpArgument = 23027;

    private static readonly ConstantCache PiCache = new(ComputePi);
    private static readonly ConstantCache ECache = new(p => ExpSeries(BigDecimal.One, p));
    private static readonly ConstantCache Ln10Cache = new(p => LnNearOne(BigDecimal.Ten, p));

    public static BigDecimal Pi(int precision) => PiCache.Get(precision);

    public static BigDecimal E(int precision) => ECache.Get(precision);

    public static BigDecimal Ln10(int precision) => Ln10Cache.Get(precision);

    /// <summary>Checks the result against the supported range: overflow throws, underflow returns zero.</summary>
    public static BigDecimal EnsureInRange(BigDecimal value)
    {
        if (value.IsZero)
        {
            return value;
        }

        if (value.Magnitude > MaxMagnitude)
        {
            throw new CalculationException(CalculationError.Overflow);
        }

        return value.Magnitude < -MaxMagnitude ? BigDecimal.Zero : value;
    }

    public static BigDecimal Divide(BigDecimal dividend, BigDecimal divisor, int precision)
    {
        if (divisor.IsZero)
        {
            throw new CalculationException(dividend.IsZero ? CalculationError.Undefined : CalculationError.DivideByZero);
        }

        if (dividend.IsZero)
        {
            return BigDecimal.Zero;
        }

        // Scale the dividend so that the integer quotient has at least precision + 2 digits.
        // The quotient is truncated, which never changes the rounding decision at 'precision' digits.
        int shift = Math.Max(0, precision + divisor.DigitCount - dividend.DigitCount + 2);
        BigInteger quotient = BigInteger.Divide(dividend.Coefficient * Powers.Of10(shift), divisor.Coefficient);
        var result = new BigDecimal(quotient, dividend.Exponent - divisor.Exponent - shift);
        return result.RoundToSignificantDigits(precision);
    }

    /// <summary>x mod y with the sign of y (floored division), computed exactly.</summary>
    public static BigDecimal Modulo(BigDecimal x, BigDecimal y)
    {
        if (y.IsZero)
        {
            throw new CalculationException(x.IsZero ? CalculationError.Undefined : CalculationError.DivideByZero);
        }

        int exponent = Math.Min(x.Exponent, y.Exponent);
        BigInteger a = x.Coefficient * Powers.Of10(x.Exponent - exponent);
        BigInteger b = y.Coefficient * Powers.Of10(y.Exponent - exponent);
        BigInteger remainder = BigInteger.Remainder(a, b);
        if (!remainder.IsZero && remainder.Sign != b.Sign)
        {
            remainder += b;
        }

        return new BigDecimal(remainder, exponent);
    }

    public static BigDecimal Sqrt(BigDecimal x, int precision)
    {
        if (x.IsNegative)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        if (x.IsZero)
        {
            return BigDecimal.Zero;
        }

        int workPrecision = precision + GuardDigits;

        // Scale by an even power of ten so that 1 <= m < 100 and the double estimate is accurate.
        int halfShift = (int)Math.Floor(x.Magnitude / 2.0);
        BigDecimal m = x.ScaleByPowerOfTen(-2 * halfShift);

        BigDecimal y = BigDecimal.FromDouble(Math.Sqrt(m.ToDouble()));
        for (int i = 0; i < 20; i++)
        {
            BigDecimal next = ((y + Divide(m, y, workPrecision)) * BigDecimal.Half).RoundToSignificantDigits(workPrecision);
            bool converged = IsNegligible(next - y, next, workPrecision);
            y = next;
            if (converged)
            {
                break;
            }
        }

        return y.ScaleByPowerOfTen(halfShift).RoundToSignificantDigits(precision);
    }

    /// <summary>The real n-th root. Odd roots of negative numbers are negative.</summary>
    public static BigDecimal NthRoot(BigDecimal x, BigInteger n, int precision)
    {
        if (n.IsZero)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        if (n.Sign < 0)
        {
            return Divide(BigDecimal.One, NthRoot(x, -n, precision + GuardDigits), precision);
        }

        if (n.IsOne)
        {
            return x.RoundToSignificantDigits(precision);
        }

        if (x.IsZero)
        {
            return BigDecimal.Zero;
        }

        if (x.IsNegative)
        {
            if (n.IsEven)
            {
                throw new CalculationException(CalculationError.InvalidInput);
            }

            return NthRoot(x.Negate(), n, precision).Negate();
        }

        if (n == 2)
        {
            return Sqrt(x, precision);
        }

        int workPrecision = precision + GuardDigits;
        BigDecimal root = Exp(Divide(Ln(x, workPrecision), n, workPrecision), workPrecision);

        // Newton steps on y^n = x remove the error of exp/ln so exact roots such as ∛27 come out exact.
        if (n <= 1000)
        {
            int exponent = (int)n;
            for (int i = 0; i < 2; i++)
            {
                BigDecimal power = PowInteger(root, exponent - 1, workPrecision);
                BigDecimal correction = Divide((power * root) - x, power * n, workPrecision);
                root = (root - correction).RoundToSignificantDigits(workPrecision);
            }
        }

        return root.RoundToSignificantDigits(precision);
    }

    public static BigDecimal Exp(BigDecimal x, int precision)
    {
        if (x.IsZero)
        {
            return BigDecimal.One;
        }

        if (x > MaxExpArgument)
        {
            throw new CalculationException(CalculationError.Overflow);
        }

        if (x < MaxExpArgument.Negate())
        {
            return BigDecimal.Zero;
        }

        // e^x = e^n · e^f with n = round(x) and |f| <= 0.5.
        int workPrecision = precision + GuardDigits + Math.Max(0, x.Magnitude + 1);
        BigDecimal n = x.RoundToInteger();
        BigDecimal f = x - n;

        BigDecimal result = ExpSeries(f, workPrecision);
        if (!n.IsZero)
        {
            result *= PowInteger(E(workPrecision), (int)n.ToBigInteger(), workPrecision);
        }

        return EnsureInRange(result.RoundToSignificantDigits(precision));
    }

    public static BigDecimal Ln(BigDecimal x, int precision)
    {
        if (x.Sign <= 0)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        if (x == BigDecimal.One)
        {
            return BigDecimal.Zero;
        }

        // Near 1 the logarithm is computed directly to avoid cancellation between ln(m) and k·ln(10).
        if (x > BigDecimal.Half && x < BigDecimal.Two)
        {
            return LnNearOne(x, precision);
        }

        int workPrecision = precision + GuardDigits + Powers.DigitCount(x.Magnitude);
        int k = x.Magnitude;
        BigDecimal m = x.ScaleByPowerOfTen(-k);
        BigDecimal result = LnNearOne(m, workPrecision) + (Ln10(workPrecision) * k);
        return result.RoundToSignificantDigits(precision);
    }

    public static BigDecimal Log10(BigDecimal x, int precision)
    {
        if (x.Sign <= 0)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        // Exact powers of ten have exact logarithms.
        if (BigInteger.Abs(x.Coefficient).IsOne)
        {
            return x.Exponent;
        }

        int workPrecision = precision + GuardDigits;
        return Divide(Ln(x, workPrecision), Ln10(workPrecision), precision);
    }

    /// <summary>log base b of x.</summary>
    public static BigDecimal LogBase(BigDecimal x, BigDecimal b, int precision)
    {
        if (x.Sign <= 0 || b.Sign <= 0 || b == BigDecimal.One)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        int workPrecision = precision + GuardDigits;
        return Divide(Ln(x, workPrecision), Ln(b, workPrecision), precision);
    }

    public static BigDecimal Pow(BigDecimal x, BigDecimal y, int precision)
    {
        if (y.IsZero)
        {
            return BigDecimal.One;
        }

        if (x.IsZero)
        {
            if (y.IsNegative)
            {
                throw new CalculationException(CalculationError.DivideByZero);
            }

            return BigDecimal.Zero;
        }

        int workPrecision = precision + GuardDigits;

        if (y.IsInteger && y.Abs() <= 100_000)
        {
            // Estimate the magnitude first so that 10^99999 style inputs fail fast instead of computing huge numbers.
            double magnitude = (double)y.ToBigInteger() * Math.Log10(Math.Abs(x.ToDouble()) + double.Epsilon);
            if (magnitude > MaxMagnitude + 1 && x.Magnitude >= 0)
            {
                throw new CalculationException(CalculationError.Overflow);
            }

            BigDecimal power = PowInteger(x, (int)y.Abs().ToBigInteger(), workPrecision + y.Abs().DigitCount);
            BigDecimal result = y.IsNegative ? Divide(BigDecimal.One, power, workPrecision) : power;
            return EnsureInRange(result.RoundToSignificantDigits(precision));
        }

        if (x.IsNegative)
        {
            // A negative base only has a real power when the exponent is a fraction with an odd denominator.
            for (int denominator = 3; denominator < 100; denominator += 2)
            {
                BigDecimal numerator = (y * denominator).RoundToSignificantDigits(workPrecision);
                BigDecimal roundedNumerator = numerator.RoundToInteger();
                if (IsNegligible(numerator - roundedNumerator, BigDecimal.One, precision))
                {
                    BigDecimal magnitude = Pow(x.Negate(), y, precision);
                    bool oddNumerator = !roundedNumerator.ToBigInteger().IsEven;
                    return oddNumerator ? magnitude.Negate() : magnitude;
                }
            }

            throw new CalculationException(CalculationError.InvalidInput);
        }

        BigDecimal exponent = y * Ln(x, workPrecision + Math.Max(0, y.Magnitude));
        return Exp(exponent.RoundToSignificantDigits(workPrecision), precision);
    }

    /// <summary>10^x, exact when x is an integer.</summary>
    public static BigDecimal PowerOfTen(BigDecimal x, int precision)
    {
        if (x.IsInteger)
        {
            if (x.Abs() > MaxMagnitude)
            {
                if (x.IsNegative)
                {
                    return BigDecimal.Zero;
                }

                throw new CalculationException(CalculationError.Overflow);
            }

            return BigDecimal.One.ScaleByPowerOfTen((int)x.ToBigInteger());
        }

        return Pow(BigDecimal.Ten, x, precision);
    }

    public static BigDecimal Sin(BigDecimal x, AngleUnit unit, int precision) => SinCos(x, unit, precision).Sin;

    public static BigDecimal Cos(BigDecimal x, AngleUnit unit, int precision) => SinCos(x, unit, precision).Cos;

    public static BigDecimal Tan(BigDecimal x, AngleUnit unit, int precision)
    {
        (BigDecimal sin, BigDecimal cos) = SinCos(x, unit, precision + GuardDigits);
        if (cos.IsZero)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        return Divide(sin, cos, precision);
    }

    public static BigDecimal Sec(BigDecimal x, AngleUnit unit, int precision)
    {
        BigDecimal cos = SinCos(x, unit, precision + GuardDigits).Cos;
        return cos.IsZero ? throw new CalculationException(CalculationError.InvalidInput) : Divide(BigDecimal.One, cos, precision);
    }

    public static BigDecimal Csc(BigDecimal x, AngleUnit unit, int precision)
    {
        BigDecimal sin = SinCos(x, unit, precision + GuardDigits).Sin;
        return sin.IsZero ? throw new CalculationException(CalculationError.InvalidInput) : Divide(BigDecimal.One, sin, precision);
    }

    public static BigDecimal Cot(BigDecimal x, AngleUnit unit, int precision)
    {
        (BigDecimal sin, BigDecimal cos) = SinCos(x, unit, precision + GuardDigits);
        return sin.IsZero ? throw new CalculationException(CalculationError.InvalidInput) : Divide(cos, sin, precision);
    }

    public static BigDecimal Asin(BigDecimal x, AngleUnit unit, int precision) =>
        FromRadians(AsinRadians(x, precision + GuardDigits), unit, precision);

    public static BigDecimal Acos(BigDecimal x, AngleUnit unit, int precision)
    {
        int workPrecision = precision + GuardDigits;
        BigDecimal radians = (Pi(workPrecision) * BigDecimal.Half) - AsinRadians(x, workPrecision);
        return FromRadians(radians, unit, precision);
    }

    public static BigDecimal Atan(BigDecimal x, AngleUnit unit, int precision) =>
        FromRadians(AtanRadians(x, precision + GuardDigits), unit, precision);

    public static BigDecimal Asec(BigDecimal x, AngleUnit unit, int precision)
    {
        if (x.Abs() < BigDecimal.One)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        return Acos(Divide(BigDecimal.One, x, precision + GuardDigits), unit, precision);
    }

    public static BigDecimal Acsc(BigDecimal x, AngleUnit unit, int precision)
    {
        if (x.Abs() < BigDecimal.One)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        return Asin(Divide(BigDecimal.One, x, precision + GuardDigits), unit, precision);
    }

    /// <summary>Inverse cotangent with the principal value in (0, π).</summary>
    public static BigDecimal Acot(BigDecimal x, AngleUnit unit, int precision)
    {
        int workPrecision = precision + GuardDigits;
        BigDecimal radians = (Pi(workPrecision) * BigDecimal.Half) - AtanRadians(x, workPrecision);
        return FromRadians(radians, unit, precision);
    }

    public static BigDecimal Sinh(BigDecimal x, int precision)
    {
        int workPrecision = precision + GuardDigits;
        if (x.Abs() < BigDecimal.One)
        {
            // The series avoids the cancellation of (e^x − e^−x) for small x.
            return SinhSeries(x, workPrecision).RoundToSignificantDigits(precision);
        }

        BigDecimal ex = Exp(x, workPrecision);
        BigDecimal result = (ex - Divide(BigDecimal.One, ex, workPrecision)) * BigDecimal.Half;
        return result.RoundToSignificantDigits(precision);
    }

    public static BigDecimal Cosh(BigDecimal x, int precision)
    {
        int workPrecision = precision + GuardDigits;
        BigDecimal ex = Exp(x, workPrecision);
        BigDecimal result = (ex + Divide(BigDecimal.One, ex, workPrecision)) * BigDecimal.Half;
        return result.RoundToSignificantDigits(precision);
    }

    public static BigDecimal Tanh(BigDecimal x, int precision)
    {
        // For |x| > precision the result differs from ±1 by less than 10^-precision.
        if (x.Abs() > precision + 2)
        {
            return x.IsNegative ? BigDecimal.One.Negate() : BigDecimal.One;
        }

        int workPrecision = precision + GuardDigits;
        return Divide(Sinh(x, workPrecision), Cosh(x, workPrecision), precision);
    }

    public static BigDecimal Sech(BigDecimal x, int precision) =>
        Divide(BigDecimal.One, Cosh(x, precision + GuardDigits), precision);

    public static BigDecimal Csch(BigDecimal x, int precision)
    {
        if (x.IsZero)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        return Divide(BigDecimal.One, Sinh(x, precision + GuardDigits), precision);
    }

    public static BigDecimal Coth(BigDecimal x, int precision)
    {
        if (x.IsZero)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        return Divide(BigDecimal.One, Tanh(x, precision + GuardDigits), precision);
    }

    public static BigDecimal Asinh(BigDecimal x, int precision)
    {
        if (x.IsZero)
        {
            return BigDecimal.Zero;
        }

        // Extra digits compensate the cancellation in ln(1 + small) for small x.
        int workPrecision = precision + GuardDigits + Math.Max(0, -x.Magnitude);
        BigDecimal absolute = x.Abs();
        BigDecimal root = Sqrt((absolute * absolute) + BigDecimal.One, workPrecision);
        BigDecimal result = Ln(absolute + root, workPrecision);
        return (x.IsNegative ? result.Negate() : result).RoundToSignificantDigits(precision);
    }

    public static BigDecimal Acosh(BigDecimal x, int precision)
    {
        if (x < BigDecimal.One)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        int workPrecision = precision + GuardDigits;
        BigDecimal root = Sqrt((x * x) - BigDecimal.One, workPrecision);
        return Ln(x + root, workPrecision).RoundToSignificantDigits(precision);
    }

    public static BigDecimal Atanh(BigDecimal x, int precision)
    {
        if (x.Abs() >= BigDecimal.One)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        if (x.IsZero)
        {
            return BigDecimal.Zero;
        }

        int workPrecision = precision + GuardDigits + Math.Max(0, -x.Magnitude);
        BigDecimal ratio = Divide(BigDecimal.One + x, BigDecimal.One - x, workPrecision);
        return (Ln(ratio, workPrecision) * BigDecimal.Half).RoundToSignificantDigits(precision);
    }

    public static BigDecimal Asech(BigDecimal x, int precision)
    {
        if (x.Sign <= 0 || x > BigDecimal.One)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        return Acosh(Divide(BigDecimal.One, x, precision + GuardDigits), precision);
    }

    public static BigDecimal Acsch(BigDecimal x, int precision)
    {
        if (x.IsZero)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        return Asinh(Divide(BigDecimal.One, x, precision + GuardDigits), precision);
    }

    public static BigDecimal Acoth(BigDecimal x, int precision)
    {
        if (x.Abs() <= BigDecimal.One)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        return Atanh(Divide(BigDecimal.One, x, precision + GuardDigits), precision);
    }

    /// <summary>x! for non-negative integers, Γ(x + 1) for other values.</summary>
    public static BigDecimal Factorial(BigDecimal x, int precision)
    {
        if (x.IsInteger)
        {
            if (x.IsNegative)
            {
                throw new CalculationException(CalculationError.InvalidInput);
            }

            if (x > MaxFactorialArgument)
            {
                throw new CalculationException(CalculationError.Overflow);
            }

            int n = (int)x.ToBigInteger();
            BigInteger product = BigInteger.One;
            for (int i = 2; i <= n; i++)
            {
                product *= i;
            }

            return new BigDecimal(product, 0).RoundToSignificantDigits(precision);
        }

        return Gamma(x + BigDecimal.One, precision);
    }

    /// <summary>The gamma function for non-integer arguments.</summary>
    public static BigDecimal Gamma(BigDecimal z, int precision)
    {
        if (z.IsInteger && z.Sign <= 0)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        if (z > MaxFactorialArgument + 1)
        {
            throw new CalculationException(CalculationError.Overflow);
        }

        // |Γ(z)| < 10^-10000 for such arguments: the result underflows to zero.
        if (z < -(MaxFactorialArgument + 1))
        {
            return BigDecimal.Zero;
        }

        // Γ(z) = Γ(z + N) / (z (z+1) ... (z+N−1)); Stirling's series is accurate for the shifted argument z + N >= precision.
        int workPrecision = precision + GuardDigits;
        int shift = Math.Max(0, workPrecision - (int)z.Floor().ToBigInteger());
        BigDecimal w = z + shift;

        BigDecimal product = BigDecimal.One;
        for (int i = 0; i < shift; i++)
        {
            product = (product * (z + i)).RoundToSignificantDigits(workPrecision);
        }

        BigDecimal lnGammaW = LnGammaStirling(w, workPrecision);
        BigDecimal lnProduct = Ln(product.Abs(), workPrecision);
        BigDecimal result = Exp((lnGammaW - lnProduct).RoundToSignificantDigits(workPrecision), workPrecision);
        if (product.IsNegative)
        {
            result = result.Negate();
        }

        return result.RoundToSignificantDigits(precision);
    }

    /// <summary>Multiplies x by itself exponent times (binary exponentiation), rounding to the given precision.</summary>
    public static BigDecimal PowInteger(BigDecimal x, int exponent, int precision)
    {
        if (exponent < 0)
        {
            return Divide(BigDecimal.One, PowInteger(x, -exponent, precision + GuardDigits), precision);
        }

        BigDecimal result = BigDecimal.One;
        BigDecimal factor = x;
        int remaining = exponent;
        while (remaining > 0)
        {
            if ((remaining & 1) == 1)
            {
                result = (result * factor).RoundToSignificantDigits(precision);
            }

            remaining >>= 1;
            if (remaining > 0)
            {
                factor = (factor * factor).RoundToSignificantDigits(precision);
                if (factor.Magnitude > 10 * MaxMagnitude)
                {
                    throw new CalculationException(CalculationError.Overflow);
                }
            }
        }

        return result;
    }

    /// <summary>Converts an angle in radians to the given unit.</summary>
    public static BigDecimal FromRadians(BigDecimal radians, AngleUnit unit, int precision)
    {
        int workPrecision = precision + GuardDigits;
        return unit switch
        {
            AngleUnit.Degrees => Divide(radians * 180, Pi(workPrecision), precision),
            AngleUnit.Gradians => Divide(radians * 200, Pi(workPrecision), precision),
            _ => radians.RoundToSignificantDigits(precision),
        };
    }

    private static (BigDecimal Sin, BigDecimal Cos) SinCos(BigDecimal x, AngleUnit unit, int precision)
    {
        int workPrecision = precision + GuardDigits;
        BigDecimal radians = x;

        if (unit != AngleUnit.Radians)
        {
            // Reduce exactly in degrees/gradians so that sin(180°) is exactly 0 and cos(90°) is exactly 0.
            BigDecimal fullTurn = unit == AngleUnit.Degrees ? 360 : 400;
            BigDecimal quarterTurn = unit == AngleUnit.Degrees ? 90 : 100;
            BigDecimal reduced = Modulo(x, fullTurn);

            BigDecimal quarters = Modulo(reduced, quarterTurn);
            if (quarters.IsZero)
            {
                int quadrant = (int)Divide(reduced, quarterTurn, 10).ToBigInteger();
                return quadrant switch
                {
                    0 => (BigDecimal.Zero, BigDecimal.One),
                    1 => (BigDecimal.One, BigDecimal.Zero),
                    2 => (BigDecimal.Zero, BigDecimal.One.Negate()),
                    _ => (BigDecimal.One.Negate(), BigDecimal.Zero),
                };
            }

            BigDecimal halfTurn = unit == AngleUnit.Degrees ? 180 : 200;
            radians = Divide(reduced * Pi(workPrecision), halfTurn, workPrecision);
        }

        if (radians.Magnitude >= precision)
        {
            // The angle is so large that its position within the period is not known to any digit.
            throw new CalculationException(CalculationError.InvalidInput);
        }

        workPrecision += Math.Max(0, radians.Magnitude + 1);
        BigDecimal halfPi = Pi(workPrecision) * BigDecimal.Half;
        BigDecimal k = Divide(radians, halfPi, workPrecision).RoundToInteger();
        BigDecimal r = (radians - (k * halfPi)).RoundToSignificantDigits(workPrecision);
        int octant = (int)Modulo(k, 4).ToBigInteger();

        BigDecimal sinR = SinSeries(r, workPrecision);
        BigDecimal cosR = CosSeries(r, workPrecision);
        (BigDecimal sin, BigDecimal cos) = octant switch
        {
            0 => (sinR, cosR),
            1 => (cosR, sinR.Negate()),
            2 => (sinR.Negate(), cosR.Negate()),
            _ => (cosR.Negate(), sinR),
        };

        // For a non-tiny argument, a result below 10^-(precision-2) is the rounding error of π (e.g. sin(π)): it is zero.
        if (radians.Magnitude > -5)
        {
            sin = SnapToZero(sin, precision);
            cos = SnapToZero(cos, precision);
        }

        return (sin.RoundToSignificantDigits(precision), cos.RoundToSignificantDigits(precision));
    }

    private static BigDecimal SnapToZero(BigDecimal value, int precision) =>
        !value.IsZero && value.Magnitude < -(precision - 2) ? BigDecimal.Zero : value;

    private static BigDecimal AsinRadians(BigDecimal x, int precision)
    {
        BigDecimal absolute = x.Abs();
        if (absolute > BigDecimal.One)
        {
            throw new CalculationException(CalculationError.InvalidInput);
        }

        if (absolute == BigDecimal.One)
        {
            BigDecimal halfPi = Pi(precision) * BigDecimal.Half;
            return x.IsNegative ? halfPi.Negate() : halfPi;
        }

        BigDecimal root = Sqrt(BigDecimal.One - (x * x), precision + GuardDigits);
        return AtanRadians(Divide(x, root, precision + GuardDigits), precision);
    }

    private static BigDecimal AtanRadians(BigDecimal x, int precision)
    {
        if (x.IsZero)
        {
            return BigDecimal.Zero;
        }

        int workPrecision = precision + GuardDigits;
        if (x.Abs() > BigDecimal.One)
        {
            // atan(x) = ±π/2 − atan(1/x)
            BigDecimal halfPi = Pi(workPrecision) * BigDecimal.Half;
            BigDecimal inner = AtanRadians(Divide(BigDecimal.One, x, workPrecision), workPrecision);
            BigDecimal result = (x.IsNegative ? halfPi.Negate() : halfPi) - inner;
            return result.RoundToSignificantDigits(precision);
        }

        // atan(x) = 2·atan(x / (1 + √(1 + x²))): four halvings make the series converge quickly.
        BigDecimal value = x;
        int halvings = 4;
        for (int i = 0; i < halvings; i++)
        {
            BigDecimal root = Sqrt(BigDecimal.One + (value * value), workPrecision);
            value = Divide(value, BigDecimal.One + root, workPrecision);
        }

        BigDecimal series = AtanSeries(value, workPrecision);
        return (series * (1 << halvings)).RoundToSignificantDigits(precision);
    }

    private static BigDecimal ExpSeries(BigDecimal x, int precision)
    {
        BigDecimal sum = BigDecimal.One;
        BigDecimal term = BigDecimal.One;
        for (int k = 1; k < 10_000; k++)
        {
            term = Divide(term * x, k, precision);
            if (IsNegligible(term, sum, precision))
            {
                break;
            }

            sum += term;
        }

        return sum.RoundToSignificantDigits(precision);
    }

    private static BigDecimal SinSeries(BigDecimal x, int precision)
    {
        BigDecimal x2 = (x * x).RoundToSignificantDigits(precision);
        BigDecimal sum = x;
        BigDecimal term = x;
        for (int k = 1; k < 10_000; k++)
        {
            term = Divide((term * x2).Negate(), (2 * k) * ((2 * k) + 1), precision);
            if (IsNegligible(term, sum, precision))
            {
                break;
            }

            sum += term;
        }

        return sum.RoundToSignificantDigits(precision);
    }

    private static BigDecimal CosSeries(BigDecimal x, int precision)
    {
        BigDecimal x2 = (x * x).RoundToSignificantDigits(precision);
        BigDecimal sum = BigDecimal.One;
        BigDecimal term = BigDecimal.One;
        for (int k = 1; k < 10_000; k++)
        {
            term = Divide((term * x2).Negate(), ((2 * k) - 1) * (2 * k), precision);
            if (IsNegligible(term, sum, precision))
            {
                break;
            }

            sum += term;
        }

        return sum.RoundToSignificantDigits(precision);
    }

    private static BigDecimal SinhSeries(BigDecimal x, int precision)
    {
        BigDecimal x2 = (x * x).RoundToSignificantDigits(precision);
        BigDecimal sum = x;
        BigDecimal term = x;
        for (int k = 1; k < 10_000; k++)
        {
            term = Divide(term * x2, (2 * k) * ((2 * k) + 1), precision);
            if (IsNegligible(term, sum, precision))
            {
                break;
            }

            sum += term;
        }

        return sum.RoundToSignificantDigits(precision);
    }

    private static BigDecimal AtanSeries(BigDecimal x, int precision)
    {
        BigDecimal x2 = (x * x).RoundToSignificantDigits(precision);
        BigDecimal sum = x;
        BigDecimal power = x;
        for (int k = 1; k < 10_000; k++)
        {
            power = (power * x2).Negate().RoundToSignificantDigits(precision);
            BigDecimal term = Divide(power, (2 * k) + 1, precision);
            if (IsNegligible(term, sum, precision))
            {
                break;
            }

            sum += term;
        }

        return sum.RoundToSignificantDigits(precision);
    }

    /// <summary>ln(x) for x in roughly [0.5, 10] by Halley's iteration on e^y = x, starting from a double estimate.</summary>
    private static BigDecimal LnNearOne(BigDecimal x, int precision)
    {
        if (x == BigDecimal.One)
        {
            return BigDecimal.Zero;
        }

        // Close to 1 the result is small: extra digits keep its relative precision.
        BigDecimal distance = (x - BigDecimal.One).Abs();
        int workPrecision = precision + GuardDigits + Math.Max(0, -distance.Magnitude);

        BigDecimal y = BigDecimal.FromDouble(Math.Log(x.ToDouble()));
        for (int i = 0; i < 20; i++)
        {
            BigDecimal ey = Exp(y, workPrecision);
            BigDecimal correction = Divide((x - ey) * BigDecimal.Two, x + ey, workPrecision);
            y = (y + correction).RoundToSignificantDigits(workPrecision);
            if (IsNegligible(correction, y, workPrecision))
            {
                break;
            }
        }

        return y.RoundToSignificantDigits(precision);
    }

    /// <summary>ln Γ(w) by Stirling's series; accurate when w is at least about the requested number of digits.</summary>
    private static BigDecimal LnGammaStirling(BigDecimal w, int precision)
    {
        int workPrecision = precision + GuardDigits;
        BigDecimal lnW = Ln(w, workPrecision);
        BigDecimal lnTwoPi = Ln(Pi(workPrecision) * BigDecimal.Two, workPrecision);

        BigDecimal sum = ((w - BigDecimal.Half) * lnW) - w + (lnTwoPi * BigDecimal.Half);
        BigDecimal w2 = (w * w).RoundToSignificantDigits(workPrecision);
        BigDecimal wPower = w; // w^(2k-1)

        IReadOnlyList<Fraction> bernoulli = BernoulliNumbers.Even(60);
        for (int k = 1; k < bernoulli.Count; k++)
        {
            Fraction b = bernoulli[k];
            BigInteger denominator = b.Denominator * (2 * k) * ((2 * k) - 1);
            BigDecimal term = Divide(Divide(b.Numerator, denominator, workPrecision), wPower, workPrecision);
            if (IsNegligible(term, sum, workPrecision))
            {
                break;
            }

            sum += term;
            wPower = (wPower * w2).RoundToSignificantDigits(workPrecision);
        }

        return sum.RoundToSignificantDigits(precision);
    }

    private static bool IsNegligible(BigDecimal term, BigDecimal reference, int precision)
    {
        if (term.IsZero)
        {
            return true;
        }

        int referenceMagnitude = reference.IsZero ? 0 : reference.Magnitude;
        return term.Magnitude < referenceMagnitude - precision - 1;
    }

    private static BigDecimal ComputePi(int precision)
    {
        // Machin's formula π = 16·atan(1/5) − 4·atan(1/239) in fixed point integer arithmetic.
        BigInteger unity = Powers.Of10(precision + GuardDigits);
        BigInteger pi = (16 * ArctanReciprocal(5, unity)) - (4 * ArctanReciprocal(239, unity));
        return new BigDecimal(pi, -(precision + GuardDigits)).RoundToSignificantDigits(precision);
    }

    private static BigInteger ArctanReciprocal(int x, BigInteger unity)
    {
        BigInteger power = unity / x;
        BigInteger sum = power;
        BigInteger xSquared = x * x;
        for (int n = 1; ; n++)
        {
            power /= xSquared;
            BigInteger term = power / ((2 * n) + 1);
            if (term.IsZero)
            {
                return sum;
            }

            sum += (n % 2 == 1) ? -term : term;
        }
    }

    /// <summary>Thread-safe cache of a constant computed to the largest precision requested so far.</summary>
    private sealed class ConstantCache(Func<int, BigDecimal> compute)
    {
        private readonly Lock _lock = new();
        private int _precision;
        private BigDecimal _value;

        public BigDecimal Get(int precision)
        {
            lock (_lock)
            {
                if (_precision < precision)
                {
                    _value = compute(precision + GuardDigits);
                    _precision = precision;
                }

                return _value.RoundToSignificantDigits(precision);
            }
        }
    }
}
