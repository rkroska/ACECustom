using System;
using System.Collections.Generic;

using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// Decides whether a crafting recipe may be applied to a Tier 11+ item.
    ///
    /// THE RULE (owner 2026-08-24): *"a weaker imbue cant go on, but not imbued could"*. So this is
    /// NOT a blanket refusal. A recipe is refused only when it would REPLACE a value the item already
    /// carries with something equal or worse. An item that rolled nothing for that property can still
    /// receive the imbue - that is a real upgrade the player earned, and blocking it would be a nerf
    /// with nothing behind it.
    ///
    /// WHY IT IS NEEDED. Player crafting fights the T11+ ladder three ways:
    ///  1. COMPETITION. Vanilla imbues do not stack with our cards, they compete via Math.Max
    ///     (WorldObject_Weapon.cs:362 / :470). skill.Base is clamped to 400 melee / 360 missile+magic
    ///     (GetBaseSkillImbued), so at cap Critical Strike is a FLAT 0.50 crit chance and Crippling
    ///     Blow a FLAT 6.0 crit mod for every player - constants, not a curve.
    ///  2. OVERWRITE. Custom 527870xxx recipes SetValue rather than add, so they replace a rolled
    ///     value in BOTH directions - a jackpot drop can be tinkered DOWN to a constant.
    ///  3. REPEATS. Several custom recipes have no target guard and can be applied repeatedly.
    ///
    /// This is LAYER 2 of the design in Craft_Gate_Plan_2026-08-24.md. LAYER 1 - the authorable
    /// (item type x salvage material) matrix in <see cref="ZoneCraftGateStore"/> - now sits ABOVE it:
    ///
    ///   0. COMPONENTS a blocked SOURCE WCID                             -> refuse, stop
    ///   1. MATRIX     explicit Allow / Deny for (item type x material)  -> obey it, stop
    ///   2. DOWNGRADE  the rule below                                    -> unchanged
    ///   3. DEFAULT    allow
    ///
    /// The matrix is SPARSE and every cell starts Auto, so an install that has authored nothing behaves
    /// EXACTLY as the layer-2-only gate did: no rule can match, and every decision falls straight
    /// through to the downgrade rule.
    ///
    /// LAYER 0 was added 2026-08-25 for a fourth failure mode the other two cannot see: a component whose
    /// recipes ADD to a property instead of setting it. Layer 2 compares incoming against current and can
    /// only ever refuse a value that is no better, so it is structurally incapable of refusing an Add;
    /// layer 1 is indexed by the salvage's MaterialType, which these components do not declare. Layer 0
    /// is therefore a flat list of source WCIDs plus one toggle - see
    /// <see cref="ZoneCraftGateStore.DefaultBlockedComponents"/>.
    /// </summary>
    public static class ZoneCraftGate
    {
        /// <summary>What a competing imbue is worth, and which property it fights over.
        ///
        /// CAREFUL - the two comparisons do NOT share semantics:
        ///  * CRIT CHANCE is compared raw. CriticalFrequency 0.70 vs Critical Strike 0.50.
        ///  * CRIT DAMAGE is compared as a MOD, and the engine then adds one:
        ///    CriticalDamageMod = 1.0f + GetWeaponCritDamageMod(...)  (DamageEvent.cs:418).
        ///    Our Crushing Blow card stores (N - 1) for an N-times multiplier, so an 8x card stores
        ///    7.0 and Crippling Blow's 6.0 is really 7.0x. Comparing a DISPLAYED multiplier against
        ///    the raw imbue mod would be wrong by exactly one, and would wave through a 6x card being
        ///    "upgraded" to a 7x imbue while reporting the card as stronger. Compare stored-to-stored.
        ///
        /// The SAME trap caught Rend Power (fixed 2026-08-24): property 9056 stores a vuln FRACTION
        /// and the engine computes rendingMod = 1 + fraction (WorldObject_Weapon.cs:657), while the
        /// imbue's own ceiling MaxRendingMod is 2.5 - a rendingMod, not a fraction. Comparing a stored
        /// fraction against 2.50 was wrong by exactly one: a weapon storing 2.0 (rendingMod 3.0, far
        /// stronger than the imbue) read as 2.0 >= 2.50 = false and was ALLOWED to take a weaker
        /// imbue. The stored-value equivalent of the imbue's ceiling is 1.50.
        ///
        /// Armor Rending is NOT affected: both sides are a fraction of armour ignored, and the skill
        /// formula bottoms out at armorRendingMod 0.4 = fraction 0.6 (DamageEvent.cs:468), so 0.60 is
        /// already in stored units.
        ///
        /// Pairing verified 2026-08-24 - the names are confusable and they are easy to swap:
        ///   Critical Strike (imbue)  &lt;-&gt; Biting Strike (card)   both CRIT CHANCE
        ///   Crippling Blow  (imbue)  &lt;-&gt; Crushing Blow (card)   both CRIT DAMAGE</summary>
        private readonly struct Competing
        {
            public readonly int PropertyId;
            public readonly double CappedValue;
            public readonly string Label;

            public Competing(int propertyId, double cappedValue, string label)
            {
                PropertyId = propertyId;
                CappedValue = cappedValue;
                Label = label;
            }
        }

        /// <summary>Imbue effect -&gt; what it competes with. Effects absent here fight over nothing this
        /// system authors (the three defense imbues, Spellbook), so they are always allowed.</summary>
        private static readonly Dictionary<ImbuedEffectType, Competing> ImbueCompetes = new()
        {
            [ImbuedEffectType.CriticalStrike] = new((int)PropertyFloat.CriticalFrequency, 0.50, "Critical Frequency"),
            [ImbuedEffectType.CripplingBlow] = new((int)PropertyFloat.CriticalMultiplier, 6.00, "Critical Multiplier"),
            [ImbuedEffectType.ArmorRending] = new(ZoneLootMutator.ArmorRendOverridePropId, 0.60, "Armor Rending"),
            [ImbuedEffectType.SlashRending] = new(ZoneLootMutator.RendingModOverridePropId, 1.50, "Rend Power"),
            [ImbuedEffectType.PierceRending] = new(ZoneLootMutator.RendingModOverridePropId, 1.50, "Rend Power"),
            [ImbuedEffectType.BludgeonRending] = new(ZoneLootMutator.RendingModOverridePropId, 1.50, "Rend Power"),
            [ImbuedEffectType.AcidRending] = new(ZoneLootMutator.RendingModOverridePropId, 1.50, "Rend Power"),
            [ImbuedEffectType.ColdRending] = new(ZoneLootMutator.RendingModOverridePropId, 1.50, "Rend Power"),
            [ImbuedEffectType.ElectricRending] = new(ZoneLootMutator.RendingModOverridePropId, 1.50, "Rend Power"),
            [ImbuedEffectType.FireRending] = new(ZoneLootMutator.RendingModOverridePropId, 1.50, "Rend Power"),
            [ImbuedEffectType.NetherRending] = new(ZoneLootMutator.RendingModOverridePropId, 1.50, "Rend Power"),
        };

        /// <summary>Which imbue a vanilla recipe applies. The recipe data does NOT say: recipe 3863
        /// has two mod rows and zero int/float/bool stat mods. The effect comes from the mod's DataId.
        ///
        /// AUTHORITATIVE SOURCE = the mutation scripts in Source/ACE.Server/Entity/Mutations/Recipes/,
        /// named by DataId ("3800002A - Black Heart.txt"). The C# switch in RecipeManager.TryMutateNative
        /// looks authoritative but is DEAD: useMutateNative is a const false (RecipeManager.cs:1562), so
        /// TryMutate always takes the script path. This table was first derived from that dead switch and
        /// consequently missed NetherRending, which has no case there but does have a script. If a DataId
        /// is ever in doubt, read the .txt - not the switch.</summary>
        private static readonly Dictionary<uint, ImbuedEffectType> DataIdToImbue = new()
        {
            [0x38000023] = ImbuedEffectType.CriticalStrike,     // Black Opal
            [0x38000024] = ImbuedEffectType.CripplingBlow,      // Fire Opal
            [0x38000025] = ImbuedEffectType.ArmorRending,       // Sunstone
            [0x3800003A] = ImbuedEffectType.AcidRending,        // Emerald
            [0x3800003B] = ImbuedEffectType.BludgeonRending,    // White Sapphire
            [0x3800003C] = ImbuedEffectType.ColdRending,        // Aquamarine
            [0x3800003D] = ImbuedEffectType.ElectricRending,    // Jet
            [0x3800003E] = ImbuedEffectType.FireRending,        // Red Garnet
            [0x3800003F] = ImbuedEffectType.PierceRending,      // Black Garnet
            [0x38000040] = ImbuedEffectType.SlashRending,       // Imperial Topaz
            [0x3800002A] = ImbuedEffectType.NetherRending,      // Black Heart (recipe 300001)
        };

        /// <summary>Properties the T11+ loot pipeline authors, for the custom-recipe path. A recipe
        /// writing one of these is compared value-for-value against what the item already carries.</summary>
        private static readonly HashSet<int> OwnedInts = new()
        {
            (int)PropertyInt.Cleaving,
            ZoneLootMutator.SplitArrowCountIntId,
        };

        /// <summary>Properties where ANY replacement is a loss, so there is no "is it bigger" question
        /// to ask. SlayerCreatureType is the case: a slayer stone at 1.75 passes a &gt;= test against a
        /// rolled 1.5x card and silently RETARGETS which creature type the weapon slays, which is not a
        /// magnitude at all. Present and being replaced = refuse.</summary>
        private static readonly HashSet<int> OwnedIdentity = new()
        {
            (int)PropertyInt.SlayerCreatureType,
        };

        private static readonly HashSet<int> OwnedFloats = new()
        {
            (int)PropertyFloat.CriticalFrequency,
            (int)PropertyFloat.CriticalMultiplier,
            (int)PropertyFloat.SlayerDamageBonus,
            (int)PropertyFloat.IgnoreShield,
            ZoneLootMutator.RendingModOverridePropId,
            ZoneLootMutator.ArmorRendOverridePropId,
            ZoneLootMutator.SplitArrowRangeFloatId,
            ZoneLootMutator.SplitArrowDmgFloatId,
        };

        /// <summary>The item's tier, as max(ZcTier, WeaponAugScaleTier).
        ///
        /// BOTH are checked because either one alone has been the whole gate's off switch. Weapons used to
        /// carry ONLY WeaponAugScaleTier - ApplyT11GearStats returned for them before any tier stamp ran -
        /// and on 2026-08-25 exactly two items on the shard carried that property, so this read 0 and the
        /// gate was silently off for every weapon. ApplyT11GearStats now stamps ZcTier on weapons too
        /// (LootGenerationFactory_ZoneSet.cs, default case), which is what makes the tier test below
        /// actually fire on a weapon. Keep taking the max of both: they are two independent signals for
        /// one fact, and a weapon that misses one still gets a tier from the other.</summary>
        public static int TierOf(WorldObject wo)
        {
            if (wo == null)
                return 0;
            var zc = wo.GetProperty(PropertyInt.ZcTier) ?? 0;
            var wep = wo.GetProperty(PropertyInt.WeaponAugScaleTier) ?? 0;
            return zc > wep ? zc : wep;
        }

        /// <summary>Back-compat overload for any caller that has no salvage object to hand. With no
        /// source there is no material, so LAYER 1 cannot match and the decision is layer 2's alone.</summary>
        public static bool IsBlocked(Recipe recipe, WorldObject target, out string reason)
            => IsBlocked(recipe, null, target, out reason);

        /// <summary>True when this recipe must not be applied to this target. <paramref name="reason"/>
        /// is player-facing and always states WHAT it would have overwritten - an unexplained refusal
        /// is indistinguishable from a bug.
        ///
        /// <paramref name="source"/> is the SALVAGE being applied; its MaterialType is the matrix row.
        /// It is optional only so the old two-argument call site keeps compiling.</summary>
        public static bool IsBlocked(Recipe recipe, WorldObject source, WorldObject target, out string reason)
        {
            reason = null;
            if (recipe == null || target == null)
                return false;

            // Master switch OFF bypasses the WHOLE gate - matrix and downgrade rule alike.
            if (!ZoneCraftGateStore.Enabled)
                return false;

            var tier = TierOf(target);
            if (tier < ZoneCraftGateStore.MinTier)
                return false;

            // ── LAYER 0: blocked crafting components, matched by SOURCE WCID (owner 2026-08-25) ──
            //
            // Sits above the matrix because it names ONE specific item, where a matrix cell names a whole
            // (material x class) square - the more specific statement wins, so an Allow cell cannot rescue
            // a blocked component. (Moot for the two default entries, which carry no MaterialType and so
            // can never match a cell at all, but the ordering has to mean something for anything added
            // later that DOES have one.)
            //
            // NOT restricted to CraftItemClass.Weapon even though the owner's ask said "T11+ weapon". The
            // blocked components' cook_book rows only ever target weapons, so narrowing it buys nothing,
            // and it would add a hole: any weapon Classify failed to bucket would sail through. Blocking
            // on tier alone has no such gap.
            if (ZoneCraftGateStore.BlockComponents && source != null
                && ZoneCraftGateStore.IsBlockedComponent(source.WeenieClassId))
            {
                reason = $"{ComponentLabel(source)} cannot be applied to a Tier {tier} item - its bonus "
                       + "adds on top of what the item already rolled. Ordinary tinkering still works.";
                return true;
            }

            // ── LAYER 1: the authored matrix. Sparse, so an un-authored install never enters here. ──
            var matrix = ResolveMatrix(source, target, out var cls, out var material);
            if (matrix == CraftRuleMode.Allow)
                return false;                       // explicit Allow skips the downgrade rule entirely
            if (matrix == CraftRuleMode.Deny)
            {
                reason = $"{ZoneCraftGateStore.MaterialName(material)} may not be applied to a Tier {tier} "
                       + $"{cls.ToString().ToLowerInvariant()}.";
                return true;
            }

            // ── the vanilla imbues, whose effect comes from the mod DataId ──
            if (recipe.IsImbuing() && TryGetImbue(recipe, out var effect)
                && ImbueCompetes.TryGetValue(effect, out var competing))
            {
                var current = target.GetProperty((PropertyFloat)competing.PropertyId);
                if (current.HasValue && current.Value >= competing.CappedValue)
                {
                    reason = $"This Tier {tier} item already has a stronger {competing.Label} "
                           + $"({Show(competing.PropertyId, current.Value)}) than this would give it "
                           + $"({Show(competing.PropertyId, competing.CappedValue)}).";
                    return true;
                }
                return false;   // nothing there, or ours is weaker - let the player have it
            }

            // ── custom recipes, which SetValue real properties ──
            if (WouldDowngrade(recipe, target, out var what, out var have, out var incoming, out var propId))
            {
                reason = OwnedIdentity.Contains(propId)
                    ? $"This Tier {tier} item already has a {what} set, and this would replace it."
                    : $"This Tier {tier} item already has a stronger {what} "
                      + $"({Show(propId, have)}) than this would give it ({Show(propId, incoming)}).";
                return true;
            }

            return false;
        }

        /// <summary>LAYER 1 lookup. Returns Auto - "the matrix has no opinion, ask layer 2" - whenever
        /// anything needed to index it is missing: no salvage object, salvage with no MaterialType, or a
        /// target the matrix has no column for. That is the whole reason an un-authored install is
        /// byte-for-byte the old behaviour.</summary>
        /// <summary>What to CALL a blocked component in the layer-0 refusal message.
        ///
        /// PREFERS MaterialType OVER Name, and the reason is a real bug the owner hit 2026-08-26: the
        /// dev command /cisalvage RENAMES the bag it creates to "Salvage (100)"
        /// (AdminCommands.HandleCISalvage), so the refusal read "The Salvage (100) cannot be applied"
        /// and did not say WHICH salvage was refused. A player-looted bag is named properly, but the
        /// material is the more reliable source either way - it is the thing the block is actually
        /// keyed against conceptually, and it cannot be renamed.
        ///
        /// Falls back to Name for components that declare no MaterialType at all. That is not a rare
        /// edge: it is exactly the two DEFAULT entries, the Fine Bandit Blade Hilt and the Finely
        /// Oiled Bowstring, whose only ints are ItemType and 25 (verified 2026-08-25) - which is also
        /// why layer 1 can never index them.</summary>
        private static string ComponentLabel(WorldObject source)
        {
            var material = source.GetProperty(PropertyInt.MaterialType) ?? 0;
            if (material > 0)
            {
                var name = Enum.GetName(typeof(MaterialType), (uint)material);
                if (!string.IsNullOrEmpty(name))
                    return Spaced(name);
            }
            return source.Name;
        }

        /// <summary>"WhiteQuartz" -> "White Quartz". The MaterialType enum names are CamelCase and go
        /// straight into player-facing chat, so they need splitting or they read as a code identifier.
        /// ASCII only - game text must stay ASCII.</summary>
        private static string Spaced(string camel)
        {
            var sb = new System.Text.StringBuilder(camel.Length + 4);
            for (var i = 0; i < camel.Length; i++)
            {
                if (i > 0 && char.IsUpper(camel[i]) && !char.IsUpper(camel[i - 1]))
                    sb.Append(' ');
                sb.Append(camel[i]);
            }
            return sb.ToString();
        }

        private static CraftRuleMode ResolveMatrix(WorldObject source, WorldObject target,
            out CraftItemClass cls, out int material)
        {
            cls = CraftItemClass.Weapon;
            material = 0;

            var mat = source?.MaterialType;
            if (!mat.HasValue || mat.Value == MaterialType.Unknown)
                return CraftRuleMode.Auto;

            var c = ZoneCraftGateStore.Classify(target);
            if (!c.HasValue)
                return CraftRuleMode.Auto;

            cls = c.Value;
            material = (int)mat.Value;
            return ZoneCraftGateStore.GetMode(material, cls);
        }

        /// <summary>Which imbue a stock mutation DataId applies, for callers outside the gate (the admin
        /// verbs and the wire payload name the imbue a material carries). Same map the decision uses -
        /// there is deliberately only one copy.</summary>
        public static bool TryGetImbueForDataId(uint dataId, out ImbuedEffectType effect)
            => DataIdToImbue.TryGetValue(dataId, out effect);

        /// <summary>What layer 2 would compare a given imbue against, for the `craft test` verb: the
        /// property it competes over and the capped value the imbue is worth. False when the effect
        /// fights over nothing this system authors (the three defense imbues, Spellbook).</summary>
        public static bool TryDescribeImbue(ImbuedEffectType effect, out int propertyId, out string label,
            out double cappedValue, out string shown)
        {
            propertyId = 0; label = null; cappedValue = 0; shown = null;
            if (!ImbueCompetes.TryGetValue(effect, out var c))
                return false;
            propertyId = c.PropertyId;
            label = c.Label;
            cappedValue = c.CappedValue;
            shown = Show(c.PropertyId, c.CappedValue);
            return true;
        }

        /// <summary>Render a STORED property value the way the player reads it on the item (crit damage
        /// gains its +1, crit chance becomes a percentage). Public so the admin `craft test` verb reports
        /// the same numbers the refusal message would.</summary>
        public static string ShowStored(int propertyId, double stored) => Show(propertyId, stored);

        /// <summary>Crit damage is stored as (multiplier - 1) because the engine computes 1 + mod, so
        /// show it the way the player reads it on the item.</summary>
        private static string Show(int propId, double stored)
        {
            if (propId == (int)PropertyFloat.CriticalMultiplier)
                return $"{stored + 1.0:0.##}x";
            if (propId == (int)PropertyFloat.CriticalFrequency)
                return $"{stored * 100.0:0.#} pct";
            return $"{stored:0.##}";
        }

        /// <summary>Which imbue this recipe applies, read from its mod DataIds.</summary>
        private static bool TryGetImbue(Recipe recipe, out ImbuedEffectType effect)
        {
            effect = ImbuedEffectType.Undef;
            if (recipe.RecipeMod == null)
                return false;

            foreach (var mod in recipe.RecipeMod)
            {
                if (mod == null || mod.DataId == 0)
                    continue;
                if (DataIdToImbue.TryGetValue((uint)mod.DataId, out effect))
                    return true;
            }
            return false;
        }

        /// <summary>Would this recipe write an owned property with a value no better than the item's?
        /// Only SetValue-style mods can downgrade; an additive mod cannot make an item worse.</summary>
        private static bool WouldDowngrade(Recipe recipe, WorldObject target,
            out string what, out double have, out double incoming, out int propId)
        {
            what = null; have = 0; incoming = 0; propId = 0;
            if (recipe.RecipeMod == null)
                return false;

            foreach (var mod in recipe.RecipeMod)
            {
                if (mod == null)
                    continue;

                if (mod.RecipeModsFloat != null)
                {
                    foreach (var m in mod.RecipeModsFloat)
                    {
                        if (m == null || !OwnedFloats.Contains(m.Stat) || !IsReplacing(m.Enum))
                            continue;
                        var cur = target.GetProperty((PropertyFloat)m.Stat);
                        if (cur.HasValue && cur.Value >= m.Value)
                        {
                            what = PropName(m.Stat); have = cur.Value; incoming = m.Value; propId = m.Stat;
                            return true;
                        }
                    }
                }

                if (mod.RecipeModsInt != null)
                {
                    foreach (var m in mod.RecipeModsInt)
                    {
                        if (m == null || !IsReplacing(m.Enum))
                            continue;

                        // identity props: replacing at all is a loss, magnitude is meaningless
                        if (OwnedIdentity.Contains(m.Stat) && target.GetProperty((PropertyInt)m.Stat).HasValue)
                        {
                            what = PropName(m.Stat); have = 0; incoming = 0; propId = m.Stat;
                            return true;
                        }

                        if (!OwnedInts.Contains(m.Stat))
                            continue;
                        var cur = target.GetProperty((PropertyInt)m.Stat);
                        if (cur.HasValue && cur.Value >= m.Value)
                        {
                            what = PropName(m.Stat); have = cur.Value; incoming = m.Value; propId = m.Stat;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>Operations that REPLACE a value, and can therefore downgrade.
        /// SetValue(1) is the obvious one. CopyFromSourceToTarget(3) also replaces - and writes the
        /// source's value or 0 when absent, so it can wipe a property outright. Add(2), AddSpell(7)
        /// and the bit ops cannot make an authored value worse, so they are left alone.
        /// (ModificationOperation, ACE.Entity/Enum/ModificationOperation.cs.)</summary>
        private static bool IsReplacing(int modEnum) =>
            modEnum == (int)ACE.Entity.Enum.ModificationOperation.SetValue
         || modEnum == (int)ACE.Entity.Enum.ModificationOperation.CopyFromSourceToTarget;

        private static string PropName(int propId)
        {
            if (propId == (int)PropertyFloat.CriticalFrequency) return "Critical Frequency";
            if (propId == (int)PropertyFloat.CriticalMultiplier) return "Critical Multiplier";
            if (propId == (int)PropertyFloat.SlayerDamageBonus) return "Slayer bonus";
            if (propId == (int)PropertyFloat.IgnoreShield) return "Shield Cleaving";
            if (propId == ZoneLootMutator.RendingModOverridePropId) return "Rend Power";
            if (propId == ZoneLootMutator.ArmorRendOverridePropId) return "Armor Rending";
            if (propId == (int)PropertyInt.Cleaving) return "Cleaving";
            if (propId == ZoneLootMutator.SplitArrowCountIntId) return "Split Arrow count";
            if (propId == ZoneLootMutator.SplitArrowRangeFloatId) return "Split Arrow range";
            if (propId == ZoneLootMutator.SplitArrowDmgFloatId) return "Split Arrow damage";
            if (propId == (int)PropertyInt.SlayerCreatureType) return "Slayer target";
            return "property " + propId;
        }
    }
}
