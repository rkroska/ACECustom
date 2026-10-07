using System;
using System.Collections.Generic;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Dressing Room: draws a player's locked-in look in place of the gear they are really wearing.
    ///
    /// This is a second, self-contained draw path. Creature.CalculateObjDesc calls it for a player and uses the result
    /// only when it is not null; for everyone else, and whenever anything here declines or fails, the ordinary path runs
    /// untouched. Nothing in this file changes an item or a property - it only builds the ObjDesc that is sent.
    /// </summary>
    partial class Creature
    {
        /// <summary>
        /// The setup id clothing tables are keyed by for this body. A copy of the switch at the top of
        /// CalculateObjDesc (kept there untouched so the ordinary draw path is byte-for-byte what it was); a race added
        /// to one must be added to the other, or a look locked on that race is refused as "would not show".
        /// </summary>
        public static uint DressingRoomClothingSetup(uint setupId)
        {
            switch (setupId)
            {
                case (uint)SetupConst.UmbraenMaleCrownGen:
                case (uint)SetupConst.UmbraenMaleNoCrown:
                case (uint)SetupConst.UmbraenMaleVoid:
                    return (uint)SetupConst.UmbraenMaleCrown;

                case (uint)SetupConst.UmbraenFemaleNoCrown:
                case (uint)SetupConst.UmbraenFemaleVoid:
                    return (uint)SetupConst.UmbraenFemaleCrown;

                case (uint)SetupConst.PenumbraenMaleCrownGen:
                case (uint)SetupConst.PenumbraenMaleNoCrown:
                case (uint)SetupConst.PenumbraenMaleVoid:
                    return (uint)SetupConst.PenumbraenMaleCrown;

                case (uint)SetupConst.PenumbraenFemaleNoCrown:
                case (uint)SetupConst.PenumbraenFemaleVoid:
                    return (uint)SetupConst.PenumbraenFemaleCrown;

                case (uint)SetupConst.UndeadMaleUndeadGen:
                case (uint)SetupConst.UndeadMaleSkeleton:
                case (uint)SetupConst.UndeadMaleSkeletonNoFlame:
                case (uint)SetupConst.UndeadMaleZombie:
                case (uint)SetupConst.UndeadMaleZombieNoFlame:
                    return (uint)SetupConst.UndeadMaleUndead;

                case (uint)SetupConst.UndeadFemaleUndeadGen:
                case (uint)SetupConst.UndeadFemaleSkeleton:
                case (uint)SetupConst.UndeadFemaleSkeletonNoFlame:
                case (uint)SetupConst.UndeadFemaleZombie:
                case (uint)SetupConst.UndeadFemaleZombieNoFlame:
                    return (uint)SetupConst.UndeadFemaleUndead;

                case (uint)SetupConst.AnakshayMale:
                    return (uint)SetupConst.HumanMale;

                case (uint)SetupConst.AnakshayFemale:
                    return (uint)SetupConst.HumanFemale;
            }
            return setupId;
        }

        /// <summary>
        /// True when this body is an ordinary player body, the only kind a look is drawn on. A character carrying its own
        /// appearance (a creature ClothingBase, a shiny variant, a full 0x04 palette, or stored part / palette / texture
        /// rows - a morphed admin, an Olthoi player) takes the special branches of CalculateObjDesc, which the look path
        /// does not reproduce, so it is drawn the ordinary way and the attendant refuses it.
        /// </summary>
        public bool CanHoldDressingRoomLook()
        {
            if (this is not Player player || player.IsOlthoiPlayer)
                return false;
            if (ClothingBase.HasValue || CreatureVariant.HasValue || HasFullPaletteOverride(PaletteTemplate))
                return false;
            return Biota.PropertiesAnimPart.GetCount(BiotaDatabaseLock) == 0
                && Biota.PropertiesPalette.GetCount(BiotaDatabaseLock) == 0
                && Biota.PropertiesTextureMap.GetCount(BiotaDatabaseLock) == 0;
        }

        /// <summary>
        /// True when the piece would actually draw on this body: its clothing table has an entry for
        /// <paramref name="clothingSetupId"/> AND that entry changes at least one body part. An entry alone is not
        /// enough - some bodies carry an empty one for gear they cannot show (a Lugian has 236 of them, among them most
        /// helms and boots; found in game 2026-10-06 when a Horned Helm and Sollerets were taken and drew nothing).
        /// </summary>
        public static bool DressingRoomPieceDraws(uint clothingBase, uint clothingSetupId)
            => clothingBase != 0
                && DatManager.PortalDat.TryReadClothingTable(clothingBase, out var table)
                && table.ClothingBaseEffects.TryGetValue(clothingSetupId, out var effect)
                && effect.CloObjectEffects.Count > 0;

        /// <summary>One thing to draw on the body: a real worn item, or a saved piece of the look.</summary>
        private readonly struct DressingRoomVisual
        {
            public readonly WorldObject Item;           // null for a saved piece
            public readonly EquipMask Location;
            public readonly ItemType ItemType;
            public readonly uint? ClothingBase;
            public readonly int? PaletteTemplate;
            public readonly double? Shade;
            public readonly bool? TopLayer;
            public readonly uint? VisualPriority;
            public readonly uint? ClothingPriority;

            public DressingRoomVisual(WorldObject item)
            {
                Item = item;
                Location = item.CurrentWieldedLocation ?? EquipMask.None;
                ItemType = item.ItemType;
                ClothingBase = item.ClothingBase;
                PaletteTemplate = item.PaletteTemplate;
                Shade = item.Shade;
                TopLayer = item.TopLayerPriority;
                VisualPriority = (uint?)item.VisualClothingPriority;
                ClothingPriority = (uint?)item.ClothingPriority;
            }

            public DressingRoomVisual(DressingRoomPiece piece)
            {
                Item = null;
                Location = (EquipMask)piece.Location;
                ItemType = (ItemType)piece.ItemType;
                ClothingBase = piece.ClothingBase;
                PaletteTemplate = piece.PaletteTemplate;
                Shade = piece.Shade;
                TopLayer = piece.TopLayer;
                VisualPriority = piece.VisualPriority;
                ClothingPriority = piece.ClothingPriority;
            }

            /// <summary>The armour layering group of CalculateObjDesc: armour, and anything on the head, hands or feet.</summary>
            public bool IsArmourGroup => ItemType == ItemType.Armor || (Location & (EquipMask.Armor | EquipMask.Extremity)) != 0;

            /// <summary>The clothing group of CalculateObjDesc: clothing that is not on an armour or extremity slot.</summary>
            public bool IsClothingGroup => ItemType == ItemType.Clothing && (Location & (EquipMask.Armor | EquipMask.Extremity)) == 0;
        }

        /// <summary>
        /// Builds the ObjDesc for a player showing a Dressing Room look, or returns null to mean "draw the ordinary way".
        ///
        /// What is drawn is exactly what CalculateObjDesc would draw if the player were wearing the saved pieces plus
        /// whichever of their real pieces share no slot with a saved one. That is always a set of pieces that could be
        /// worn together, so the layering rules it goes through are the ones the game already uses.
        ///
        /// <paramref name="worn"/> is the ordinary path's own sorted list of worn items (clothing first, then armour),
        /// with VisualClothingPriority already stamped on the armour.
        /// </summary>
        private ACE.Entity.ObjDesc TryCalculateDressingRoomObjDesc(Player player, List<WorldObject> worn, uint thisSetupId, bool showHelm, bool showCloak)
        {
            try
            {
                var look = DressingRoom.ShownLook(player);
                if (look == null || !CanHoldDressingRoomLook())
                    return null;

                // A saved piece with no model for this body (the character changed race or body style since) is left
                // out, and so hides nothing: the real gear in that slot shows instead.
                var saved = new List<DressingRoomVisual>(look.Pieces.Count);
                uint savedSlots = 0;
                foreach (var piece in look.Pieces)
                {
                    if (!DressingRoomPieceDraws(piece.ClothingBase, thisSetupId))
                        continue;
                    var visual = new DressingRoomVisual(piece);
                    if (!visual.IsArmourGroup && !visual.IsClothingGroup)
                        continue;
                    saved.Add(visual);
                    savedSlots |= piece.Location & DressingRoomLook.SlotMask;
                }
                if (saved.Count == 0)
                    return null;

                var visuals = new List<DressingRoomVisual>(saved.Count + worn.Count);
                foreach (var w in worn)
                    if (!DressingRoomLook.Overlaps((uint)(w.CurrentWieldedLocation ?? EquipMask.None), savedSlots))
                        visuals.Add(new DressingRoomVisual(w));
                visuals.AddRange(saved);

                // the same order CalculateObjDesc builds: clothing by ClothingPriority, then armour with
                // TopLayerPriority false, unset, true - each by VisualClothingPriority
                var armour = visuals.Where(v => v.IsArmourGroup).ToList();
                var ordered = visuals.Where(v => v.IsClothingGroup).OrderBy(v => v.ClothingPriority)
                    .Concat(armour.Where(v => v.TopLayer == false).OrderBy(v => v.VisualPriority))
                    .Concat(armour.Where(v => v.TopLayer == null).OrderBy(v => v.VisualPriority))
                    .Concat(armour.Where(v => v.TopLayer == true).OrderBy(v => v.VisualPriority))
                    .ToList();

                var objDesc = new ACE.Entity.ObjDesc();
                AddBaseModelData(objDesc);

                var coverage = new List<uint>();

                foreach (var w in ordered)
                {
                    if (w.Location == EquipMask.HeadWear && !showHelm)
                        continue;

                    if (w.Location == EquipMask.Cloak && !showCloak)
                        continue;

                    if ((w.Location & (EquipMask.Clothing | EquipMask.Armor | EquipMask.Cloak)) == 0)
                        continue;

                    if (!w.ClothingBase.HasValue || !DatManager.PortalDat.TryReadClothingTable(w.ClothingBase.Value, out ClothingTable item))
                    {
                        // Only a real item reaches here (a saved piece was checked above). Same handling as the
                        // ordinary path: no cloak without a clothing table, everything else by its setup.
                        if (w.Item == null || (w.Location & EquipMask.Cloak) != 0)
                            continue;

                        objDesc = AddSetupAsClothingBase(objDesc, w.Item);
                        foreach (var a in objDesc.AnimPartChanges)
                            if (!coverage.Contains(a.Index))
                                coverage.Add(a.Index);
                        continue;
                    }

                    if (!item.ClothingBaseEffects.TryGetValue(thisSetupId, out ClothingBaseEffect clothingBaseEffect))
                        continue;

                    foreach (CloObjectEffect t in clothingBaseEffect.CloObjectEffects)
                    {
                        coverage.Add((byte)t.Index);

                        objDesc.AddAnimPartChange(new PropertiesAnimPart { Index = (byte)t.Index, AnimationId = t.ModelId });

                        foreach (CloTextureEffect t1 in t.CloTextureEffects)
                            objDesc.AddTextureChange(new PropertiesTextureMap { PartIndex = (byte)t.Index, OldTexture = t1.OldTexture, NewTexture = t1.NewTexture });
                    }

                    if (item.ClothingSubPalEffects.Count == 0)
                        continue;

                    // the piece's PaletteTemplate picks the colour option; one the table does not define falls back to its first
                    var palOption = (uint)(w.PaletteTemplate ?? 0);
                    if (!item.ClothingSubPalEffects.TryGetValue(palOption, out CloSubPalEffect itemSubPal))
                        itemSubPal = item.ClothingSubPalEffects[item.ClothingSubPalEffects.Keys.ElementAt(0)];

                    var shade = w.Shade.HasValue ? (float)w.Shade.Value : 0.0f;
                    for (int i = 0; i < itemSubPal.CloSubPalettes.Count; i++)
                    {
                        var itemPalSet = DatManager.PortalDat.ReadFromDat<PaletteSet>(itemSubPal.CloSubPalettes[i].PaletteSet);
                        if (itemPalSet == null)
                            continue;

                        ushort itemPal = (ushort)itemPalSet.GetPaletteID(shade);

                        for (int j = 0; j < itemSubPal.CloSubPalettes[i].Ranges.Count; j++)
                        {
                            ushort palOffset = (ushort)(itemSubPal.CloSubPalettes[i].Ranges[j].Offset / 8);
                            ushort numColors = (ushort)(itemSubPal.CloSubPalettes[i].Ranges[j].NumColors / 8);
                            objDesc.SubPalettes.Add(new PropertiesPalette { SubPaletteId = itemPal, Offset = palOffset, Length = numColors });
                        }
                    }
                }

                // the body parts nothing covers, as the ordinary path adds them (the head came from AddBaseModelData)
                if (SetupTableId > 0)
                {
                    var baseSetup = DatManager.PortalDat.ReadFromDat<SetupModel>(SetupTableId);
                    for (byte i = 0; i < baseSetup.Parts.Count; i++)
                    {
                        if (!coverage.Contains(i) && i != 0x10)
                            objDesc.AnimPartChanges.Add(new PropertiesAnimPart { Index = i, AnimationId = baseSetup.Parts[i] });
                    }
                }

                // both are no-ops on a body CanHoldDressingRoomLook accepts; called so the tail matches the ordinary path
                ApplyPaletteTemplateOverride(objDesc, thisSetupId);
                return ApplyBiotaPartOverrides(objDesc);
            }
            catch (Exception ex)
            {
                DressingRoom.ReportDrawFailure(player, ex);
                return null;
            }
        }
    }
}
