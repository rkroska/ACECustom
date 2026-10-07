using System;
using System.Globalization;
using System.Text.RegularExpressions;

using ACE.Server.Managers;

namespace ACE.Server.Command
{
    /// <summary>
    /// K / M / B / T / Q amounts (owner 2026-09-27: "Too hard to read 00000000000").
    /// Audit lines print amounts short while <c>audit_short_numbers</c> is on (the default), and the admin
    /// grant commands accept either form: "5B", "1.5m", "250K" or plain "5000000000" / "5,000,000,000".
    /// Amounts only - ids (wcids, guids) are never run through this.
    /// </summary>
    public static class ShortNumber
    {
        /// <summary>The grant commands' documented ceiling; a shaped amount above it is left for the usage message.</summary>
        public const long MaxGrantAmount = 999_999_999_999;

        // Commas only as thousands separators ("5,000,000,000", "1,500K"), never as a decimal comma ("1,5M").
        private static readonly Regex Shape = new Regex(@"^(\d{1,3}(,\d{3})+|\d*)(\.\d+)?[KMBTQ]?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>An audit-line amount: full with commas; while audit_short_numbers is on the short form leads and the exact
        /// amount follows ("5B (5,000,000,001)") - an auditor must be able to recover the real grant (CodeRabbit #539).</summary>
        public static string Audit(long value)
            => ServerConfig.audit_short_numbers.Value ? Both(value) : value.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>Short then exact (owner 2026-10-06): "5B (5,000,000,000)"; below 10,000 the short form is the exact one, so
        /// it shows once ("5,000"). Player-facing balances.</summary>
        public static string Both(long value)
        {
            var exact = value.ToString("N0", CultureInfo.InvariantCulture);
            var shortForm = Format(value);
            return shortForm == exact ? exact : $"{shortForm} ({exact})";
        }

        /// <summary>A player-facing amount with its label between the two forms (owner 2026-10-06): Amount(v, "Lum") =
        /// "5B Lum (5,000,000,000)"; below 10,000 it shows once ("5,000 Lum").</summary>
        public static string Amount(long value, string label)
        {
            var exact = value.ToString("N0", CultureInfo.InvariantCulture);
            var shortForm = Format(value);
            return shortForm == exact ? $"{exact} {label}" : $"{shortForm} {label} ({exact})";
        }

        /// <summary>5B / 1.2M / 250K; exact with commas below 10,000. Same thresholds and "0.#" rounding as
        /// Creature.FormatDamage mode 2 (and the web portal's shortNumber.ts), but culture-invariant.</summary>
        public static string Format(long value)
        {
            if (value == long.MinValue) value++;   // Math.Abs would overflow
            var abs = (ulong)Math.Abs(value);
            string s;
            if (abs < 10_000)                          s = abs.ToString("N0", CultureInfo.InvariantCulture);
            else if (abs >= 1_000_000_000_000_000UL)   s = Unit(abs / 1_000_000_000_000_000.0, "Q");
            else if (abs >= 1_000_000_000_000UL)       s = Unit(abs / 1_000_000_000_000.0, "T");
            else if (abs >= 1_000_000_000UL)           s = Unit(abs / 1_000_000_000.0, "B");
            else if (abs >= 1_000_000UL)               s = Unit(abs / 1_000_000.0, "M");
            else                                       s = Unit(abs / 1_000.0, "K");
            return value < 0 ? "-" + s : s;
        }

        private static string Unit(double n, string suffix) => n.ToString("0.#", CultureInfo.InvariantCulture) + suffix;

        /// <summary>Plain or suffixed amount -> long. False on anything else (a player name, a decimal comma,
        /// a fraction of a unit, an overflow), never an exception.</summary>
        public static bool TryParse(string text, out long value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var s = text.Trim();
            if (!Shape.IsMatch(s))
                return false;
            s = s.Replace(",", "");
            if (s.Length == 0)
                return false;

            decimal mult = 1;
            switch (char.ToUpperInvariant(s[^1]))
            {
                case 'K': mult = 1_000m; break;
                case 'M': mult = 1_000_000m; break;
                case 'B': mult = 1_000_000_000m; break;
                case 'T': mult = 1_000_000_000_000m; break;
                case 'Q': mult = 1_000_000_000_000_000m; break;
            }
            if (mult != 1)
                s = s[..^1];

            if (s.Length == 0 || !decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                return false;

            if (number > long.MaxValue / mult)
                return false;   // checked before the multiply - decimal throws past ~7.9e28
            var result = number * mult;
            if (result != Math.Floor(result))
                return false;   // 1.2345K is not a whole number of anything

            value = (long)result;
            return true;
        }

        /// <summary>Rewrites a trailing suffixed or comma'd amount ("5B", "5,000,000,000") to plain digits so the stock
        /// PositiveLong parameter parser takes it. Leaves the array alone for anything else (plain digits already parse),
        /// and for an amount above <see cref="MaxGrantAmount"/>, so a slip like "5T" for "5B" gets the usage message.</summary>
        public static string[] ExpandLastAmount(string[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return parameters;
            var last = parameters[^1];
            var shaped = last.Length >= 2 && (char.IsLetter(last[^1]) || last.Contains(','));
            if (!shaped || !TryParse(last, out var v) || v > MaxGrantAmount)
                return parameters;
            var copy = (string[])parameters.Clone();
            copy[^1] = v.ToString(CultureInfo.InvariantCulture);
            return copy;
        }
    }
}
