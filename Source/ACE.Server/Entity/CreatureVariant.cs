using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Models;
using ACE.Server.WorldObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Entity
{
    public enum CreatureVariant
    {
        Shiny = 1,      
    }

    public class CreatureVariantHelper
    {
        private const uint NullPart = 0x010001EC;
        private const uint TransparentSurface = 0x08000157;
        private const uint ShinyTexture = 0x0500190C;
        private readonly struct PartTexture
        {
            public required readonly uint PartIndex { get; init; }
            public required readonly uint TextureId { get; init; }

        }

        /// <summary>
        /// Gets a list of non-null non-transparent part-texture mappings for a creature's setup.
        /// </summary>
        /// <param name="creature">The creature whose setup we are checking for textures.</param>
        /// <param name="skipPartIndexes">Part indexes that are skipped (perhaps covered).</param>
        /// <param name="limit">The maximum number of entries to return.</param>
        /// <returns></returns>
        private static List<PartTexture> GetPartTextures(Creature creature, List<uint> skipPartIndexes = null, int? limit = null)
        {
            List<PartTexture> result = [];
            SetupModel setup = DatManager.PortalDat.ReadFromDat<SetupModel>(creature.SetupTableId);

            for (byte i = 0; i < setup.Parts.Count; i++)
            {
                if (skipPartIndexes?.Contains(i) ?? false) continue;
                uint partId = setup.Parts[i];
                if (partId == NullPart) continue;
                GfxObj partObj = DatManager.PortalDat.ReadFromDat<GfxObj>(setup.Parts[i]);
                foreach (uint surfaceId in partObj.Surfaces)
                {
                    if (surfaceId == TransparentSurface) continue; // Transparent textures
                    Surface surface = DatManager.PortalDat.ReadFromDat<Surface>(surfaceId);
                    if (surface.Translucency == 1) continue;
                        
                    result.Add(new PartTexture { PartIndex = i, TextureId = surface.OrigTextureId });
                    if (limit.HasValue && result.Count >= limit.Value) return result;
                }
            }
            return result;
        }

        /// <summary>
        /// Gets a list of texture changes mapping textures to shinies.
        /// </summary>
        /// <param name="creature"></param>
        /// <param name="skipPartIndexes"></param>
        /// <returns></returns>
        public static List<PropertiesTextureMap> GetTextureChanges(Creature creature, List<uint> skipPartIndexes)
        {
            return GetPartTextures(creature, skipPartIndexes, limit: null)
                .Select(pt => new PropertiesTextureMap
                {
                    PartIndex = (byte)pt.PartIndex,
                    OldTexture = pt.TextureId,
                    NewTexture = ShinyTexture
                })
                .ToList();
        }

        /// <summary>
        /// Checks to see if we are allowed to apply the variant to this creature.
        /// </summary>
        /// <param name="creature"></param>
        /// <returns></returns>
        private static bool CanApplyVariant(Creature creature)
        {
            // Check shiny blacklist
            if (BlacklistManager.IsNoShiny(creature.WeenieClassId)) return false;
            
            // Creature types not allowed to receive a variant.
            if (creature.CreatureType == ACE.Entity.Enum.CreatureType.Wisp) return false;

            return GetPartTextures(creature, skipPartIndexes: null, limit: 1).Count > 0;
        }

        /// <summary>
        /// Applies a variant to a creature.
        /// </summary>
        /// <param name="creature">The creature to apply the variant to.</param>
        /// <param name="variant">The variant to apply to the creature.</param>
        public static void ApplyVariant(Creature creature, CreatureVariant variant)
        {
            if (!CanApplyVariant(creature)) return;
            creature.Name = $"{Enum.GetName(variant)} {creature.Name}";
            creature.CreatureVariant = variant;

            // When adding variant types, use ACViewer to look at the setup.
            // Pick a setup that has a high number of parts when possible.
            switch (variant)
            {
                case CreatureVariant.Shiny:
                    creature.ClothingBase = null;
                    creature.PaletteBaseId = null;
                    creature.PaletteTemplate = null;
                    creature.ObjScale *= 1.2f;
                    break;
            }
        }

        /// <summary>
        /// Possibly applies a random variant to the creature.
        /// </summary>
        /// <param name="creature">The creature to apply the variant to.</param>
        /// <param name="chance">The chance of applying a variant.</param>
        public static void MaybeApplyRandomVariant(Creature creature, float chance)
        {
            CreatureVariant? variant = PickRandomVariant(chance);
            if (variant == null) return;
            ApplyVariant(creature, variant.Value);
        }

        /// <summary>
        /// Picks a random variant from the list of possible variants.
        /// </summary>
        /// <param name="chance"></param>
        /// <returns></returns>
        private static CreatureVariant? PickRandomVariant(float chance)
        {
            if (Random.Shared.NextDouble() < chance)
            {
                return CreatureVariant.Shiny;
            }

            return null;
        }
    }
}
