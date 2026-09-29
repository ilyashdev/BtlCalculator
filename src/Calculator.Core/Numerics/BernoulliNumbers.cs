namespace Calculator.Core.Numerics;

/// <summary>Bernoulli numbers B0, B2, B4, ... computed exactly with the Akiyama–Tanigawa algorithm.</summary>
internal static class BernoulliNumbers
{
    private static readonly Lock CacheLock = new();
    private static IReadOnlyList<Fraction> _cache = [];

    /// <summary>Returns [B0, B2, B4, ..., B(2(count-1))].</summary>
    public static IReadOnlyList<Fraction> Even(int count)
    {
        lock (CacheLock)
        {
            if (_cache.Count < count)
            {
                _cache = Compute(count);
            }

            return _cache;
        }
    }

    private static List<Fraction> Compute(int count)
    {
        int maxIndex = 2 * (count - 1);
        var row = new Fraction[maxIndex + 1];
        var even = new List<Fraction>(count);

        for (int m = 0; m <= maxIndex; m++)
        {
            row[m] = new Fraction(1, m + 1);
            for (int j = m; j >= 1; j--)
            {
                row[j - 1] = (row[j - 1] - row[j]) * j;
            }

            if (m % 2 == 0)
            {
                even.Add(row[0]);
            }
        }

        return even;
    }
}
