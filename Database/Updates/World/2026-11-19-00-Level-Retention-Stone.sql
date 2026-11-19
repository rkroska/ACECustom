/* Level Retention Stone (98760530): from Enlightenment 50 (enl_keeps_level_and_skills_from) enlightening keeps skills,
   specializations and skill credits but returns the level to 1. Using one of these stones first makes the NEXT enlightenment
   keep the level too, up to level 700 (enl_keeps_level_up_to); that enlightenment removes the mark, so every enlightenment
   needs a new stone. Using it asks Yes/No and is refused below Enlightenment 50 or when the next enlightenment is already
   covered; one stone is used up on Yes. Stackable, Bonded (kept on death), Sticky Attuned (no trading, giving or dropping,
   not even to NPCs that take anything), unsellable (Attuned alone does not stop vendors buying it), never expires. Value 25,000,000 pyreals = 100 MMD (a vendor with SellPrice 1.0 charges exactly that; the vendor is not
   part of this file).
   Code: Enlightenment.UseLevelRetentionStone / LevelRetentionStoneWcid, hooked in Gem.ActOnUse; the mark is
   PropertyInt.LevelRetentionPending (50605).
   Built like the Enlarging / Shrinking Stones (2026-10-10-00-Size-Stones.sql) and the Deedkeeper's Writ
   (2026-10-24-00-Deedkeepers-Writ.sql). All text is 7-bit ASCII per CLAUDE.md.
   Run against ace_world. Safe to re-run (deletes then inserts). */

DELETE FROM weenie WHERE class_Id = 98760530;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (98760530, 'ace98760530-levelretentionstone', 38, '2026-11-19 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 98760530;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (98760530, 11, 1)  /* IgnoreCollisions */
     , (98760530, 13, 1)  /* Ethereal */
     , (98760530, 14, 1)  /* GravityStatus */
     , (98760530, 69, 0); /* IsSellable - False: some vendors buy gems at full or double Value */

DELETE FROM weenie_properties_int WHERE object_Id = 98760530;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (98760530,   1,     2048)  /* ItemType - Gem */
     , (98760530,   5,        5)  /* EncumbranceVal */
     , (98760530,   8,        5)  /* Mass */
     , (98760530,  11,       50)  /* MaxStackSize - 50 x 25M = 1.25B, under the int stack Value limit */
     , (98760530,  12,        1)  /* StackSize */
     , (98760530,  13,        5)  /* StackUnitEncumbrance */
     , (98760530,  14,        5)  /* StackUnitMass */
     , (98760530,  16,        8)  /* ItemUseable - Contained (used from the packs) */
     , (98760530,  18,        1)  /* UiEffects - Magical */
     , (98760530,  19, 25000000)  /* Value - 25,000,000 Pyreals (100 MMD) */
     , (98760530,  33,        1)  /* Bonded - kept on death */
     , (98760530,  93,     1044)  /* PhysicsState - Ethereal, IgnoreCollisions, Gravity */
     , (98760530, 114,        2); /* Attuned - Sticky: no trade, give, sell or drop */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 98760530;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (98760530,  1,   33556407) /* Setup (0x020007B7) - stone */
     , (98760530,  8,  100670494); /* Icon (0x06001C1E) */

DELETE FROM weenie_properties_string WHERE object_Id = 98760530;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760530,  1, 'Level Retention Stone')
     , (98760530, 14, 'Use this stone at Enlightenment 50 or higher, and your next enlightenment keeps your level (up to level 700) instead of returning it to 1. One stone covers one enlightenment.')
     , (98760530, 15, 'A smooth, heavy stone that holds on to what you have earned.')
     , (98760530, 16, 'A smooth, heavy stone that holds on to what you have earned. Used before you enlighten, it keeps your level through the ritual, up to level 700.')
     , (98760530, 20, 'Level Retention Stones');
