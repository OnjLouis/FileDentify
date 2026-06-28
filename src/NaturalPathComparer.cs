using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FileDentify
{
    internal sealed class NaturalPathComparer : IComparer<string>
    {
        public static readonly NaturalPathComparer Instance = new NaturalPathComparer();

        private NaturalPathComparer()
        {
        }

        public int Compare(string x, string y)
        {
            if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase))
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;

            var dirCompare = string.Compare(Path.GetDirectoryName(x), Path.GetDirectoryName(y), StringComparison.OrdinalIgnoreCase);
            if (dirCompare != 0)
                return dirCompare;

            var nameCompare = CompareNatural(Path.GetFileName(x), Path.GetFileName(y));
            return nameCompare != 0 ? nameCompare : string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareNatural(string x, string y)
        {
            var ix = 0;
            var iy = 0;
            while (ix < x.Length && iy < y.Length)
            {
                if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
                {
                    var nx = ReadNumber(x, ref ix);
                    var ny = ReadNumber(y, ref iy);
                    var numberCompare = nx.CompareTo(ny);
                    if (numberCompare != 0)
                        return numberCompare;
                    continue;
                }

                var cx = char.ToUpperInvariant(x[ix]);
                var cy = char.ToUpperInvariant(y[iy]);
                if (cx != cy)
                    return cx.CompareTo(cy);
                ix++;
                iy++;
            }
            return x.Length.CompareTo(y.Length);
        }

        private static decimal ReadNumber(string value, ref int index)
        {
            var start = index;
            while (index < value.Length && char.IsDigit(value[index]))
                index++;

            decimal number;
            return decimal.TryParse(value.Substring(start, index - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : 0;
        }
    }
}
