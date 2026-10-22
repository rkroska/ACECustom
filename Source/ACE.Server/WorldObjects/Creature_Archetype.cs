using System;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.WorldObjects
{
    /// <summary>The attack modes a monster archetype uses. A roll picks one of the 7 non-empty combinations.</summary>
    [Flags]
    public enum ZcArchetype
    {
        None = 0,       // never rolled (not governed, a player, a pet) - retail behaviour everywhere
        Melee = 1,
        Missile = 2,
        Magic = 4,
    }

    /// <summary>
    /// MONSTER ARCHETYPES (owner 2026-10-20): every Zone Control monster rolls ONE combination of melee / missile / magic at
    /// spawn, among the combinations its body allows, so a pack can come out all melee, all casters, all bow users or any mix.
    ///   - melee only = exactly today's monster (its own book still rolls its retail chance);
    ///   - missile = it is handed a launcher + ammo (or a thrown stack), never drops the bow, walks in when out of range;
    ///   - magic = casts on every attack (or on archetype_cast_chance of them when it has a second mode) from its caster list.
    /// Only a magic archetype casts: a bow user without magic (Bowyer, Grenadier, Skirmisher) never rolls its book.
    /// The roll is written once, before the monster enters the world, and only read afterwards (landblock thread).
    /// Store keys: ZoneStat.Archetype* (tier Default / zone / rank / wcid like any stat).
    /// </summary>
    partial class Creature
    {
        /// <summary>What this monster rolled. None = never rolled: every archetype code path is skipped.</summary>
        public ZcArchetype ZcArchetype { get; private set; }

        /// <summary>True once ApplyArchetype has run, rolled or not - a re-apply onto a live monster never re-rolls.</summary>
        private bool zcArchetypeRolled;

        /// <summary>The kit handed to a missile archetype, and the melee weapon it stows while the bow is out.</summary>
        private WorldObject zcBow, zcAmmo, zcMeleeWeapon;
        private bool zcSwapPending;

        /// <summary>Missile / magic archetypes walk instead of giving up: Range = out of bow range, Sight = no line of sight.</summary>
        private enum ZcApproach { None, Range, Sight }
        private ZcApproach zcApproach;
        private double zcApproachUntil, zcNextSightCheck;

        public bool ZcHasMissile => (ZcArchetype & ZcArchetype.Missile) != 0;
        public bool ZcHasMagic => (ZcArchetype & ZcArchetype.Magic) != 0;
        public bool ZcHasMelee => (ZcArchetype & ZcArchetype.Melee) != 0;

        /// <summary>A rolled archetype other than plain melee - the only monsters whose AI this file changes.</summary>
        public bool ZcArchetypeActive => ZcArchetype != ZcArchetype.None && ZcArchetype != ZcArchetype.Melee;

        /// <summary>True while an approach overrides the ranged "turn only" movement (StartTurn / GetMovementParameters).</summary>
        public bool ZcApproaching => zcApproach != ZcApproach.None;

        // the 7 combinations, in the order of their weight keys
        private static readonly (ZcArchetype Combo, string Key, double Default)[] ZcCombos =
        {
            (ZcArchetype.Melee, ZoneStat.ArchetypeWeightMelee, 15),
            (ZcArchetype.Missile, ZoneStat.ArchetypeWeightMissile, 15),
            (ZcArchetype.Magic, ZoneStat.ArchetypeWeightMagic, 15),
            (ZcArchetype.Missile | ZcArchetype.Melee, ZoneStat.ArchetypeWeightMissileMelee, 15),
            (ZcArchetype.Magic | ZcArchetype.Melee, ZoneStat.ArchetypeWeightMagicMelee, 15),
            (ZcArchetype.Missile | ZcArchetype.Magic, ZoneStat.ArchetypeWeightMissileMagic, 15),
            (ZcArchetype.Missile | ZcArchetype.Magic | ZcArchetype.Melee, ZoneStat.ArchetypeWeightAll, 10),
        };

        private static double ZcStat(EvaluatedProfile profile, string key, double unset)
        {
            if (profile == null || !profile.Has(key))
                return unset;
            var v = profile.Get(key);
            return double.IsFinite(v) ? v : unset;
        }

        /// <summary>
        /// Rolls and sets up the archetype. Called once from ZoneSpawnScaler.ApplyToSpawn, BEFORE the monster enters the world
        /// (so the kit is equipped without broadcasts, like the constructor's own EquipInventoryItems). A re-apply onto a live
        /// monster (the prestige command) is a no-op: the archetype is decided once per monster.
        /// </summary>
        public void ApplyArchetype(EvaluatedProfile profile)
        {
            if (zcArchetypeRolled)
            {
                // a re-apply onto a live monster (the prestige command) never re-rolls - but ApplyAppearance just reset an
                // authored display name, so the role word goes back on (cosmetic; the name was never broadcast anyway)
                if (zcRoleWord != null && !string.IsNullOrEmpty(Name) && !Name.EndsWith(" " + zcRoleWord, StringComparison.Ordinal))
                {
                    ZcBaseName = Name;
                    Name = Name + " " + zcRoleWord;
                    SetProperty(PropertyString.Name, Name);
                }
                // ApplyProps just ran again too: a zone prop stamp may have put mana use / magic delay back
                if (ZcHasMagic)
                    ZcStampCasterPacing(profile);
                return;
            }
            // monsters only: never a player, a pet, an NPC / vendor standing in a zone, or anything not attackable
            if (this is Player || this is CombatPet || profile == null || IsNPC || !Attackable || this is Vendor)
                return;
            // the Zone Control master switch off = no archetypes (the AI hooks fall back to retail as well)
            if (!ServerConfig.zonecontrol_enabled.Value)
                return;
            zcArchetypeRolled = true;

            // only BEFORE it enters the world: the kit is equipped without broadcasts and the name is not re-sent, so a monster
            // already standing in the world (a /prestige re-apply, a create-then-scale path) stays exactly what it is
            if (CurrentLandblock != null)
                return;

            // a body that already wields a launcher or thrown weapon of its own stays retail: a second missile weapon (or a
            // magic roll that ignores its bow) breaks the weapon choice
            if (GetEquippedMissileWeapon() != null || GetEquippedAmmo() != null)
                return;

            var canMissile = ZcStat(profile, ZoneStat.ArchetypeCanMissile, 0) >= 0.5
                && ZcStat(profile, ZoneStat.ArcherLauncherWcid, 0) > 0;
            // magic only when there is something to cast: a caster-list entry, or a book / zone spell with a chance above 0
            var canMagic = ZcStat(profile, ZoneStat.ArchetypeCanMagic, 0) >= 0.5 && ZcHasCastableSpell(profile);

            // a body that can do nothing but melee never rolls - it stays exactly the monster it was (ZcArchetype None)
            if (!canMissile && !canMagic)
                return;

            double total = 0;
            Span<double> weights = stackalloc double[ZcCombos.Length];
            for (int i = 0; i < ZcCombos.Length; i++)
            {
                var combo = ZcCombos[i].Combo;
                if ((combo & ZcArchetype.Missile) != 0 && !canMissile) continue;
                if ((combo & ZcArchetype.Magic) != 0 && !canMagic) continue;
                var w = ZcStat(profile, ZcCombos[i].Key, ZcCombos[i].Default);
                weights[i] = w > 0 ? w : 0;
                total += weights[i];
            }

            var rolled = ZcArchetype.Melee;
            if (total > 0)
            {
                var pick = ThreadSafeRandom.Next(0.0f, 1.0f) * total;
                for (int i = 0; i < ZcCombos.Length; i++)
                {
                    if (weights[i] <= 0) continue;
                    pick -= weights[i];
                    if (pick < 0 || i == ZcCombos.Length - 1) { rolled = ZcCombos[i].Combo; break; }
                }
                // float rounding on the last positive weight: take the last allowed combination
                if (rolled == ZcArchetype.Melee && pick >= 0)
                    for (int i = ZcCombos.Length - 1; i >= 0; i--)
                        if (weights[i] > 0) { rolled = ZcCombos[i].Combo; break; }
            }

            if ((rolled & ZcArchetype.Missile) != 0 && !ZcGiveBow(profile))
                rolled &= ~ZcArchetype.Missile;       // the kit failed to make: keep whatever else it rolled
            if (rolled == ZcArchetype.None)
                rolled = ZcArchetype.Melee;

            ZcArchetype = rolled;
            zcMeleeRange = (float)Math.Clamp(ZcStat(profile, ZoneStat.ArchetypeMeleeRange, 4.0), 0.0, 50.0);

            if (ZcHasMagic)
                ZcStampCasterPacing(profile);

            if (ZcArchetype != ZcArchetype.Melee && ZcStat(profile, ZoneStat.ArchetypeKeepName, 0) < 0.5)
            {
                var role = ZcRoleName(ZcArchetype, zcAmmo == null && zcBow != null);
                if (!string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(Name))
                {
                    zcRoleWord = role;
                    ZcBaseName = Name;
                    Name = Name + " " + role;
                    SetProperty(PropertyString.Name, Name);
                }
            }

            if (ServerConfig.spawn_diag_verbose.Value)
                log.Info($"[ZC archetype] {Name} 0x{Guid}:{WeenieClassId} rolled {ZcArchetype} (bow {canMissile}, magic {canMagic})");
        }

        /// <summary>A monster casting on every attack would empty its mana and then stand still doing nothing (Monster_Magic:
        /// "if (AiUsesMana && !UseMana()) return;"), so a magic archetype casts free, at caster_magic_delay when authored.</summary>
        private void ZcStampCasterPacing(EvaluatedProfile profile)
        {
            SetProperty(PropertyBool.AiUsesMana, false);
            var delay = ZcStat(profile, ZoneStat.CasterMagicDelay, double.NaN);
            if (double.IsFinite(delay) && delay > 0)
                SetProperty(PropertyFloat.AiUseMagicDelay, Math.Clamp(delay, 0.5, 60.0));
        }

        /// <summary>The name before the role word went on (null = no role word). The Pet Log / capture keys species by name,
        /// so it records this one - one species per monster, not eight.</summary>
        public string ZcBaseName { get; private set; }
        private string zcRoleWord;

        /// <summary>True when a magic archetype would have something to cast: a caster-list entry with weight above 0, or a
        /// DAMAGE spell of its book / zone-added spells with a chance above 0. Otherwise a "Sorcerer" would melee on every
        /// attack - or heal / buff / debuff on every attack, from a book of nothing but those.</summary>
        private bool ZcHasCastableSpell(EvaluatedProfile profile)
        {
            var rules = profile?.SpellRules;
            if (rules != null)
                foreach (var r in rules)
                {
                    if (r.Disabled) continue;
                    if (r.Caster && (r.Chance ?? 10.0) > 0) return true;
                    if (!r.Caster && (Biota.PropertiesSpellBook == null || !Biota.PropertiesSpellBook.ContainsKey(r.SpellId)) && (r.Chance ?? 2.0) > 0
                        && ZcIsDamageSpell(r.SpellId)) return true;
                }
            if (Biota.PropertiesSpellBook != null)
                foreach (var s in Biota.PropertiesSpellBook)
                    if (ZcBookWeight(rules, s.Key, s.Value) > 0 && ZcIsDamageSpell(s.Key)) return true;
            return false;
        }

        /// <summary>The spells a magic archetype casts, from its caster list (the caster command) or its book: a projectile
        /// (bolt / streak / arc ...), a Harm Health or a Drain Health - the kinds that take the zone's spell_damage.</summary>
        public static bool ZcIsDamageSpell(Spell spell)
        {
            if (spell == null || spell.NotFound)
                return false;
            if (spell.IsProjectile)
                return true;
            var isHarm = spell.MetaSpellType == SpellType.Boost && Math.Min(spell.Boost, spell.MaxBoost) < 0
                && spell.VitalDamageType == DamageType.Health;
            var isDrain = spell.MetaSpellType == SpellType.Transfer && spell.Source == PropertyAttribute2nd.Health
                && spell.TransferFlags.HasFlag(TransferFlags.TargetSource | TransferFlags.CasterDestination);
            return isHarm || isDrain;
        }

        private static bool ZcIsDamageSpell(int spellId) => ZcIsDamageSpell(new Spell((uint)spellId));

        /// <summary>The role word that goes after the name (owner 2026-10-20: "variations of caster or archer like Bowyer,
        /// grenader, sorcerer"). ASCII only; never names the system.</summary>
        private static string ZcRoleName(ZcArchetype a, bool thrown)
        {
            switch (a)
            {
                case ZcArchetype.Missile: return thrown ? "Grenadier" : "Bowyer";
                case ZcArchetype.Magic: return "Sorcerer";
                case ZcArchetype.Missile | ZcArchetype.Melee: return "Skirmisher";
                case ZcArchetype.Magic | ZcArchetype.Melee: return "Battlemage";
                case ZcArchetype.Missile | ZcArchetype.Magic: return "Spellslinger";
                case ZcArchetype.Missile | ZcArchetype.Magic | ZcArchetype.Melee: return "Warmaster";
                default: return null;
            }
        }

        /// <summary>Makes and equips the launcher + ammo (or the thrown stack). Both are Bonded=Destroy with no destination,
        /// so neither ever reaches the corpse (Creature_Death moves only Treasure / WieldTreasure, skips Bonded Destroy).
        /// The kit and any stowed melee weapon live in the monster's own pack / slots, so they are destroyed with it.</summary>
        private bool ZcGiveBow(EvaluatedProfile profile)
        {
            var launcherWcid = (uint)Math.Clamp(Math.Round(ZcStat(profile, ZoneStat.ArcherLauncherWcid, 0)), 0, uint.MaxValue);
            var ammoWcid = (uint)Math.Clamp(Math.Round(ZcStat(profile, ZoneStat.ArcherAmmoWcid, 0)), 0, uint.MaxValue);
            if (launcherWcid == 0)
                return false;

            var bow = WorldObjectFactory.CreateNewWorldObject(launcherWcid);
            if (bow == null || !bow.ValidLocations.HasValue || (bow.ValidLocations.Value & EquipMask.MissileWeapon) == 0)
            {
                log.Warn($"[ZC archetype] {Name}:{WeenieClassId}: archer_launcher_wcid {launcherWcid} is not a missile weapon - no bow");
                bow?.Destroy();
                return false;
            }
            WorldObject ammo = null;
            if (bow.IsAmmoLauncher)
            {
                ammo = ammoWcid != 0 ? WorldObjectFactory.CreateNewWorldObject(ammoWcid) : null;
                if (ammo == null || ammo.WeenieType != WeenieType.Ammunition || ammo.AmmoType != bow.AmmoType)
                {
                    log.Warn($"[ZC archetype] {Name}:{WeenieClassId}: archer_ammo_wcid {ammoWcid} does not fit launcher {launcherWcid} - no bow");
                    bow.Destroy();
                    ammo?.Destroy();
                    return false;
                }
            }

            ZcMakeUnlootable(bow);
            if (ammo != null)
                ZcMakeUnlootable(ammo);

            // the pack holds the stowed weapons: room for the melee weapon now and the bow + ammo on a swap (a body with no
            // ItemsCapacity would otherwise lose its bow on its first swap). Monster packs are never shown to anyone.
            if ((ItemCapacity ?? 0) < Inventory.Count + 4)
                ItemCapacity = (byte)Math.Min(255, Inventory.Count + 4);

            // a wielded MAIN-HAND melee weapon (the beach leaders' blades) goes into the pack: it keeps its own drop rules
            // (Creature_Death takes a pack item off the creature before the corpse gets it) and comes back out when a
            // missile + melee monster swaps to melee
            var melee = GetEquippedMeleeWeapon(true);
            if (melee != null && TryDequipObject(melee.Guid, out var stowed, out var stowedFrom))
            {
                if (TryAddToInventory(stowed, burdenCheck: false))
                {
                    zcMeleeWeapon = stowed;
                    zcMeleeSlot = stowedFrom;
                }
                else if (!TryEquipObject(stowed, stowedFrom))
                    stowed.Destroy();   // cannot happen with the room made above; never leave it in no container
            }

            if (!TryWieldObject(bow, EquipMask.MissileWeapon) || (ammo != null && !TryWieldObject(ammo, EquipMask.MissileAmmo)))
            {
                if (bow.Wielder != null) TryDequipObject(bow.Guid, out _, out _);
                bow.Destroy();
                ammo?.Destroy();
                if (zcMeleeWeapon != null && TryRemoveFromInventory(zcMeleeWeapon.Guid) && !TryEquipObject(zcMeleeWeapon, zcMeleeSlot))
                    TryAddToInventory(zcMeleeWeapon, burdenCheck: false);
                zcMeleeWeapon = null;
                return false;
            }

            // a launcher that cannot reach (no velocity) would leave a Bowyer hugging its target without ever shooting
            if (GetMaxMissileRange() < 5.0f)
            {
                log.Warn($"[ZC archetype] {Name}:{WeenieClassId}: launcher {launcherWcid} reaches only {GetMaxMissileRange():0.0} m - no bow");
                TryDequipObject(bow.Guid, out _, out _);
                if (ammo != null) TryDequipObject(ammo.Guid, out _, out _);
                bow.Destroy();
                ammo?.Destroy();
                if (zcMeleeWeapon != null && TryRemoveFromInventory(zcMeleeWeapon.Guid) && !TryEquipObject(zcMeleeWeapon, zcMeleeSlot))
                    TryAddToInventory(zcMeleeWeapon, burdenCheck: false);
                zcMeleeWeapon = null;
                return false;
            }

            zcBow = bow;
            zcAmmo = ammo;
            return true;
        }

        private EquipMask zcMeleeSlot = EquipMask.MeleeWeapon;

        /// <summary>The melee weapon a monster archetype stowed in its pack (null = none) - Creature_Death takes it off the
        /// creature before the corpse gets it.</summary>
        public WorldObject ZcStowedMeleeWeapon => zcMeleeWeapon;

        /// <summary>True for the launcher / ammo a missile archetype was handed (not its own gear) - a capture skips them.</summary>
        public bool ZcIsArchetypeKit(WorldObject item) => item != null && (item == zcBow || item == zcAmmo);

        private static void ZcMakeUnlootable(WorldObject item)
        {
            item.Bonded = BondedStatus.Destroy;
            item.DestinationType = DestinationType.Undef;
        }

        // ------------------------------------------------------------------ attack choice

        /// <summary>
        /// The next attack type of an archetype monster (GetNextAttackType calls this when ZcArchetypeActive).
        ///   - magic: always when magic is its only mode, else on archetype_cast_chance of its attacks;
        ///   - missile (+ melee): the bow at range; inside archetype_melee_range it swaps to melee, past twice that back to the bow;
        ///   - otherwise melee.
        /// </summary>
        private CombatType? ZcNextAttackType()
        {
            // the master switch off (or the monster left its zone): null = the retail choice
            var profile = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveCombatProfile(this);
            if (profile == null)
                return null;

            // never while a weapon swap is under way: its stance change would land in the middle of a cast
            if (ZcHasMagic && !zcSwapPending)
            {
                // magic alone casts every attack; magic + melee (no bow) also casts every attack while the target is
                // beyond twice the melee range - it may swing up close, but never runs across the room to do it
                // (owner 2026-10-20: "should not run across the room to melee when they could cast")
                var dist = GetDistanceToTarget();
                var castAlways = (!ZcHasMissile || zcBowBlocked) && (!ZcHasMelee || dist > zcMeleeRange * 2);
                var chance = Math.Clamp(ZcStat(profile, ZoneStat.ArchetypeCastChance, 0.5), 0.0, 1.0);
                if ((castAlways || ThreadSafeRandom.Next(0.0f, 1.0f) < chance) && ZcTryRollArchetypeSpell(profile))
                {
                    // bow out and the target past the spell's reach: shoot instead (a ranged stance only turns, so a cast
                    // out of reach would stand turning until the target came closer)
                    if (ZcHasMissile && GetEquippedMissileWeapon() != null && dist > GetSpellMaxRange())
                        CurrentSpell = null;
                    else
                        return CombatType.Magic;
                }
            }

            if (ZcHasMissile)
            {
                var bowOut = GetEquippedMissileWeapon() != null;
                // never in the middle of an attack chain (aim / launch / reload, a swing): the chain holds the old weapon
                var chainDone = Timers.RunningTime >= NextMoveTime && Timers.RunningTime >= NextAttackTime;
                if (ZcHasMelee && !zcSwapPending && chainDone && Timers.RunningTime >= zcNextSwapTime)
                {
                    var swapAt = zcMeleeRange;
                    var dist = GetDistanceToTarget();
                    if (bowOut && dist < swapAt)
                        ZcSwapWeapons(toBow: false);
                    else if (!bowOut && dist > swapAt * 2 && !zcBowBlocked)
                        ZcSwapWeapons(toBow: true);
                }
                if (bowOut && !zcSwapPending)
                    return CombatType.Missile;
                if (!ZcHasMelee && !zcSwapPending && chainDone && zcBow != null && zcBow.Wielder == null)
                    ZcSwapWeapons(toBow: true);   // a missile-only monster that somehow lost its bow out: put it back
            }

            return CombatType.Melee;
        }

        /// <summary>
        /// Picks the spell of a magic archetype's cast. The caster list (spell rules with Caster on, Chance = weight) when the
        /// zone authors one; else the monster's own book (+ zone-added spells), weighted by their chances. Always succeeds when
        /// either list has a castable spell - the "cast every attack" half of the archetype.
        /// </summary>
        private bool ZcTryRollArchetypeSpell(EvaluatedProfile profile)
        {
            CurrentSpell = null;
            var rules = profile?.SpellRules;

            uint pick = 0;
            double total = 0;
            if (rules != null)
            {
                foreach (var r in rules)
                    if (r.Caster && !r.Disabled && (r.Chance ?? 10.0) > 0) total += r.Chance ?? 10.0;
                if (total > 0)
                {
                    var roll = ThreadSafeRandom.Next(0.0f, 1.0f) * total;
                    foreach (var r in rules)
                    {
                        if (!r.Caster || r.Disabled) continue;
                        var w = r.Chance ?? 10.0;
                        if (w <= 0) continue;
                        pick = (uint)r.SpellId;
                        roll -= w;
                        if (roll < 0) break;
                    }
                }
            }

            if (pick == 0)
            {
                // the book, the same chances TryRollSpell uses, but as weights: one of them always fires (damage spells only)
                total = 0;
                if (Biota.PropertiesSpellBook != null)
                    foreach (var s in Biota.PropertiesSpellBook)
                        total += ZcIsDamageSpell(s.Key) ? ZcBookWeight(rules, s.Key, s.Value) : 0;
                if (rules != null)
                    foreach (var r in rules)
                        if (!r.Caster && !r.Disabled && (Biota.PropertiesSpellBook == null || !Biota.PropertiesSpellBook.ContainsKey(r.SpellId)))
                            total += ZcIsDamageSpell(r.SpellId) ? Math.Max(0.0, (r.Chance ?? 2.0) / 100.0) : 0;
                if (total <= 0)
                    return false;

                var roll = ThreadSafeRandom.Next(0.0f, 1.0f) * total;
                if (Biota.PropertiesSpellBook != null)
                    foreach (var s in Biota.PropertiesSpellBook)
                    {
                        var w = ZcIsDamageSpell(s.Key) ? ZcBookWeight(rules, s.Key, s.Value) : 0;
                        if (w <= 0) continue;
                        pick = (uint)s.Key;
                        roll -= w;
                        if (roll < 0) break;
                    }
                if (roll >= 0 && rules != null)
                    foreach (var r in rules)
                    {
                        if (r.Caster || r.Disabled || (Biota.PropertiesSpellBook != null && Biota.PropertiesSpellBook.ContainsKey(r.SpellId))) continue;
                        var w = ZcIsDamageSpell(r.SpellId) ? Math.Max(0.0, (r.Chance ?? 2.0) / 100.0) : 0;
                        if (w <= 0) continue;
                        pick = (uint)r.SpellId;
                        roll -= w;
                        if (roll < 0) break;
                    }
            }

            if (pick == 0)
                return false;
            var spell = new Spell(pick);
            if (spell.NotFound)
                return false;
            CurrentSpell = spell;
            return true;
        }

        private static double ZcBookWeight(System.Collections.Generic.IReadOnlyList<ZoneSpellRule> rules, int spellId, float bookValue)
        {
            double p = bookValue > 2.0f ? bookValue - 2.0f : bookValue / 100.0f;
            if (rules != null)
                for (int i = 0; i < rules.Count; i++)
                {
                    var r = rules[i];
                    if (r.SpellId != spellId || r.Caster) continue;
                    if (r.Disabled) return 0;
                    if (r.Chance.HasValue) p = r.Chance.Value / 100.0;
                }
            return p > 0 && double.IsFinite(p) ? p : 0;
        }

        // ------------------------------------------------------------------ bow <-> melee swap (missile + melee)

        /// <summary>Stows the bow and draws the melee weapon (or bare hands), or the other way round, after a stance change
        /// out of combat - the same shape as SwitchToMeleeAttack, but nothing is destroyed. Blocks attacks until done.</summary>
        private void ZcSwapWeapons(bool toBow)
        {
            if (zcSwapPending || IsDead || zcBow == null)
                return;

            zcSwapPending = true;
            zcNextSwapTime = Timers.RunningTime + ZcSwapLockout;
            ZcHaltMove(broadcast: true);     // stand still for the swap (a chase run-in would carry it into melee range)
            SwitchWeaponsPending = true;     // RangeAttack waits while this is set
            zcSightApproachRequested = false;
            MonsterProjectile_OnCollideEnvironment_Counter = 0;
            NextAttackTime = Math.Max(NextAttackTime, Timers.RunningTime + 3.0);
            NextMoveTime = Math.Max(NextMoveTime, Timers.RunningTime + 3.0);   // no run-in starts while the stance changes

            var chain = new ActionChain();
            EnqueueMotion_Force(chain, MotionStance.NonCombat, MotionCommand.Ready, (MotionCommand)CurrentMotionState.Stance);
            chain.AddAction(this, ActionType.MonsterArchetype_SwapWeapons, () =>
            {
                try
                {
                    if (IsDead || zcBow == null)
                        return;

                    if (toBow)
                    {
                        var melee = GetEquippedMeleeWeapon(true);
                        if (melee != null && TryUnwieldObjectWithBroadcasting(melee.Guid, out var m, out var mSlot))
                        {
                            if (TryAddToInventory(m, burdenCheck: false)) { zcMeleeWeapon = m; zcMeleeSlot = mSlot; }
                            else if (!TryWieldObjectWithBroadcasting(m, mSlot)) m.Destroy();
                        }
                        if (zcBow.Wielder == null && TryRemoveFromInventory(zcBow.Guid) && !TryWieldObjectWithBroadcasting(zcBow, EquipMask.MissileWeapon))
                            TryAddToInventory(zcBow, burdenCheck: false);
                        if (zcAmmo != null && zcAmmo.Wielder == null && TryRemoveFromInventory(zcAmmo.Guid) && !TryWieldObjectWithBroadcasting(zcAmmo, EquipMask.MissileAmmo))
                            TryAddToInventory(zcAmmo, burdenCheck: false);
                    }
                    else
                    {
                        if (zcBow.Wielder != null && TryUnwieldObjectWithBroadcasting(zcBow.Guid, out var b, out _) && !TryAddToInventory(b, burdenCheck: false))
                            { b.Destroy(); zcBow = null; }
                        if (zcAmmo != null && zcAmmo.Wielder != null && TryUnwieldObjectWithBroadcasting(zcAmmo.Guid, out var a, out _) && !TryAddToInventory(a, burdenCheck: false))
                            { a.Destroy(); zcAmmo = null; }
                        if (zcMeleeWeapon != null && zcMeleeWeapon.Wielder == null && TryRemoveFromInventory(zcMeleeWeapon.Guid)
                            && !TryWieldObjectWithBroadcasting(zcMeleeWeapon, zcMeleeSlot))
                            TryAddToInventory(zcMeleeWeapon, burdenCheck: false);   // never in no container
                    }

                    // gone to sleep or heading home meanwhile: the weapons are swapped, but it stays out of combat stance
                    if (MonsterState == State.Awake && AttackTarget != null)
                    {
                        var stanceTime = SetCombatMode(GetEquippedMissileWeapon() != null ? CombatMode.Missile : CombatMode.Melee);
                        NextMoveTime = NextAttackTime = Timers.RunningTime + stanceTime;
                        PhysicsObj?.StartTimer();
                        ResetAttack();
                    }
                }
                finally
                {
                    zcSwapPending = false;
                    SwitchWeaponsPending = false;
                    // the lockout counts from the END of the swap, so kiting it across the swap line costs it less
                    zcNextSwapTime = Timers.RunningTime + ZcSwapLockout;
                }
            });
            chain.EnqueueChain();
        }

        // ------------------------------------------------------------------ approach instead of giving up

        /// <summary>Starts walking toward the target: Range = beyond bow range, Sight = no line of sight (3 shots into
        /// terrain, or a projectile spell with nothing to see). Overrides the ranged "turn only" movement until it ends.</summary>
        private void ZcStartApproach(bool sight, bool forSpell = false)
        {
            if (AiImmobile)
                return;
            if (forSpell)
                zcSightTries++;
            // the clock starts when it can actually move (a walk asked for right after a cast / shot waits out its recovery)
            var start = Math.Max(Timers.RunningTime, NextMoveTime);
            zcApproach = sight ? ZcApproach.Sight : ZcApproach.Range;
            zcApproachUntil = start + (sight ? 4.0 : 8.0);
            zcNextSightCheck = start + 1.0;
            ZcHaltMove(broadcast: true);
        }

        /// <summary>Consecutive "can't see it" walks of a ranged-only monster. After 3 without a sighting the retail fallback
        /// runs again (re-roll, then melee), so a caster that cannot path round a wall is never pinned on it forever.</summary>
        private int zcSightTries;

        /// <summary>GetMaxRange: may a ranged-only archetype walk instead of falling back to melee?</summary>
        public bool ZcSightApproachAllowed => !AiImmobile && zcSightTries < 3;

        /// <summary>GetMaxRange saw the target: the walk counter starts over.</summary>
        public void ZcSightOk() => zcSightTries = 0;

        /// <summary>Clears every walk / run-in flag: the monster went home or to sleep (no stale approach on the next fight).</summary>
        public void ZcResetMovement()
        {
            zcApproach = ZcApproach.None;
            zcSightApproachRequested = false;
            zcSightTries = 0;
            zcBowSightWalks = 0;
            zcBowBlocked = false;
            ZcRunIn = false;
        }

        /// <summary>A Skirmisher whose 3 sight walks ran out keeps its melee weapon until it goes home or to sleep.</summary>
        private bool zcBowBlocked;

        /// <summary>False with the master switch off or out of its zone: the archetype swaps stand down (retail choice).</summary>
        private bool ZcProfileLive() => ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveCombatProfile(this) != null;

        /// <summary>Sight walks of a bow user this fight (3 shots into terrain each); reset when it goes home or to sleep.</summary>
        private int zcBowSightWalks;

        private bool zcSightApproachRequested;

        /// <summary>From a projectile's terrain collision (3 shots into the ground): walk on the monster's own next tick.</summary>
        private void ZcRequestSightApproach() => zcSightApproachRequested = true;

        /// <summary>The per-tick archetype step in Monster_Tick, after the distance is known. True = the tick is done.
        ///   - a requested sight walk starts;
        ///   - a running walk ends when in range / in sight / out of time;
        ///   - a bow user that is not walking never keeps sliding on a melee run-in (a swap to the bow mid-run);
        ///   - a missile + melee monster chasing in melee mode re-decides (and draws the bow) once the target is far.</summary>
        private bool ZcTickApproach(float targetDist)
        {
            if (zcSightApproachRequested)
            {
                zcSightApproachRequested = false;
                if (!ZcApproaching && CurrentAttack == CombatType.Missile && GetEquippedMissileWeapon() != null)
                {
                    // at most 3 walks per fight: a ledge lip it can see over but not shoot over would loop forever.
                    // After that a Skirmisher draws its melee weapon; a bow-only monster keeps shooting from where it is.
                    if (zcBowSightWalks < 3)
                    {
                        zcBowSightWalks++;
                        ZcStartApproach(sight: true);
                    }
                    else if (ZcHasMelee && !zcSwapPending && zcBow != null && ZcProfileLive())
                    {
                        zcBowBlocked = true;     // no bow back out this fight (it would shoot the same ledge again)
                        ZcSwapWeapons(toBow: false);
                        ResetAttack();
                        MonsterProjectile_OnCollideEnvironment_Counter = 0;
                        return true;
                    }
                }
                MonsterProjectile_OnCollideEnvironment_Counter = 0;
            }
            if (ZcApproaching)
                return ZcApproachTick(targetDist);
            if (CurrentAttack == CombatType.Missile)
                ZcStopForRangedAttack();

            var now = Timers.RunningTime;
            // bow out, target inside melee range: swap to melee once the last shot's chain is over (the decision right
            // after a shot always sees the chain still running, so it can never make this call itself)
            if (ZcHasMissile && ZcHasMelee && CurrentAttack == CombatType.Missile && !zcSwapPending && zcBow != null
                && GetEquippedMissileWeapon() != null && now >= zcNextSwapTime && now >= NextMoveTime && now >= NextAttackTime
                && targetDist < zcMeleeRange && ZcProfileLive())
            {
                ZcSwapWeapons(toBow: false);
                ResetAttack();
                return true;
            }
            if (ZcHasMissile && ZcHasMelee && CurrentAttack == CombatType.Melee && !zcSwapPending && zcBow != null
                && GetEquippedMissileWeapon() == null && now >= zcNextSwapTime && now >= NextMoveTime && now >= NextAttackTime
                && targetDist > zcMeleeRange * 2 && !zcBowBlocked && ZcProfileLive())
            {
                ZcHaltMove(broadcast: true);
                ResetAttack();      // the next tick decides again: far away = the bow comes out
                return true;
            }
            return false;
        }

        /// <summary>Called every tick while approaching. True = it ended the approach this tick (the caller returns and the
        /// next tick decides again from where it now stands).</summary>
        private bool ZcApproachTick(float targetDist)
        {
            var now = Timers.RunningTime;
            var done = now >= zcApproachUntil;
            var sawIt = false;
            if (zcApproach == ZcApproach.Range)
            {
                var inRange = targetDist <= MaxRange * 0.85f;
                // still out of reach when the time is up: keep walking (a halt + restart every 8 s is a visible stutter;
                // the chase range and going home end the walk as for any monster)
                if (done && !inRange)
                {
                    zcApproachUntil = now + 8.0;
                    return false;
                }
                done = inRange;
            }
            if (!done && zcApproach == ZcApproach.Sight && now >= zcNextSightCheck)
            {
                zcNextSightCheck = now + 1.0;
                done = sawIt = AttackTarget != null && IsDirectVisible(AttackTarget);
            }
            if (!done)
                return false;

            var wasSight = zcApproach == ZcApproach.Sight;
            zcApproach = ZcApproach.None;
            if (sawIt)
                zcSightTries = 0;
            ZcHaltMove(broadcast: true);
            if (wasSight)
            {
                MonsterProjectile_OnCollideEnvironment_Counter = 0;
                ResetAttack();      // decide again: the LOS check runs fresh
            }
            return true;
        }

        /// <summary>A missile / magic archetype starting a ranged attack must not keep sliding toward the target: the sticky
        /// MoveTo of the run-in survives ResetAttack, and that is how retail casters "creep" into melee (owner 2026-10-20).</summary>
        private void ZcStopForRangedAttack()
        {
            // only a RUN-IN is cut: a turn toward the target (TurnToObject, also "moving to" in the physics) must finish
            if (!ZcRunIn)
                return;
            ZcHaltMove(broadcast: true);
        }

        /// <summary>Stops the physics move where the monster stands, without ResetAttack (CancelMoveTo would also push
        /// NextMoveTime and, going home, teleport). broadcast = tell clients it stopped, so they do not keep animating the run.</summary>
        private void ZcHaltMove(bool broadcast)
        {
            ZcRunIn = false;
            if (PhysicsObj?.MovementManager?.MoveToManager?.is_moving_to() == true)
            {
                PhysicsObj.MovementManager.MoveToManager.CancelMoveTo(WeenieError.ActionCancelled);
                PhysicsObj.CachedVelocity = System.Numerics.Vector3.Zero;
                if (broadcast)
                    EnqueueBroadcastMotion(new Motion(CurrentMotionState.Stance, MotionCommand.Ready));
            }
            IsMoving = false;
            IsTurning = false;
        }

        /// <summary>Set by StartTurn: true when its last order was a run toward the target (MoveTo), false for a turn.</summary>
        public bool ZcRunIn;

        /// <summary>A missile + melee monster swaps at most once every this many seconds (no kiting it into endless swaps).</summary>
        private const double ZcSwapLockout = 8.0;
        private double zcNextSwapTime;

        /// <summary>archetype_melee_range at spawn: inside it a missile + melee monster swaps to melee, past twice it to the bow.</summary>
        private float zcMeleeRange = 4f;

        // ------------------------------------------------------------------ damage dials

        /// <summary>x on this monster's damage to a player by attack kind: archetype_missile_damage_mult on its MISSILE hits,
        /// archetype_magic_damage_mult on its SPELL hits (and their DoT ticks), when it rolled that mode. Melee hits and every
        /// monster that did not roll that mode: 1.0. Read live, so a change applies at once.</summary>
        public float ZcArchetypeDamageMult(EvaluatedProfile profile, CombatType? kind)
        {
            if (!ZcArchetypeActive || kind == null || profile == null)
                return 1f;
            string key;
            if (kind == CombatType.Missile && ZcHasMissile) key = ZoneStat.ArchetypeMissileDamageMult;
            else if (kind == CombatType.Magic && ZcHasMagic) key = ZoneStat.ArchetypeMagicDamageMult;
            else return 1f;
            // 0 = that mode deals no damage, above 10 is held at 10 (a typo never one-shots); NaN / unset = 1
            var m = ZcStat(profile, key, 1.0);
            return (float)Math.Clamp(m, 0.0, 10.0);
        }

        /// <summary>The zone spell damage a magic archetype's Harm / Drain deals to a player, the projectile way: spell_damage
        /// spread down by spell_variance, plus its spell True Damage. False (leave the retail amount) for anything else -
        /// a non-magic monster, no zone, no spell_damage authored, or a spell that is not the monster's own cast (a weapon
        /// proc, an emote). Both amounts are held to the player's max health (a typo never wraps the damage to a heal).</summary>
        public static bool ZcArchetypeSpellDamage(Creature caster, Player target, Spell spell, out float baseDamage, out float trueDamage)
        {
            baseDamage = trueDamage = 0f;
            if (caster == null || target == null || spell == null || caster is Player || !caster.ZcHasMagic || !caster.ZcIsOwnCast(spell))
                return false;
            var zp = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveCombatProfile(caster);
            if (zp == null || !zp.Has(ZoneStat.SpellDamage))
                return false;
            var b = zp.Get(ZoneStat.SpellDamage);
            if (!double.IsFinite(b))
                return false;
            var cap = (double)Math.Max(1u, target.Health.MaxValue);
            trueDamage = (float)Math.Clamp((double)GetTrueDamage(caster, target, false, isSpell: true), 0.0, cap);
            if (b <= 0)
                return true;     // 0 = no base damage (its True Damage still lands), as a bolt of this monster (floored at 0)
            if (zp.Has(ZoneStat.SpellVariance))
            {
                var v = zp.Get(ZoneStat.SpellVariance);
                if (double.IsFinite(v))
                    b *= 1.0 - Math.Clamp(v, 0.0, 1.0) * ThreadSafeRandom.Next(0.0f, 1.0f);
            }
            // the same steps a bolt of this monster takes (SpellProjectile): the zone spell_damage_mult, then the rating
            // chain - its Damage Rating against the player's Damage Resist Rating (no crit: Harm / Drain never crit)
            if (zp.Has(ZoneStat.SpellDamageMult))
            {
                var sm = zp.Get(ZoneStat.SpellDamageMult);
                if (double.IsFinite(sm) && sm >= 0) b *= sm;
            }
            b *= GetPositiveRatingMod(caster.GetDamageRating()) * target.GetDamageResistRatingMod(CombatType.Magic);
            baseDamage = (float)Math.Clamp(b, 0.0, cap);
            return true;
        }

        /// <summary>The spell of the monster's own cast while it resolves (Monster_Magic sets it around CastSpell): a weapon
        /// proc or an emote cast is never it. The decision fields (CurrentAttack / CurrentSpell) move on during the wind-up.</summary>
        private Spell zcOwnCast;

        /// <summary>True while this exact spell is the monster's own cast resolving (never a proc, emote or another pick).</summary>
        public bool ZcIsOwnCast(Spell spell) => spell != null && ReferenceEquals(zcOwnCast, spell);

        /// <summary>The kind for the archetype damage dial on a Harm / Drain: Magic for the monster's own cast, else none.</summary>
        public CombatType? ZcOwnCastKind(Spell spell) => ZcIsOwnCast(spell) ? CombatType.Magic : (CombatType?)null;

        /// <summary>A one-line summary for GM commands and diagnostics.</summary>
        public string ZcArchetypeLabel()
        {
            if (!zcArchetypeRolled) return "not rolled";
            if (ZcArchetype == ZcArchetype.None) return "melee only (body)";
            return ZcArchetype.ToString().Replace(", ", "+");
        }
    }
}
