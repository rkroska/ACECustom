/* QB Deeds: Deedkeeper's Writ (777700311) - using it does exactly what /rebuff does (Player.HandleDeedsRebuff: Deeds ON,
   10,000 QB, the 120 s lockout after a hostile dispel, the same messages). Players get one with /deeds item at 10,000 QB
   when they do not carry one already. Bonded and attuned: kept on death, never dropped, given or traded. No burden.
   Code: Gem.ActOnUse (the use), Player_Deeds.cs (HandleDeedsItem), DeedsTiers.DeedkeepersWritWcid.
   All text is 7-bit ASCII per CLAUDE.md.
   Run against ace_world. Safe to re-run (deletes then inserts). */

DELETE FROM weenie WHERE class_Id = 777700311;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (777700311, 'ace777700311-deedkeeperswrit', 38, '2026-10-24 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 777700311;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (777700311, 11, 1)  /* IgnoreCollisions */
     , (777700311, 13, 1)  /* Ethereal */
     , (777700311, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 777700311;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (777700311,   1, 2048)  /* ItemType - Gem */
     , (777700311,   5,    0)  /* EncumbranceVal - no burden */
     , (777700311,   8,    0)  /* Mass */
     , (777700311,  16,    8)  /* ItemUseable - Contained (used from the packs) */
     , (777700311,  19,    0)  /* Value */
     , (777700311,  33,    1)  /* Bonded - kept on death */
     , (777700311,  93, 1044)  /* PhysicsState - Ethereal, IgnoreCollisions, Gravity */
     , (777700311, 114,    1); /* Attuned - cannot be dropped, given or traded */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 777700311;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (777700311,  1,  33554556)  /* Setup */
     , (777700311,  3, 536870932)  /* SoundTable */
     , (777700311,  6,  67111919)  /* PaletteBase */
     , (777700311,  8, 100675650)  /* Icon */
     , (777700311, 50, 100667550); /* IconUnderlay */

DELETE FROM weenie_properties_string WHERE object_Id = 777700311;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (777700311,  1, 'Deedkeeper''s Writ')  /* Name */
     , (777700311, 14, 'Use this item to cast all your known level 8 buffs at once, like /rebuff.')  /* Use */
     , (777700311, 16, 'Few in Dereth have walked far enough to earn this. It answers only to the one who did. When a monster or a trap dispels your blessings, it falls silent for 2 minutes.');  /* LongDesc */
