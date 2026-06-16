/* Summon Essence Refill Charm — WCID 78780030 (charm block 78780030-78780089), ability id 24.
 * Admin: docs/ADMIN_PET_SUMMON_CHARMS.md. WCIDs: docs/WCID_ALLOCATION_7878.md.
 * Requires pet_device_pyreal_auto_refill_enabled + server build.
 */
START TRANSACTION;

DELETE FROM `weenie` WHERE `class_Id` = 78780030;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780030, 'ace78780030_summonessencerefillcharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780030,    11, 1)
     , (78780030,    13, 1)
     , (78780030,    14, 1)
     , (78780030,    63, 1)
     , (78780030,  9040, 1)
     , (78780030, 50000, 1);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780030,     1, 2048)
     , (78780030,     5,    5)
     , (78780030,     8,    5)
     , (78780030,    16,    8)
     , (78780030,    19,    1)
     , (78780030,    33,    1)
     , (78780030,    83,    2)
     , (78780030,    93, 1044)
     , (78780030,   114,    1)
     , (78780030, 50000,   24)
     , (78780030, 50005,    1)
     , (78780030, 50006,    1);

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780030,    1, 33554556)
     , (78780030,    3, 536870932)
     , (78780030,    8, 0x06006E89)
     , (78780030,   48, 100676435)
     , (78780030,   50, 0x06001080);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780030,  1, 'Summon Essence Refill Charm')
     , (78780030, 14, '
Double-click to activate. While active, when you use a summoning essence that has no charges, you may pay pyreals (server-configured cost per charge) to restore one charge before your pet is summoned. Deactivate the charm to opt out.
');

COMMIT;
