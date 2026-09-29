using System.Numerics;
using System.Security.Cryptography;
using Calculator.Core.Numerics;

namespace Calculator.Core.Engine;

/// <summary>Source of the "rand" key values; replaceable in tests.</summary>
public interface IRandomSource
{
    /// <summary>Returns a uniformly distributed number in [0, 1) with the given number of decimal digits.</summary>
    BigDecimal Next(int digits);
}

public sealed class SystemRandomSource : IRandomSource
{
    public BigDecimal Next(int digits)
    {
        BigInteger value = BigInteger.Zero;
        for (int i = 0; i < digits; i++)
        {
            value = (value * 10) + RandomNumberGenerator.GetInt32(10);
        }

        return new BigDecimal(value, -digits);
    }
}
