using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Factories;
using ACE.Server.Managers.WeaponScaling;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.Network;
using ACE.Server.WorldObjects;

namespace ACE.Server.Command.Handlers
{
    public static partial class TestCharacterCommands
    {
        // -- /testchar preset (owner 2026-10-21, Schneebly) -----------------------------------------------------------
        // One command that builds a tester at a tier and play style - stats, augs, skills, gear, weapons and the style's
        // levelled charm - so testers do not need the plugin's Character tab or a page of numbers. It runs the SAME
        // setters, forges and charm toggles the plugin and the other test commands use, in the plugin's order.
        //
        // Where every default comes from (nothing is guessed per tier):
        //   - augs, entry: the tier's WIELD requirements in the live weapon scaling config (WeaponScalingManager tier
        //     row: item augs, creature augs, life augs, Triune and skill-charm counters) - the character that can just
        //     equip this tier's gear. School augs (war / void / melee / missile / duration) match the item augs.
        //   - augs, max: the top of the tier's aug curves (zone Default aug_armor_max / aug_prot_max / relief_aug_max),
        //     raw augs at their buy caps (4000, creature 6000) and Triune for the rest - the character that has outgrown
        //     the tier (every aug cut at its cap).
        //   - attributes, vitals, level, enlightenment: rounded averages of four real geared T12-T25 players in the
        //     2026-10-14 prod copy (Nerd Parade, Schneebly, Meribia, Reawakener). The same at every tier: what changes
        //     with the tier is augs and gear.
        //   - gear: /asforge premade (avg at entry, bis at max); weapons: /wsforge at quality 500 (entry) / 1000 (max).

        private const string PresetAttrs = "465,500,580,550,560,500";   // str,end,coord,quick,focus,self (base, unbuffed)
        private const string PresetVitals = "750,900,850";               // health,stamina,mana (unbuffed max)
        private const int PresetLevel = 1400;
        private const int PresetEnlightenment = 325;

        /// <summary>The six levelled charm items (Split Arrow, Chain, Pandemic, Improved Cleave, Armor Breaker, Shield Arc).</summary>
        private static readonly uint[] LeveledCharmWcids = { 777700035, 777700036, 777700037, 777700038, 777700039, 777700040 };

        private sealed class PresetStyle
        {
            public string Key, Label;
            public int CharmAbilityId;
            public (string Class, string Element)[] Weapons;
            public bool Shield, Arrows;
        }

        private static readonly PresetStyle[] PresetStyles =
        {
            new PresetStyle { Key = "2h", Label = "Two-Hander", CharmAbilityId = CharmAbilityRegistry.ImprovedCleaveAbilityId,
                Weapons = new[] { ("cleaver", "base") } },
            // two different single-strike weapons: /wsforge skips a weapon already held, and dual wield wants single strikes
            new PresetStyle { Key = "dw", Label = "Dual Wield", CharmAbilityId = CharmAbilityRegistry.ArmorBreakerAbilityId,
                Weapons = new[] { ("sword", "base"), ("axe", "base") } },
            new PresetStyle { Key = "shield", Label = "Sword and Shield", CharmAbilityId = CharmAbilityRegistry.ShieldArcAbilityId,
                Weapons = new[] { ("sword", "base") }, Shield = true },
            new PresetStyle { Key = "bow", Label = "Archer", CharmAbilityId = CharmAbilityRegistry.SplitArrowAbilityId,
                Weapons = new[] { ("bow", "base") }, Arrows = true },
            new PresetStyle { Key = "war", Label = "War Mage", CharmAbilityId = CharmAbilityRegistry.ChainAbilityId,
                Weapons = new[] { ("wand", "fire") } },
            new PresetStyle { Key = "void", Label = "Void Mage", CharmAbilityId = CharmAbilityRegistry.PandemicAbilityId,
                Weapons = new[] { ("wand", "nether") } },
        };

        private const string PresetUsage =
            "Usage: /testchar preset <tier 11-25> <2h|dw|shield|bow|war|void> [entry|max] [charm level 1-100]\n" +
            "  entry (default) = just meets the tier's wield requirements; max = has outgrown the tier (aug curves capped).\n" +
            "  Resets the character to T0 first (admin + characters only), then sets stats, augs, skills and spells, forges the\n" +
            "  tier's suit and the style's weapons, adds all six levelled charms and switches on the style's charm.\n" +
            "  Example: /testchar preset 13 dw entry 50";

        private static void HandlePreset(Session session, Player player, string[] parameters)
        {
            void Msg(string s) => ChatPacket.SendServerMessage(session, s, ChatMessageType.Broadcast);

            if (parameters.Length < 3 || !int.TryParse(parameters[1], out var tier) || tier < 11 || tier > 25)
            {
                Msg(PresetUsage);
                return;
            }
            var style = PresetStyles.FirstOrDefault(s => s.Key.Equals(parameters[2], StringComparison.OrdinalIgnoreCase));
            if (style == null)
            {
                Msg("testchar preset: unknown style '" + parameters[2] + "'. Styles: " + string.Join(" ", PresetStyles.Select(s => s.Key)));
                return;
            }

            var max = false;
            var charmLevel = 1;
            foreach (var p in parameters.Skip(3))
            {
                if (p.Equals("max", StringComparison.OrdinalIgnoreCase)) max = true;
                else if (p.Equals("entry", StringComparison.OrdinalIgnoreCase)) max = false;
                else if (int.TryParse(p, out var lv)) charmLevel = Math.Clamp(lv, 1, 100);
                else { Msg("testchar preset: unknown option '" + p + "'.\n" + PresetUsage); return; }
            }

            var row = WeaponScalingManager.GetTier(tier);
            if (row == null)
            {
                Msg($"testchar preset: the weapon scaling config has no tier {tier} row - nothing changed.");
                return;
            }

            // the wipe guard: admin (+) characters only, exactly like /testchar T0
            if (!GuardWipe(session, player)) return;

            var augs = PresetAugs(tier, row, max);
            Msg($"testchar preset: T{tier} {style.Label}, {(max ? "max" : "entry")}, charm level {charmLevel}. Augs (creature,item,life,war,void,duration,specialize,summon,melee,missile) = {augs.Augs}; charms (weave,wrath,nether,steel,trueshot) = {augs.Charms}.");

            // 1. the character - the plugin's preset batch, in its order (attributes and enlightenment before vitals).
            // The reset destroys the charm items but not the abilities they switched on, so turn those off first, or the
            // previous preset's charm stays live on top of this one.
            foreach (var abilityId in CharmAbilityRegistry.RegisteredIds.ToList())
                if (CharmAbilityRegistry.IsActive(player, abilityId))
                    CharmAbilityRegistry.Apply(player, abilityId, false);
            ResetToTier0(player);
            // the coins put into each levelled charm live on the player, so the wipe leaves them: clear all six, or the
            // previous preset's levels come back on the charms this one hands out
            foreach (var block in CharmSettingsManager.LeveledBlocks.Values)
                player.SetCharmCoins(CharmSettingsManager.LeveledAbilityId(block.Name), 0);
            HandleCharacterSetter(session, player, new[] { "set", "attrs", PresetAttrs });
            HandleCharacterSetter(session, player, new[] { "set", "level", PresetLevel.ToString() });
            HandleCharacterSetter(session, player, new[] { "set", "enl", PresetEnlightenment.ToString() });
            HandleCharacterSetter(session, player, new[] { "set", "vitals", PresetVitals });
            HandleCharacterSetter(session, player, new[] { "set", "augs", augs.Augs });
            HandleCharacterSetter(session, player, new[] { "set", "charms", augs.Charms });
            HandleCharacterSetter(session, player, new[] { "set", "raugs", "max" });
            HandleCharacterSetter(session, player, new[] { "apply", "skills,spells,aetheria,manastone" });

            // 2. gear: the tier's 18-piece suit, the style's weapons (and shield / arrows)
            HandleAsForgePremade(session, player, new[] { "premade", tier.ToString(), max ? "bis" : "avg" });
            var quality = max ? "1000" : "500";
            foreach (var (cls, element) in style.Weapons)
                WeaponScalingCommands.HandleWsForge(session, cls, quality, element, tier.ToString());
            if (style.Shield)
                Msg(MintZoneShield(player, tier));
            if (style.Arrows)
                HandleCharacterSetter(session, player, new[] { "extra", "arrow" });

            // 3. charms: all six levelled charms in the pack, the style's one on at the chosen level
            SpawnLeveledCharms(player);
            Msg(ActivateLeveledCharm(player, style.CharmAbilityId, charmLevel));

            player.SaveBiotaToDatabase();
            Msg($"testchar preset: done. LOG OUT AND BACK IN (the spellbook refreshes), buff, then stand in a T{tier} zone (Tou Tou: @televariant {tier}). Check hits with @debugdamage attack|defense and /nearbyhealth; change the charm with /charm level <name> <1-100>.");
        }

        /// <summary>The aug and counter CSVs for /testchar set augs / set charms (see the header for where they come from).</summary>
        internal static (string Augs, string Charms) PresetAugs(int tier, WeaponScalingTier row, bool max)
        {
            long creature, item, life, school, triune, skillCharm;
            if (!max)
            {
                item = row.MinWieldAugs;
                creature = row.MinWieldCreature;
                life = row.MinWieldLife;
                triune = row.MinWieldTriune;
                skillCharm = row.MinWieldSkillCharm;
            }
            else
            {
                // the top of the tier's aug curves; raw augs stop at the buy caps, Triune (counted as item AND life augs)
                // carries the rest
                var zp = ZoneControlManager.EvaluateTierDefault(tier);
                double CurveMax(string stat) => zp != null && zp.Has(stat) ? zp.Get(stat) : 0;
                var top = Math.Max(CurveMax(ZoneStat.AugArmorMax), Math.Max(CurveMax(ZoneStat.AugProtMax), CurveMax(ZoneStat.ReliefAugMax)));
                item = 4000;
                life = 4000;
                creature = 6000;
                triune = Math.Max(row.MinWieldTriune, (long)Math.Ceiling(Math.Max(0, top - 4000)));
                skillCharm = Math.Max(row.MinWieldSkillCharm, triune);
            }
            school = Math.Min(4000, item);
            var augsCsv = string.Join(",", creature, item, life, school, school, school, 0, 0, school, school);
            var charmsCsv = string.Join(",", triune, skillCharm, skillCharm, skillCharm, skillCharm);
            return (augsCsv, charmsCsv);
        }

        /// <summary>Puts the six levelled charms in the Ability Charms Pack, skipping any the character already holds
        /// (so /testchar charms tops up an older pack too).</summary>
        private static void SpawnLeveledCharms(Player player)
        {
            var held = new HashSet<uint>(player.GetAllPossessionsDeep().Select(i => i.WeenieClassId));
            var pack = GetOrCreatePack(player, "Ability Charms Pack");
            foreach (var wcid in LeveledCharmWcids)
            {
                if (held.Contains(wcid)) continue;
                var charm = WorldObjectFactory.CreateNewWorldObject(wcid);
                if (charm != null)
                    PlaceInPackOrLoose(player, charm, pack);
            }
        }

        /// <summary>Sets the player's level of a levelled charm and switches the carried charm on (the same three steps
        /// the charm's own use does). Any other active copy of the same charm is left as it is.</summary>
        private static string ActivateLeveledCharm(Player player, int abilityId, int level)
        {
            var name = CharmAbilityRegistry.GetDisplayName(abilityId) ?? $"ability {abilityId}";
            var charm = player.GetAllPossessionsDeep().FirstOrDefault(i => i.IsAbilityCharm && i.CharmGrantsAbility == abilityId);
            if (charm == null)
                return $"testchar preset: no {name} charm in the pack - run the charm SQL (777700035-777700040), then /testchar charms.";

            player.SetCharmLevel(abilityId, level);
            charm.IsCharmActivated = true;
            CharmAbilityRegistry.Apply(player, abilityId, true, level);
            player.SyncLevelledCharmItem(charm, abilityId);
            charm.SaveBiotaToDatabase();
            return $"testchar preset: {name} charm on at level {level}. " + (CharmSettingsManager.PlayerTextFor(abilityId, level) ?? "");
        }

        /// <summary>
        /// One T11+ shield through the real drop path (owner 2026-10-21: /asforge shield) - the one-item zone loot set and
        /// the per-item sweep a kill at this tier runs (gear budget, mutations, wield gate), from the tier Default profile
        /// (or the zone the caller stands in when it is this tier). An ordinary drop, not a rare. Attuned + Bonded.
        /// </summary>
        internal static string MintZoneShield(Player player, int tier)
        {
            tier = Math.Clamp(tier, 11, 25);
            var zoneProfile = ZoneControlManager.GetEffectiveVariation(player) == tier ? ZoneControlManager.ResolveZoneDefaultForPlayer(player) : null;
            var p = zoneProfile ?? ZoneControlManager.EvaluateTierDefault(tier);

            var one = new LootGenerationFactory.ZoneLootSetCounts { Shield = 1 };
            var treasure = Creature.ZoneFallbackTreasureAt(tier);
            var dropFloor = ZoneStatResolver.GradeFloorOf(p);
            List<WorldObject> made;
            using (ZoneStatResolver.ScopeDropFloor(dropFloor))
                made = LootGenerationFactory.CreateZoneLootSet(treasure, one);

            var wo = made.FirstOrDefault(i => i.IsShield);
            foreach (var extra in made)
                if (!ReferenceEquals(extra, wo))
                    extra.Destroy();
            if (wo == null)
                return $"asforge shield: the loot generator produced no shield at tier {tier} (see the server log) - nothing created.";

            Creature.ApplyZoneDropSweep(wo, p, null, tier, false, null, dropFloor);

            wo.Attuned = AttunedStatus.Attuned;
            wo.Bonded = BondedStatus.Bonded;
            var kept = (wo.LongDesc ?? "").Split('\n').Where(l => !l.StartsWith("Location:") && !l.StartsWith("Dropped by")).ToList();
            while (kept.Count > 0 && kept[kept.Count - 1].Trim().Length == 0)
                kept.RemoveAt(kept.Count - 1);
            var provenance = $"Created by: {player.Name}\nTier: {tier}";
            wo.LongDesc = kept.Count > 0 ? string.Join("\n", kept) + "\n\n" + provenance : provenance;

            if (!player.TryCreateInInventoryWithNetworking(wo))
            {
                wo.Destroy();
                return "asforge shield: no room in the pack - nothing created.";
            }
            return $"asforge shield: {wo.Name} (T{tier}, armor {wo.ArmorLevel ?? 0}) in your pack.";
        }
    }
}
