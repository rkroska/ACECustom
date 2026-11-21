/* Ancient Statue of unknown origin (300002): sells the Skill Credit Gems (2026-11-21-00-Skill-Credit-Gems.sql), and its
   shop is put in a cleaner order. Before, it was grouped by size (all 1x gems, then 10x, 50x, 100x) with the Enlightenment
   Token in the middle of the 1x gems, the Green Signet after the 100x gems and the Summoning gems added at the very end.
   Now each kind of gem is together, 1x, 10x, 50x, 100x, in this order:
     Creature Magic Augmentation Gems
     Item Magic Augmentation Gems
     Life Magic Augmentation Gems
     War Magic Augmentation Gems
     Void Magic Augmentation Gems
     Summoning Augmentation Gems
     Spell Duration Augmentation Gems
     Melee Damage Augmentation Gems
     Missile Damage Augmentation Gems
     Specialized Skill Augmentation Gems
     Skill Credit Gems (new)
     Enlightenment Token
     Green Signet
   Anything else in the shop (added after the 2026-11-09 dump this list was made from) is kept, after these, in its
   current order. Nothing is removed. The Skill Credit Gems get the same shop row values as the Specialized Skill
   Augmentation Gems (stack size 0, palette 1, shade 0.5).

   How: the vendor lists its shop in create list id order (Vendor.LoadInventory fills DefaultItemsForSale in that order,
   and ApproachVendor sends them that way), so the shop rows are copied to a temporary table, deleted and inserted back
   sorted (INSERT ... SELECT ... ORDER BY gives the new auto-increment ids in that order). The delete and the insert are
   one transaction. Only rows with object_Id 300002 and destination_Type 4 (Shop) are touched.

   Run against ace_world, after the gems (a shop row for a missing weenie is skipped by the vendor, so the order of the
   two files is not critical). Safe to re-run: the gems are listed once, and the result is the same. Restart the server
   (or @clearcache and respawn the statue) so the statue loads its new shop. All text is 7-bit ASCII. */

DROP TEMPORARY TABLE IF EXISTS statue_shop;

CREATE TEMPORARY TABLE statue_shop AS
SELECT id, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond
FROM weenie_properties_create_list
WHERE object_Id = 300002 AND destination_Type = 4 /* Shop */;

/* The Skill Credit Gems, once each */
DELETE FROM statue_shop WHERE weenie_Class_Id IN (98760540, 98760541, 98760542, 98760543);

INSERT INTO statue_shop (id, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond)
VALUES (0, 98760540, 0, 1, 0.5, 0)
     , (0, 98760541, 0, 1, 0.5, 0)
     , (0, 98760542, 0, 1, 0.5, 0)
     , (0, 98760543, 0, 1, 0.5, 0);

START TRANSACTION;

DELETE FROM weenie_properties_create_list WHERE object_Id = 300002 AND destination_Type = 4 /* Shop */;

INSERT INTO weenie_properties_create_list (object_Id, destination_Type, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond)
SELECT 300002, 4, weenie_Class_Id, stack_Size, palette, shade, try_To_Bond
FROM statue_shop
ORDER BY CASE weenie_Class_Id
        /* Creature Magic Augmentation Gems */
        WHEN 300005 THEN 1 WHEN 81000125 THEN 2 WHEN 81000135 THEN 3 WHEN 81000145 THEN 4
        /* Item Magic Augmentation Gems */
        WHEN 300006 THEN 5 WHEN 81000126 THEN 6 WHEN 81000136 THEN 7 WHEN 81000146 THEN 8
        /* Life Magic Augmentation Gems */
        WHEN 300007 THEN 9 WHEN 81000127 THEN 10 WHEN 81000137 THEN 11 WHEN 81000147 THEN 12
        /* War Magic Augmentation Gems */
        WHEN 300008 THEN 13 WHEN 81000128 THEN 14 WHEN 81000138 THEN 15 WHEN 81000148 THEN 16
        /* Void Magic Augmentation Gems */
        WHEN 300009 THEN 17 WHEN 81000129 THEN 18 WHEN 81000139 THEN 19 WHEN 81000149 THEN 20
        /* Summoning Augmentation Gems */
        WHEN 2003001 THEN 21 WHEN 81000132 THEN 22 WHEN 81000142 THEN 23 WHEN 81000152 THEN 24
        /* Spell Duration Augmentation Gems */
        WHEN 300016 THEN 25 WHEN 81000130 THEN 26 WHEN 81000140 THEN 27 WHEN 81000150 THEN 28
        /* Melee Damage Augmentation Gems */
        WHEN 2003002 THEN 29 WHEN 81000133 THEN 30 WHEN 81000143 THEN 31 WHEN 81000153 THEN 32
        /* Missile Damage Augmentation Gems */
        WHEN 2003003 THEN 33 WHEN 81000134 THEN 34 WHEN 81000144 THEN 35 WHEN 81000154 THEN 36
        /* Specialized Skill Augmentation Gems */
        WHEN 300021 THEN 37 WHEN 81000131 THEN 38 WHEN 81000141 THEN 39 WHEN 81000151 THEN 40
        /* Skill Credit Gems (new) */
        WHEN 98760540 THEN 41 WHEN 98760541 THEN 42 WHEN 98760542 THEN 43 WHEN 98760543 THEN 44
        /* Enlightenment Token */
        WHEN 300000 THEN 45
        /* Green Signet */
        WHEN 867530176 THEN 46
        ELSE 1000 END
    , id;

COMMIT;

DROP TEMPORARY TABLE statue_shop;
