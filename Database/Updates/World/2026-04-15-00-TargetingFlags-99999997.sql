/* TargetingFlags refactor: test weenie 99999997 — remove UseCustomTargetingLists (9018), set PropertyInt.TargetingFlags (9041). */

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 99999997 AND `type` = 9018;

/* PropertyInt 9041 = TargetingFlags (FriendlyToQuestPlayer | HostileToAllPlayers = 8 + 16 = 24). Separate from PropertyBool 9041 in weenie_properties_bool. */
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 99999997 AND `type` = 9041;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (99999997, 9041, 24);

UPDATE `weenie_properties_string` SET `value` = '11,18' WHERE `object_Id` = 99999997 AND `type` = 9014;

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 99999997 AND `type` = 9015;

UPDATE `weenie_properties_bool` SET `value` = 1 WHERE `object_Id` = 99999997 AND `type` = 9042;
