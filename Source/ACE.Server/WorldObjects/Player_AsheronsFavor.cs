using System.Linq;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.Structure;

namespace ACE.Server.WorldObjects
{
    public partial class Player
    {
        // ── Spell IDs maintained by the Asheron's Favor charm ─────────────────
        private const uint AsheronsFavorSpell_Health = 4024; // Asheron's Lesser Benediction  (+Health%)
        private const uint AsheronsFavorSpell_Armor  = 3811; // Blackmoor's Favor             (+Natural Armor)

        // ── Tier scaling ───────────────────────────────────────────────────────
        // Tier 1: +10% Health, +50  Natural Armor
        // Tier 2: +15% Health, +100 Natural Armor
        // Tier 3: +20% Health, +250 Natural Armor

        private static float GetAsheronsFavorHealthMod(int level) => level switch
        {
            2 => 1.15f,
            3 => 1.20f,
            _ => 1.10f,  // Tier 1 default
        };

        private static float GetAsheronsFavorArmorMod(int level) => level switch
        {
            2 => 100f,
            3 => 250f,
            _ => 50f,    // Tier 1 default
        };

        /// <summary>
        /// Called when the Asheron's Favor charm is activated (or restored on login).
        /// Dispels any existing copies of both spells, then re-applies them as
        /// infinite-duration enchantments scaled to the active charm tier.
        /// </summary>
        public void ApplyAsheronsFavorEnchantments(WorldObject charm = null)
        {
            ActiveCharmLevels.TryGetValue(CharmAbilityRegistry.AsheronsFavorAbilityId, out var level);
            if (level < 1) level = 1;

            // Resolve the charm item — needed so EnchantmentManager records it as CasterObjectId,
            // which lets AuditItemSpells find it in allPossessions and keep the enchantments.
            charm ??= GetAllPossessions().FirstOrDefault(i =>
                (i.GetProperty(PropertyBool.IsAbilityCharm) ?? false) &&
                (i.GetProperty(PropertyInt.CharmGrantsAbility) ?? 0) == CharmAbilityRegistry.AsheronsFavorAbilityId);

            var spell4024 = new Spell(AsheronsFavorSpell_Health);
            var spell3811 = new Spell(AsheronsFavorSpell_Armor);

            if (spell4024.NotFound || spell3811.NotFound)
            {
                log.Warn("[AsheronsFavor] One or both charm spells not found in spell table — charm effects not applied.");
                return;
            }

            // Override mode: remove existing entries so we don't stack layers.
            DispelAsheronsFavorSpell(AsheronsFavorSpell_Health);
            DispelAsheronsFavorSpell(AsheronsFavorSpell_Armor);

            // Apply spell 4024 with infinite duration + tier-scaled StatModValue.
            // Pass charm as caster so CasterObjectId = charm GUID (survives AuditItemSpells).
            var result4024 = EnchantmentManager.Add(spell4024, charm, null);
            if (result4024?.Enchantment != null)
            {
                result4024.Enchantment.StatModValue = GetAsheronsFavorHealthMod(level);
                result4024.Enchantment.Duration     = -1.0;
                result4024.Enchantment.PowerLevel   = int.MaxValue; // Ensure manual gem use is always Surpassed
                Session?.Network.EnqueueSend(
                    new GameEventMagicUpdateEnchantment(Session, new Enchantment(this, result4024.Enchantment)));
            }

            // Apply spell 3811 with infinite duration + tier-scaled StatModValue.
            var result3811 = EnchantmentManager.Add(spell3811, charm, null);
            if (result3811?.Enchantment != null)
            {
                result3811.Enchantment.StatModValue = GetAsheronsFavorArmorMod(level);
                result3811.Enchantment.Duration     = -1.0;
                result3811.Enchantment.PowerLevel   = int.MaxValue; // Ensure manual gem use is always Surpassed
                Session?.Network.EnqueueSend(
                    new GameEventMagicUpdateEnchantment(Session, new Enchantment(this, result3811.Enchantment)));
            }

            ChangesDetected = true;
        }

        /// <summary>
        /// Called when the Asheron's Favor charm is deactivated or leaves inventory.
        /// Silently dispels both maintained enchantments.
        /// </summary>
        public void RemoveAsheronsFavorEnchantments()
        {
            DispelAsheronsFavorSpell(AsheronsFavorSpell_Health);
            DispelAsheronsFavorSpell(AsheronsFavorSpell_Armor);
        }

        /// <summary>
        /// Dispels ALL registry layers for a given spell ID on this player.
        /// Handles edge cases where multiple layers exist.
        /// </summary>
        private void DispelAsheronsFavorSpell(uint spellId)
        {
            const int maxAttempts = 10;
            var attempts = 0;
            var entry = EnchantmentManager.GetEnchantment(spellId);
            while (entry != null && attempts < maxAttempts)
            {
                EnchantmentManager.Dispel(entry);
                attempts++;
                entry = EnchantmentManager.GetEnchantment(spellId);
            }
            if (attempts >= maxAttempts)
                log.Warn($"[AsheronsFavor] DispelAsheronsFavorSpell: spell {spellId} still present after {maxAttempts} dispel attempts — possible EnchantmentManager issue.");
        }
    }
}
