using System;
using System.Linq;

using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// Zone Control's spawn-time stat application — standalone (consults only <see cref="ZoneControlManager"/>).
    /// Called wherever a creature enters the world, right after any other spawn scaling, so an authored zone
    /// stat is authoritative: attributes and Stamina/Mana/Health maxes are set ABSOLUTELY on the live spawn
    /// (per-instance only — weenies are never touched; despawn/respawn naturally reverts), then the profile's
    /// generic prop overrides are stamped. A creature with no governing zone is untouched.
    /// </summary>
    public static class ZoneSpawnScaler
    {
        private static readonly log4net.ILog log =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Applies the governing zone's spawn snapshot to a freshly-spawned creature: attributes first
        /// (Endurance/Self feed the vital formulas), then Stamina/Mana, then Health — each preserving the
        /// current % across the max change (no free heal/refill). No-op when no enabled zone governs the
        /// creature at its effective variation.
        /// </summary>
        public static void ApplyToSpawn(Creature creature)
        {
            if (creature == null)
                return;

            // ORDERING GUARD (2026-07-30): every resolve below keys on Location (landblock + variation).
            // A caller that applies the snapshot BEFORE assigning a Location silently gets nothing —
            // no attributes, no vitals, no prop stamps, no appearance — with no error anywhere. That is
            // exactly how GeneratorProfile.Spawn skipped the snapshot for every generator-spawned mob
            // until it was found by hand. Fail LOUD instead of silently, so the next such call site is
            // caught in the log rather than months later in an appraisal window.
            if (creature.Location == null)
            {
                log.Error($"ZoneSpawnScaler.ApplyToSpawn({creature.Name} 0x{creature.Guid}:{creature.WeenieClassId}) " +
                          "called with a NULL Location - zone resolution needs one, so NOTHING was applied " +
                          "(no attributes/vitals/props/appearance). Assign Location before calling this.");
                return;
            }

            // Cosmetic appearance is resolved and stamped SEPARATELY from stats (its own zone lookup), so a
            // per-WCID appearance override never creates a stat bucket and can't detach the mob from scaling.
            ApplyAppearance(creature);

            var profile = ZoneControlManager.ResolveForCreature(creature);
            if (profile == null)
                return;

            // VITAL FILL RATIOS ARE CAPTURED HERE, BEFORE ANY ATTRIBUTE MOVES (owner 2026-09-01).
            // Endurance feeds MaxHealth (and Self feeds Mana), so raising an attribute lifts MaxValue
            // instantly while Current stays where the weenie left it. Reading the ratio AFTER that -
            // which is what ApplyVital and the MaxHealth block below used to do - reads an already
            // collapsed number and then faithfully preserves the wrong thing.
            // Retail wcid 4124 in a T11 zone: Endurance 178 -> 1100 lifted MaxHealth ~89 -> ~550 while
            // Current stayed 89, so the "preserved" ratio was 16 pct and the monster spawned at
            // 161,819 / 1,000,000. Custom T11 monsters hid it because their attributes are already
            // near 1100, so scaling barely moves their max - it only bites the arbitrary retail mob,
            // i.e. exactly the drop-in case the whole Bestiary lane exists to serve.
            var fillHealth = FillRatio(creature.Health);
            var fillStamina = FillRatio(creature.Stamina);
            var fillMana = FillRatio(creature.Mana);

            ApplyAttribute(creature, profile, ZoneStat.Strength, PropertyAttribute.Strength);
            ApplyAttribute(creature, profile, ZoneStat.Endurance, PropertyAttribute.Endurance);
            ApplyAttribute(creature, profile, ZoneStat.Coordination, PropertyAttribute.Coordination);
            ApplyAttribute(creature, profile, ZoneStat.Quickness, PropertyAttribute.Quickness);
            ApplyAttribute(creature, profile, ZoneStat.Focus, PropertyAttribute.Focus);
            ApplyAttribute(creature, profile, ZoneStat.Self, PropertyAttribute.Self);

            // Level (2026-08-31): zone-wide, so an arbitrary retail weenie stops advertising its
            // shipped level in a T11 zone. Stamped, not read live - Level is plain item data that
            // Monster_Awareness target weighting and the appraisal panel read straight off the
            // creature. Floored at 1; level 0 reads as "unset" to several consumers.
            if (profile.Has(ZoneStat.MonsterLevel))
                creature.SetProperty(PropertyInt.Level,
                    (int)Math.Max(1.0, Math.Round(profile.Get(ZoneStat.MonsterLevel))));

            // Creature type (2026-08-31): FALLBACK ONLY. Applied when the weenie has no species, or
            // Invalid - never over a real one, so a Drudge stays a Drudge. Without a species the mob
            // silently suppresses every slayer roll in its own loot (ZoneLootMutator eligibility).
            if (profile.Has(ZoneStat.MonsterCreatureType)
                && (creature.CreatureType == null || creature.CreatureType == ACE.Entity.Enum.CreatureType.Invalid))
            {
                var ct = (int)Math.Round(profile.Get(ZoneStat.MonsterCreatureType));
                // CreatureType is `: uint` - IsDefined THROWS on a type mismatch rather than
                // returning false, so the boxed value must be uint. This is the SPAWN path: an int
                // here would have thrown on every governed spawn the moment the stat was authored.
                if (ct > 0 && System.Enum.IsDefined(typeof(ACE.Entity.Enum.CreatureType), (uint)ct))
                    creature.SetProperty(PropertyInt.CreatureType, ct);
            }

            ApplyVital(creature, profile, ZoneStat.MaxStamina, creature.Stamina);
            ApplyVital(creature, profile, ZoneStat.MaxMana, creature.Mana);

            if (profile.Has(ZoneStat.MaxHealth))
            {
                var maxBefore = creature.Health.MaxValue;
                var target = (long)Math.Round(profile.Get(ZoneStat.MaxHealth));
                // subtract the non-starting contributions (endurance formula, ranks) so MaxValue lands on target
                var nonStarting = (long)maxBefore - creature.Health.StartingValue;
                creature.Health.StartingValue = (uint)Math.Clamp(target - nonStarting, 1L, uint.MaxValue);
            }

            // Refill to the ratios captured above, AFTER every max has settled - attributes, the
            // authored vitals, and max_health all move MaxValue, so this has to be the last word.
            // A fresh spawn was at 100 pct, so it comes up full; a live re-apply onto a damaged
            // monster keeps it damaged, which is the behaviour the ratio existed for.
            RefillVital(creature.Health, fillHealth);
            RefillVital(creature.Stamina, fillStamina);
            RefillVital(creature.Mana, fillMana);

            // Incoming crit protection: REPLACE the base rating props on the live spawn (engine reads
            // them generically - Creature_Rating). The OUTGOING pair (crit_rating/crit_damage_rating)
            // is NOT a prop stamp: those are read live at the combat choke points as absolute
            // replacements (crit chance in percent / final crit multiplier) - WorldObject_Weapon.
            ApplyRatingProp(creature, profile, ZoneStat.CritResistRating, PropertyInt.CritResistRating);
            ApplyRatingProp(creature, profile, ZoneStat.CritDamageResistRating, PropertyInt.CritDamageResistRating);

            // Generic prop overrides applied to this instance at (re)spawn. Int/Float/Bool/Int64 biota
            // collections are per-instance clones (WeenieConverter), so this never touches the shared
            // weenie; despawn/respawn naturally reverts. Guarded by ZonePropGuard.
            ApplyProps(creature, profile);
        }

        /// <summary>Set a rating int prop absolutely on the live spawn when the zone authors its stat.</summary>
        private static void ApplyRatingProp(Creature creature, EvaluatedProfile profile, string statKey, PropertyInt prop)
        {
            if (!profile.Has(statKey))
                return;
            creature.SetProperty(prop, (int)Math.Round(profile.Get(statKey)));
        }

        /// <summary>Set an attribute's StartingValue absolutely (mobs carry 0 XP/ranks, so this IS the Base).</summary>
        private static void ApplyAttribute(Creature creature, EvaluatedProfile profile, string statKey, PropertyAttribute attr)
        {
            if (!profile.Has(statKey))
                return;

            var target = (long)Math.Round(profile.Get(statKey));
            creature.Attributes[attr].StartingValue = (uint)Math.Clamp(target, 1L, uint.MaxValue);
        }

        /// <summary>Set a vital's (Stamina/Mana) max absolutely, as a spawn snapshot preserving current %
        /// (same pattern as the MaxHealth block above; Health is handled separately in ApplyToSpawn).</summary>
        /// <summary>How full a vital is, sampled BEFORE anything moves its maximum. See the block comment
        /// at the top of ApplyToSpawn for why the timing is the whole point.</summary>
        private static float FillRatio(WorldObjects.Entity.CreatureVital vital)
            => vital != null && vital.MaxValue > 0 ? (float)vital.Current / vital.MaxValue : 1f;

        /// <summary>Restore a vital to a ratio captured earlier, clamped to its CURRENT maximum.</summary>
        private static void RefillVital(WorldObjects.Entity.CreatureVital vital, float fill)
        {
            if (vital == null)
                return;
            var max = vital.MaxValue;
            vital.Current = (uint)Math.Clamp((uint)Math.Round(Math.Clamp(fill, 0f, 1f) * max), 0u, max);
        }

        /// <summary>Set a vital's authored maximum. The fill ratio is NOT applied here - RefillVital runs
        /// once at the end of ApplyToSpawn, after every contributor to MaxValue has settled.</summary>
        private static void ApplyVital(Creature creature, EvaluatedProfile profile, string statKey, WorldObjects.Entity.CreatureVital vital)
        {
            if (profile == null || !profile.Has(statKey))
                return;

            var maxBefore = vital.MaxValue;
            var target = (long)Math.Round(profile.Get(statKey));
            var nonStarting = (long)maxBefore - vital.StartingValue;
            vital.StartingValue = (uint)Math.Clamp(target - nonStarting, 1L, uint.MaxValue);
        }

        /// <summary>Stamp a zone profile's generic prop overrides onto a freshly-spawned governed monster.</summary>
        private static void ApplyProps(Creature creature, EvaluatedProfile profile)
        {
            if (profile.PropInts != null)
                foreach (var kv in profile.PropInts)
                    if (!ZonePropGuard.IsBlockedInt(kv.Key))
                        creature.SetProperty((PropertyInt)kv.Key, (int)kv.Value);

            if (profile.PropInt64s != null)
                foreach (var kv in profile.PropInt64s)
                    if (!ZonePropGuard.IsBlockedInt64(kv.Key))
                        creature.SetProperty((PropertyInt64)kv.Key, kv.Value);

            if (profile.PropFloats != null)
                foreach (var kv in profile.PropFloats)
                    if (!ZonePropGuard.IsBlockedFloat(kv.Key))
                        creature.SetProperty((PropertyFloat)kv.Key, kv.Value);

            if (profile.PropBools != null)
                foreach (var kv in profile.PropBools)
                    if (!ZonePropGuard.IsBlockedBool(kv.Key))
                        creature.SetProperty((PropertyBool)kv.Key, kv.Value);
        }

        /// <summary>Stamp the governing zone's COSMETIC appearance onto a freshly-spawned monster, resolved from
        /// the dedicated appearance layer (zone default overlaid by per-WCID). Per-instance property writes at
        /// (re)spawn (revert on respawn), independent of the stat profile. Phase 1 levers: PaletteTemplate/Shade
        /// (recolor), DefaultScale (size), Translucency (ghost), CreatureVariant (shiny texture swap).</summary>
        private static void ApplyAppearance(Creature creature)
        {
            var ap = ZoneControlManager.ResolveAppearanceForCreature(creature);
            if (ap == null)
                return;

            // ── Display name override (owner 2026-08-09; per-WCID only, enforced at the command) ──
            if (!string.IsNullOrEmpty(ap.Name))
            {
                creature.Name = ap.Name;
                creature.SetProperty(PropertyString.Name, ap.Name);
            }

            // ── Model / data swaps (DataId; guarded by class byte so a wrong id can't garble the client) ──
            // A Setup swap replaces the body: clear the base weenie's own overlay + saved ObjDesc FIRST so its
            // appearance doesn't bleed onto the new model (mirrors PetDevice.SummonCreature's capture apply),
            // then set the new setup plus its matching motion/sound tables.
            if (ap.SetupTableId.HasValue && IsDataId(ap.SetupTableId.Value, 0x02))
            {
                creature.RemoveProperty(PropertyDataId.ClothingBase);
                creature.RemoveProperty(PropertyDataId.PaletteBase);
                creature.RemoveProperty(PropertyInt.PaletteTemplate);
                creature.RemoveProperty(PropertyFloat.Shade);
                creature.Biota.PropertiesAnimPart?.Clear();
                creature.Biota.PropertiesPalette?.Clear();
                creature.Biota.PropertiesTextureMap?.Clear();

                creature.SetupTableId = ap.SetupTableId.Value;
            }

            if (ap.MotionTable.HasValue && IsDataId(ap.MotionTable.Value, 0x09))
                creature.MotionTableId = ap.MotionTable.Value;
            if (ap.SoundTable.HasValue && IsDataId(ap.SoundTable.Value, 0x20))
                creature.SoundTableId = ap.SoundTable.Value;

            // ── Overlay / recolor (applied AFTER any model-swap clear so the intended overrides win) ──
            if (ap.PaletteBase.HasValue && IsDataId(ap.PaletteBase.Value, 0x04))
                creature.PaletteBaseId = ap.PaletteBase.Value;
            if (ap.ClothingBase.HasValue && IsDataId(ap.ClothingBase.Value, 0x10))
                creature.ClothingBase = ap.ClothingBase.Value;
            if (ap.Icon.HasValue && IsDataId(ap.Icon.Value, 0x06))
                creature.IconId = ap.Icon.Value;

            if (ap.PaletteTemplate.HasValue)
                creature.SetProperty(PropertyInt.PaletteTemplate, ap.PaletteTemplate.Value);
            if (ap.Shade.HasValue)
                creature.SetProperty(PropertyFloat.Shade, ap.Shade.Value);
            if (ap.Scale.HasValue)
                creature.SetProperty(PropertyFloat.DefaultScale, ap.Scale.Value);
            if (ap.Translucency.HasValue)
                creature.SetProperty(PropertyFloat.Translucency, ap.Translucency.Value);
            if (ap.Shiny.HasValue)
            {
                if (ap.Shiny.Value)
                    creature.SetProperty(PropertyInt.CreatureVariant, 1);
                else
                    creature.RemoveProperty(PropertyInt.CreatureVariant);
            }

            // ── Per-part body swaps (copied from a donor via copylook). Applied LAST so they survive the
            // Setup-swap clear above; a non-null override REPLACES the creature's parts wholesale. Creatures
            // with no equipped items render these biota rows directly (Creature.CalculateObjDesc). Spawn-time,
            // pre-EnterWorld, single-threaded for this creature -> wholesale list assignment is safe (mirrors
            // the unlocked biota Clear above). Reverts on respawn since the weenie is never touched.
            if (ap.AnimParts != null)
                creature.Biota.PropertiesAnimPart = ap.AnimParts
                    .Select(p => new PropertiesAnimPart { Index = p.Index, AnimationId = p.GfxObj }).ToList();
            if (ap.TextureMaps != null)
                creature.Biota.PropertiesTextureMap = ap.TextureMaps
                    .Select(t => new PropertiesTextureMap { PartIndex = t.Index, OldTexture = t.OldTex, NewTexture = t.NewTex }).ToList();
        }

        /// <summary>True if the DataId's class byte (top 8 bits) matches — a cheap guard against a wildly wrong id
        /// (e.g. a "Setup" that isn't 0x02) that could break the client render. 0 is never valid.</summary>
        private static bool IsDataId(uint id, byte classByte) => id != 0 && (id >> 24) == classByte;
    }
}
