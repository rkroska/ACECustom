/* Attack school gems stack to 1,000 (2026-11-24, PR #624): Gem of War / Void / Melee / Missile Augmentation
   (777700052, 777700057, 777700062, 777700067). Only MaxStackSize (PropertyInt 11) changes.

   The server keeps one earner and tier stamp per stack (SchoolGems.StacksMatch): gems only merge with the same stamp, a
   split keeps it (SchoolGems.CopyStamp), and a use takes one gem. It also raises MaxStackSize on every school gem it makes
   or meets (the corpse drop, a split, a merge, the login pass over the packs), so earned gems stack the same before this is
   imported; this keeps the weenies in line for gems made any other way.

   Only Gem weenies (type 38) can stack, so the SELECT skips any of the four that is not one. Check first:
   SELECT class_Id, class_Name, type FROM weenie WHERE class_Id IN (777700052, 777700057, 777700062, 777700067);

   All text is 7-bit ASCII. Run against ace_world. Safe to re-run. Restart the server (or @clearcache) after importing. */

START TRANSACTION;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
SELECT `class_Id`, 11 /* MaxStackSize */, 1000
FROM `weenie`
WHERE `class_Id` IN (777700052, 777700057, 777700062, 777700067)
  AND `type` = 38 /* Gem */
ON DUPLICATE KEY UPDATE `value` = VALUES(`value`);

COMMIT;
