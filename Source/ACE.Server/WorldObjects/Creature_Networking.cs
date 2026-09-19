using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Structure;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        public void HandleActionWorldBroadcast(string message, ChatMessageType messageType)
        {
            ActionChain chain = new ActionChain();
            chain.AddAction(this, ActionType.CreatureNetworking_DoWorldBroadcast, () => DoWorldBroadcast(message, messageType));
            chain.EnqueueChain();
        }

        public void DoWorldBroadcast(string message, ChatMessageType messageType)
        {
            GameMessageSystemChat sysMessage = new GameMessageSystemChat(message, messageType);

            PlayerManager.BroadcastToAll(sysMessage);
            PlayerManager.LogBroadcastChat(Channel.AllBroadcast, this, message);
        }

        public override ACE.Entity.ObjDesc CalculateObjDesc()
        {
            ACE.Entity.ObjDesc objDesc = new ACE.Entity.ObjDesc();
            ClothingTable item;

            AddBaseModelData(objDesc);

            var coverage = new List<uint>();

            uint thisSetupId = SetupTableId;
            bool showHelm = true;
            bool showCloak = true;
            if (this is Player player)
            {
                showHelm = player.GetCharacterOption(CharacterOption.ShowYourHelmOrHeadGear);
                showCloak = player.GetCharacterOption(CharacterOption.ShowYourCloak);
            }

            // Some player races use an AlternateSetupDid, either at creation or via Barber options.
            // BUT -- those values do not correspond with entries in the Clothing Table.
            // So, we need to make some adjustments to look up something that DOES exist and is appropriate for the AlternateSetup model.
            switch (thisSetupId)
            {
                //case (uint)SetupConst.UmbraenMaleCrown:
                case (uint)SetupConst.UmbraenMaleCrownGen:
                case (uint)SetupConst.UmbraenMaleNoCrown:
                case (uint)SetupConst.UmbraenMaleVoid:
                    thisSetupId = (uint)SetupConst.UmbraenMaleCrown;
                    break;

                //case (uint)SetupConst.UmbraenFemaleCrown:
                //case (uint)SetupConst.UmbraenFemaleCrownGen:
                case (uint)SetupConst.UmbraenFemaleNoCrown:
                case (uint)SetupConst.UmbraenFemaleVoid:
                    thisSetupId = (uint)SetupConst.UmbraenFemaleCrown;
                    break;

                //case (uint)SetupConst.PenumbraenMaleCrown:
                case (uint)SetupConst.PenumbraenMaleCrownGen:
                case (uint)SetupConst.PenumbraenMaleNoCrown:
                case (uint)SetupConst.PenumbraenMaleVoid:
                    thisSetupId = (uint)SetupConst.PenumbraenMaleCrown;
                    break;

                //case (uint)SetupConst.PenumbraenFemaleCrown:
                //case (uint)SetupConst.PenumbraenFemaleCrownGen:
                case (uint)SetupConst.PenumbraenFemaleNoCrown:
                case (uint)SetupConst.PenumbraenFemaleVoid:
                    thisSetupId = (uint)SetupConst.PenumbraenFemaleCrown;
                    break;

                case (uint)SetupConst.UndeadMaleUndeadGen:
                case (uint)SetupConst.UndeadMaleSkeleton:
                case (uint)SetupConst.UndeadMaleSkeletonNoFlame:
                case (uint)SetupConst.UndeadMaleZombie:
                case (uint)SetupConst.UndeadMaleZombieNoFlame:
                    thisSetupId = (uint)SetupConst.UndeadMaleUndead;
                    break;

                case (uint)SetupConst.UndeadFemaleUndeadGen:
                case (uint)SetupConst.UndeadFemaleSkeleton:
                case (uint)SetupConst.UndeadFemaleSkeletonNoFlame:
                case (uint)SetupConst.UndeadFemaleZombie:
                case (uint)SetupConst.UndeadFemaleZombieNoFlame:
                    thisSetupId = (uint)SetupConst.UndeadFemaleUndead;
                    break;

                case (uint)SetupConst.AnakshayMale:
                    thisSetupId = (uint)SetupConst.HumanMale;
                    break;

                case (uint)SetupConst.AnakshayFemale:
                    thisSetupId = (uint)SetupConst.HumanFemale;
                    break;
            }

            // get all the Armor Items, and any Clothing items that might be equipped (robes, slippers, gloves, kasa, etc) so we can calculate their priority
            var armorItems = EquippedObjects.Values.Where(x => (x.ItemType == ItemType.Armor || (x.CurrentWieldedLocation & (EquipMask.Armor | EquipMask.Extremity)) != 0)).ToList();
            foreach (var w in armorItems)
                w.setVisualClothingPriority();

            // sort the armor into the proper order... TopLayerPriority first, then no priority, then TopLayerPriority=false.
            // Secondary sort field is the calculated "VisualClothingPriority"
            var top = armorItems.Where(x => x.TopLayerPriority == true).OrderBy(x => x.VisualClothingPriority);
            var noLayer = armorItems.Where(x => x.TopLayerPriority == null).OrderBy(x => x.VisualClothingPriority);
            var bottom = armorItems.Where(x => x.TopLayerPriority == false).OrderBy(x => x.VisualClothingPriority);
            var sortedArmorItems = bottom.Concat(noLayer).Concat(top).ToList();

            var clothesAndCloaks = EquippedObjects.Values
                .Where(x => (x.ItemType == ItemType.Clothing) && (x.CurrentWieldedLocation & (EquipMask.Armor | EquipMask.Extremity)) == 0) // Extremity, Head/Foot/Hands, is included in the ArmorItems above
                                .OrderBy(x => x.ClothingPriority);

            var eo = clothesAndCloaks.Concat(sortedArmorItems).ToList();

            if (eo.Count == 0)
            {
                // Check if there is any defined ObjDesc in the Biota and, if so, apply them
                if (Biota.PropertiesAnimPart.GetCount(BiotaDatabaseLock) > 0 || Biota.PropertiesPalette.GetCount(BiotaDatabaseLock) > 0 || Biota.PropertiesTextureMap.GetCount(BiotaDatabaseLock) > 0)
                {
                    Biota.PropertiesAnimPart.CopyTo(objDesc.AnimPartChanges, BiotaDatabaseLock);

                    Biota.PropertiesPalette.CopyTo(objDesc.SubPalettes, BiotaDatabaseLock);

                    Biota.PropertiesTextureMap.CopyTo(objDesc.TextureChanges, BiotaDatabaseLock);

                    // A captured or bred pet returns here (its body parts are biota anim-part rows), so
                    // the recolour at the end of this method would never run for it.
                    ApplyPaletteTemplateOverride(objDesc, thisSetupId);

                    return objDesc;
                }
            }

            foreach (var w in eo)
            {
                if ((w.CurrentWieldedLocation == EquipMask.HeadWear) && !showHelm && (this is Player))
                    continue;

                if ((w.CurrentWieldedLocation == EquipMask.Cloak) && !showCloak && (this is Player))
                    continue;

                // We can wield things that are not part of our model, only use those items that can cover our model.
                if ((w.CurrentWieldedLocation & (EquipMask.Clothing | EquipMask.Armor | EquipMask.Cloak)) != 0)
                {
                    if (!w.ClothingBase.HasValue || !DatManager.PortalDat.TryReadClothingTable((uint)w.ClothingBase, out item))
                    {
                        // AddSetupAsClothingBase maps the ITEM's setup parts onto the CHARACTER's
                        // animation parts by POSITIONAL INDEX. That is only meaningful for pieces
                        // authored as body-part overlays (armour/clothing), where index i means the
                        // same slot on both. A cloak is a standalone object with its own model
                        // space, so index i is NOT the character's part i - the mesh lands on the
                        // wrong slot with the wrong orientation (owner 2026-08-04: the T11 Cloak
                        // rendered "horizontal not vertical").
                        // Draw nothing rather than something mangled. This is damage control only:
                        // the real fix is the missing ClothingBase entry - see
                        // ClothingBase_Missing_2026-08-04.md.
                        if ((w.CurrentWieldedLocation & EquipMask.Cloak) != 0)
                            continue;

                        objDesc = AddSetupAsClothingBase(objDesc, w);
                        // Add any potentially added parts back into the coverage list
                        foreach(var a in objDesc.AnimPartChanges)
                            if (!coverage.Contains(a.Index))
                                coverage.Add(a.Index);
                        continue;
                    }

                    if (item.ClothingBaseEffects.ContainsKey(thisSetupId))
                    // Check if the player model has data. Gear Knights, this is usually you.
                    {
                        // Add the model and texture(s)
                        ClothingBaseEffect clothingBaseEffect = item.ClothingBaseEffects[thisSetupId];
                        foreach (CloObjectEffect t in clothingBaseEffect.CloObjectEffects)
                        {
                            byte partNum = (byte)t.Index;
                            coverage.Add(partNum);

                            objDesc.AddAnimPartChange(new PropertiesAnimPart { Index = (byte)t.Index, AnimationId = t.ModelId });

                            foreach (CloTextureEffect t1 in t.CloTextureEffects)
                                objDesc.AddTextureChange(new PropertiesTextureMap { PartIndex = (byte)t.Index, OldTexture = t1.OldTexture, NewTexture = t1.NewTexture });
                        }

                        if (item.ClothingSubPalEffects.Count > 0)
                        {
                            int size = item.ClothingSubPalEffects.Count;
                            int palCount = size;

                            CloSubPalEffect itemSubPal;
                            int palOption = 0;
                            if (w.PaletteTemplate.HasValue)
                                palOption = (int)w.PaletteTemplate;
                            if (item.ClothingSubPalEffects.ContainsKey((uint)palOption))
                            {
                                itemSubPal = item.ClothingSubPalEffects[(uint)palOption];
                            }
                            else
                            {
                                itemSubPal = item.ClothingSubPalEffects[item.ClothingSubPalEffects.Keys.ElementAt(0)];
                            }

                            float shade = 0.0f;
                            if (w.Shade.HasValue)
                                shade = (float)w.Shade.Value;
                            for (int i = 0; i < itemSubPal.CloSubPalettes.Count; i++)
                            {
                                ushort itemPal = 0;
                                if ((palOption & 0xFF000000) == 0x04000000)
                                {
                                    itemPal = (ushort)(palOption & 0xFFFF);
                                }
                                else if (palOption > 0 && !item.ClothingSubPalEffects.ContainsKey((uint)palOption))
                                {
                                    itemPal = (ushort)(palOption & 0xFFFF);
                                }
                                else
                                {
                                    var itemPalSet = DatManager.PortalDat.ReadFromDat<PaletteSet>(itemSubPal.CloSubPalettes[i].PaletteSet);
                                    if (itemPalSet != null)
                                        itemPal = (ushort)itemPalSet.GetPaletteID(shade);
                                }

                                if (itemPal != 0)
                                {
                                    for (int j = 0; j < itemSubPal.CloSubPalettes[i].Ranges.Count; j++)
                                    {
                                        ushort palOffset = (ushort)(itemSubPal.CloSubPalettes[i].Ranges[j].Offset / 8);
                                        ushort numColors = (ushort)(itemSubPal.CloSubPalettes[i].Ranges[j].NumColors / 8);
                                        objDesc.SubPalettes.Add(new PropertiesPalette { SubPaletteId = itemPal, Offset = palOffset, Length = numColors });
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (coverage.Count == 0 && ClothingBase.HasValue)
            {
                if (DatManager.PortalDat.TryReadClothingTable((uint)ClothingBase.Value, out var creatureCloTable))
                {
                    if (creatureCloTable.ClothingBaseEffects.TryGetValue(thisSetupId, out var cloEffect))
                    {
                        foreach (CloObjectEffect t in cloEffect.CloObjectEffects)
                        {
                            byte partNum = (byte)t.Index;
                            coverage.Add(partNum);
                            objDesc.AddAnimPartChange(new PropertiesAnimPart { Index = (byte)t.Index, AnimationId = t.ModelId });
                            foreach (CloTextureEffect t1 in t.CloTextureEffects)
                                objDesc.AddTextureChange(new PropertiesTextureMap { PartIndex = (byte)t.Index, OldTexture = t1.OldTexture, NewTexture = t1.NewTexture });
                        }
                    }

                    int palOption = PaletteTemplate.HasValue ? (int)PaletteTemplate.Value : 0;
                    uint setupTexPal = GetSetupDefaultPaletteId(thisSetupId);
                    if (setupTexPal > 0 && (objDesc.PaletteID == 0 || objDesc.PaletteID == 0x040002AB || objDesc.PaletteID == 0x0400007E || ClothingBase.Value == 0x100000AF))
                    {
                        objDesc.PaletteID = setupTexPal;
                    }

                    if ((palOption & 0xFF000000) == 0x04000000)
                    {
                        ushort itemPal = (ushort)(palOption & 0xFFFF);

                        // Subpalettes overlay the base PaletteID. If the base is unset the client has nothing
                        // to overlay onto and discards the whole palette block, rendering the model default.
                        // The ClothingSubPalEffects branch below already guards this; mirror it here.
                        if (objDesc.PaletteID == 0)
                            objDesc.PaletteID = (uint)(0x04000000 | itemPal);

                        objDesc.SubPalettes.Add(new PropertiesPalette { SubPaletteId = itemPal, Offset = 0, Length = 255 });
                        objDesc.SubPalettes.Add(new PropertiesPalette { SubPaletteId = itemPal, Offset = 255, Length = 1 });
                    }
                    else if (creatureCloTable != null && creatureCloTable.ClothingSubPalEffects != null && creatureCloTable.ClothingSubPalEffects.Count > 0)
                    {
                        CloSubPalEffect itemSubPal = null;
                        if (creatureCloTable.ClothingSubPalEffects.ContainsKey((uint)palOption))
                        {
                            itemSubPal = creatureCloTable.ClothingSubPalEffects[(uint)palOption];
                        }
                        else if (creatureCloTable.ClothingSubPalEffects.Count > 0)
                        {
                            itemSubPal = creatureCloTable.ClothingSubPalEffects[creatureCloTable.ClothingSubPalEffects.Keys.ElementAt(0)];
                        }

                        if (itemSubPal != null)
                        {
                            float shade = Shade.HasValue ? (float)Shade.Value : 0.5f;
                            for (int i = 0; i < itemSubPal.CloSubPalettes.Count; i++)
                            {
                                ushort itemPal = 0;
                                if (palOption > 0 && !creatureCloTable.ClothingSubPalEffects.ContainsKey((uint)palOption))
                                {
                                    itemPal = (ushort)(palOption & 0xFFFF);
                                }
                                else
                                {
                                    var itemPalSet = DatManager.PortalDat.ReadFromDat<PaletteSet>(itemSubPal.CloSubPalettes[i].PaletteSet);
                                    if (itemPalSet != null)
                                        itemPal = (ushort)itemPalSet.GetPaletteID(shade);
                                }

                                if (itemPal != 0)
                                {
                                    if (objDesc.PaletteID == 0)
                                    {
                                        objDesc.PaletteID = (uint)(0x04000000 | itemPal);
                                    }

                                    for (int j = 0; j < itemSubPal.CloSubPalettes[i].Ranges.Count; j++)
                                    {
                                        ushort rawOffset = (ushort)itemSubPal.CloSubPalettes[i].Ranges[j].Offset;
                                        if (rawOffset == 320 && j == 0 && i == 0)
                                        {
                                            // Map chunk 40 (Offset 320 body colors) into low-index body parts (like Olthoi legs [0..319]) in-game!
                                            objDesc.SubPalettes.Add(new PropertiesPalette { SubPaletteId = itemPal, Offset = 40, Length = 40 });
                                        }

                                        ushort palOffset = (ushort)(rawOffset / 8);
                                        ushort numColors = (ushort)(itemSubPal.CloSubPalettes[i].Ranges[j].NumColors / 8);
                                        while (numColors > 0)
                                        {
                                            ushort chunkLength = numColors > 255 ? (ushort)255 : numColors;
                                            objDesc.SubPalettes.Add(new PropertiesPalette { SubPaletteId = itemPal, Offset = palOffset, Length = chunkLength });
                                            palOffset += chunkLength;
                                            numColors -= chunkLength;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (CreatureVariant.HasValue)
                    {
                        var baseObjDesc = base.CalculateObjDesc();
                        baseObjDesc.TextureChanges.AddRange(CreatureVariantHelper.GetTextureChanges(this, coverage));
                        return ApplyBiotaPartOverrides(baseObjDesc);
                    }
                    return ApplyBiotaPartOverrides(base.CalculateObjDesc());
                }
            }

            // Add the "naked" body parts. These are the ones not already covered.

            if (SetupTableId > 0)
            {
                var baseSetup = DatManager.PortalDat.ReadFromDat<SetupModel>(SetupTableId);
                for (byte i = 0; i < baseSetup.Parts.Count; i++)
                {
                    if (!coverage.Contains(i) && i != 0x10) // Don't add body parts for those that are already covered. Also don't add the head, that was already covered by AddCharacterBaseModelData()
                        objDesc.AnimPartChanges.Add(new PropertiesAnimPart { Index = i, AnimationId = baseSetup.Parts[i] });
                }
            }

            if (CreatureVariant.HasValue)
            {
                objDesc.TextureChanges.AddRange(CreatureVariantHelper.GetTextureChanges(this, coverage));
            }

            ApplyPaletteTemplateOverride(objDesc, thisSetupId);

            if (ServerConfig.pet_visual_packet_debug.Value)
            {
                log.Info($"[CREATURE PACKET DEBUG] {Name} (WCID {WeenieClassId}): Setup=0x{SetupTableId:X8}, ClothingBase=0x{(ClothingBase ?? 0):X8}, PaletteID=0x{objDesc.PaletteID:X8}, PaletteTemplate=0x{(PaletteTemplate ?? 0):X8}, Shade={(Shade?.ToString("F2") ?? "null")}, SubPalettes={objDesc.SubPalettes.Count}, AnimParts={objDesc.AnimPartChanges.Count}, Textures={objDesc.TextureChanges.Count}");
                foreach (var sp in objDesc.SubPalettes)
                {
                    log.Info($"   -> SubPalette: Id=0x{sp.SubPaletteId:X4}, Offset={sp.Offset}, Length={sp.Length}");
                }
            }

            return ApplyBiotaPartOverrides(objDesc);
        }

        /// <summary>
        /// Paints a full 0x04 PaletteTemplate onto an ObjDesc: pick a base PaletteID the client can
        /// overlay onto, then cover the whole 2048-colour palette with two sub-palette ranges. The
        /// client ignores a PaletteID with no sub-palettes, so both halves matter.
        ///
        /// Called from the end of CalculateObjDesc and from its biota early-return. That early return
        /// fires whenever a creature carries ANY biota anim-part, palette or texture rows and has
        /// nothing equipped - which is every captured or bred pet, since its body parts live in those
        /// anim-part rows. Without this call such a pet kept its weenie colours and every bred palette,
        /// @mutate_pet roll and tailored look was silently dropped.
        /// </summary>
        private void ApplyPaletteTemplateOverride(ACE.Entity.ObjDesc objDesc, uint thisSetupId)
        {
            int directPalOption = PaletteTemplate.HasValue ? (int)PaletteTemplate.Value : 0;
            if ((directPalOption & 0xFF000000) != 0x04000000)
                return;

            uint setupTexPal = GetSetupDefaultPaletteId(thisSetupId);
            if (setupTexPal > 0 && (objDesc.PaletteID == 0 || objDesc.PaletteID == 0x040002AB || objDesc.PaletteID == 0x0400007E || (ClothingBase.HasValue && ClothingBase.Value == 0x100000AF)))
                objDesc.PaletteID = setupTexPal;
            else if (objDesc.PaletteID == 0)
                objDesc.PaletteID = setupTexPal > 0 ? setupTexPal : (uint)directPalOption;

            ushort itemPal = (ushort)(directPalOption & 0xFFFF);
            foreach (var sp in objDesc.SubPalettes)
            {
                if (sp.SubPaletteId == itemPal)
                    return;
            }

            objDesc.SubPalettes.Add(new PropertiesPalette { SubPaletteId = itemPal, Offset = 0, Length = 255 });
            objDesc.SubPalettes.Add(new PropertiesPalette { SubPaletteId = itemPal, Offset = 255, Length = 1 });
        }

        /// <summary>Overlay the biota anim-part + texture overrides (zone appearance, baked looks) onto an ObjDesc,
        /// replacing any change at the same index / source texture so ours wins. No-op when the biota has none.</summary>
        private ACE.Entity.ObjDesc ApplyBiotaPartOverrides(ACE.Entity.ObjDesc objDesc)
        {
            // Zone per-part / baked custom part overrides must win even when the creature has equipped items
            // (eo.Count>0). That case skips the biota-parts early-return near the top, and the ObjDesc is re-sent
            // whenever a creature equips - which clobbers a part swap that only survived the no-equipment path.
            // Overlay the biota anim-part + texture overrides here, replacing any change at the same index so ours
            // wins. No-op for creatures with no biota overrides (Clone returns null); Tusgian-style mobs with parts
            // and no equipment already returned above, so they're untouched.
            var _zcAnimOverrides = Biota.PropertiesAnimPart.Clone(BiotaDatabaseLock);
            if (_zcAnimOverrides != null)
                foreach (var p in _zcAnimOverrides)
                {
                    objDesc.AnimPartChanges.RemoveAll(c => c.Index == p.Index);
                    objDesc.AnimPartChanges.Add(p);
                }
            var _zcTexOverrides = Biota.PropertiesTextureMap.Clone(BiotaDatabaseLock);
            if (_zcTexOverrides != null)
                foreach (var t in _zcTexOverrides)
                {
                    // AddTextureChange only de-duplicates an identical (part, old, new) triple; drop any earlier
                    // mapping for the same part + source texture so the biota override is the one that ships
                    objDesc.TextureChanges.RemoveAll(c => c.PartIndex == t.PartIndex && c.OldTexture == t.OldTexture);
                    objDesc.AddTextureChange(t);
                }

            return objDesc;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, uint> _setupDefaultPaletteCache = new();

        /// <summary>
        /// The native base palette of a creature model: the DefaultPaletteId baked into its textures.
        /// Creature texture data lives in client_highres.dat; the portal copies are stubs whose
        /// DefaultPaletteId is 0, so reading only the portal DAT (as this used to) returned 0 for
        /// every creature and silently disabled the native-base fallback in CalculateObjDesc.
        /// Cached per setup: this runs on every creature ObjDesc build.
        /// </summary>
        internal static uint GetSetupDefaultPaletteId(uint setupId)
        {
            if (setupId == 0) return 0;
            return _setupDefaultPaletteCache.GetOrAdd(setupId, ResolveSetupDefaultPaletteId);
        }

        private static uint ResolveSetupDefaultPaletteId(uint setupId)
        {
            var setupModel = DatManager.PortalDat.ReadFromDat<SetupModel>(setupId);
            if (setupModel?.Parts == null) return 0;

            foreach (var partId in setupModel.Parts)
            {
                var gfx = DatManager.PortalDat.ReadFromDat<GfxObj>(partId);
                if (gfx?.Surfaces == null) continue;

                foreach (var sId in gfx.Surfaces)
                {
                    var surf = DatManager.PortalDat.ReadFromDat<Surface>(sId);
                    uint tex = surf?.OrigTextureId ?? 0;
                    if (tex == 0) continue;

                    // 0x05 SurfaceTexture -> its first 0x06 Texture; 0x06 is usable directly.
                    if ((tex & 0xFF000000) == 0x05000000)
                    {
                        var st = DatManager.PortalDat.ReadFromDat<SurfaceTexture>(tex)
                                 ?? DatManager.HighResDat?.ReadFromDat<SurfaceTexture>(tex);
                        if (st?.Textures == null || st.Textures.Count == 0) continue;
                        tex = st.Textures[0];
                    }

                    var t = DatManager.PortalDat.ReadFromDat<ACE.DatLoader.FileTypes.Texture>(tex);
                    if ((t?.DefaultPaletteId ?? 0) == 0)
                        t = DatManager.HighResDat?.ReadFromDat<ACE.DatLoader.FileTypes.Texture>(tex);

                    if (t?.DefaultPaletteId is uint pal && pal > 0)
                        return pal;
                }
            }
            return 0;
        }

        /// <summary>
        /// Certain items do not contain a ClothingBase. Ursuin Guise, WCID 32155 is one of them. This function will use the Setup of the weenie as a pseudo-ClothingBase.
        /// </summary>
        protected ACE.Entity.ObjDesc AddSetupAsClothingBase(ACE.Entity.ObjDesc objDesc, WorldObject wo)
        {
            // Loop over the parts in the Setup of the WorldObject
            for (var i = 0; i < wo.CSetup.Parts.Count; i++)
            {
                if(wo.CSetup.Parts[i] != 0x010001EC || i != 16) // This is essentially a "null" part, so do not add it for the head
                    objDesc.AnimPartChanges.Add(new PropertiesAnimPart { Index = (byte)i, AnimationId = wo.CSetup.Parts[i] });
            }

            return objDesc;
        }


        protected static void WriteIdentifyObjectCreatureProfile(BinaryWriter writer, Creature creature, bool success)
        {
            var creatureProfile = new CreatureProfile(creature, success);
            writer.Write(creatureProfile);
        }
    }
}
