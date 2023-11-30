using System;

namespace ACE.Common.Extensions
{
    public static class DoubleExtensions
    {
        private static readonly System.Buffers.SearchValues<char> digits = System.Buffers.SearchValues.Create("123456789");

        public static string FormatChance(this double chance)
        {
            if (chance == 1)
            {
                return "100%";
            }
            if (chance == 0)
            {
                return "0%";
            }
            double r = (chance * 100);
            string p = r.ToString("F99").TrimEnd('0');
            if (!p.StartsWith("0."))
            {
                int extra = 2;
                if (p.IndexOf(".0") > -1 || p.EndsWith("."))
                {
                    extra = 0;
                }
                return string.Concat(p.AsSpan(0, p.IndexOf('.') + extra), "%");
            }
            int i = p.AsSpan().IndexOfAny(digits);
            if (i < 0)
            {
                return "0%";
            }
            return string.Concat(p.AsSpan(0, i + 1), "%");
        }
    }
}
