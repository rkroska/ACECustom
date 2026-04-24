-- Infinite Mana Scarab (WCID 900002)
-- Adding PropertyBool.InfiniteCharges (9043) = 1

DELETE FROM `weenie_properties_bool` WHERE `weenie_class_Id` = 900002 AND `type` = 9043;
INSERT INTO `weenie_properties_bool` (`weenie_class_Id`, `type`, `value`) VALUES (900002, 9043, 1);

-- Ensure it has charges and max charges (optional since it's infinite now, but good for UI)
DELETE FROM `weenie_properties_int` WHERE `weenie_class_Id` = 900002 AND `type` IN (12, 13);
INSERT INTO `weenie_properties_int` (`weenie_class_Id`, `type`, `value`) VALUES (900002, 12, 30000); -- Structure (Current Charges)
INSERT INTO `weenie_properties_int` (`weenie_class_Id`, `type`, `value`) VALUES (900002, 13, 30000); -- MaxStructure (Max Charges)
