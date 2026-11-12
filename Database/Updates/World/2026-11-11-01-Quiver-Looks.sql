/* Quiver looks (Source/ACE.Server/Entity/QuiverLook.cs): arrows, quarrels and darts that look like a captured creature.
 *   78780430 Quiver Tailoring Kit  - sold by Sorrel the Dyer (78780404, beside the Dye Vat), 125,000,000 pyreals like
 *                                    the Pet Tailoring Kit. Use on a Siphoned / Hollowed Essence (or the essence on
 *                                    the kit): makes a Quiver Pattern; the essence and the kit are consumed.
 *   78780431 Quiver Pattern        - holds the look. Use on the Universal Quiver Charm (look of every empty-slot shot)
 *                                    or on a bow / crossbow / atlatl (look of every shot from it; wins over the charm).
 *                                    Consumed. The code names it "Quiver Pattern (<creature>)".
 *   78780432 / 78780433 Arrow Enlarging / Shrinking Stones - 12,500,000; arrow size only (50%-200%) on the charm or a
 *                                    launcher, never the launcher itself.
 *   78780434 Quiver Unraveling Kit - 12,500,000; takes the look and arrow size off a charm or launcher.
 * The kits are copies of the Pet Tailoring Kit (78780259 / 78780260), the stones of the Size Stones (78780265 / 66), each
 * with its own name, text, targets and price.
 * TargetType: the kit targets Misc (128, the essences); the pattern targets Gem (2048, the charm) + MissileWeapon (256).
 * Plain ASCII only: the client can't draw anything else.
 */
START TRANSACTION;

-- ---------------------------------------------------------------- Quiver Tailoring Kit
DELETE FROM `weenie` WHERE `class_Id` = 78780430;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780430, 'ace78780430-quivertailoringkit', 44, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780430, 11, 1)
     , (78780430, 13, 1)
     , (78780430, 14, 1);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780430,   1,       128)
     , (78780430,   5,        25)
     , (78780430,   8,        25)
     , (78780430,  11,        10)
     , (78780430,  12,         1)
     , (78780430,  13,         1)
     , (78780430,  14,         1)
     , (78780430,  16,    524296)
     , (78780430,  18,        10)
     , (78780430,  19, 125000000)
     , (78780430,  93,      1044)
     , (78780430,  94,       128);

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780430,  1, 0x02001122)
     , (78780430,  6, 0x040018FE)
     , (78780430,  7, 0x10000564)
     , (78780430,  8, 0x06006BCB)
     , (78780430, 22, 0x3400002B)
     , (78780430, 52, 0x060065FB);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780430,  1, 'Quiver Tailoring Kit')
     , (78780430, 14, 'Use on a Siphoned or Hollowed Essence to stitch its creature''s likeness into a Quiver Pattern. The essence and this kit are consumed.')
     , (78780430, 15, 'A tailoring kit for arrows, quarrels and darts.')
     , (78780430, 16, 'A tailoring kit for arrows, quarrels and darts.');

-- ---------------------------------------------------------------- Quiver Pattern
DELETE FROM `weenie` WHERE `class_Id` = 78780431;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780431, 'ace78780431-quiverpattern', 44, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780431, 11, 1)
     , (78780431, 13, 1)
     , (78780431, 14, 1);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780431,   1,    128)
     , (78780431,   5,     25)
     , (78780431,   8,     25)
     , (78780431,  11,      1)
     , (78780431,  12,      1)
     , (78780431,  13,      1)
     , (78780431,  14,      1)
     , (78780431,  16, 524296)
     , (78780431,  18,     10)
     , (78780431,  19,      1)
     , (78780431,  93,   1044)
     , (78780431,  94,   2304);

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780431,  1, 0x02001122)
     , (78780431,  6, 0x040018FE)
     , (78780431,  7, 0x10000564)
     , (78780431,  8, 0x06006FC7)
     , (78780431, 22, 0x3400002B)
     , (78780431, 52, 0x060065FB);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780431,  1, 'Quiver Pattern')
     , (78780431, 14, 'Use on your Universal Quiver Charm to give every shot from an empty ammunition slot this look, or on a bow, crossbow or atlatl to give every shot from it this look. A launcher''s look wins over the charm''s. The pattern is consumed, and a new pattern replaces the old look.')
     , (78780431, 15, 'A Quiver Pattern holding a creature''s likeness.')
     , (78780431, 16, 'A Quiver Pattern holding a creature''s likeness.');

-- ---------------------------------------------------------------- Sorrel the Dyer sells the kit
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780404 AND `weenie_Class_Id` = 78780430;
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
SELECT 78780404, 4, 78780430, -1, 0, 0, 0
WHERE EXISTS (SELECT 1 FROM `weenie` WHERE `class_Id` = 78780404);

-- ---------------------------------------------------------------- Arrow Enlarging / Shrinking Stones
-- Copies of the Enlarging / Shrinking Stones (78780265 / 78780266) that size only a Quiver look's arrows: used on the
-- Universal Quiver Charm or a bow / crossbow / atlatl, a tenth at a time, 50% to 200% (PropertyFloat 54001). The
-- launcher itself is never resized (that stays the Size Stones' job). TargetType: Gem (2048) + MissileWeapon (256).
DELETE FROM `weenie` WHERE `class_Id` IN (78780432, 78780433);
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780432, 'ace78780432-arrowenlargingstone', 44, NOW())
     , (78780433, 'ace78780433-arrowshrinkingstone', 44, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780432, 11, 1), (78780432, 13, 1), (78780432, 14, 1)
     , (78780433, 11, 1), (78780433, 13, 1), (78780433, 14, 1);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780432,  1,     2048), (78780433,  1,     2048)
     , (78780432,  5,       10), (78780433,  5,       10)
     , (78780432,  8,       10), (78780433,  8,       10)
     , (78780432, 11,      100), (78780433, 11,      100)
     , (78780432, 12,        1), (78780433, 12,        1)
     , (78780432, 13,       10), (78780433, 13,       10)
     , (78780432, 14,       10), (78780433, 14,       10)
     , (78780432, 16,   524296), (78780433, 16,   524296)
     , (78780432, 18,       10), (78780433, 18,       10)
     , (78780432, 19, 12500000), (78780433, 19, 12500000)
     , (78780432, 93,     1044), (78780433, 93,     1044)
     , (78780432, 94,     2304), (78780433, 94,     2304);

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780432,  1, 0x020007B7), (78780433,  1, 0x020007B7)
     , (78780432,  8, 0x06001C1E), (78780433,  8, 0x06001C1E)
     , (78780432, 52, 0x060065FC), (78780433, 52, 0x060065FB);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780432,  1, 'Arrow Enlarging Stone')
     , (78780432, 14, 'Use on your Universal Quiver Charm or on a bow, crossbow or atlatl to make its Quiver-look arrows 10% larger, up to 200% of their normal size. The launcher itself is not resized.')
     , (78780432, 15, 'A heavy stone that swells whatever arrow is rubbed against it.')
     , (78780432, 16, 'A heavy stone that swells whatever arrow is rubbed against it.')
     , (78780432, 20, 'Arrow Enlarging Stones')
     , (78780433,  1, 'Arrow Shrinking Stone')
     , (78780433, 14, 'Use on your Universal Quiver Charm or on a bow, crossbow or atlatl to make its Quiver-look arrows 10% smaller, down to 50% of their normal size. The launcher itself is not resized.')
     , (78780433, 15, 'A light, pitted stone that draws in whatever arrow is rubbed against it.')
     , (78780433, 16, 'A light, pitted stone that draws in whatever arrow is rubbed against it.')
     , (78780433, 20, 'Arrow Shrinking Stones');

DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780404 AND `weenie_Class_Id` IN (78780432, 78780433);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
SELECT 78780404, 4, 78780432, -1, 0, 0, 0 WHERE EXISTS (SELECT 1 FROM `weenie` WHERE `class_Id` = 78780404)
UNION ALL
SELECT 78780404, 4, 78780433, -1, 0, 0, 0 WHERE EXISTS (SELECT 1 FROM `weenie` WHERE `class_Id` = 78780404);

-- ---------------------------------------------------------------- Quiver Unraveling Kit
-- Copy of the Quiver Tailoring Kit: used on the Universal Quiver Charm or a bow / crossbow / atlatl, it takes the Quiver
-- look and arrow size off (back to normal; the charm gets its own name and art back). 12,500,000 pyreals, like a stone.
DELETE FROM `weenie` WHERE `class_Id` = 78780434;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780434, 'ace78780434-quiverunravelingkit', 44, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780434, 11, 1)
     , (78780434, 13, 1)
     , (78780434, 14, 1);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780434,   1,      128)
     , (78780434,   5,       25)
     , (78780434,   8,       25)
     , (78780434,  11,       10)
     , (78780434,  12,        1)
     , (78780434,  13,        1)
     , (78780434,  14,        1)
     , (78780434,  16,   524296)
     , (78780434,  18,       10)
     , (78780434,  19, 12500000)
     , (78780434,  93,     1044)
     , (78780434,  94,     2304);

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780434,  1, 0x02001122)
     , (78780434,  6, 0x040018FE)
     , (78780434,  7, 0x10000564)
     , (78780434,  8, 0x06006BCB)
     , (78780434, 22, 0x3400002B)
     , (78780434, 52, 0x060065FC);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780434,  1, 'Quiver Unraveling Kit')
     , (78780434, 14, 'Use on your Universal Quiver Charm or on a bow, crossbow or atlatl to unravel its Quiver look and arrow size, returning its arrows to normal. The kit is consumed.')
     , (78780434, 15, 'A kit for unpicking a Quiver look.')
     , (78780434, 16, 'A kit for unpicking a Quiver look.');

DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780404 AND `weenie_Class_Id` = 78780434;
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
SELECT 78780404, 4, 78780434, -1, 0, 0, 0 WHERE EXISTS (SELECT 1 FROM `weenie` WHERE `class_Id` = 78780404);

COMMIT;
