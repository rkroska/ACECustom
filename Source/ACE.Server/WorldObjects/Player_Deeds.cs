using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using ACE.Common;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Structure;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// QB Deeds: every 1,000 QB unlocks one of 15
    /// cumulative tiers of DeedsTiers. No item - /deeds on / off is a saved character flag (PropertyBool.DeedsOff, missing = ON).
    ///
    /// The buffs are Duration -1 enchantments at each spell's REAL power level, with no aug bake, carrying the reserved caster
    /// id DeedsCasterId (no object ever has it). A Deeds enchantment is exactly: a spell in the Deeds set + caster DeedsCasterId +
    /// Duration -1. Its own caster id means a cast of the player's own of the same spell writes its own layer and never
    /// refreshes (times out) the Deeds one, and /deeds off never touches the player's casts. AuditItemSpells keeps the Deeds
    /// ones while they are owed (IsKeptDeedsEnchantment); death keeps every Duration -1 enchantment, so they survive it like
    /// item cantrips.
    ///
    /// PvE only: the T12 / T13 / T14 effects apply only against a non-player creature.
    ///
    /// The hooks in shared files read DeedsTier only - a cached int set at login, on /deeds on / off and when a quest stamp
    /// moves the QB across a tier.
    /// </summary>
    partial class Player
    {
        /// <summary>
        /// The caster object id on every Deeds enchantment. 0x00DEED00 is below ObjectGuid.StaticObjectMin (0x01000000), so it is
        /// GuidType.Undef - outside every range ACE hands out (players 0x50000001-0x5FFFFFFF, world / static 0x01000000-0xEFFFFFFF,
        /// dynamic 0xF0000000-0xFFFFFFFE) and not 0 (no caster) or 0xFFFFFFFF (invalid). A lookup of it finds nothing.
        /// </summary>
        public const uint DeedsCasterId = 0x00DEED00;

        /// <summary>/rebuff waits this long after a monster or trap dispels one of the player's buffs.</summary>
        public const double DeedsRebuffDispelLockoutSeconds = 120.0;

        /// <summary>When another caster (monster, trap, player) last dispelled a beneficial enchantment off this player.</summary>
        public double LastHostileDispelTimestamp { get; set; }

        /// <summary>The highest Deeds tier in effect: 0 when Deeds is OFF, else TierFor(QB). Every hook reads this.</summary>
        public int DeedsTier { get; private set; }

        /// <summary>The player's Deeds setting: ON unless they typed /deeds off (saved on the character).</summary>
        public bool DeedsEnabled
        {
            get => !(GetProperty(PropertyBool.DeedsOff) ?? false);
            set { if (value) RemoveProperty(PropertyBool.DeedsOff); else SetProperty(PropertyBool.DeedsOff, true); }
        }

        /// <summary>The character's QB as last read for Deeds (PropertyInt64 56000, saved on the character).</summary>
        public long? DeedsQb
        {
            get => GetProperty(PropertyInt64.DeedsQb);
            set { if (!value.HasValue) RemoveProperty(PropertyInt64.DeedsQb); else SetProperty(PropertyInt64.DeedsQb, value.Value); }
        }

        /// <summary>
        /// The player's QB the way /qb and the QB bounty count it (BountyManager.QbOf): the ACCOUNT's quest-bonus count, the
        /// character's own copy only when the account is not loaded. The ONE read Deeds uses (a test mod patches it).
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public long ReadDeedsQb() => Account?.CachedQuestBonusCount ?? QuestCompletionCount ?? 0;

        /// <summary>The tier the player is owed right now (0 when Deeds is OFF) - read live, so it is right before the first refresh.</summary>
        private int OwedDeedsTier() => DeedsEnabled ? DeedsTiers.TierFor(ReadDeedsQb()) : 0;

        /// <summary>A Deeds enchantment: a spell in the Deeds set, with the Deeds caster id, permanent.</summary>
        public static bool IsDeedsEnchantment(PropertiesEnchantmentRegistry entry)
            => entry.CasterObjectId == DeedsCasterId && entry.Duration == -1 && DeedsTiers.SpellTier.ContainsKey((uint)entry.SpellId);

        /// <summary>The tier AuditItemSpells keeps (read once per audit): the tier owed now.</summary>
        public int DeedsAuditTier() => OwedDeedsTier();

        /// <summary>AuditItemSpells: keep a Deeds enchantment while Deeds is ON and the tier (DeedsAuditTier) reaches its spell.</summary>
        public static bool IsKeptDeedsEnchantment(PropertiesEnchantmentRegistry entry, int owedTier)
            => IsDeedsEnchantment(entry) && DeedsTiers.SpellTier[(uint)entry.SpellId] <= owedTier;

        // -- own removals: never a dispel for the /rebuff or Auto-Rebuff lockouts --------------------------------

        private int ownRemovalDepth;

        /// <summary>True while the server removes the player's enchantments on its own account (Deeds sync, /deeds off, Asheron's
        /// Favor re-apply, AuditItemSpells) - EnchantmentManager.Dispel then does not start the Auto-Rebuff dispel lockout.</summary>
        public bool IsOwnRemoval => ownRemovalDepth > 0;

        public OwnRemovalScope BeginOwnRemoval() => new OwnRemovalScope(this);

        public readonly struct OwnRemovalScope : IDisposable
        {
            private readonly Player player;
            public OwnRemovalScope(Player player) { this.player = player; player.ownRemovalDepth++; }
            public void Dispose() => player.ownRemovalDepth--;
        }

        /// <summary>HandleCastSpell_Dispel: another caster dispelled beneficial enchantments off this player (starts the /rebuff lockout).</summary>
        public void OnHostileDispel() => LastHostileDispelTimestamp = Time.GetUnixTime();

        /// <summary>True from the start of a Deeds buff sync until one succeeds: RefreshDeeds then syncs again even with the tier unchanged.</summary>
        private bool deedsSyncPending;

        /// <summary>
        /// Called at login (after AuditItemSpells), on /deeds on / off and whenever a quest stamp may have moved the QB. Saves
        /// the QB on the character; when the owed tier changed, makes the Deeds buffs exactly that tier's. Cheap when nothing
        /// changed (one cached-count read and an int compare).
        /// </summary>
        public void RefreshDeeds(bool login = false)
        {
            var qb = ReadDeedsQb();
            if (DeedsQb != qb)
                DeedsQb = qb;

            var tier = DeedsEnabled ? DeedsTiers.TierFor(qb) : 0;
            if (tier == DeedsTier && !login && !deedsSyncPending)
            {
                RebakeDeedsSurges();   // the life augs may have moved since (an aug bought, a Triune Weave)
                return;
            }

            var prevTier = DeedsTier;
            DeedsTier = tier;

            // a failed sync must never break the login / quest stamp / command that called this; it stays pending, so the next
            // refresh tries again even when the tier has not moved
            deedsSyncPending = true;
            try
            {
                SyncDeedsEnchantments();
                deedsSyncPending = false;
            }
            catch (Exception ex)
            {
                log.Error($"[Deeds] {Name} (0x{Guid}): could not sync the Deeds buffs to tier {tier}", ex);
                return;
            }

            // (CodeRabbit 10-22: the first tier in a session too - 999 -> 1,000 QB says so; login / on-off stay silent)
            if (!login && DeedsEnabled && tier > prevTier)
                SendMessage($"Your Deeds grow stronger. Your {qb:N0} QB now unlocks {tier} of {DeedsTiers.MaxTier} tiers.", ChatMessageType.Advancement);
        }

        /// <summary>The first layer of a spell that is not the Deeds one - Asheron's Favor shares 4024 / 3811 and dispels its own
        /// layers by spell id; this keeps it off the Deeds layer.</summary>
        public PropertiesEnchantmentRegistry GetEnchantmentNotDeeds(uint spellId)
            => Biota.PropertiesEnchantmentRegistry.Clone(BiotaDatabaseLock).FirstOrDefault(e => e.SpellId == (int)spellId && e.CasterObjectId != DeedsCasterId);

        /// <summary>/deeds on | off: saved, takes effect at once.</summary>
        public void SetDeedsEnabled(bool enabled)
        {
            DeedsEnabled = enabled;
            RefreshDeeds(login: true);   // (login: always sync, no "grows stronger" line)
            SendMessage($"Your Deeds bonuses are {(enabled ? "ON" : "OFF")}. Your {ReadDeedsQb():N0} QB {(enabled ? "unlocks" : "would unlock")} {DeedsTiers.TierFor(ReadDeedsQb())} of {DeedsTiers.MaxTier} tiers.");
        }

        /// <summary>
        /// Makes the player's Deeds enchantments exactly the buff spells of DeedsTier: adds what is missing, dispels what is no
        /// longer owed. Silent - no "cast X on you" line per spell.
        /// </summary>
        private void SyncDeedsEnchantments()
        {
            var want = DeedsTiers.SpellsUpTo(DeedsTier).ToHashSet();

            var mine = Biota.PropertiesEnchantmentRegistry.Clone(BiotaDatabaseLock).Where(IsDeedsEnchantment).ToList();

            var stale = mine.Where(e => !want.Contains((uint)e.SpellId)).ToList();
            if (stale.Count > 0)
            {
                using (BeginOwnRemoval())
                    EnchantmentManager.Dispel(stale);
            }

            var have = mine.Select(e => (uint)e.SpellId).ToHashSet();
            var maxVitalsChanged = false;
            var runRateChanged = false;

            foreach (var spellId in want)
            {
                if (have.Contains(spellId))
                    continue;

                var spell = new Spell(spellId);
                if (spell.NotFound)
                {
                    log.Warn($"[Deeds] spell {spellId} not found in the spell table - skipped.");
                    continue;
                }

                // no caster object: no aug bake, real PowerLevel, its own layer (a null caster never refreshes); equip = true:
                // Duration -1, StartTime 0. Then the reserved Deeds caster id on the new entry.
                var result = EnchantmentManager.Add(spell, null, null, true);
                if (result?.Enchantment == null)
                    continue;
                result.Enchantment.CasterObjectId = DeedsCasterId;
                // T6 surges: the player's own aug bonus, exactly what their own cast of the spell would carry
                if (DeedsTiers.SurgeSpellIds.Contains(spellId))
                    BakeDeedsSurge(result.Enchantment, spell);

                Session?.Network.EnqueueSend(new GameEventMagicUpdateEnchantment(Session, new Enchantment(this, result.Enchantment)));

                maxVitalsChanged |= spell.UpdatesMaxVitals.Count > 0;
                runRateChanged |= spell.UpdatesRunRate;
            }

            RebakeDeedsSurges();   // surges kept from before (a login) take today's augs

            if (maxVitalsChanged)
                SendDeedsMaxVitals();

            if (runRateChanged && ACE.Server.Managers.ServerConfig.runrate_add_hooks.Value)
                HandleRunRateUpdate();

            ChangesDetected = true;
        }

        /// <summary>
        /// T6: each Deeds surge carries the player's CURRENT aug bonus - rebuilt from the spell when the augs moved since it was
        /// baked (AugmentationLevelWhenCast holds the level it was baked at). Cheap: at most three entries.
        /// </summary>
        public void RebakeDeedsSurges()
        {
            if (DeedsTier < DeedsTiers.SurgeTier)
                return;
            foreach (var entry in Biota.PropertiesEnchantmentRegistry.Clone(BiotaDatabaseLock).Where(IsDeedsEnchantment))
            {
                if (!DeedsTiers.SurgeSpellIds.Contains((uint)entry.SpellId))
                    continue;
                var spell = new Spell((uint)entry.SpellId);
                if (spell.NotFound)
                    continue;
                // the base value BuildEntry starts from (an over-time spell per tick at the player's heartbeat), then the bake
                var heartbeat = HeartbeatInterval ?? 5.0;
                var fresh = new PropertiesEnchantmentRegistry { StatModValue = spell.IsDamageOverTime && heartbeat != 5.0 ? spell.GetDamagePerTick((float)heartbeat) : spell.StatModVal };
                BakeDeedsSurge(fresh, spell);
                if (fresh.AugmentationLevelWhenCast == entry.AugmentationLevelWhenCast && fresh.StatModValue == entry.StatModValue)
                    continue;
                // the live registry entry (Clone copies the list, not the entries)
                entry.StatModValue = fresh.StatModValue;
                entry.AugmentationLevelWhenCast = fresh.AugmentationLevelWhenCast;
                ChangesDetected = true;
                Session?.Network.EnqueueSend(new GameEventMagicUpdateEnchantment(Session, new Enchantment(this, entry)));
            }
        }

        /// <summary>
        /// T6: a Deeds surge gets what BuildEntry gives the player's own cast of it - the caster aug bake, then for the over-time
        /// one (Surge of Regeneration, a heal over time) the per-tick aug term too (CalculateDotEnchantment_StatModValue:
        /// + 0.01 x life augs - 30 becomes 70 at 4,000).
        /// </summary>
        private void BakeDeedsSurge(PropertiesEnchantmentRegistry entry, Spell spell)
        {
            EnchantmentManager.ApplyCreatureCasterAugs(entry, spell, this);
            if (spell.IsDamageOverTime)
                entry.StatModValue = CalculateDotEnchantment_StatModValue(spell, this, null, entry.StatModValue);
        }

        /// <summary>T6: an Aetheria proc of a surge the player already has from Deeds does not fire (the always-on one replaces it).</summary>
        public bool HasDeedsSurge(uint spellId) => DeedsTier >= DeedsTiers.SurgeTier && DeedsTiers.SurgeSpellIds.Contains(spellId);

        /// <summary>
        /// /deeds item: a Deedkeeper's Writ (using it = /rebuff) - Deeds ON, 10,000 QB, and only when the player carries none.
        /// </summary>
        public void HandleDeedsItem()
        {
            if (!DeedsEnabled)
            {
                SendMessage("Your Deeds bonuses are OFF. Type /deeds on to turn them on.");
                return;
            }
            if (DeedsTier < DeedsTiers.RebuffTier)
            {
                SendMessage($"The Deedkeeper's Writ unlocks at {DeedsTiers.RebuffTier * DeedsTiers.QbPerTier:N0} QB. You have {ReadDeedsQb():N0}.");
                return;
            }
            if (GetInventoryItemsOfWCID(DeedsTiers.DeedkeepersWritWcid).Count > 0)
            {
                SendMessage("You already have a Deedkeeper's Writ.");
                return;
            }
            var writ = WorldObjectFactory.CreateNewWorldObject(DeedsTiers.DeedkeepersWritWcid);
            if (writ == null)
            {
                SendMessage("The Deedkeeper's Writ is not on this server yet.");
                return;
            }
            if (!TryCreateInInventoryWithNetworking(writ))
            {
                writ.Destroy();
                SendMessage("Your packs are full - make room for the Deedkeeper's Writ and try again.");
                return;
            }
            SendMessage("A Deedkeeper's Writ is in your pack. Use it to cast all your known level 8 buffs at once, like /rebuff.");
        }

        /// <summary>One delayed max-vital refresh for a whole batch (HandleMaxVitalUpdate sends one per spell).</summary>
        private void SendDeedsMaxVitals()
        {
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(1.0f);      // client needs time for primary attribute updates
            actionChain.AddAction(this, ActionType.PlayerSpells_HandleMaxVitalUpdate, () =>
            {
                foreach (var vital in new[] { PropertyAttribute2nd.MaxHealth, PropertyAttribute2nd.MaxStamina, PropertyAttribute2nd.MaxMana })
                {
                    var playerVital = Vitals[vital];
                    Session?.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute2ndLevel(this, playerVital.ToEnum(), playerVital.Current));
                }
            });
            actionChain.EnqueueChain();
        }

        // -- special-effect hooks (each one: Deeds ON and the tier reached, via the cached DeedsTier) --------------

        /// <summary>PvE only: a Deeds combat effect applies against a creature that is not a player.</summary>
        private static bool IsDeedsTarget(WorldObject target) => target is Creature && target is not Player;

        /// <summary>T13: the separate x1.10 damage term for every attack style against monsters (1.0 for anyone else / vs players).</summary>
        public static float GetDeedsDamageMod(WorldObject attacker, WorldObject defender)
            => attacker is Player p && p.DeedsTier >= DeedsTiers.DamageTier && IsDeedsTarget(defender) ? DeedsTiers.DamageMultiplier : 1.0f;

        /// <summary>
        /// T14: +100 pct of the BASE crit chance, ADDED on top of the crit chance from every
        /// other source - the caller passes its own path's base (before Biting Strike / ratings / buffs). 0 for anyone else.
        /// </summary>
        public static float GetDeedsBaseCritBonus(Creature wielder, Creature target, float baseCritChance)
            => wielder is Player p && p.DeedsTier >= DeedsTiers.CritTier && IsDeedsTarget(target) ? baseCritChance : 0.0f;

        /// <summary>T15: the kill's loot is rolled with luck (the killer from the damage history, a pet's owner included).</summary>
        public static bool HasDeedsLootLuck(DamageHistoryInfo killer)
            => killer?.TryGetPetOwnerOrAttacker() is Player p && p.DeedsTier >= DeedsTiers.LootLuckTier;

        /// <summary>T12: the attack target a Breath of Renewal roll is allowed for (see TryDeedsBreathOfRenewal).</summary>
        public bool DeedsBreathTargetOk(WorldObject attackTarget)
            => attackTarget == null ? PlayerKillerStatus == PlayerKillerStatus.NPK : IsDeedsTarget(attackTarget);

        /// <summary>T12: the Breath of Renewal roll alone (ServerConfig.deeds_breath_chance), false below the tier. Separate from any armor-gem proc.</summary>
        public bool RollDeedsBreath()
            => DeedsTier >= DeedsTiers.BreathTier && ThreadSafeRandom.Next(0.0f, 1.0f) < DeedsBreathChance;

        /// <summary>T12: the live proc chance, clamped to 0..1.</summary>
        public static double DeedsBreathChance => Math.Clamp(ACE.Server.Managers.ServerConfig.deeds_breath_chance.Value, 0.0, 1.0);

        /// <summary>
        /// T12: true only while TryDeedsBreathOfRenewal is casting, so HandleCastSpell_Boost scales THIS heal
        /// (deeds_breath_heal_mult, deeds_breath_life_aug_mult) and never the Paragon armor gem's Breath of Renewal.
        /// The cast is synchronous, so the flag never outlives the call.
        /// </summary>
        public bool DeedsBreathCasting { get; private set; }

        /// <summary>
        /// T12: called on each of the player's attacks, next to the self-proc call of melee, missile and spell attacks, with the
        /// attack's target. Monsters only: a player target never rolls; an untargeted cast (a ring) rolls only for a player who
        /// cannot fight players (NPK), since a ring can hit them otherwise.
        /// </summary>
        public void TryDeedsBreathOfRenewal(WorldObject attackTarget)
        {
            if (!DeedsBreathTargetOk(attackTarget) || IsDead || !RollDeedsBreath())
                return;

            var spell = new Spell(DeedsTiers.BreathOfRenewalSpellId);
            if (spell.NotFound)
                return;

            // the player casts it on themself, as a proc (no item behind it)
            DeedsBreathCasting = true;
            try
            {
                TryCastSpell(spell, this, null, null, isWeaponSpell: false, fromProc: true, tryResist: false);
            }
            finally
            {
                DeedsBreathCasting = false;
            }
        }

        private static readonly ConcurrentDictionary<uint, (SpellCategory Category, double Value)> deedsFloorSpells = new();

        private static (SpellCategory Category, double Value) GetDeedsFloorSpell(uint spellId)
            => deedsFloorSpells.GetOrAdd(spellId, id =>
            {
                var spell = new Spell(id);   // with the DB row: StatModVal (the +80 / +0.25) lives there, not in the DAT
                return spell.NotFound ? (SpellCategory.Undef, 0.0) : (spell.Category, spell.StatModVal);
            });

        /// <summary>How far the piece's own enchantments in that spell's category fall short of it (0 when they reach it).</summary>
        private static double GetDeedsFloorDelta(WorldObject armor, uint spellId)
        {
            var (category, value) = GetDeedsFloorSpell(spellId);
            if (category == SpellCategory.Undef || value <= 0)
                return 0.0;

            var have = 0.0;
            foreach (var entry in armor.EnchantmentManager.GetEnchantments(category))
                have = Math.Max(have, entry.StatModValue);

            return Math.Max(0.0, value - have);
        }

        private static bool DeedsArmorFloorApplies(WorldObject armor)
            => armor?.Wielder is Player p && p.DeedsTier >= DeedsTiers.BaneFloorTier
                && (armor.GetProperty(PropertyInt.ArmorLevel) ?? 0) > 0;

        /// <summary>
        /// T9: the Legendary Impenetrability the piece is missing, as armor level - a FLOOR in the math, never an enchantment
        /// on the item (it would travel with traded armor). A piece that already has it (its own Legendary cantrip) gets 0.
        /// </summary>
        public static int GetDeedsImpenFloor(WorldObject armor)
            => DeedsArmorFloorApplies(armor) ? (int)Math.Round(GetDeedsFloorDelta(armor, DeedsTiers.LegendaryImpenetrabilitySpellId)) : 0;

        /// <summary>T9: the Legendary bane for this damage type the piece is missing, as armor-vs-type (see GetDeedsImpenFloor).</summary>
        public static float GetDeedsBaneFloor(WorldObject armor, DamageType damageType)
        {
            if (!DeedsArmorFloorApplies(armor) || !DeedsTiers.LegendaryBaneSpellIds.TryGetValue(damageType, out var spellId))
                return 0.0f;

            return (float)GetDeedsFloorDelta(armor, spellId);
        }

        // -- commands ---------------------------------------------------------------------------------------------

        /// <summary>/rebuff (10k QB): every known level 8 self buff (Shield Mastery too), Impenetrability and the banes on the
        /// equipped armor / shield at once (CastDeedsRebuff). Refused for 120 s after a monster or trap dispelled the player.</summary>
        public void HandleDeedsRebuff()
        {
            if (!DeedsEnabled)
            {
                SendMessage("Your Deeds bonuses are OFF. Type /deeds on to turn them on.");
                return;
            }

            if (DeedsTier < DeedsTiers.RebuffTier)
            {
                SendMessage($"/rebuff unlocks at {DeedsTiers.RebuffTier * DeedsTiers.QbPerTier:N0} QB. You have {ReadDeedsQb():N0}.");
                return;
            }

            var lockoutLeft = DeedsRebuffLockoutRemaining();
            if (lockoutLeft > 0)
            {
                SendMessage($"You were dispelled recently. You can use /rebuff again in {lockoutLeft}s.");
                return;
            }

            CastDeedsRebuff();
        }

        /// <summary>
        /// The /rebuff cast: the highest level installed (8, else 7) of each spell on the buffers' list (Buffs) plus Shield
        /// Mastery - "@" = cast on the equipped items (Impenetrability, the banes) - that the player could cast
        /// themselves - the school trained or specialized, its focus / augment when the school needs one, the spell learned.
        /// Self buffs on the player; Impenetrability and the banes on the EQUIPPED armor and shield only,
        /// never the packs. Normal timed enchantments cast by the player.
        /// </summary>
        private void CastDeedsRebuff()
        {
            var level = DatabaseManager.World.GetCachedSpell((uint)SpellId.ArmorOther8) != null ? 8 : 7;
            var selfSpells = new List<Spell>();
            var gearSpells = new List<Spell>();

            foreach (var entry in Buffs.Append("ShieldMastery"))
            {
                var onGear = entry.StartsWith('@');
                var name = onGear ? entry.Substring(1) : entry;
                SpellId id;
                if (onGear)
                {
                    if (!Enum.TryParse(name + level, out id))
                        continue;
                }
                else if (!Enum.TryParse(name + "Self" + level, out id) && !Enum.TryParse(name + "Other" + level, out id))
                    continue;

                var spell = new Spell(ResolveBuffSpellId(name, (uint)id));
                if (spell.NotFound || !SpellIsKnown(spell.Id))
                    continue;

                var skill = GetCreatureSkill(spell.School);
                if (skill == null || skill.AdvancementClass < SkillAdvancementClass.Trained)
                    continue;
                if (FociWCIDs.ContainsKey(spell.School) && !HasFoci(spell.School))
                    continue;

                (onGear ? gearSpells : selfSpells).Add(spell);
            }

            if (selfSpells.Count == 0 && gearSpells.Count == 0)
            {
                SendMessage("/rebuff: you do not meet the magic requirements (trained school, focus or augment, learned spell) for any of its spells.");
                return;
            }

            foreach (var spell in selfSpells)
                CreateEnchantmentSilent(spell, this);

            var gear = EquippedObjects.Values.Where(i => (i.WeenieType == WeenieType.Clothing || i.IsShield) && i.IsEnchantable).ToList();
            foreach (var spell in gearSpells)
                foreach (var item in gear)
                    CreateEnchantmentSilent(spell, item);

            var effect = selfSpells.Count > 0 ? selfSpells[0] : gearSpells[0];
            EnqueueBroadcast(new GameMessageScript(Guid, effect.TargetEffect, 1f));
            SendMessage("You feel a surge of ultimate blessings flow through you and all your gear!");
        }

        /// <summary>Seconds left on the /rebuff dispel lockout (0 when free): 120 s after a monster or trap dispelled a buff.</summary>
        public int DeedsRebuffLockoutRemaining()
        {
            var elapsed = Time.GetUnixTime() - LastHostileDispelTimestamp;
            return elapsed < DeedsRebuffDispelLockoutSeconds ? (int)Math.Ceiling(DeedsRebuffDispelLockoutSeconds - elapsed) : 0;
        }

        /// <summary>
        /// /deeds and /deeds list: a status line (QB, ON / OFF, next tier), then every tier from DeedsTiers - the same table the
        /// code applies - one line per reward, "(unlocked)" on each tier the QB reaches. ASCII only.
        /// </summary>
        public List<string> BuildDeedsList()
        {
            var qb = ReadDeedsQb();
            var reached = DeedsTiers.TierFor(qb);
            var next = reached < DeedsTiers.MaxTier
                ? $" Next tier at {(reached + 1) * DeedsTiers.QbPerTier:N0} QB."
                : "";

            var lines = new List<string>
            {
                $"Your QB: {qb:N0} - Deeds bonuses are {(DeedsEnabled ? "ON" : "OFF")}, {reached} of {DeedsTiers.MaxTier} tiers unlocked.{next}",
            };

            foreach (var tier in DeedsTiers.Tiers)
            {
                lines.Add($"{tier.Qb} QB{(qb >= tier.Qb ? " (unlocked)" : "")}");

                foreach (var spellId in tier.Spells)
                    lines.Add($" * {DeedsTiers.SpellName(spellId)}");
                foreach (var spellId in tier.ListedSpells)
                    lines.Add($" * {DeedsTiers.SpellName(spellId)}");
                foreach (var line in tier.Lines)
                    lines.Add($" * {line}");
                if (tier.Number == DeedsTiers.BreathTier)
                    lines.Add($" * ({DeedsBreathChance * 100:0.#} percent chance on each of your attacks)");
            }

            return lines;
        }

        /// <summary>Sends BuildDeedsList to the player, one chat message per tier (the status line first).</summary>
        public void ShowDeedsList()
        {
            var block = new List<string>();
            foreach (var line in BuildDeedsList())
            {
                // a tier header starts a new message
                if (block.Count > 0 && !line.StartsWith(" *"))
                {
                    SendMessage(string.Join("\n", block));
                    block.Clear();
                }
                block.Add(line);
            }
            if (block.Count > 0)
                SendMessage(string.Join("\n", block));
        }
    }
}
