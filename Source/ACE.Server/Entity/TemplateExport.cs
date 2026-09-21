using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;

using Weenie = ACE.Database.Models.World.Weenie;

namespace ACE.Server.Entity
{
    /// <summary>
    /// The pure half of @export-template / @et / @ed template: turns a snapshot of a live world object into a
    /// ready-to-load weenie (ACE.Database.Models.World.Weenie) that the existing WeenieSQLWriter serialises,
    /// exactly like @export-sql does for a database weenie.
    ///
    /// Nothing in here touches the world, the database or a session, so every rule is unit-testable
    /// (ACE.Server.Tests/TemplateExportTests.cs). DeveloperContentCommands.ExportTemplate does the world-side
    /// work: picking the target, snapshotting it on the thread that owns it, allocating the wcid, writing the file.
    ///
    /// What is captured: identity (type, name, creature type, level), the appearance as RENDERED (the object's
    /// CalculateObjDesc result: anim parts, sub palettes, texture changes, palette id) plus setup / motion / sound /
    /// combat / physics tables, clothing base, palette template, shade and scale, the property bags, attributes,
    /// vitals, skills, body parts, spell book, emotes, create list and held (wielded) items.
    ///
    /// What is deliberately dropped: instance links (every PropertyInstanceId: owner, container, wielder, generator,
    /// pet owner / device ...), instance positions (Location, Home, Sanctuary ...), timestamps, generator rows,
    /// allegiance / enchantment / house data, and the pet bookkeeping families (PetBond*, PetMut*, PetMaturity*,
    /// PetPotency*, Captured*, VisualOverride*, PetIsMaleOverride ...). A player source is reduced to an appearance
    /// whitelist so no account or character state can leak into a template.
    /// </summary>
    public static class TemplateExport
    {
        public enum Flavour
        {
            /// <summary>Faithful copy: attackable, keeps combat stats, loot-free. The default.</summary>
            Monster,
            /// <summary>Same look, NPC behaviour mirrored from the Ruggan's Annex Ivo block.</summary>
            Npc
        }

        // ------------------------------------------------------------------------------------------------
        // Arguments
        // ------------------------------------------------------------------------------------------------

        public sealed class Arguments
        {
            public uint? ExplicitWcid { get; set; }
            /// <summary>Null when no flavour keyword was typed; the caller then picks the default for the source.</summary>
            public Flavour? Flavour { get; set; }
            public bool Overwrite { get; set; }
            /// <summary>Null when no name was typed; the live object's own name is used.</summary>
            public string NameOverride { get; set; }
        }

        /// <summary>
        /// Order-insensitive, forgiving parse: the first bare number (decimal or 0x hex) is the wcid, the words
        /// npc / monster / overwrite are keywords, the literal word template (from "@ed template ...") is ignored,
        /// and everything else is the name override joined with spaces. Nothing is rejected on ordering grounds.
        /// </summary>
        public static Arguments ParseArguments(IEnumerable<string> tokens)
        {
            var args = new Arguments();
            var nameTokens = new List<string>();

            foreach (var raw in tokens ?? Enumerable.Empty<string>())
            {
                var token = raw?.Trim();
                if (string.IsNullOrEmpty(token))
                    continue;

                var lower = token.ToLowerInvariant();

                if (lower == "template")
                    continue;

                if (lower == "npc") { args.Flavour = TemplateExport.Flavour.Npc; continue; }
                if (lower == "monster") { args.Flavour = TemplateExport.Flavour.Monster; continue; }
                if (lower == "overwrite") { args.Overwrite = true; continue; }

                if (args.ExplicitWcid == null && TryParseWcid(token, out var wcid))
                {
                    args.ExplicitWcid = wcid;
                    continue;
                }

                nameTokens.Add(token);
            }

            args.NameOverride = nameTokens.Count > 0 ? string.Join(" ", nameTokens) : null;
            return args;
        }

        public static bool TryParseWcid(string token, out uint wcid)
        {
            wcid = 0;
            if (string.IsNullOrEmpty(token))
                return false;

            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(token.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out wcid) && wcid > 0;

            return uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out wcid) && wcid > 0;
        }

        /// <summary>
        /// Removes every "force" token from an import command's parameters and reports whether one was present.
        /// </summary>
        public static string[] StripForceKeyword(string[] parameters, out bool force)
        {
            force = false;
            if (parameters == null)
                return Array.Empty<string>();

            var kept = new List<string>(parameters.Length);
            foreach (var p in parameters)
            {
                if (p != null && p.Equals("force", StringComparison.OrdinalIgnoreCase))
                    force = true;
                else
                    kept.Add(p);
            }
            return kept.ToArray();
        }

        // ------------------------------------------------------------------------------------------------
        // WCID allocation
        // ------------------------------------------------------------------------------------------------

        public sealed class WcidChoice
        {
            public uint Wcid { get; set; }
            public bool Explicit { get; set; }
            /// <summary>An explicit id that is not inside the export block (allowed, but warned about).</summary>
            public bool OutsideBlock { get; set; }
            public bool Refused { get; set; }
            public string RefusalReason { get; set; }
            /// <summary>Name of the weenie already at an explicit id (refusal text, or the overwrite notice).</summary>
            public string ExistingName { get; set; }
            public bool ReplacesExisting { get; set; }
            /// <summary>Value to persist in content_template_export_next_wcid; unchanged when not higher.</summary>
            public long NextHighWaterMark { get; set; }
            /// <summary>How much of the block is consumed once this id is handed out (0 when outside the block).</summary>
            public double BlockUsedFraction { get; set; }
        }

        public const double BlockUsageWarningFraction = 0.8;

        /// <summary>
        /// Picks the wcid for an export.
        /// Explicit id: refused when it already exists unless overwrite was passed; allowed outside the block
        /// with a warning. Automatic: the lowest free id in [blockStart, blockEnd] that is at or above the persisted
        /// high-water mark, skipping every id present in <paramref name="existingWeenies"/>. The block is never
        /// wrapped: when it is used up the choice is refused and names the property to extend.
        /// </summary>
        public static WcidChoice ChooseWcid(uint? explicitWcid, bool overwrite, uint blockStart, uint blockEnd, long highWaterMark, IReadOnlyDictionary<uint, string> existingWeenies)
        {
            existingWeenies ??= new Dictionary<uint, string>();
            var choice = new WcidChoice { NextHighWaterMark = highWaterMark };

            if (blockEnd < blockStart)
            {
                choice.Refused = true;
                choice.RefusalReason = $"Export block is misconfigured: content_template_export_wcid_start ({blockStart}) is above content_template_export_wcid_end ({blockEnd}). Fix them with @modifylong before exporting.";
                return choice;
            }

            var blockSize = (double)(blockEnd - blockStart + 1);

            if (explicitWcid.HasValue)
            {
                var wcid = explicitWcid.Value;
                choice.Wcid = wcid;
                choice.Explicit = true;
                choice.OutsideBlock = wcid < blockStart || wcid > blockEnd;

                if (existingWeenies.TryGetValue(wcid, out var existingName))
                {
                    choice.ExistingName = existingName ?? "";
                    if (!overwrite)
                    {
                        choice.Refused = true;
                        choice.RefusalReason = $"Refused: wcid {wcid} already exists in the world database as '{choice.ExistingName}'. Pick another id, leave the id out to auto-allocate, or add the word overwrite to replace it.";
                        return choice;
                    }
                    choice.ReplacesExisting = true;
                }

                if (!choice.OutsideBlock)
                {
                    choice.BlockUsedFraction = (wcid - blockStart + 1) / blockSize;
                    // an explicit id inside the block moves the mark past it so a later auto pick cannot hand
                    // out the same id before this file has been loaded
                    if ((long)wcid + 1 > highWaterMark)
                        choice.NextHighWaterMark = (long)wcid + 1;
                }
                return choice;
            }

            // automatic: max(high-water mark, block start), then walk past anything the database already has
            long candidate = blockStart;
            if (highWaterMark > candidate)
                candidate = highWaterMark;

            while (candidate <= blockEnd && existingWeenies.ContainsKey((uint)candidate))
                candidate++;

            if (candidate > blockEnd)
            {
                choice.Refused = true;
                choice.RefusalReason = $"Refused: the export block {blockStart}-{blockEnd} is used up (next id would be {candidate}). The tool never wraps around. Raise content_template_export_wcid_end (or move the block with content_template_export_wcid_start / _end and reset content_template_export_next_wcid) with @modifylong.";
                return choice;
            }

            choice.Wcid = (uint)candidate;
            choice.NextHighWaterMark = candidate + 1;
            choice.BlockUsedFraction = (candidate - blockStart + 1) / blockSize;
            return choice;
        }

        public static bool IsInExportBlock(uint wcid, uint blockStart, uint blockEnd)
        {
            return blockStart <= blockEnd && wcid >= blockStart && wcid <= blockEnd;
        }

        /// <summary>The exact refusal an import command prints for a wcid inside the export block.</summary>
        public static string ImportRefusal(uint wcid, uint blockStart, uint blockEnd)
        {
            return $"Refused: wcid {wcid} is inside the temporary export staging block {blockStart}-{blockEnd}. Exports land there so they can be spawned and iterated, but nothing should be imported there as real content. Renumber the weenie into your own hand-authored range (7878 prefix) and import that, or add the word force to import it anyway.";
        }

        public static uint ClampToUint(long value)
        {
            if (value < 0) return 0;
            if (value > uint.MaxValue) return uint.MaxValue;
            return (uint)value;
        }

        // ------------------------------------------------------------------------------------------------
        // Names
        // ------------------------------------------------------------------------------------------------

        /// <summary>weenie.class_Name is varchar(100).</summary>
        public const int MaxClassNameLength = 100;

        public const string ClassNamePrefix = "tmpl";

        /// <summary>Override when given, else the live name, else a placeholder; always 7-bit ASCII.</summary>
        public static string ChooseName(string liveName, string nameOverride)
        {
            var chosen = !string.IsNullOrWhiteSpace(nameOverride) ? nameOverride : liveName;
            chosen = ToAscii(chosen ?? "").Trim();
            return chosen.Length > 0 ? chosen : "Exported Object";
        }

        /// <summary>
        /// The stable class name this tool generates: tmpl{wcid}_{slug}. The slug is lowercase with every
        /// non-alphanumeric run collapsed to a single underscore, truncated so the whole thing fits the column.
        /// It contains no quotes, so it is safe inside the SQL the writer emits without escaping.
        /// </summary>
        public static string BuildClassName(uint wcid, string name)
        {
            var prefix = $"{ClassNamePrefix}{wcid}_";
            var slug = Slugify(name);

            var room = MaxClassNameLength - prefix.Length;
            if (slug.Length > room)
                slug = slug.Substring(0, room).TrimEnd('_');

            if (slug.Length == 0)
                slug = "object";

            return prefix + slug;
        }

        public static string Slugify(string name)
        {
            var sb = new StringBuilder();
            var pendingUnderscore = false;

            foreach (var c in (name ?? "").ToLowerInvariant())
            {
                var ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (ok)
                {
                    if (pendingUnderscore && sb.Length > 0)
                        sb.Append('_');
                    pendingUnderscore = false;
                    sb.Append(c);
                }
                else
                    pendingUnderscore = true;
            }

            return sb.ToString();
        }

        /// <summary>Drops anything outside printable 7-bit ASCII (the client cannot draw it).</summary>
        public static string ToAscii(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (c >= 0x20 && c <= 0x7E)
                    sb.Append(c);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------------------------------------
        // Snapshot of the live object (filled by the command on the owning thread)
        // ------------------------------------------------------------------------------------------------

        /// <summary>One held item on the source, exported as a create-list Wield row.</summary>
        public sealed record WieldedItem(uint Wcid, string Name, int PaletteTemplate, float Shade);

        /// <summary>
        /// What the command reads off the live object. The biota collections are read while the caller holds the
        /// object's BiotaDatabaseLock; the ObjDesc lists are the object's own CalculateObjDesc() result and the
        /// wielded list is the held-slot equipment (EquipMask.Selectable), both plain copies.
        /// </summary>
        public sealed class Snapshot
        {
            public Biota Biota { get; set; }
            public string LiveName { get; set; }
            public bool IsPlayer { get; set; }
            /// <summary>Pet / CombatPet: the held weapons are capture skins, see wield handling.</summary>
            public bool IsPet { get; set; }
            public bool IsCreature { get; set; }
            public uint ObjDescPaletteId { get; set; }
            public IReadOnlyList<PropertiesAnimPart> AnimParts { get; set; } = Array.Empty<PropertiesAnimPart>();
            public IReadOnlyList<PropertiesPalette> SubPalettes { get; set; } = Array.Empty<PropertiesPalette>();
            public IReadOnlyList<PropertiesTextureMap> TextureChanges { get; set; } = Array.Empty<PropertiesTextureMap>();
            public IReadOnlyList<WieldedItem> Wielded { get; set; } = Array.Empty<WieldedItem>();
        }

        /// <summary>Players default to npc (a copy that fights back is almost never wanted); everything else to monster.</summary>
        public static Flavour DefaultFlavour(Snapshot snapshot)
        {
            return snapshot != null && snapshot.IsPlayer ? Flavour.Npc : Flavour.Monster;
        }

        // ------------------------------------------------------------------------------------------------
        // Property filtering
        // ------------------------------------------------------------------------------------------------

        private static readonly string[] BookkeepingPrefixes =
        {
            "Pet",                  // PetBond*, PetMut*, PetMaturity*, PetPotency*, PetMale*, PetIsMaleOverride, PetIsJuvenile, PetNeutered, PetDevice*, PetClass ...
            "Captured",             // Captured* (essence-side copies of the look)
            "CaptureCreatureType",
            "VisualOverride",       // device-side overrides that were already applied to the live look
            "FailedShinyCapture",
            "IsCapturedAppearance",
            "CreatedByAccountId",
            "PCAPRecorded",
        };

        /// <summary>Instance state or pet bookkeeping, decided by the enum member name so new members of a family are caught.</summary>
        public static bool IsBookkeepingName(string enumName)
        {
            if (string.IsNullOrEmpty(enumName))
                return false;

            foreach (var prefix in BookkeepingPrefixes)
            {
                if (enumName.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return enumName.EndsWith("Timestamp", StringComparison.Ordinal);
        }

        // Player sources are reduced to a whitelist: appearance and identity only.
        private static readonly HashSet<PropertyInt> PlayerKeepInts = new()
        {
            PropertyInt.ItemType, PropertyInt.CreatureType, PropertyInt.Level, PropertyInt.HeritageGroup, PropertyInt.Gender,
            PropertyInt.PaletteTemplate, PropertyInt.PhysicsState, PropertyInt.CreatureVariant,
        };

        private static readonly HashSet<PropertyFloat> PlayerKeepFloats = new()
        {
            PropertyFloat.DefaultScale, PropertyFloat.Shade, PropertyFloat.Translucency,
        };

        private static readonly HashSet<PropertyDataId> PlayerKeepDids = new()
        {
            PropertyDataId.Setup, PropertyDataId.MotionTable, PropertyDataId.SoundTable, PropertyDataId.CombatTable,
            PropertyDataId.PhysicsEffectTable, PropertyDataId.PhysicsScript, PropertyDataId.PaletteBase, PropertyDataId.ClothingBase, PropertyDataId.Icon,
        };

        public static bool KeepInt(PropertyInt p, bool isPlayer)
        {
            if (isPlayer)
                return PlayerKeepInts.Contains(p);

            if (p == PropertyInt.PlacementPosition || p == PropertyInt.CurrentWieldedLocation)
                return false;

            return !IsBookkeepingName(p.ToString());
        }

        public static bool KeepInt64(PropertyInt64 p, bool isPlayer)
        {
            if (isPlayer)
                return false;
            return !IsBookkeepingName(p.ToString());
        }

        public static bool KeepBool(PropertyBool p, bool isPlayer)
        {
            if (isPlayer)
                return false;
            return !IsBookkeepingName(p.ToString());
        }

        public static bool KeepFloat(PropertyFloat p, bool isPlayer)
        {
            if (isPlayer)
                return PlayerKeepFloats.Contains(p);
            return !IsBookkeepingName(p.ToString());
        }

        public static bool KeepString(PropertyString p, bool isPlayer)
        {
            if (p == PropertyString.Name)
                return false; // always replaced by the chosen name
            if (isPlayer)
                return false;
            return !IsBookkeepingName(p.ToString());
        }

        public static bool KeepDid(PropertyDataId p, bool isPlayer)
        {
            if (isPlayer)
                return PlayerKeepDids.Contains(p);
            return !IsBookkeepingName(p.ToString());
        }

        /// <summary>Template-level positions only; Location, Home, Sanctuary and the rest are per instance.</summary>
        public static bool KeepPosition(PositionType p, bool isPlayer)
        {
            if (isPlayer)
                return false;
            return p == PositionType.Destination || p == PositionType.LinkedPortalOne || p == PositionType.LinkedPortalTwo || p == PositionType.LinkedLifestone;
        }

        public static WeenieType MapWeenieType(WeenieType source, bool isPlayer)
        {
            if (isPlayer)
                return WeenieType.Creature;

            switch (source)
            {
                case WeenieType.Pet:
                case WeenieType.CombatPet:
                    return WeenieType.Creature;
                default:
                    return source;
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Building the weenie
        // ------------------------------------------------------------------------------------------------

        public sealed class CaptureSummary
        {
            public uint? Setup { get; set; }
            public uint? ClothingBase { get; set; }
            public uint? PaletteBase { get; set; }
            public int? PaletteTemplate { get; set; }
            public double? Shade { get; set; }
            public double? Scale { get; set; }
            public int? CreatureVariant { get; set; }
            public int AnimParts { get; set; }
            public int TextureMaps { get; set; }
            public int Palettes { get; set; }
            public int BodyParts { get; set; }
            public int Attributes { get; set; }
            public int Vitals { get; set; }
            public int Skills { get; set; }
            public int Spells { get; set; }
            public int Emotes { get; set; }
            public int CreateListRows { get; set; }
            public int WieldedItems { get; set; }
            /// <summary>Held items were present but not exported (monster flavour of a pet).</summary>
            public bool WieldSkippedForPet { get; set; }
            /// <summary>The flavour keyword had nothing to act on (not a creature).</summary>
            public bool FlavourIgnored { get; set; }

            public string ToLine()
            {
                var parts = new List<string>();
                if (Setup.HasValue) parts.Add($"setup 0x{Setup.Value:X8}");
                if (ClothingBase.HasValue) parts.Add($"clothingbase 0x{ClothingBase.Value:X8}");
                if (PaletteBase.HasValue) parts.Add($"palette base 0x{PaletteBase.Value:X8}");
                if (PaletteTemplate.HasValue) parts.Add($"palette template {PaletteTemplate.Value}");
                if (Shade.HasValue) parts.Add($"shade {Shade.Value.ToString("0.00", CultureInfo.InvariantCulture)}");
                if (Scale.HasValue) parts.Add($"scale {Scale.Value.ToString("0.00", CultureInfo.InvariantCulture)}");
                if (CreatureVariant.HasValue) parts.Add($"variant {CreatureVariant.Value}");
                parts.Add($"{AnimParts} anim parts");
                parts.Add($"{TextureMaps} texture maps");
                parts.Add($"{Palettes} palettes");
                if (BodyParts > 0) parts.Add($"{BodyParts} body parts");
                if (Attributes > 0) parts.Add($"{Attributes} attributes");
                if (Vitals > 0) parts.Add($"{Vitals} vitals");
                if (Skills > 0) parts.Add($"{Skills} skills");
                if (Spells > 0) parts.Add($"{Spells} spells");
                if (Emotes > 0) parts.Add($"{Emotes} emote sets");
                if (CreateListRows > 0) parts.Add($"{CreateListRows} create list rows");
                if (WieldedItems > 0) parts.Add($"{WieldedItems} wielded items");
                return string.Join(", ", parts);
            }
        }

        /// <summary>
        /// Builds the database weenie for the snapshot. The caller holds the source's BiotaDatabaseLock (read) while
        /// this runs, because the biota dictionaries are read directly.
        /// </summary>
        public static Weenie BuildWeenie(Snapshot s, uint newWcid, string className, string name, Flavour flavour, DateTime lastModifiedUtc, out CaptureSummary summary)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.Biota == null) throw new ArgumentException("Snapshot has no biota", nameof(s));

            var biota = s.Biota;
            var isPlayer = s.IsPlayer;
            summary = new CaptureSummary();

            var w = new Weenie
            {
                ClassId = newWcid,
                ClassName = className,
                Type = (int)MapWeenieType(biota.WeenieType, isPlayer),
                LastModified = lastModifiedUtc,
            };

            // --- scalar property bags ---------------------------------------------------------------
            if (biota.PropertiesInt != null)
                foreach (var kvp in biota.PropertiesInt)
                    if (KeepInt(kvp.Key, isPlayer))
                        SetInt(w, kvp.Key, kvp.Value);

            if (biota.PropertiesInt64 != null)
                foreach (var kvp in biota.PropertiesInt64)
                    if (KeepInt64(kvp.Key, isPlayer))
                        w.WeeniePropertiesInt64.Add(new WeeniePropertiesInt64 { ObjectId = newWcid, Type = (ushort)kvp.Key, Value = kvp.Value });

            if (biota.PropertiesBool != null)
                foreach (var kvp in biota.PropertiesBool)
                    if (KeepBool(kvp.Key, isPlayer))
                        SetBool(w, kvp.Key, kvp.Value);

            if (biota.PropertiesFloat != null)
                foreach (var kvp in biota.PropertiesFloat)
                    if (KeepFloat(kvp.Key, isPlayer))
                        w.WeeniePropertiesFloat.Add(new WeeniePropertiesFloat { ObjectId = newWcid, Type = (ushort)kvp.Key, Value = kvp.Value });

            if (biota.PropertiesString != null)
                foreach (var kvp in biota.PropertiesString)
                    if (KeepString(kvp.Key, isPlayer) && kvp.Value != null)
                        w.WeeniePropertiesString.Add(new WeeniePropertiesString { ObjectId = newWcid, Type = (ushort)kvp.Key, Value = ToAscii(kvp.Value) });

            w.WeeniePropertiesString.Add(new WeeniePropertiesString { ObjectId = newWcid, Type = (ushort)PropertyString.Name, Value = name });

            if (biota.PropertiesDID != null)
                foreach (var kvp in biota.PropertiesDID)
                    if (KeepDid(kvp.Key, isPlayer))
                        SetDid(w, kvp.Key, kvp.Value);

            // PropertyInstanceId: every one is a link to another live object, none belong in a template.

            if (biota.PropertiesPosition != null)
            {
                foreach (var kvp in biota.PropertiesPosition)
                {
                    if (!KeepPosition(kvp.Key, isPlayer) || kvp.Value == null)
                        continue;

                    var p = kvp.Value;
                    w.WeeniePropertiesPosition.Add(new WeeniePropertiesPosition
                    {
                        ObjectId = newWcid,
                        PositionType = (ushort)kvp.Key,
                        ObjCellId = p.ObjCellId,
                        OriginX = p.PositionX, OriginY = p.PositionY, OriginZ = p.PositionZ,
                        AnglesW = p.RotationW, AnglesX = p.RotationX, AnglesY = p.RotationY, AnglesZ = p.RotationZ,
                    });
                }
            }

            // --- appearance, as rendered ------------------------------------------------------------
            if (s.ObjDescPaletteId != 0)
                SetDid(w, PropertyDataId.PaletteBase, s.ObjDescPaletteId);

            foreach (var ap in s.AnimParts ?? Array.Empty<PropertiesAnimPart>())
                w.WeeniePropertiesAnimPart.Add(new WeeniePropertiesAnimPart { ObjectId = newWcid, Index = ap.Index, AnimationId = ap.AnimationId });

            foreach (var pal in s.SubPalettes ?? Array.Empty<PropertiesPalette>())
                w.WeeniePropertiesPalette.Add(new WeeniePropertiesPalette { ObjectId = newWcid, SubPaletteId = pal.SubPaletteId, Offset = pal.Offset, Length = pal.Length });

            foreach (var tm in s.TextureChanges ?? Array.Empty<PropertiesTextureMap>())
                w.WeeniePropertiesTextureMap.Add(new WeeniePropertiesTextureMap { ObjectId = newWcid, Index = tm.PartIndex, OldId = tm.OldTexture, NewId = tm.NewTexture });

            // --- creature tables --------------------------------------------------------------------
            // Raised levels are folded into InitLevel: a template has no CP/PP history, and for a monster source
            // LevelFromCP / LevelFromPP are already 0 so this is a no-op.
            if (biota.PropertiesAttribute != null)
            {
                foreach (var kvp in biota.PropertiesAttribute)
                {
                    var a = kvp.Value;
                    w.WeeniePropertiesAttribute.Add(new WeeniePropertiesAttribute
                    {
                        ObjectId = newWcid, Type = (ushort)kvp.Key,
                        InitLevel = a.InitLevel + a.LevelFromCP, LevelFromCP = 0, CPSpent = 0,
                    });
                }
            }

            if (biota.PropertiesAttribute2nd != null)
            {
                foreach (var kvp in biota.PropertiesAttribute2nd)
                {
                    var v = kvp.Value;
                    // CurrentLevel is the live pool (a wounded pet exports wounded); creatures spawn at full
                    // vitals anyway (Creature.SetEphemeralValues), so it is written as 0 like retail rows.
                    w.WeeniePropertiesAttribute2nd.Add(new WeeniePropertiesAttribute2nd
                    {
                        ObjectId = newWcid, Type = (ushort)kvp.Key,
                        InitLevel = v.InitLevel + v.LevelFromCP, LevelFromCP = 0, CPSpent = 0, CurrentLevel = 0,
                    });
                }
            }

            if (biota.PropertiesSkill != null)
            {
                foreach (var kvp in biota.PropertiesSkill)
                {
                    var sk = kvp.Value;
                    if (isPlayer && sk.SAC != SkillAdvancementClass.Trained && sk.SAC != SkillAdvancementClass.Specialized)
                        continue; // a player's untrained rows carry nothing a creature needs

                    w.WeeniePropertiesSkill.Add(new WeeniePropertiesSkill
                    {
                        ObjectId = newWcid, Type = (ushort)kvp.Key,
                        InitLevel = sk.InitLevel + sk.LevelFromPP, LevelFromPP = 0, PP = 0,
                        SAC = (uint)sk.SAC, ResistanceAtLastCheck = 0, LastUsedTime = 0,
                    });
                }
            }

            if (biota.PropertiesBodyPart != null)
            {
                foreach (var kvp in biota.PropertiesBodyPart)
                {
                    var bp = kvp.Value;
                    w.WeeniePropertiesBodyPart.Add(new WeeniePropertiesBodyPart
                    {
                        ObjectId = newWcid, Key = (ushort)kvp.Key,
                        DType = (int)bp.DType, DVal = bp.DVal, DVar = bp.DVar, BaseArmor = bp.BaseArmor,
                        ArmorVsSlash = bp.ArmorVsSlash, ArmorVsPierce = bp.ArmorVsPierce, ArmorVsBludgeon = bp.ArmorVsBludgeon,
                        ArmorVsCold = bp.ArmorVsCold, ArmorVsFire = bp.ArmorVsFire, ArmorVsAcid = bp.ArmorVsAcid,
                        ArmorVsElectric = bp.ArmorVsElectric, ArmorVsNether = bp.ArmorVsNether,
                        BH = bp.BH, HLF = bp.HLF, MLF = bp.MLF, LLF = bp.LLF, HRF = bp.HRF, MRF = bp.MRF, LRF = bp.LRF,
                        HLB = bp.HLB, MLB = bp.MLB, LLB = bp.LLB, HRB = bp.HRB, MRB = bp.MRB, LRB = bp.LRB,
                    });
                }
            }

            // A player's spell book is everything they know; a creature casts from its book, so it is not copied.
            if (!isPlayer && biota.PropertiesSpellBook != null)
                foreach (var kvp in biota.PropertiesSpellBook)
                    w.WeeniePropertiesSpellBook.Add(new WeeniePropertiesSpellBook { ObjectId = newWcid, Spell = kvp.Key, Probability = kvp.Value });

            if (!isPlayer && biota.PropertiesEmote != null)
                foreach (var emote in biota.PropertiesEmote)
                    w.WeeniePropertiesEmote.Add(CopyEmote(newWcid, emote));

            if (!isPlayer && biota.PropertiesEventFilter != null)
                foreach (var ev in biota.PropertiesEventFilter)
                    w.WeeniePropertiesEventFilter.Add(new WeeniePropertiesEventFilter { ObjectId = newWcid, Event = ev });

            if (!isPlayer && biota.PropertiesCreateList != null)
            {
                foreach (var c in biota.PropertiesCreateList)
                {
                    w.WeeniePropertiesCreateList.Add(new WeeniePropertiesCreateList
                    {
                        ObjectId = newWcid, DestinationType = (sbyte)c.DestinationType, WeenieClassId = c.WeenieClassId,
                        StackSize = c.StackSize, Palette = c.Palette, Shade = c.Shade, TryToBond = c.TryToBond,
                    });
                }
            }

            // Generator rows are instance wiring for spawners and are not copied.

            if (biota.PropertiesBook != null)
                w.WeeniePropertiesBook = new WeeniePropertiesBook { ObjectId = newWcid, MaxNumPages = biota.PropertiesBook.MaxNumPages, MaxNumCharsPerPage = biota.PropertiesBook.MaxNumCharsPerPage };

            if (biota.PropertiesBookPageData != null)
            {
                uint pageId = 0;
                foreach (var page in biota.PropertiesBookPageData)
                {
                    w.WeeniePropertiesBookPageData.Add(new WeeniePropertiesBookPageData
                    {
                        ObjectId = newWcid, PageId = pageId++,
                        AuthorId = 0, AuthorName = ToAscii(page.AuthorName ?? ""), AuthorAccount = "", IgnoreAuthor = page.IgnoreAuthor,
                        PageText = ToAscii(page.PageText ?? ""),
                    });
                }
            }

            // --- held items ---------------------------------------------------------------------------
            // A held weapon changes how a creature fights (weapon damage instead of body parts). A pet's held
            // weapons are capture skins whose damage is neutralised at runtime only, so for the monster flavour of
            // a pet they are left out; the npc flavour, and every non-pet source, gets one Wield row per held item
            // and drops any Wield rows the biota already had (the live set replaces them).
            var wielded = s.Wielded ?? Array.Empty<WieldedItem>();
            if (wielded.Count > 0)
            {
                if (flavour == Flavour.Monster && s.IsPet)
                    summary.WieldSkippedForPet = true;
                else
                {
                    foreach (var row in w.WeeniePropertiesCreateList.Where(r => (r.DestinationType & (sbyte)DestinationType.Wield) != 0).ToList())
                        w.WeeniePropertiesCreateList.Remove(row);

                    foreach (var item in wielded)
                    {
                        w.WeeniePropertiesCreateList.Add(new WeeniePropertiesCreateList
                        {
                            ObjectId = newWcid,
                            DestinationType = (sbyte)DestinationType.Wield,
                            WeenieClassId = item.Wcid,
                            StackSize = 1,
                            Palette = ClampToSByte(item.PaletteTemplate),
                            Shade = item.Shade,
                            TryToBond = false,
                        });
                    }
                    summary.WieldedItems = wielded.Count;
                }
            }

            // --- flavour ----------------------------------------------------------------------------------
            if (s.IsCreature)
            {
                if (flavour == Flavour.Npc)
                    ApplyNpcTreatment(w);
                else if (isPlayer)
                    SetBool(w, PropertyBool.Attackable, true); // the whitelist carries no Attackable; a monster must have it
            }
            else if (flavour == Flavour.Npc)
                summary.FlavourIgnored = true;

            // --- summary ------------------------------------------------------------------------------------
            summary.Setup = GetDid(w, PropertyDataId.Setup);
            summary.ClothingBase = GetDid(w, PropertyDataId.ClothingBase);
            summary.PaletteBase = GetDid(w, PropertyDataId.PaletteBase);
            summary.PaletteTemplate = GetInt(w, PropertyInt.PaletteTemplate);
            summary.Shade = GetFloat(w, PropertyFloat.Shade);
            summary.Scale = GetFloat(w, PropertyFloat.DefaultScale);
            summary.CreatureVariant = GetInt(w, PropertyInt.CreatureVariant);
            summary.AnimParts = w.WeeniePropertiesAnimPart.Count;
            summary.TextureMaps = w.WeeniePropertiesTextureMap.Count;
            summary.Palettes = w.WeeniePropertiesPalette.Count;
            summary.BodyParts = w.WeeniePropertiesBodyPart.Count;
            summary.Attributes = w.WeeniePropertiesAttribute.Count;
            summary.Vitals = w.WeeniePropertiesAttribute2nd.Count;
            summary.Skills = w.WeeniePropertiesSkill.Count;
            summary.Spells = w.WeeniePropertiesSpellBook.Count;
            summary.Emotes = w.WeeniePropertiesEmote.Count;
            summary.CreateListRows = w.WeeniePropertiesCreateList.Count;

            return w;
        }

        private static WeeniePropertiesEmote CopyEmote(uint newWcid, PropertiesEmote emote)
        {
            var e = new WeeniePropertiesEmote
            {
                ObjectId = newWcid,
                Category = (uint)emote.Category,
                Probability = emote.Probability,
                WeenieClassId = emote.WeenieClassId,
                Style = (uint?)emote.Style,
                Substyle = (uint?)emote.Substyle,
                Quest = emote.Quest,
                VendorType = (int?)emote.VendorType,
                MinHealth = emote.MinHealth,
                MaxHealth = emote.MaxHealth,
                DamageType = (int?)emote.DamageType,
            };

            uint order = 0;
            foreach (var a in emote.PropertiesEmoteAction ?? Enumerable.Empty<PropertiesEmoteAction>())
            {
                e.WeeniePropertiesEmoteAction.Add(new WeeniePropertiesEmoteAction
                {
                    Order = order++,
                    Type = a.Type, Delay = a.Delay, Extent = a.Extent, Motion = (uint?)a.Motion,
                    Message = a.Message, TestString = a.TestString,
                    Min = a.Min, Max = a.Max, Min64 = a.Min64, Max64 = a.Max64, MinDbl = a.MinDbl, MaxDbl = a.MaxDbl,
                    Stat = a.Stat, Display = a.Display, Amount = a.Amount, Amount64 = a.Amount64, HeroXP64 = a.HeroXP64,
                    Percent = a.Percent, SpellId = a.SpellId, WealthRating = a.WealthRating, TreasureClass = a.TreasureClass,
                    TreasureType = a.TreasureType, PScript = (int?)a.PScript, Sound = (int?)a.Sound,
                    DestinationType = a.DestinationType, WeenieClassId = a.WeenieClassId, StackSize = a.StackSize,
                    Palette = a.Palette, Shade = a.Shade, TryToBond = a.TryToBond,
                    ObjCellId = a.ObjCellId, OriginX = a.OriginX, OriginY = a.OriginY, OriginZ = a.OriginZ,
                    AnglesW = a.AnglesW, AnglesX = a.AnglesX, AnglesY = a.AnglesY, AnglesZ = a.AnglesZ,
                });
            }

            return e;
        }

        private static sbyte ClampToSByte(int value)
        {
            if (value < sbyte.MinValue) return sbyte.MinValue;
            if (value > sbyte.MaxValue) return sbyte.MaxValue;
            return (sbyte)value;
        }

        // ------------------------------------------------------------------------------------------------
        // NPC treatment (Database/Updates/World/2026-09-09-00-Ruggans-Annex-NPCs.sql, the Ivo block, plus the
        // no-loot / no-XP / no-corpse trio MatingGuardian.cs sets)
        // ------------------------------------------------------------------------------------------------

        /// <summary>The int properties the Ivo block deletes before re-adding its own (67, 68, 16, 95, 133, 134, 290, 291).</summary>
        public static readonly PropertyInt[] NpcRemovedInts =
        {
            PropertyInt.Tolerance, PropertyInt.TargetingTactic, PropertyInt.ItemUseable, PropertyInt.RadarBlipColor,
            PropertyInt.ShowableOnRadar, PropertyInt.PlayerKillerStatus, PropertyInt.HearLocalSignals, PropertyInt.HearLocalSignalsRadius,
        };

        public static void ApplyNpcTreatment(Weenie w)
        {
            foreach (var p in NpcRemovedInts)
                RemoveInt(w, p);

            SetInt(w, PropertyInt.ItemUseable, (int)Usable.Remote);                     // clickable for dialogue
            SetInt(w, PropertyInt.RadarBlipColor, (int)RadarColor.NPC);
            SetInt(w, PropertyInt.ShowableOnRadar, (int)RadarBehavior.ShowAlways);
            SetInt(w, PropertyInt.PlayerKillerStatus, (int)ACE.Entity.Enum.PlayerKillerStatus.RubberGlue);

            SetBool(w, PropertyBool.Stuck, true);       // stays where it is placed
            SetBool(w, PropertyBool.Attackable, false); // Creature.IsNPC => !Attackable && TargetingTactic == None
            SetBool(w, PropertyBool.Invincible, true);  // belt and braces

            // no loot / XP / corpse, the way MatingGuardian.cs does it
            SetBool(w, PropertyBool.NoCorpse, true);
            SetInt(w, PropertyInt.XpOverride, 0);
            RemoveDid(w, PropertyDataId.DeathTreasureType);
            foreach (var row in w.WeeniePropertiesCreateList.Where(r => (r.DestinationType & (sbyte)DestinationType.Treasure) != 0).ToList())
                w.WeeniePropertiesCreateList.Remove(row);
        }

        // ------------------------------------------------------------------------------------------------
        // SQL text around the writer's INSERT
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The DELETE is narrow on purpose: id AND the class name this tool generated. Re-running the file
        /// replaces only this export; a foreign weenie that later took the id is left alone and the INSERT fails
        /// loudly on the primary key instead of replacing it. Child rows go with the parent (ON DELETE CASCADE).
        /// </summary>
        public static string BuildDeleteStatement(uint wcid, string className)
        {
            var sb = new StringBuilder();
            sb.Append("-- Narrow on purpose: this removes only the row this tool wrote (id AND generated class_Name).\n");
            sb.Append("-- If a later patch has taken the id, that weenie is left alone and the INSERT below fails on the\n");
            sb.Append("-- primary key instead of silently replacing it. Child rows follow the parent (ON DELETE CASCADE).\n");
            sb.Append($"DELETE FROM `weenie` WHERE `class_Id` = {wcid} AND `class_Name` = '{className}';\n");
            return sb.ToString();
        }

        public sealed class HeaderInfo
        {
            public uint NewWcid { get; set; }
            public string ClassName { get; set; }
            public string Name { get; set; }
            public uint SourceWcid { get; set; }
            public string SourceName { get; set; }
            public uint SourceGuid { get; set; }
            public WeenieType SourceType { get; set; }
            public uint? CapturedFromWcid { get; set; }
            public bool SourceIsPlayer { get; set; }
            public string Exporter { get; set; }
            public DateTime ExportedUtc { get; set; }
            public Flavour Flavour { get; set; }
            public CaptureSummary Summary { get; set; }
            public IReadOnlyList<WieldedItem> Wielded { get; set; } = Array.Empty<WieldedItem>();
            public uint BlockStart { get; set; }
            public uint BlockEnd { get; set; }
        }

        /// <summary>The comment block at the top of the file; LF line endings, 7-bit ASCII.</summary>
        public static string BuildHeader(HeaderInfo h)
        {
            var sb = new StringBuilder();
            const string rule = "-- ------------------------------------------------------------------------------------\n";

            sb.Append(rule);
            sb.Append($"-- Template export: '{ToAscii(h.Name)}' -> wcid {h.NewWcid} ({h.ClassName})\n");
            var source = $"-- Source: '{ToAscii(h.SourceName)}' wcid {h.SourceWcid} (WeenieType {h.SourceType}, guid 0x{h.SourceGuid:X8}";
            if (h.CapturedFromWcid.HasValue)
                source += $", captured from wcid {h.CapturedFromWcid.Value}";
            if (h.SourceIsPlayer)
                source += ", a player character";
            sb.Append(source + ")\n");
            sb.Append($"-- Exported by {ToAscii(h.Exporter)} at {h.ExportedUtc:yyyy-MM-dd HH:mm:ss} UTC, flavour: {FlavourName(h.Flavour)}\n");
            sb.Append($"-- Appearance baked from the rendered ObjDesc (CalculateObjDesc) at export time: {h.Summary?.ToLine()}\n");
            if (h.SourceIsPlayer)
                sb.Append("-- The look is what the character wore at that moment; account and character state was stripped.\n");

            if (h.Wielded != null && h.Wielded.Count > 0)
            {
                sb.Append("-- Wield dependencies (each wcid must exist on the shard that loads this file):\n");
                foreach (var item in h.Wielded)
                    sb.Append($"--   {item.Wcid}  {ToAscii(item.Name)}  (palette {item.PaletteTemplate}, shade {item.Shade.ToString("0.00", CultureInfo.InvariantCulture)})\n");
                if (h.Summary != null && h.Summary.WieldSkippedForPet)
                    sb.Append("--   (not written: monster flavour of a pet keeps body-part combat; use npc to carry the held items)\n");
            }

            sb.Append("-- WARNING: a captured look depends on the client having the same DAT art (setup, clothing base, palettes,\n");
            sb.Append("-- textures) as the exporting shard; on other DATs it renders differently or not at all.\n");
            sb.Append($"-- The id sits in the temporary export block {h.BlockStart}-{h.BlockEnd} (whole 7879 prefix): spawn it, iterate, then renumber\n");
            sb.Append("-- into hand-authored space (7878) when it is final. @id / @import-sql refuse this block without the word force.\n");
            sb.Append(rule);
            return sb.ToString();
        }

        public static string FlavourName(Flavour f)
        {
            return f == Flavour.Npc ? "npc" : "monster";
        }

        // ------------------------------------------------------------------------------------------------
        // Chat reply
        // ------------------------------------------------------------------------------------------------

        public sealed class ReplyInfo
        {
            public string Name { get; set; }
            public uint Wcid { get; set; }
            public string ClassName { get; set; }
            public Flavour Flavour { get; set; }
            public bool FlavourIgnored { get; set; }
            public bool SourceIsPlayer { get; set; }
            public bool ReplacedExisting { get; set; }
            public string ExistingName { get; set; }
            public bool OutsideBlock { get; set; }
            public string SourceName { get; set; }
            public uint SourceWcid { get; set; }
            public uint? CapturedFromWcid { get; set; }
            public string FilePath { get; set; }
            public bool SentToDiscord { get; set; }
            /// <summary>The file is being loaded into ace_world in the background; a second line confirms it.</summary>
            public bool ImportStarted { get; set; }
            public string SummaryLine { get; set; }
            public bool WieldSkippedForPet { get; set; }
            public uint BlockStart { get; set; }
            public uint BlockEnd { get; set; }
            public double BlockUsedFraction { get; set; }
        }

        /// <summary>At most eight lines, joined with "\n", 7-bit ASCII.</summary>
        public static string BuildChatReply(ReplyInfo r)
        {
            var lines = new List<string>();

            var first = $"Exported '{r.Name}' as wcid {r.Wcid} ({r.ClassName}), flavour {FlavourName(r.Flavour)}.";
            if (r.ReplacedExisting)
                first += $" Replaced the existing weenie '{r.ExistingName}' (overwrite).";
            if (r.FlavourIgnored)
                first += " Flavour keyword ignored: the source is not a creature.";
            lines.Add(first);

            var source = $"Source: '{r.SourceName}' wcid {r.SourceWcid}";
            if (r.CapturedFromWcid.HasValue)
                source += $", captured from wcid {r.CapturedFromWcid.Value}";
            if (r.SourceIsPlayer)
                source += ". Player: the look was baked from what they wore at that moment, so re-exporting after a gear change gives a different result";
            lines.Add(source + ".");

            lines.Add($"File: {r.FilePath}" + (r.SentToDiscord ? " (also sent to Discord)" : ""));

            var captured = $"Captured: {r.SummaryLine}";
            if (r.WieldSkippedForPet)
                captured += ". Held items skipped: the monster flavour of a pet fights with its body parts; use npc to carry them";
            lines.Add(captured + ".");

            if (r.OutsideBlock)
                lines.Add($"WARNING: {r.Wcid} is outside the export block {r.BlockStart}-{r.BlockEnd}. It was verified free at export time, but check docs/WCID_ALLOCATION_7878.md before loading it.");
            else
            {
                var status = $"Id {r.Wcid} was verified free at export time; block {r.BlockStart}-{r.BlockEnd} is reserved for exports (temporary staging).";
                if (r.BlockUsedFraction > BlockUsageWarningFraction)
                    status += $" WARNING: the block is {Math.Round(r.BlockUsedFraction * 100)}% used.";
                lines.Add(status);
            }

            string next;
            if (r.ImportStarted)
                next = $"Next: loading it into ace_world now; once it says it is loaded, @ci {r.Wcid} or @create {r.Wcid} to try it.";
            else if (r.OutsideBlock)
                next = $"Next: review the file, then @import-sql {r.Wcid}, then @ci {r.Wcid} or @create {r.Wcid} to try it.";
            else
                next = $"Next: @import-sql {r.Wcid} force (the block is staging, hence force), then @ci {r.Wcid} or @create {r.Wcid} to try it.";
            if (r.SourceIsPlayer && r.Flavour == Flavour.Monster && !r.FlavourIgnored)
                next += " A player export has no body parts or TargetingTactic; add them by hand if it should fight.";
            lines.Add(next);

            lines.Add("When it is final, renumber it into your own hand-authored range (7878) before it becomes permanent content; the export block is staging only.");

            // explicit "\n" only: the client draws a bare "\r" as a music note (CLAUDE.md)
            return string.Join("\n", lines.Select(ToAscii));
        }

        // ------------------------------------------------------------------------------------------------
        // Weenie row helpers
        // ------------------------------------------------------------------------------------------------

        public static void SetInt(Weenie w, PropertyInt p, int value)
        {
            var row = w.WeeniePropertiesInt.FirstOrDefault(r => r.Type == (ushort)p);
            if (row != null) row.Value = value;
            else w.WeeniePropertiesInt.Add(new WeeniePropertiesInt { ObjectId = w.ClassId, Type = (ushort)p, Value = value });
        }

        public static int? GetInt(Weenie w, PropertyInt p)
        {
            return w.WeeniePropertiesInt.FirstOrDefault(r => r.Type == (ushort)p)?.Value;
        }

        public static void RemoveInt(Weenie w, PropertyInt p)
        {
            foreach (var row in w.WeeniePropertiesInt.Where(r => r.Type == (ushort)p).ToList())
                w.WeeniePropertiesInt.Remove(row);
        }

        public static void SetBool(Weenie w, PropertyBool p, bool value)
        {
            var row = w.WeeniePropertiesBool.FirstOrDefault(r => r.Type == (ushort)p);
            if (row != null) row.Value = value;
            else w.WeeniePropertiesBool.Add(new WeeniePropertiesBool { ObjectId = w.ClassId, Type = (ushort)p, Value = value });
        }

        public static bool? GetBool(Weenie w, PropertyBool p)
        {
            return w.WeeniePropertiesBool.FirstOrDefault(r => r.Type == (ushort)p)?.Value;
        }

        public static void SetDid(Weenie w, PropertyDataId p, uint value)
        {
            var row = w.WeeniePropertiesDID.FirstOrDefault(r => r.Type == (ushort)p);
            if (row != null) row.Value = value;
            else w.WeeniePropertiesDID.Add(new WeeniePropertiesDID { ObjectId = w.ClassId, Type = (ushort)p, Value = value });
        }

        public static uint? GetDid(Weenie w, PropertyDataId p)
        {
            return w.WeeniePropertiesDID.FirstOrDefault(r => r.Type == (ushort)p)?.Value;
        }

        public static void RemoveDid(Weenie w, PropertyDataId p)
        {
            foreach (var row in w.WeeniePropertiesDID.Where(r => r.Type == (ushort)p).ToList())
                w.WeeniePropertiesDID.Remove(row);
        }

        public static double? GetFloat(Weenie w, PropertyFloat p)
        {
            return w.WeeniePropertiesFloat.FirstOrDefault(r => r.Type == (ushort)p)?.Value;
        }

        public static string GetString(Weenie w, PropertyString p)
        {
            return w.WeeniePropertiesString.FirstOrDefault(r => r.Type == (ushort)p)?.Value;
        }
    }
}
