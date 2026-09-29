using System.Numerics;

namespace Calculator.Core.Numerics;

/// <summary>Cached powers of ten and digit counting for <see cref="BigInteger"/>.</summary>
internal static class Powers
{
    private const int CacheSize = 1024;
    private static readonly BigInteger[] Cache = CreateCache();

    public static BigInteger Of10(int exponent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        return exponent < CacheSize ? Cache[exponent] : BigInteger.Pow(10, exponent);
    }

    /// <summary>Number of decimal digits of |value|; 1 for zero.</summary>
    public static int DigitCount(BigInteger value)
    {
        value = BigInteger.Abs(value);
        if (value.IsZero)
        {
            return 1;
        }

        // log10(2) ≈ 0.30103: estimate from the bit length, then correct by at most one.
        int estimate = (int)(value.GetBitLength() * 0.30102999566398120) + 1;
        if (value < Of10(estimate - 1))
        {
            return estimate - 1;
        }

        return value >= Of10(estimate) ? estimate + 1 : estimate;
    }

    private static BigInteger[] CreateCache()
    {
        var cache = new BigInteger[CacheSize];
        cache[0] = BigInteger.One;
        for (int i = 1; i < CacheSize; i++)
        {
            cache[i] = cache[i - 1] * 10;
        }

        return cache;
    }
}
