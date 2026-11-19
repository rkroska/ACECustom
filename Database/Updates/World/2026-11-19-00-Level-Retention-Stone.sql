/* Level Retention Stone (98760530): using one at Enlightenment 50 or higher (enl_level_retention_from) makes the character's
   NEXT enlightenment keep its level instead of returning it to 1, up to level 700 (enl_keeps_level_up_to); above 700 the
   level drops to 700. It only keeps the level: skills still reset, and the skill credit pool is rebuilt for the kept level.
   That enlightenment removes the mark, so every enlightenment needs a new stone. Using it asks Yes/No and is refused below
   Enlightenment 50 or when the next enlightenment is already covered; one stone is used up on Yes.
   Stacks to 1,000. Bonded (kept on death), Sticky Attuned (no trading, giving or dropping, not even to NPCs that take
   anything), unsellable (Attuned alone does not stop vendors buying it), never expires.
   Value 100: priced in MMD notes. A vendor whose AlternateCurrency is the MMD note (20630) and whose SellPrice is 1.0
   charges 100 MMD a stone, from MMD notes in the pack first, then banked pyreals. A pyreal Value of 25,000,000 would
   overflow the int Value of a stack past 85 stones. The vendor is not part of this file.
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
     , (98760530, 69, 0); /* IsSellable - False */

DELETE FROM weenie_properties_int WHERE object_Id = 98760530;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (98760530,   1, 2048)  /* ItemType - Gem */
     , (98760530,   5,    1)  /* EncumbranceVal */
     , (98760530,   8,    1)  /* Mass */
     , (98760530,  11, 1000)  /* MaxStackSize */
     , (98760530,  12,    1)  /* StackSize */
     , (98760530,  13,    1)  /* StackUnitEncumbrance - a full stack of 1,000 weighs 1,000 */
     , (98760530,  14,    1)  /* StackUnitMass */
     , (98760530,  15,  100)  /* StackUnitValue */
     , (98760530,  16,    8)  /* ItemUseable - Contained (used from the packs) */
     , (98760530,  18,    1)  /* UiEffects - Magical */
     , (98760530,  19,  100)  /* Value - 100 (MMD notes at the stone's vendor) */
     , (98760530,  33,    1)  /* Bonded - kept on death */
     , (98760530,  93, 1044)  /* PhysicsState - Ethereal, IgnoreCollisions, Gravity */
     , (98760530, 114,    2); /* Attuned - Sticky: no trade, give, sell or drop */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 98760530;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (98760530,  1,   33556407) /* Setup (0x020007B7) - stone */
     , (98760530,  8,  100670494); /* Icon (0x06001C1E) */

DELETE FROM weenie_properties_string WHERE object_Id = 98760530;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760530,  1, 'Level Retention Stone')
     , (98760530, 14, 'Use this stone at Enlightenment 50 or higher, and your next enlightenment keeps your level (up to level 700) instead of returning it to 1. Your skills still reset. One stone covers one enlightenment.')
     , (98760530, 15, 'A smooth, heavy stone that holds on to what you have earned.')
     , (98760530, 16, 'A smooth, heavy stone that holds on to what you have earned. Used before you enlighten, it keeps your level through the ritual, up to level 700. It does not keep your skills.')
     , (98760530, 20, 'Level Retention Stones');
