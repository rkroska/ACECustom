using System;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Zone Control crit compression (owner 2026-10-21, plan C+D of Plan_CritCompression_2026-09-02): a player's crit on a
    /// Zone Control monster lands at raw^p times a normal hit, never below a floor.
    ///
    /// Before this, the monster's Crit Damage Resist Rating (CDR) divided every crit: crit = normal x raw x k, with
    /// k = (100 + DRR) / (100 + DRR + CDR). The CDR rows are solved per tier so a BiS character's crit lands at exactly 3x -
    /// so anyone below BiS crit for less, and under a third of the BiS stack a crit landed BELOW a normal hit (live,
    /// 2026-10-21: a 147,106 crit between 140,016 and 151,026 normal hits).
    ///
    /// Now the same rows set a curve under the old division: the BiS stack they were solved for is target / k, the exponent
    /// p = ln(target) / ln(BiS stack) keeps that stack at exactly target x, and every stack below crits for more than a
    /// normal hit. A crit is the bigger of the curve and the old division, so above BiS nothing changes (owner 2026-10-21:
    /// nobody loses damage). No new data: the tier's existing CDR row is the input. Players' crits only (owner 2026-10-21).
    /// </summary>
    public static class CritCompression
    {
        /// <summary>
        /// The damage resist modifier a crit uses instead of <paramref name="critResistMod"/>.
        /// </summary>
        /// <param name="raw">the crit's multiple of a max normal hit before any rating on the defender (crit multiplier x Crit
        /// Damage Rating)</param>
        /// <param name="normalResistMod">the defender's damage resist modifier for a normal hit</param>
        /// <param name="critResistMod">the same with Crit Damage Resist added (what a crit used before this)</param>
        /// <param name="target">the crit ratio the CDR rows were solved for (3)</param>
        /// <param name="floor">the smallest crit ratio allowed (1.5), capped at the target and at the un-resisted crit (raw);
        /// 0 = none</param>
        /// <returns><paramref name="critResistMod"/> unchanged when there is nothing to compress (no Crit Damage Resist) or a
        /// value is unusable</returns>
        public static float ResistMod(double raw, double normalResistMod, double critResistMod, double target, double floor)
        {
            var ratio = Ratio(raw, normalResistMod, critResistMod, target, floor);
            return ratio.HasValue ? (float)(normalResistMod * ratio.Value / raw) : (float)critResistMod;
        }

        /// <summary>The crit's final multiple of a normal hit, or null when <see cref="ResistMod"/> leaves the crit as it was.</summary>
        public static double? Ratio(double raw, double normalResistMod, double critResistMod, double target, double floor)
        {
            if (!double.IsFinite(raw) || raw <= 0 || !double.IsFinite(normalResistMod) || normalResistMod <= 0
                || !double.IsFinite(critResistMod) || critResistMod <= 0 || !double.IsFinite(target) || target <= 1)
                return null;

            // k < 1 only when the defender has Crit Damage Resist; without it there is no row to read the curve from
            var k = critResistMod / normalResistMod;
            if (k >= 1)
                return null;

            var bisStack = target / k;
            var p = Math.Log(target) / Math.Log(bisStack);   // bisStack > target > 1, so 0 < p < 1
            // never below today's crit (owner 2026-10-21, "Never lower today's crit"): above the BiS stack the old division
            // (raw x k) is the bigger one and stays, so nobody loses damage
            var ratio = Math.Max(Math.Pow(raw, p), raw * k);

            // the floor: never above the target (crit gear would stop mattering) and never above the un-resisted crit itself
            // (a retail-path crit of ~1.01x must not become 1.5x)
            if (!double.IsNaN(floor) && floor > 0)
                ratio = Math.Max(ratio, Math.Min(Math.Min(floor, target), raw));

            return double.IsFinite(ratio) ? ratio : null;
        }
    }
}
