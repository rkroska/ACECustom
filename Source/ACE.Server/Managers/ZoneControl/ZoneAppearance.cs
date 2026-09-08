using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// A zone's COSMETIC override set — kept deliberately separate from the stat/prop profile so that
    /// appearance is orthogonal to a monster's "true" stats and abilities. Every field is nullable: null
    /// means "not overridden, inherit". A zone carries one <see cref="ControlledArea.AppearanceDefault"/>
    /// plus per-WCID entries in <see cref="ControlledArea.AppearanceByWcid"/>; resolution LAYERS the two
    /// (per-WCID non-null wins over the default), unlike the stat profile which REPLACES. Because this lives
    /// outside <see cref="ACE.Server.Managers.ZoneScaling.ZoneVariantProfile"/>, setting a per-WCID
    /// appearance never creates a stat override bucket, so it can't detach a monster from zone stat scaling.
    ///
    /// Recolor/resize levers stamp through per-instance property writes at (re)spawn. The DataId levers
    /// (model/clothing/palette-base/tables) swap the actual model data — validated by DID class byte and, on
    /// a full model swap, applied after clearing the base weenie's own overlay so it doesn't bleed through.
    /// </summary>
    public class ZoneAppearance
    {
        /// <summary>PropertyString.Name (1) display-name override (owner 2026-08-09). Set per-WCID
        /// only (the command enforces --wcid; a zone-wide default would rename every mob identically).</summary>
        public string Name { get; set; }

        // ── Recolor / resize (per-instance property writes) ──
        /// <summary>PropertyInt.PaletteTemplate (3): recolor via a ClothingBase's sub-palette option.</summary>
        public int? PaletteTemplate { get; set; }

        /// <summary>PropertyFloat.Shade (12): palette shade 0..1 (needs a ClothingBase to take).</summary>
        public double? Shade { get; set; }

        /// <summary>PropertyFloat.DefaultScale (39) = ObjScale: model size (1.0 = normal).</summary>
        public double? Scale { get; set; }

        /// <summary>PropertyFloat.Translucency (76): 0 = solid .. 1 = invisible.</summary>
        public double? Translucency { get; set; }

        /// <summary>PropertyInt.CreatureVariant (9038) shiny: true stamps 1 (shiny texture swap), false stamps 0.</summary>
        public bool? Shiny { get; set; }

        // ── Model / data swaps (DataId; validated by class byte in the applier) ──
        /// <summary>PropertyDataId.Setup (1) 0x02: the base model (body parts). A model swap.</summary>
        public uint? SetupTableId { get; set; }

        /// <summary>PropertyDataId.MotionTable (2) 0x09: animations — swap alongside a new Setup or it won't animate.</summary>
        public uint? MotionTable { get; set; }

        /// <summary>PropertyDataId.SoundTable (3) 0x20: creature sounds.</summary>
        public uint? SoundTable { get; set; }

        /// <summary>PropertyDataId.PaletteBase (6) 0x04: whole-object base palette (recolors a bare model too).</summary>
        public uint? PaletteBase { get; set; }

        /// <summary>PropertyDataId.ClothingBase (7) 0x10: clothing/armor overlay (must match the Setup to render).</summary>
        public uint? ClothingBase { get; set; }

        /// <summary>PropertyDataId.Icon (8) 0x06: the examine/selection icon.</summary>
        public uint? Icon { get; set; }

        // ── Per-part body swaps (whole-set overrides: null = inherit; a non-null list REPLACES the target's
        //    parts wholesale). Populated by copylook from a donor with custom parts (e.g. Tusgian's 21 anim +
        //    27 texture rows). Part indices are relative to the Setup, so these travel with the donor's Setup. ──
        /// <summary>weenie_properties_anim_part: each entry swaps a body-part slot's GfxObj model.</summary>
        public List<AnimPartEntry> AnimParts { get; set; }

        /// <summary>weenie_properties_texture_map: each entry swaps a part slot's texture (old -> new).</summary>
        public List<TextureMapEntry> TextureMaps { get; set; }

        public bool IsEmpty =>
            string.IsNullOrEmpty(Name) &&
            !PaletteTemplate.HasValue && !Shade.HasValue && !Scale.HasValue &&
            !Translucency.HasValue && !Shiny.HasValue &&
            !SetupTableId.HasValue && !MotionTable.HasValue && !SoundTable.HasValue &&
            !PaletteBase.HasValue && !ClothingBase.HasValue && !Icon.HasValue &&
            (AnimParts == null || AnimParts.Count == 0) && (TextureMaps == null || TextureMaps.Count == 0);

        /// <summary>Reset every field to "not overridden" (used by clearappearance-all / the plugin's Revert all).</summary>
        public void Clear()
        {
            Name = null;
            PaletteTemplate = null; Shade = null; Scale = null; Translucency = null; Shiny = null;
            SetupTableId = null; MotionTable = null; SoundTable = null;
            PaletteBase = null; ClothingBase = null; Icon = null;
            AnimParts = null; TextureMaps = null;
        }

        /// <summary>Total per-part overrides (anim + texture), for the plugin's "Body Parts: N swapped" row.</summary>
        public int PartCount => (AnimParts?.Count ?? 0) + (TextureMaps?.Count ?? 0);

        public ZoneAppearance Clone() => new ZoneAppearance
        {
            Name = Name,
            PaletteTemplate = PaletteTemplate,
            Shade = Shade,
            Scale = Scale,
            Translucency = Translucency,
            Shiny = Shiny,
            SetupTableId = SetupTableId,
            MotionTable = MotionTable,
            SoundTable = SoundTable,
            PaletteBase = PaletteBase,
            ClothingBase = ClothingBase,
            Icon = Icon,
            AnimParts = AnimParts?.Select(p => p.Clone()).ToList(),
            TextureMaps = TextureMaps?.Select(t => t.Clone()).ToList(),
        };

        /// <summary>Returns a new set = this overlaid by <paramref name="overlay"/> (overlay's non-null fields win).
        /// Used to layer a per-WCID entry over the zone default. Null args are treated as empty.</summary>
        public static ZoneAppearance Merge(ZoneAppearance base_, ZoneAppearance overlay)
        {
            if (base_ == null && overlay == null) return null;
            var result = base_?.Clone() ?? new ZoneAppearance();
            if (overlay == null) return result;
            if (!string.IsNullOrEmpty(overlay.Name)) result.Name = overlay.Name;
            if (overlay.PaletteTemplate.HasValue) result.PaletteTemplate = overlay.PaletteTemplate;
            if (overlay.Shade.HasValue) result.Shade = overlay.Shade;
            if (overlay.Scale.HasValue) result.Scale = overlay.Scale;
            if (overlay.Translucency.HasValue) result.Translucency = overlay.Translucency;
            if (overlay.Shiny.HasValue) result.Shiny = overlay.Shiny;
            if (overlay.SetupTableId.HasValue) result.SetupTableId = overlay.SetupTableId;
            if (overlay.MotionTable.HasValue) result.MotionTable = overlay.MotionTable;
            if (overlay.SoundTable.HasValue) result.SoundTable = overlay.SoundTable;
            if (overlay.PaletteBase.HasValue) result.PaletteBase = overlay.PaletteBase;
            if (overlay.ClothingBase.HasValue) result.ClothingBase = overlay.ClothingBase;
            if (overlay.Icon.HasValue) result.Icon = overlay.Icon;
            if (overlay.AnimParts != null) result.AnimParts = overlay.AnimParts.Select(p => p.Clone()).ToList();
            if (overlay.TextureMaps != null) result.TextureMaps = overlay.TextureMaps.Select(t => t.Clone()).ToList();
            return result;
        }
    }

    /// <summary>One body-part model swap (weenie_properties_anim_part): part slot <see cref="Index"/> -> a
    /// GfxObj (0x01) model in <see cref="GfxObj"/>. Plain serializable DTO for the zone store.</summary>
    public class AnimPartEntry
    {
        public byte Index { get; set; }
        public uint GfxObj { get; set; }
        public AnimPartEntry Clone() => new AnimPartEntry { Index = Index, GfxObj = GfxObj };
    }

    /// <summary>One body-part texture swap (weenie_properties_texture_map): part slot <see cref="Index"/>,
    /// <see cref="OldTex"/> -> <see cref="NewTex"/> (0x05 textures). Plain serializable DTO for the zone store.</summary>
    public class TextureMapEntry
    {
        public byte Index { get; set; }
        public uint OldTex { get; set; }
        public uint NewTex { get; set; }
        public TextureMapEntry Clone() => new TextureMapEntry { Index = Index, OldTex = OldTex, NewTex = NewTex };
    }
}
