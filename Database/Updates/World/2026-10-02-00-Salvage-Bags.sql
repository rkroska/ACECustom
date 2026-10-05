/* Salvage Bags (78780310-78780319, 78780323-78780324) - created 2026-10-02 as "Gear Essences", renamed 2026-10-04 (owner).
   Use on a tier 11+ Zone Control item in your pack.
   Core:   Bag of Forgetfulness (310) remove one, Bag of Memory (311) add one, Bag of Second Thoughts (312) reroll one,
           Bag of Tempering (313) reroll Weapon Grade.
   Extras: Bag of Fortune (314) reroll one and keep the better, Bag of Rebirth (315) reroll all, Bag of Locking (316) lock one
           at random, Bag of Vengeance (317) set a weapon's Slayer to the monster type that dropped it, Bag of Madness (318)
           50/50 boost or wipe then Tainted (REDESIGN pending), Bag of Transmutation (319) a weapon's damage type becomes a
           random NEW one of the eight, Bag of Emptiness (323) remove all, Bag of Exchange (324) remove one and add one.
   Protected and never touched: the Always Rolled resists (50-53), Reinforced (49), the slot specials and a locked line.
   Code: Source/ACE.Server/Entity/GearEssences.cs (dispatched from CraftTool.cs). See docs/SALVAGE_BAGS.md.
   Drops: zone kills only, one roll per kill; zone stats bag_odds and bag_weight_* (unset odds = no drops; the extras'
   weights default to 0, so they never drop until set). 78780320-78780322 and 78780325-78780329 are free.
   Stackable (Bag of Vengeance is not: each one carries its own monster type) and tradeable.
   ICONS: the retail Salvage Bag (0x0600102C), a retail material picture as overlay (50) and a green S badge as underlay (52),
   which shows in the bag's see-through top-left corner, so they do not read as real salvage. The client gets exactly
   these three icon layers (icon, overlay, underlay) - PropertyDataId 51 is never sent. Setup = the retail salvage bag model.
   No client DAT changes.
   TargetType 33039 = MeleeWeapon | Armor | Clothing | Jewelry | MissileWeapon | Caster.
   Run against ace_world. Safe to re-run (deletes then inserts). */

/* ========================================================================= */
/* 1. Bag of Forgetfulness (78780310) */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780310;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780310, 'ace78780310-forgetfulnessbag', 44, '2026-10-02 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780310;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780310, 11, 1)  /* IgnoreCollisions */
     , (78780310, 13, 1)  /* Ethereal */
     , (78780310, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780310;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780310,   1,     128)  /* ItemType - Misc */
     , (78780310,   5,       5)  /* EncumbranceVal */
     , (78780310,   8,       5)  /* Mass */
     , (78780310,  11,     100)  /* MaxStackSize */
     , (78780310,  12,       1)  /* StackSize */
     , (78780310,  13,       5)  /* StackUnitEncumbrance */
     , (78780310,  14,       5)  /* StackUnitMass */
     , (78780310,  15,    1000)  /* StackUnitValue */
     , (78780310,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780310,  18,       1)  /* UiEffects - Magical */
     , (78780310,  19,    1000)  /* Value */
     , (78780310,  93,    1044)  /* PhysicsState */
     , (78780310,  94,   33039); /* TargetType - weapons, armor, clothing, jewelry */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780310;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780310,  1,   33554817) /* Setup - salvage bag (0x02000181) */
     , (78780310,  8,  100667436) /* Icon (0x0600102C) - Salvage Bag */
     , (78780310, 50,  100673302) /* IconOverlay (0x06002716) - Smoky Quartz salvage picture */
     , (78780310, 52,  100670894); /* IconUnderlay (0x06001DAE) - green S badge top-left: not salvage (owner pick 10-04) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780310;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780310,  1, 'Bag of Forgetfulness')
     , (78780310, 14, 'Use on a tier 11 or higher Zone weapon, armor, jewelry or clothing piece in your pack to remove one random property. It never takes the last one.')
     , (78780310, 15, 'A bag of grey dust. Whatever it touches forgets a little.')
     , (78780310, 16, 'A bag of grey dust. Whatever it touches forgets a little.');

/* ========================================================================= */
/* 2. Bag of Memory (78780311) */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780311;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780311, 'ace78780311-memorybag', 44, '2026-10-02 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780311;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780311, 11, 1)  /* IgnoreCollisions */
     , (78780311, 13, 1)  /* Ethereal */
     , (78780311, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780311;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780311,   1,     128)  /* ItemType - Misc */
     , (78780311,   5,       5)  /* EncumbranceVal */
     , (78780311,   8,       5)  /* Mass */
     , (78780311,  11,     100)  /* MaxStackSize */
     , (78780311,  12,       1)  /* StackSize */
     , (78780311,  13,       5)  /* StackUnitEncumbrance */
     , (78780311,  14,       5)  /* StackUnitMass */
     , (78780311,  15,    1000)  /* StackUnitValue */
     , (78780311,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780311,  18,       1)  /* UiEffects - Magical */
     , (78780311,  19,    1000)  /* Value */
     , (78780311,  93,    1044)  /* PhysicsState */
     , (78780311,  94,   33039); /* TargetType - weapons, armor, clothing, jewelry */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780311;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780311,  1,   33554817) /* Setup - salvage bag (0x02000181) */
     , (78780311,  8,  100667436) /* Icon (0x0600102C) - Salvage Bag */
     , (78780311, 50,  100673285) /* IconOverlay (0x06002705) - Moonstone salvage picture */
     , (78780311, 52,  100670894); /* IconUnderlay (0x06001DAE) - green S badge top-left: not salvage (owner pick 10-04) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780311;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780311,  1, 'Bag of Memory')
     , (78780311, 14, 'Use on a tier 11 or higher Zone weapon, armor, jewelry or clothing piece in your pack to add one random property. Rare properties stay as rare as they drop.')
     , (78780311, 15, 'A bag of pale grit that remembers what things could have been.')
     , (78780311, 16, 'A bag of pale grit that remembers what things could have been.');

/* ========================================================================= */
/* 3. Bag of Second Thoughts (78780312) */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780312;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780312, 'ace78780312-secondthoughtsbag', 44, '2026-10-02 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780312;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780312, 11, 1)  /* IgnoreCollisions */
     , (78780312, 13, 1)  /* Ethereal */
     , (78780312, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780312;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780312,   1,     128)  /* ItemType - Misc */
     , (78780312,   5,       5)  /* EncumbranceVal */
     , (78780312,   8,       5)  /* Mass */
     , (78780312,  11,     100)  /* MaxStackSize */
     , (78780312,  12,       1)  /* StackSize */
     , (78780312,  13,       5)  /* StackUnitEncumbrance */
     , (78780312,  14,       5)  /* StackUnitMass */
     , (78780312,  15,    1000)  /* StackUnitValue */
     , (78780312,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780312,  18,       1)  /* UiEffects - Magical */
     , (78780312,  19,    1000)  /* Value */
     , (78780312,  93,    1044)  /* PhysicsState */
     , (78780312,  94,   33039); /* TargetType - weapons, armor, clothing, jewelry */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780312;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780312,  1,   33554817) /* Setup - salvage bag (0x02000181) */
     , (78780312,  8,  100667436) /* Icon (0x0600102C) - Salvage Bag */
     , (78780312, 50,  100673288) /* IconOverlay (0x06002708) - Opal salvage picture */
     , (78780312, 52,  100670894); /* IconUnderlay (0x06001DAE) - green S badge top-left: not salvage (owner pick 10-04) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780312;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780312,  1, 'Bag of Second Thoughts')
     , (78780312, 14, 'Use on a tier 11 or higher Zone weapon, armor, jewelry or clothing piece in your pack to reroll one random property within its range. It can go up or down.')
     , (78780312, 15, 'A bag of shifting sand that never settles.')
     , (78780312, 16, 'A bag of shifting sand that never settles.');

/* ========================================================================= */
/* 4. Bag of Tempering (78780313) */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780313;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780313, 'ace78780313-temperingbag', 44, '2026-10-02 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780313;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780313, 11, 1)  /* IgnoreCollisions */
     , (78780313, 13, 1)  /* Ethereal */
     , (78780313, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780313;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780313,   1,     128)  /* ItemType - Misc */
     , (78780313,   5,       5)  /* EncumbranceVal */
     , (78780313,   8,       5)  /* Mass */
     , (78780313,  11,     100)  /* MaxStackSize */
     , (78780313,  12,       1)  /* StackSize */
     , (78780313,  13,       5)  /* StackUnitEncumbrance */
     , (78780313,  14,       5)  /* StackUnitMass */
     , (78780313,  15,    1000)  /* StackUnitValue */
     , (78780313,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780313,  18,       1)  /* UiEffects - Magical */
     , (78780313,  19,    1000)  /* Value */
     , (78780313,  93,    1044)  /* PhysicsState */
     , (78780313,  94,   33039); /* TargetType - weapons, armor, clothing, jewelry */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780313;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780313,  1,   33554817) /* Setup - salvage bag (0x02000181) */
     , (78780313,  8,  100667436) /* Icon (0x0600102C) - Salvage Bag */
     , (78780313, 50,  100673237) /* IconOverlay (0x060026D5) - Steel salvage picture */
     , (78780313, 52,  100670894); /* IconUnderlay (0x06001DAE) - green S badge top-left: not salvage (owner pick 10-04) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780313;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780313,  1, 'Bag of Tempering')
     , (78780313, 14, 'Use on a weapon in your pack that shows a Weapon Grade to reroll it. It can go up or down.')
     , (78780313, 15, 'A bag of quench salts from a smith who never got it right the first time.')
     , (78780313, 16, 'A bag of quench salts from a smith who never got it right the first time.');

/* ========================================================================= */
/* 5. Bag of Fortune (78780314) */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780314;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780314, 'ace78780314-fortunebag', 44, '2026-10-02 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780314;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780314, 11, 1)  /* IgnoreCollisions */
     , (78780314, 13, 1)  /* Ethereal */
     , (78780314, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780314;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780314,   1,     128)  /* ItemType - Misc */
     , (78780314,   5,       5)  /* EncumbranceVal */
     , (78780314,   8,       5)  /* Mass */
     , (78780314,  11,     100)  /* MaxStackSize */
     , (78780314,  12,       1)  /* StackSize */
     , (78780314,  13,       5)  /* StackUnitEncumbrance */
     , (78780314,  14,       5)  /* StackUnitMass */
     , (78780314,  15,    1000)  /* StackUnitValue */
     , (78780314,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780314,  18,       1)  /* UiEffects - Magical */
     , (78780314,  19,    1000)  /* Value */
     , (78780314,  93,    1044)  /* PhysicsState */
     , (78780314,  94,   33039); /* TargetType - weapons, armor, clothing, jewelry */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780314;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780314,  1,   33554817) /* Setup - salvage bag (0x02000181) */
     , (78780314,  8,  100667436) /* Icon (0x0600102C) - Salvage Bag */
     , (78780314, 50,  100673228) /* IconOverlay (0x060026CC) - Gold salvage picture */
     , (78780314, 52,  100670894); /* IconUnderlay (0x06001DAE) - green S badge top-left: not salvage (owner pick 10-04) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780314;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780314,  1, 'Bag of Fortune')
     , (78780314, 14, 'Use on a tier 11 or higher Zone weapon, armor, jewelry or clothing piece in your pack to reroll one random property. If the new roll is no better, the old one stays.')
     , (78780314, 15, 'A bag of lucky gold flakes.')
     , (78780314, 16, 'A bag of lucky gold flakes.');

/* ========================================================================= */
/* 6. Bag of Rebirth (78780315) */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780315;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780315, 'ace78780315-rebirthbag', 44, '2026-10-02 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780315;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780315, 11, 1)  /* IgnoreCollisions */
     , (78780315, 13, 1)  /* Ethereal */
     , (78780315, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780315;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780315,   1,     128)  /* ItemType - Misc */
     , (78780315,   5,       5)  /* EncumbranceVal */
     , (78780315,   8,       5)  /* Mass */
     , (78780315,  11,     100)  /* MaxStackSize */
     , (78780315,  12,       1)  /* StackSize */
     , (78780315,  13,       5)  /* StackUnitEncumbrance */
     , (78780315,  14,       5)  /* StackUnitMass */
     , (78780315,  15,    1000)  /* StackUnitValue */
     , (78780315,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780315,  18,       1)  /* UiEffects - Magical */
     , (78780315,  19,    1000)  /* Value */
     , (78780315,  93,    1044)  /* PhysicsState */
     , (78780315,  94,   33039); /* TargetType - weapons, armor, clothing, jewelry */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780315;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780315,  1,   33554817) /* Setup - salvage bag (0x02000181) */
     , (78780315,  8,  100667436) /* Icon (0x0600102C) - Salvage Bag */
     , (78780315, 50,  100673273) /* IconOverlay (0x060026F9) - Fire Opal salvage picture */
     , (78780315, 52,  100670894); /* IconUnderlay (0x06001DAE) - green S badge top-left: not salvage (owner pick 10-04) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780315;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780315,  1, 'Bag of Rebirth')
     , (78780315, 14, 'Use on a tier 11 or higher Zone weapon, armor, jewelry or clothing piece in your pack to reroll every property. A locked property stays.')
     , (78780315, 15, 'A bag of warm ash that stirs on its own.')
     , (78780315, 16, 'A bag of warm ash that stirs on its own.');

/* ========================================================================= */
/* 7. Bag of Locking (78780316) */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780316;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780316, 'ace78780316-lockingbag', 44, '2026-10-02 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780316;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780316, 11, 1)  /* IgnoreCollisions */
     , (78780316, 13, 1)  /* Ethereal */
     , (78780316, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780316;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780316,   1,     128)  /* ItemType - Misc */
     , (78780316,   5,       5)  /* EncumbranceVal */
     , (78780316,   8,       5)  /* Mass */
     , (78780316,  11,     100)  /* MaxStackSize */
     , (78780316,  12,       1)  /* StackSize */
     , (78780316,  13,       5)  /* StackUnitEncumbrance */
     , (78780316,  14,       5)  /* StackUnitMass */
     , (78780316,  15,    1000)  /* StackUnitValue */
     , (78780316,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780316,  18,       1)  /* UiEffects - Magical */
     , (78780316,  19,    1000)  /* Value */
     , (78780316,  93,    1044)  /* PhysicsState */
     , (78780316,  94,   33039); /* TargetType - weapons, armor, clothing, jewelry */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780316;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780316,  1,   33554817) /* Setup - salvage bag (0x02000181) */
     , (78780316,  8,  100667436) /* Icon (0x0600102C) - Salvage Bag */
     , (78780316, 50,  100673230) /* IconOverlay (0x060026CE) - Iron salvage picture */
     , (78780316, 52,  100670894); /* IconUnderlay (0x06001DAE) - green S badge top-left: not salvage (owner pick 10-04) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780316;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780316,  1, 'Bag of Locking')
     , (78780316, 14, 'Use on a tier 11 or higher Zone weapon, armor, jewelry or clothing piece in your pack to lock one random property. Other bags will not change it. Another Bag of Locking moves the lock.')
     , (78780316, 15, 'A bag of iron filings that cling to whatever they touch.')
     , (78780316, 16, 'A bag of iron filings that cling to whatever they touch.');

/* ========================================================================= */
/* 8. Bag of Vengeance (78780317) */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780317;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780317, 'ace78780317-vengeancebag', 44, '2026-10-02 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780317;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780317, 11, 1)  /* IgnoreCollisions */
     , (78780317, 13, 1)  /* Ethereal */
     , (78780317, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780317;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780317,   1,     128)  /* ItemType - Misc */
     , (78780317,   5,       5)  /* EncumbranceVal */
     , (78780317,   8,       5)  /* Mass */
     , (78780317,  11,       1)  /* MaxStackSize */
     , (78780317,  12,       1)  /* StackSize */
     , (78780317,  13,       5)  /* StackUnitEncumbrance */
     , (78780317,  14,       5)  /* StackUnitMass */
     , (78780317,  15,    1000)  /* StackUnitValue */
     , (78780317,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780317,  18,       1)  /* UiEffects - Magical */
     , (78780317,  19,    1000)  /* Value */
     , (78780317,  93,    1044)  /* PhysicsState */
     , (78780317,  94,   33039); /* TargetType - weapons, armor, clothing, jewelry */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780317;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780317,  1,   33554817) /* Setup - salvage bag (0x02000181) */
     , (78780317,  8,  100667436) /* Icon (0x0600102C) - Salvage Bag */
     , (78780317, 50,  100673266) /* IconOverlay (0x060026F2) - Bloodstone salvage picture */
     , (78780317, 52,  100670894); /* IconUnderlay (0x06001DAE) - green S badge top-left: not salvage (owner pick 10-04) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780317;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780317,  1, 'Bag of Vengeance')
     , (78780317, 14, 'Use on a tier 11 or higher Zone weapon in your pack to make it a Slayer of the kind of monster that dropped this bag. A weapon that already has a Slayer keeps its strength; a new Slayer needs a free property slot.')
     , (78780317, 15, 'A bag of grave dirt that holds a grudge.')
     , (78780317, 16, 'A bag of grave dirt that holds a grudge.');

/* ========================================================================= */
/* 9. Bag of Madness (78780318) */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780318;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780318, 'ace78780318-madnessbag', 44, '2026-10-02 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780318;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780318, 11, 1)  /* IgnoreCollisions */
     , (78780318, 13, 1)  /* Ethereal */
     , (78780318, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780318;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780318,   1,     128)  /* ItemType - Misc */
     , (78780318,   5,       5)  /* EncumbranceVal */
     , (78780318,   8,       5)  /* Mass */
     , (78780318,  11,     100)  /* MaxStackSize */
     , (78780318,  12,       1)  /* StackSize */
     , (78780318,  13,       5)  /* StackUnitEncumbrance */
     , (78780318,  14,       5)  /* StackUnitMass */
     , (78780318,  15,    1000)  /* StackUnitValue */
     , (78780318,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780318,  18,       1)  /* UiEffects - Magical */
     , (78780318,  19,    1000)  /* Value */
     , (78780318,  93,    1044)  /* PhysicsState */
     , (78780318,  94,   33039); /* TargetType - weapons, armor, clothing, jewelry */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780318;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780318,  1,   33554817) /* Setup - salvage bag (0x02000181) */
     , (78780318,  8,  100667436) /* Icon (0x0600102C) - Salvage Bag */
     , (78780318, 50,  100673265) /* IconOverlay (0x060026F1) - Black Opal salvage picture */
     , (78780318, 52,  100670894); /* IconUnderlay (0x06001DAE) - green S badge top-left: not salvage (owner pick 10-04) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780318;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780318,  1, 'Bag of Madness')
     , (78780318, 14, 'Use on a tier 11 or higher Zone weapon, armor, jewelry or clothing piece in your pack. Half the time every property rolls high and one more is added (if the tier has room); half the time every property is removed. A locked property stays. Either way, no bag will work on it again.')
     , (78780318, 15, 'A bag that whispers when nobody is listening.')
     , (78780318, 16, 'A bag that whispers when nobody is listening.');

/* ========================================================================= */
/* 10. Bag of Transmutation (78780319) */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780319;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780319, 'ace78780319-transmutationbag', 44, '2026-10-02 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780319;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780319, 11, 1)  /* IgnoreCollisions */
     , (78780319, 13, 1)  /* Ethereal */
     , (78780319, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780319;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780319,   1,     128)  /* ItemType - Misc */
     , (78780319,   5,       5)  /* EncumbranceVal */
     , (78780319,   8,       5)  /* Mass */
     , (78780319,  11,     100)  /* MaxStackSize */
     , (78780319,  12,       1)  /* StackSize */
     , (78780319,  13,       5)  /* StackUnitEncumbrance */
     , (78780319,  14,       5)  /* StackUnitMass */
     , (78780319,  15,    1000)  /* StackUnitValue */
     , (78780319,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780319,  18,       1)  /* UiEffects - Magical */
     , (78780319,  19,    1000)  /* Value */
     , (78780319,  93,    1044)  /* PhysicsState */
     , (78780319,  94,   33039); /* TargetType - weapons, armor, clothing, jewelry */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780319;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780319,  1,   33554817) /* Setup - salvage bag (0x02000181) */
     , (78780319,  8,  100667436) /* Icon (0x0600102C) - Salvage Bag */
     , (78780319, 50,  100673291) /* IconOverlay (0x0600270B) - Pyreal salvage picture */
     , (78780319, 52,  100670894); /* IconUnderlay (0x06001DAE) - green S badge top-left: not salvage (owner pick 10-04) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780319;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780319,  1, 'Bag of Transmutation')
     , (78780319, 14, 'Use on a tier 11 or higher Zone weapon in your pack to change its damage type to a random new one: Nether, Fire, Frost, Lightning, Acid, Slashing, Piercing or Bludgeoning. Its rending, Cast on Strike and any type word at the start of its name change to match.')
     , (78780319, 15, 'A bag of glittering powder that never looks the same twice.')
     , (78780319, 16, 'A bag of glittering powder that never looks the same twice.');

/* ========================================================================= */
/* 11. Bag of Emptiness (78780323) */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780323;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780323, 'ace78780323-emptinessbag', 44, '2026-10-02 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780323;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780323, 11, 1)  /* IgnoreCollisions */
     , (78780323, 13, 1)  /* Ethereal */
     , (78780323, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780323;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780323,   1,     128)  /* ItemType - Misc */
     , (78780323,   5,       5)  /* EncumbranceVal */
     , (78780323,   8,       5)  /* Mass */
     , (78780323,  11,     100)  /* MaxStackSize */
     , (78780323,  12,       1)  /* StackSize */
     , (78780323,  13,       5)  /* StackUnitEncumbrance */
     , (78780323,  14,       5)  /* StackUnitMass */
     , (78780323,  15,    1000)  /* StackUnitValue */
     , (78780323,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780323,  18,       1)  /* UiEffects - Magical */
     , (78780323,  19,    1000)  /* Value */
     , (78780323,  93,    1044)  /* PhysicsState */
     , (78780323,  94,   33039); /* TargetType - weapons, armor, clothing, jewelry */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780323;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780323,  1,   33554817) /* Setup - salvage bag (0x02000181) */
     , (78780323,  8,  100667436) /* Icon (0x0600102C) - Salvage Bag */
     , (78780323, 50,  100673309) /* IconOverlay (0x0600271D) - White Quartz salvage picture */
     , (78780323, 52,  100670894); /* IconUnderlay (0x06001DAE) - green S badge top-left: not salvage (owner pick 10-04) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780323;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780323,  1, 'Bag of Emptiness')
     , (78780323, 14, 'Use on a tier 11 or higher Zone weapon, armor, jewelry or clothing piece in your pack to remove every property a bag can change. Protected properties and a locked property stay.')
     , (78780323, 15, 'An empty bag. It is heavier than it should be.')
     , (78780323, 16, 'An empty bag. It is heavier than it should be.');

/* ========================================================================= */
/* 12. Bag of Exchange (78780324) */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780324;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780324, 'ace78780324-exchangebag', 44, '2026-10-02 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780324;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780324, 11, 1)  /* IgnoreCollisions */
     , (78780324, 13, 1)  /* Ethereal */
     , (78780324, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780324;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780324,   1,     128)  /* ItemType - Misc */
     , (78780324,   5,       5)  /* EncumbranceVal */
     , (78780324,   8,       5)  /* Mass */
     , (78780324,  11,     100)  /* MaxStackSize */
     , (78780324,  12,       1)  /* StackSize */
     , (78780324,  13,       5)  /* StackUnitEncumbrance */
     , (78780324,  14,       5)  /* StackUnitMass */
     , (78780324,  15,    1000)  /* StackUnitValue */
     , (78780324,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780324,  18,       1)  /* UiEffects - Magical */
     , (78780324,  19,    1000)  /* Value */
     , (78780324,  93,    1044)  /* PhysicsState */
     , (78780324,  94,   33039); /* TargetType - weapons, armor, clothing, jewelry */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780324;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780324,  1,   33554817) /* Setup - salvage bag (0x02000181) */
     , (78780324,  8,  100667436) /* Icon (0x0600102C) - Salvage Bag */
     , (78780324, 50,  100673301) /* IconOverlay (0x06002715) - Silver salvage picture */
     , (78780324, 52,  100670894); /* IconUnderlay (0x06001DAE) - green S badge top-left: not salvage (owner pick 10-04) */

DELETE FROM weenie_properties_string WHERE object_Id = 78780324;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780324,  1, 'Bag of Exchange')
     , (78780324, 14, 'Use on a tier 11 or higher Zone weapon, armor, jewelry or clothing piece in your pack to remove one random property and add a different one in its place.')
     , (78780324, 15, 'A bag of silver bits that never takes without giving something back.')
     , (78780324, 16, 'A bag of silver bits that never takes without giving something back.');

/* ========================================================================= */
/* Retired single-type bags (owner 2026-10-04: one Transmutation instead).     */
/* The weenie_properties_* rows go with them (ON DELETE CASCADE). Only the old */
/* essence rows by name: content that later takes these free ids is safe.     */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id IN (78780320, 78780321, 78780322, 78780325, 78780326, 78780327)
  AND class_Name LIKE 'ace7878032_-%essence';
