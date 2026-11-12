/* Universal Quiver Charm - WCID 78780032, ability id 36 (CharmAbilityRegistry.UniversalQuiverAbilityId).
 * While active, sets PropertyBool 50061 HasUniversalQuiverCharm: with the ammo slot EMPTY, a bow, crossbow or atlatl
 * fires the matching Infinite Deadly Prismatic ammo (server property universal_quiver_projectiles). Equipped ammo is
 * always used first. Kill switch: /modifybool universal_quiver_enabled false. Code: Player_UniversalQuiver.cs.
 * Copy of the Universal Summoning Mastery Charm (78780031) with its own name, ability and icon (Infinite Deadly
 * Prismatic Arrow's icon and underlay).
 */
START TRANSACTION;

DELETE FROM `weenie` WHERE `class_Id` = 78780032;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780032, 'ace78780032_universalquivercharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780032,    11, 1)
     , (78780032,    13, 1)
     , (78780032,    14, 1)
     , (78780032,    63, 1)
     , (78780032,  9040, 1)
     , (78780032, 50000, 1);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780032,     1, 2048)
     , (78780032,     5,    5)
     , (78780032,     8,    5)
     , (78780032,    16,    8)
     , (78780032,    19,    1)
     , (78780032,    33,    1)
     , (78780032,    83,    2)
     , (78780032,    93, 1044)
     , (78780032,   114,    1)
     , (78780032, 50000,   36)
     , (78780032, 50005,    1)
     , (78780032, 50006,    1);

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780032,    1, 33554556)
     , (78780032,    3, 536870932)
     , (78780032,    8, 0x06006FC7)
     , (78780032,   48, 100676435)
     , (78780032,   50, 0x06007412)
     , (78780032,   52, 0x060033C3);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780032,  1, 'Universal Quiver Charm')
     , (78780032, 14, '
Double-click to activate. While active and your ammunition slot is empty, any bow, crossbow or atlatl you wield fires the matching Infinite Deadly Prismatic ammunition. You must meet that ammunition''s wield requirements. Ammunition you equip yourself is always used first. Double-click again to deactivate.
');

COMMIT;
