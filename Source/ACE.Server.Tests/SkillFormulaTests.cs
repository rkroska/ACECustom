using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Server.WorldObjects;

namespace ACE.Server.Tests
{
    [TestClass]
    public class SkillFormulaTests
    {
        [TestMethod]
        public void FiftyFiftyIsAccurate()
        {
            var result = SkillCheck.GetSkillChance(100, 100);
            Assert.AreEqual(0.5d, result);
        }

        /// <summary>
        /// Portal combat calculator uses GetCombatSkillChancePreview — same sigmoid path as live combat helpers.
        /// Values below match Source/ACE.WebPortal/ClientApp/src/utils/combatCalc.ts computeChanceHit().
        /// </summary>
        [TestMethod]
        public void CombatPreview_MatchesPortalFormula_Unscaled()
        {
            var server = SkillCheck.GetCombatSkillChancePreview(100, 100, 0.03f, scalingEnabled: false, aggression: 1.0);
            var portal = PortalComputeChanceHit(100, 100, 0.03, scalingEnabled: false, aggression: 1.0);
            Assert.AreEqual(portal, server, 1e-10);
        }

        [TestMethod]
        public void CombatPreview_MatchesPortalFormula_WithScaling()
        {
            const int attack = 36525;
            const int defense = 35000;
            const double aggression = 0.75;

            var server = SkillCheck.GetCombatSkillChancePreview(attack, defense, 0.03f, scalingEnabled: true, aggression);
            var portal = PortalComputeChanceHit(attack, defense, 0.03, scalingEnabled: true, aggression);
            Assert.AreEqual(portal, server, 1e-6);
        }

        [TestMethod]
        public void CombatPreview_MagicFactor_MatchesPortalConfig()
        {
            Assert.AreEqual(0.07f, SkillCheck.GetScaledCombatBaseFactor("magic"));
            Assert.AreEqual(0.03f, SkillCheck.GetScaledCombatBaseFactor("melee"));
            Assert.AreEqual(0.07f, SkillCheck.GetUnscaledCombatFactor("magic", playerAttacksMonster: true));
            Assert.AreEqual(0.03f, SkillCheck.GetUnscaledCombatFactor("magic", playerAttacksMonster: false));
        }

        /// <summary>Duplicate of combatCalc.ts computeChanceHit for cross-layer parity checks.</summary>
        private static double PortalComputeChanceHit(int attackSkill, int defenseSkill, double baseFactor, bool scalingEnabled, double aggression)
        {
            var f = baseFactor;
            if (scalingEnabled)
            {
                var avg = (attackSkill + defenseSkill) / 2.0;
                f *= Math.Pow(500.0 / Math.Max(500.0, avg), aggression);
            }

            var chance = 1.0 - 1.0 / (1.0 + Math.Exp(f * (attackSkill - defenseSkill)));
            return Math.Min(1.0, Math.Max(0.0, chance));
        }
    }
}
