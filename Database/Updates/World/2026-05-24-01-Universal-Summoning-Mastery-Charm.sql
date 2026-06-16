/* Universal Summoning Mastery Charm — WCID 78780031, ability id 25.
 * While active, sets PropertyBool 50038; bypasses PetDevice mastery vs player (int 362) when server config on.
 * Does not change player SummoningMastery (362). Admin: docs/ADMIN_PET_SUMMON_CHARMS.md.
 */
START TRANSACTION;

DELETE FROM `weenie` WHERE `class_Id` = 78780031;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780031, 'ace78780031_universalsummoningmasterycharm', 38, NOW());

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780031,    11, 1)
     , (78780031,    13, 1)
     , (78780031,    14, 1)
     , (78780031,    63, 1)
     , (78780031,  9040, 1)
     , (78780031, 50000, 1);

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780031,     1, 2048)
     , (78780031,     5,    5)
     , (78780031,     8,    5)
     , (78780031,    16,    8)
     , (78780031,    19,    1)
     , (78780031,    33,    1)
     , (78780031,    83,    2)
     , (78780031,    93, 1044)
     , (78780031,   114,    1)
     , (78780031, 50000,   25)
     , (78780031, 50005,    1)
     , (78780031, 50006,    1);

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780031,    1, 33554556)
     , (78780031,    3, 536870932)
     , (78780031,    8, 0x06006E89)
     , (78780031,   48, 100676435)
     , (78780031,   50, 0x06007412);

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780031,  1, 'Universal Summoning Mastery Charm')
     , (78780031, 14, '
Double-click to activate (when enabled on this server). While active, you may use summoning essences of any mastery (Primalist, Necromancer, or Naturalist) regardless of the mastery you chose on your character. Deactivate to return to normal restrictions.
');

COMMIT;
