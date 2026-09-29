using System.Numerics;

namespace Calculator.Core.Numerics;

/// <summary>A minimal exact rational number, used to generate Bernoulli numbers.</summary>
internal readonly record struct Fraction
{
    public Fraction(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new ArgumentException("The denominator must not be zero.", nameof(denominator));
        }

        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        BigInteger divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
        if (!divisor.IsOne && !divisor.IsZero)
        {
            numerator /= divisor;
            denominator /= divisor;
        }

        Numerator = numerator;
        Denominator = denominator;
    }

    public BigInteger Numerator { get; }

    public BigInteger Denominator { get; }

    public static Fraction operator -(Fraction left, Fraction right) =>
        new(left.Numerator * right.Denominator - right.Numerator * left.Denominator, left.Denominator * right.Denominator);

    public static Fraction operator *(Fraction left, BigInteger right) => new(left.Numerator * right, left.Denominator);
}
