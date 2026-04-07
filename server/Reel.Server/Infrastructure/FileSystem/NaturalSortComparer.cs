namespace Reel.Server.Infrastructure.FileSystem;

using System.Globalization;

public sealed class NaturalSortComparer : IComparer<string>
{
    public static readonly NaturalSortComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var ix = 0;
        var iy = 0;

        while (ix < x.Length && iy < y.Length)
        {
            var cx = x[ix];
            var cy = y[iy];

            if (char.IsDigit(cx) && char.IsDigit(cy))
            {
                // Compare numeric segments as integers
                var numX = ReadNumericSegment(x, ref ix);
                var numY = ReadNumericSegment(y, ref iy);

                var cmp = numX.CompareTo(numY);
                if (cmp != 0) return cmp;
            }
            else
            {
                var cmp = char.ToLower(cx, CultureInfo.InvariantCulture)
                    .CompareTo(char.ToLower(cy, CultureInfo.InvariantCulture));
                if (cmp != 0) return cmp;

                ix++;
                iy++;
            }
        }

        return x.Length.CompareTo(y.Length);
    }

    private static long ReadNumericSegment(string s, ref int index)
    {
        var start = index;
        while (index < s.Length && char.IsDigit(s[index]))
            index++;

        return long.Parse(s.AsSpan(start, index - start), CultureInfo.InvariantCulture);
    }
}
