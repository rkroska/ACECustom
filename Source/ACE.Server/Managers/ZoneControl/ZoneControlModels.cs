using System;
using System.Collections.Generic;
using ACE.Server.Managers.ZoneScaling;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// One controlled Zone: a named set of landblocks governed at a specific world Variation, with an on/off
    /// toggle and a stat profile. A monster is governed by a zone when it stands on one of the zone's landblocks
    /// AND its variation equals the zone's <see cref="Variation"/> (0 = the normal world; 11+ = variant instances).
    ///
    /// No prestige/tier/boss concepts: the stat payload is a single DEFAULT set applied to every monster in the
    /// zone (the profile's default variant), plus optional per-monster (WCID) overrides.
    /// </summary>
    public class ControlledArea
    {
        /// <summary>Unique key (case-insensitive) — e.g. "tusker_barracks".</summary>
        public string Name { get; set; }

        /// <summary>Member landblocks (a dungeon's landblock, or every block of an overworld region).</summary>
        public HashSet<ushort> Landblocks { get; set; } = new();

        /// <summary>The world variation this zone governs. 0 = the normal (base) world; 11+ = variant instances.</summary>
        public int Variation { get; set; }

        /// <summary>Master switch. Off ⇒ the zone resolves to null (monsters revert to baseline; live stats instantly, HP on respawn).</summary>
        public bool Enabled { get; set; }

        /// <summary>When true (and the zone is Enabled), players at this zone's Variation may only roam the
        /// landblocks of bounded zones at that variation (the union across such zones forms the variation's
        /// player allowlist). Enforced by the boundary punishment loop, guide wisp and perimeter markers.
        /// Only meaningful at variations 11+ (the command refuses retail variations); runtime zones never bound.</summary>
        public bool Bounded { get; set; }

        /// <summary>Zone Share (owner 2026-09-23): everyone standing in the zone shares kill XP, kill luminance and kill-task
        /// credit as if they were all in one fellowship (ZoneShareManager). Only while the zone is Enabled and the Zone
        /// Control master switch is on. Missing on deserialize of older stores = off.</summary>
        public bool ZoneShare { get; set; }

        /// <summary>Kill Reward (owner 2026-09-23): an item every N kills per player, at most once per cooldown. Only while the
        /// zone is Enabled and the Zone Control master switch is on. Missing on deserialize of older stores = off.</summary>
        public KillRewardConfig KillReward { get; set; } = new();

        public string Notes { get; set; }

        /// <summary>Manual terrain overrides for the Territory map, keyed by landblock → terrain tag
        /// (water|beach|obsidian|snow|ice|swamp|grass|dirt|rock). The survey reports this tag instead of the
        /// DAT-derived dominant terrain wherever present. Display-only: terrain drives nothing but map color, so
        /// an admin can re-tag mixed grass/rock/obsidian blocks to whatever reads best for planning generators.</summary>
        public Dictionary<ushort, string> TerrainOverrides { get; set; } = new();

        /// <summary>Stat payload: the default set (profile default variant) for all monsters + per-WCID overrides.</summary>
        public ZoneScalingProfile Profile { get; set; } = new();

        /// <summary>Zone-wide rules applied to PLAYERS standing in the zone (independent of monster stats).</summary>
        public ZoneEffects Effects { get; set; } = new();

        /// <summary>COSMETIC appearance overrides, kept SEPARATE from Profile so they never touch a monster's
        /// stats/abilities. <see cref="AppearanceDefault"/> applies to every governed monster; the
        /// <see cref="AppearanceByWcid"/> entries LAYER on top of it per monster type (non-null fields win).
        /// Missing on deserialize of older stores = empty (backward compatible). See ZoneAppearance.</summary>
        public ZoneAppearance AppearanceDefault { get; set; } = new();
        public Dictionary<uint, ZoneAppearance> AppearanceByWcid { get; set; } = new();

        /// <summary>The cosmetic bucket to edit: the per-WCID overlay (auto-created when <paramref name="create"/>)
        /// or the zone default when <paramref name="wcid"/> is null. Mirrors Profile.VariantForWcid.</summary>
        public ZoneAppearance AppearanceFor(uint? wcid, bool create = false)
        {
            if (!wcid.HasValue) return AppearanceDefault ??= new ZoneAppearance();
            AppearanceByWcid ??= new Dictionary<uint, ZoneAppearance>();
            if (AppearanceByWcid.TryGetValue(wcid.Value, out var ap) && ap != null) return ap;
            if (!create) return null;
            AppearanceByWcid[wcid.Value] = ap = new ZoneAppearance();
            return ap;
        }
    }

    /// <summary>
    /// Kill Reward settings (owner 2026-09-23), for a Zone Control zone (ControlledArea.KillReward) or a room dungeon (its
    /// source weenie's PropertyString.RoomAssignKillReward, via <see cref="Format"/> / <see cref="Parse"/>). Each player's
    /// own kills in the area count; every <see cref="KillRewardEntry.Kills"/> of them earn <see cref="KillRewardEntry.Amount"/>
    /// x <see cref="KillRewardEntry.Wcid"/>, but never more often than once per <see cref="KillRewardEntry.CooldownMinutes"/> -
    /// kills during the cooldown do not count at all (no pre-hunting). See KillRewardManager.
    /// </summary>
    public class KillRewardConfig
    {
        public bool Enabled { get; set; }

        /// <summary>Several rewards per area (owner 2026-09-23: "may use multiple rewards in some zones"). Each counts on its own.</summary>
        public List<KillRewardEntry> Entries { get; set; } = new();

        /// <summary>On, with at least one complete reward - the only state that awards anything.</summary>
        public bool Active => Enabled && Entries != null && Entries.Exists(e => e != null && e.Valid);

        public bool HasItem => Entries != null && Entries.Exists(e => e != null && e.Wcid != 0);

        /// <summary>
        /// The next reward ID (owner 2026-09-23: two admins editing at once). Edits name a reward by its ID, never by its
        /// place in the list, so a row removed by one admin can never turn another admin's edit onto a different row. IDs are
        /// never reused - a stale edit for a removed reward is refused, it cannot land on a new one.
        /// </summary>
        public int NextId { get; set; } = 1;

        /// <summary>Gives every reward without an ID (older data) one, and keeps NextId past them all.</summary>
        public void EnsureIds()
        {
            Entries ??= new List<KillRewardEntry>();
            foreach (var e in Entries)
                if (e != null && e.Id >= NextId) NextId = e.Id + 1;
            foreach (var e in Entries)
                if (e != null && e.Id <= 0) e.Id = NextId++;
        }

        public KillRewardEntry Find(int id) => Entries?.Find(e => e != null && e.Id == id);

        /// <summary>
        /// A copy, made SAFE (review 2026-09-24): every reward within the edit command's bounds, IDs given to rows that have none,
        /// and a duplicated ID moved to a new one - so a zone store edited by hand cannot hand out 2 billion items, or count one
        /// kill twice through two rows sharing a progress key. The kill hook only ever reads copies made here (the zone snapshot,
        /// every edit), and Parse does the same for a dungeon's string.
        /// </summary>
        public KillRewardConfig Clone()
        {
            var copy = new KillRewardConfig
            {
                Enabled = Enabled,
                NextId = NextId,
                Entries = Entries?.ConvertAll(e => e?.Clone()) ?? new List<KillRewardEntry>(),
            };
            copy.Sanitize();
            return copy;
        }

        /// <summary>Clamps every reward to the command's bounds and makes every ID present and unique. See Clone.</summary>
        public void Sanitize()
        {
            Entries ??= new List<KillRewardEntry>();
            Entries.RemoveAll(e => e == null);

            foreach (var e in Entries)
            {
                e.Amount = Math.Clamp(e.Amount, 1, KillRewardManager.MaxAmount);
                e.Kills = Math.Clamp(e.Kills, 1, KillRewardManager.MaxKills);
                e.CooldownMinutes = double.IsFinite(e.CooldownMinutes) ? Math.Clamp(e.CooldownMinutes, 0, KillRewardManager.MaxCooldownMinutes) : 5;
            }

            var seen = new HashSet<int>();
            foreach (var e in Entries)
                if (e.Id > 0 && !seen.Add(e.Id))
                    e.Id = 0;   // a duplicate: EnsureIds gives it a new one

            EnsureIds();
        }

        /// <summary>For a dungeon source's weenie: "on;#nextId;wcid|amount|kills|minutes|id;...".</summary>
        public string Format()
        {
            var sb = new System.Text.StringBuilder(Enabled ? "1" : "0");
            sb.Append(";#").Append(NextId);
            if (Entries != null)
                foreach (var e in Entries)
                    if (e != null) sb.Append(';').Append(e.Format());
            return sb.ToString();
        }

        /// <summary>The dungeon form. Also reads the first build's single "on|wcid|amount|kills|minutes" (2026-09-23, test only).</summary>
        public static KillRewardConfig Parse(string raw)
        {
            var c = new KillRewardConfig();
            if (string.IsNullOrWhiteSpace(raw)) return c;

            if (raw.IndexOf(';') < 0 && raw.Split('|').Length >= 5)
            {
                var f = raw.Split('|');
                c.Enabled = f[0].Trim() == "1";
                var one = KillRewardEntry.Parse(string.Join("|", f, 1, f.Length - 1));
                if (one != null) c.Entries.Add(one);
                c.Sanitize();
                return c;
            }

            var parts = raw.Split(';');
            c.Enabled = parts[0].Trim() == "1";
            for (int i = 1; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                if (part.StartsWith("#"))
                {
                    if (int.TryParse(part.Substring(1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var next) && next > 0)
                        c.NextId = next;
                    continue;
                }
                var e = KillRewardEntry.Parse(part);
                if (e != null) c.Entries.Add(e);
            }
            c.Sanitize();
            return c;
        }
    }

    /// <summary>One Kill Reward: <see cref="Amount"/> x <see cref="Wcid"/> every <see cref="Kills"/> of a player's kills, at most once per <see cref="CooldownMinutes"/>.</summary>
    public class KillRewardEntry
    {
        /// <summary>Permanent within its area (KillRewardConfig.NextId): what edits address. 0 = not given one yet.</summary>
        public int Id { get; set; }

        public uint Wcid { get; set; }
        public int Amount { get; set; } = 1;
        public int Kills { get; set; } = 100;
        public double CooldownMinutes { get; set; } = 5;

        public bool Valid => Wcid != 0 && Amount > 0 && Kills > 0;

        /// <summary>
        /// The cooldown as the kill hook uses it, in seconds: never negative, never past a year, and the 5-minute default
        /// when the stored value is not a number (a zone store is JSON anyone can edit). Review 2026-09-24.
        /// </summary>
        public double CooldownSeconds
            => (double.IsNaN(CooldownMinutes) ? 5 : Math.Clamp(CooldownMinutes, 0, KillRewardManager.MaxCooldownMinutes)) * 60.0;

        public KillRewardEntry Clone() => new KillRewardEntry { Id = Id, Wcid = Wcid, Amount = Amount, Kills = Kills, CooldownMinutes = CooldownMinutes };

        /// <summary>
        /// Its own progress on a character, by its permanent ID (review 2026-09-24): two rows for the same item and count keep
        /// separate counts, an edited row keeps its progress, and a removed-then-re-added row starts fresh. A row from old
        /// data with no ID yet falls back to item and count.
        /// </summary>
        public string ProgressKey => Id > 0 ? "r" + Id : Wcid + "x" + Kills;

        public string Format()
            => Wcid + "|" + Amount + "|" + Kills + "|" + CooldownMinutes.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "|" + Id;

        public static KillRewardEntry Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var f = raw.Split('|');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var e = new KillRewardEntry();
            if (f.Length < 1 || !uint.TryParse(f[0], System.Globalization.NumberStyles.Integer, inv, out var w)) return null;
            e.Wcid = w;
            // The same bounds as the edit command (review 2026-09-24): a hand-edited string cannot store what no admin could type.
            if (f.Length > 1 && int.TryParse(f[1], System.Globalization.NumberStyles.Integer, inv, out var a) && a > 0) e.Amount = Math.Min(a, KillRewardManager.MaxAmount);
            if (f.Length > 2 && int.TryParse(f[2], System.Globalization.NumberStyles.Integer, inv, out var k) && k > 0) e.Kills = Math.Min(k, KillRewardManager.MaxKills);
            if (f.Length > 3 && double.TryParse(f[3], System.Globalization.NumberStyles.Float, inv, out var m) && double.IsFinite(m) && m >= 0) e.CooldownMinutes = Math.Min(m, KillRewardManager.MaxCooldownMinutes);
            if (f.Length > 4 && int.TryParse(f[4], System.Globalization.NumberStyles.Integer, inv, out var id) && id > 0) e.Id = id;
            return e;
        }
    }

    /// <summary>
    /// The DEFAULT layer for one world variation (2026-07-30): the baseline every zone at that variation
    /// inherits. Resolution is <c>VariationDefault -&gt; zone -&gt; wcid</c>, merged PER STAT — a zone that
    /// authors nothing IS its variation's Default, and a zone that authors one stat overrides one stat.
    ///
    /// Progression across v11-v25 is expressed as 15 of these, each explicitly authored. The server never
    /// derives a stat from the variation number (owner ruling: computed scaling is out).
    ///
    /// Runtime zones (rift runs) deliberately do NOT inherit — they live at negative variations and set
    /// their own stats at registration, so a rift can never pick up Tide combat numbers.
    /// </summary>
    public class VariationDefault
    {
        /// <summary>Stat/prop/body-part/list payload — the same shape a zone carries.</summary>
        public ZoneVariantProfile Profile { get; set; } = new();

        /// <summary>Player effects (DoT etc.) inherited by zones at this variation. Nullable per field.</summary>
        public ZoneEffects Effects { get; set; } = new();

        /// <summary>Cosmetic baseline, overlaid by the zone's own appearance then per-WCID.</summary>
        public ZoneAppearance Appearance { get; set; } = new();

        public string Notes { get; set; }

        public bool IsEmpty =>
            (Profile == null || Profile.IsEmpty)
            && (Effects == null || Effects.IsEmpty)
            && (Appearance == null || Appearance.IsEmpty);
    }

    /// <summary>
    /// Per-zone effects applied to PLAYERS inside the zone, evaluated each player heartbeat by
    /// <see cref="ZoneControl.ZoneEffectManager"/>. Only <see cref="DotEnabled"/> is wired today; the
    /// slow/charm fields are reserved placeholders so the wire format + store schema are forward-compatible.
    /// </summary>
    public class ZoneEffects
    {
        // Every field is NULLABLE (2026-07-30 Default layer): null means "not authored at THIS layer", so a
        // variation Default can supply the DoT and an individual zone can override just the damage number
        // without restating type/interval. The Effective* accessors below apply the defaults a caller needs.

        // ── Damage over time ("the floor is lava") ──
        /// <summary>When true, players in the zone take a periodic hit every <see cref="DotIntervalSeconds"/>.</summary>
        public bool? DotEnabled { get; set; }

        /// <summary>Amount applied PER TICK. Flat points normally, or a percent of the player's max health when
        /// <see cref="DotPercent"/> is true (e.g. 5 = 5% of max health per tick).</summary>
        public double? DotDamage { get; set; }

        /// <summary>When true, <see cref="DotDamage"/> is a percent of the player's max health (drains Health).</summary>
        public bool? DotPercent { get; set; }

        /// <summary>Seconds between ticks (min 1). Applied by a per-player timer, independent of the 5s heartbeat.</summary>
        public double? DotIntervalSeconds { get; set; }

        /// <summary>ACE.Entity.Enum.DamageType as int (default Fire = 0x10). Stored as int to keep the model enum-free.
        /// Stamina/Mana drain those pools; Health = "drained"; percent mode forces Health.</summary>
        public int? DotDamageType { get; set; }

        // ── Suppression (regen) ──
        /// <summary>Master switch for the Suppression card: regen suppression for players in the zone.</summary>
        public bool? SuppressEnabled { get; set; }

        /// <summary>When true (the default while suppression is on), the Prodigal regen enchantment line
        /// (Regeneration 3731 / Rejuvenation 3732 / Mana Renewal 3725) is excluded from the player's regen
        /// math — a retail regen buff underneath still applies. Computed per vital tick, never cached.</summary>
        public bool? SuppressProdigal { get; set; }

        /// <summary>Scales players' natural POSITIVE regen ticks (all three vitals). 1 = normal, 0 = no regen.
        /// Never scales a negative (degen) tick — suppression must not become a shield.</summary>
        public double? SuppressRegenMult { get; set; }

        // ── Reserved for later slices (NOT applied yet) ──
        public bool? SlowEnabled { get; set; }
        public double? SlowPercent { get; set; }
        public bool? CharmEnabled { get; set; }

        // ── Effective reads (the defaults that used to be field initializers) ──
        public bool EffectiveDotEnabled => DotEnabled == true;
        public double EffectiveDotDamage => DotDamage ?? 0.0;
        public bool EffectiveDotPercent => DotPercent == true;
        public double EffectiveDotIntervalSeconds => DotIntervalSeconds ?? 5.0;
        public int EffectiveDotDamageType => DotDamageType ?? 0x10;

        public bool EffectiveSuppressEnabled => SuppressEnabled == true;
        public bool EffectiveSuppressProdigal => SuppressProdigal ?? true;
        public double EffectiveSuppressRegenMult => System.Math.Clamp(SuppressRegenMult ?? 1.0, 0.0, 1.0);

        /// <summary>True if any effect is active — used to skip zones that author no effects during resolution.</summary>
        public bool AnyActive => DotEnabled == true || SuppressEnabled == true || SlowEnabled == true || CharmEnabled == true;

        /// <summary>True when nothing at all is authored at this layer.</summary>
        public bool IsEmpty =>
            DotEnabled == null && DotDamage == null && DotPercent == null && DotIntervalSeconds == null
            && DotDamageType == null && SuppressEnabled == null && SuppressProdigal == null
            && SuppressRegenMult == null && SlowEnabled == null && SlowPercent == null && CharmEnabled == null;

        public ZoneEffects Clone() => new ZoneEffects
        {
            DotEnabled = DotEnabled, DotDamage = DotDamage, DotPercent = DotPercent,
            DotIntervalSeconds = DotIntervalSeconds, DotDamageType = DotDamageType,
            SuppressEnabled = SuppressEnabled, SuppressProdigal = SuppressProdigal,
            SuppressRegenMult = SuppressRegenMult,
            SlowEnabled = SlowEnabled, SlowPercent = SlowPercent, CharmEnabled = CharmEnabled,
        };

        /// <summary>Per-FIELD layered merge: any authored (non-null) field on <paramref name="upper"/> wins,
        /// everything else falls through to <paramref name="lower"/>. Returns a new instance.</summary>
        public static ZoneEffects Merge(ZoneEffects lower, ZoneEffects upper)
        {
            if (lower == null) return upper?.Clone() ?? new ZoneEffects();
            if (upper == null) return lower.Clone();
            return new ZoneEffects
            {
                DotEnabled = upper.DotEnabled ?? lower.DotEnabled,
                DotDamage = upper.DotDamage ?? lower.DotDamage,
                DotPercent = upper.DotPercent ?? lower.DotPercent,
                DotIntervalSeconds = upper.DotIntervalSeconds ?? lower.DotIntervalSeconds,
                DotDamageType = upper.DotDamageType ?? lower.DotDamageType,
                SuppressEnabled = upper.SuppressEnabled ?? lower.SuppressEnabled,
                SuppressProdigal = upper.SuppressProdigal ?? lower.SuppressProdigal,
                SuppressRegenMult = upper.SuppressRegenMult ?? lower.SuppressRegenMult,
                SlowEnabled = upper.SlowEnabled ?? lower.SlowEnabled,
                SlowPercent = upper.SlowPercent ?? lower.SlowPercent,
                CharmEnabled = upper.CharmEnabled ?? lower.CharmEnabled,
            };
        }
    }
}
