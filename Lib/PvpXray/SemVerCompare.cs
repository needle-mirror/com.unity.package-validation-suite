using System.Collections.Generic;

namespace PvpXray
{
    class SemVerCompare : IComparer<string[]>
    {
        public static readonly SemVerCompare Comparer = new SemVerCompare();

        public static int Compare(string x, string y)
        {
            return ((IComparer<string[]>)Comparer).Compare(Key(x), Key(y));
        }

        public static string[] Key(string arg)
        {
            var i = arg.IndexOf('+');
            if (i != -1) arg = arg.Substring(0, i);
            return arg.Replace('-', '.').Split('.');
        }

        public int Compare(string[] x, string[] y)
        {
            var i = -1;
            while (true)
            {
                ++i;
                if (i == x.Length && i == y.Length) return 0;
                if (i == x.Length && i == 3) return 1;
                if (i == y.Length && i == 3) return -1;
                if (i == y.Length) return 1;
                if (i == x.Length) return -1;

                var xStr = x[i];
                var yStr = y[i];
                if (xStr == yStr) continue;

                var isXInt = int.TryParse(xStr, out var xInt);
                var isYInt = int.TryParse(yStr, out var yInt);
                if (isXInt && isYInt) return xInt - yInt;
                if (isXInt) return -1;
                if (isYInt) return 1;
                return string.CompareOrdinal(xStr, yStr);
            }
        }
    }
}
