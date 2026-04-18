-- ============================================================================
-- MISSING TIERED CHARMS (L2/L3)
-- ============================================================================

-- Heavy Swing Tiers
-- Greater Heavy Swing (777710005)
DELETE FROM `weenie` WHERE `class_Id` = 777710005;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (777710005, 'ilt_heavyswingcharm_level2', 38, '2026-04-18 00:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (777710005, 11, 1), (777710005, 13, 1), (777710005, 14, 1), (777710005, 63, 1), (777710005, 50000, 1);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES (777710005, 1, 65536), (777710005, 16, 8), (777710005, 83, 2), (777710005, 50000, 2), (777710005, 50005, 2);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES (777710005, 1, 33558517), (777710005, 8, 100676392), (777710005, 50, 100663297);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES (777710005, 1, 'Greater Heavy Swing Charm'), (777710005, 14, '[Tier 2] Melee attacks spend 15% current stamina for 2.5x damage.');

-- Master Heavy Swing (777720005)
DELETE FROM `weenie` WHERE `class_Id` = 777720005;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (777720005, 'ilt_heavyswingcharm_level3', 38, '2026-04-18 00:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (777720005, 11, 1), (777720005, 13, 1), (777720005, 14, 1), (777720005, 63, 1), (777720005, 50000, 1);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES (777720005, 1, 65536), (777720005, 16, 8), (777720005, 83, 2), (777720005, 50000, 2), (777720005, 50005, 3);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES (777720005, 1, 33558517), (777720005, 8, 100676392), (777720005, 50, 100663297);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES (777720005, 1, 'Master Heavy Swing Charm'), (777720005, 14, '[Tier 3] Melee attacks spend 10% current stamina for 3.0x damage.');

-- Heavy Draw Tiers
-- Greater Heavy Draw (777710006)
DELETE FROM `weenie` WHERE `class_Id` = 777710006;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (777710006, 'ilt_heavydrawcharm_level2', 38, '2026-04-18 00:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (777710006, 11, 1), (777710006, 13, 1), (777710006, 14, 1), (777710006, 63, 1), (777710006, 50000, 1);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES (777710006, 1, 65536), (777710006, 16, 8), (777710006, 83, 2), (777710006, 50000, 3), (777710006, 50005, 2);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES (777710006, 1, 33558517), (777710006, 8, 100676392), (777710006, 50, 100663297);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES (777710006, 1, 'Greater Heavy Draw Charm'), (777710006, 14, '[Tier 2] Missile attacks spend 15% current stamina for 2.5x damage.');

-- Master Heavy Draw (777720006)
DELETE FROM `weenie` WHERE `class_Id` = 777720006;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (777720006, 'ilt_heavydrawcharm_level3', 38, '2026-04-18 00:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (777720006, 11, 1), (777720006, 13, 1), (777720006, 14, 1), (777720006, 63, 1), (777720006, 50000, 1);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES (777720006, 1, 65536), (777720006, 16, 8), (777720006, 83, 2), (777720006, 50000, 3), (777720006, 50005, 3);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES (777720006, 1, 33558517), (777720006, 8, 100676392), (777720006, 50, 100663297);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES (777720006, 1, 'Master Heavy Draw Charm'), (777720006, 14, '[Tier 3] Missile attacks spend 10% current stamina for 3.0x damage.');

-- Focused Casting Tiers
-- Greater Focused Casting (777710007)
DELETE FROM `weenie` WHERE `class_Id` = 777710007;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (777710007, 'ilt_focusedcastingcharm_level2', 38, '2026-04-18 00:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (777710007, 11, 1), (777710007, 13, 1), (777710007, 14, 1), (777710007, 63, 1), (777710007, 50000, 1);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES (777710007, 1, 65536), (777710007, 16, 8), (777710007, 83, 2), (777710007, 50000, 4), (777710007, 50005, 2);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES (777710007, 1, 33558517), (777710007, 8, 100676392), (777710007, 50, 100663297);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES (777710007, 1, 'Greater Focused Casting Charm'), (777710007, 14, '[Tier 2] Magic attacks spend 8% maximum mana for 2.5x damage.');

-- Master Focused Casting (777720007)
DELETE FROM `weenie` WHERE `class_Id` = 777720007;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (777720007, 'ilt_focusedcastingcharm_level3', 38, '2026-04-18 00:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (777720007, 11, 1), (777720007, 13, 1), (777720007, 14, 1), (777720007, 63, 1), (777720007, 50000, 1);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES (777720007, 1, 65536), (777720007, 16, 8), (777720007, 83, 2), (777720007, 50000, 4), (777720007, 50005, 3);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES (777720007, 1, 33558517), (777720007, 8, 100676392), (777720007, 50, 100663297);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES (777720007, 1, 'Master Focused Casting Charm'), (777720007, 14, '[Tier 3] Magic attacks spend 5% maximum mana for 3.0x damage.');

-- Chaining Tiers
-- Greater Chaining (777710002)
DELETE FROM `weenie` WHERE `class_Id` = 777710002;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (777710002, 'ilt_chainingcharm_level2', 38, '2026-04-18 00:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (777710002, 11, 1), (777710002, 13, 1), (777710002, 14, 1), (777710002, 63, 1), (777710002, 50000, 1);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES (777710002, 1, 65536), (777710002, 16, 8), (777710002, 83, 2), (777710002, 50000, 5), (777710002, 50005, 2);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES (777710002, 1, 33558517), (777710002, 8, 100676392), (777710002, 50, 100663297);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES (777710002, 1, 'Greater Chaining Charm'), (777710002, 14, '[Tier 2] Spells jump to more targets with increased range.');

-- Master Chaining (777720002)
DELETE FROM `weenie` WHERE `class_Id` = 777720002;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (777720002, 'ilt_chainingcharm_level3', 38, '2026-04-18 00:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (777720002, 11, 1), (777720002, 13, 1), (777720002, 14, 1), (777720002, 63, 1), (777720002, 50000, 1);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES (777720002, 1, 65536), (777720002, 16, 8), (777720002, 83, 2), (777720002, 50000, 5), (777720002, 50005, 3);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES (777720002, 1, 33558517), (777720002, 8, 100676392), (777720002, 50, 100663297);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES (777720002, 1, 'Master Chaining Charm'), (777720002, 14, '[Tier 3] Spells jump to even more targets with maximum range.');

-- Repeater Tiers
-- Greater Repeater (777710003)
DELETE FROM `weenie` WHERE `class_Id` = 777710003;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (777710003, 'ilt_repeatercharm_level2', 38, '2026-04-18 00:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (777710003, 11, 1), (777710003, 13, 1), (777710003, 14, 1), (777710003, 63, 1), (777710003, 50000, 1);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES (777710003, 1, 65536), (777710003, 16, 8), (777710003, 83, 2), (777710003, 50000, 6), (777710003, 50005, 2);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES (777710003, 1, 33558517), (777710003, 8, 100676392), (777710003, 50, 100663297);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES (777710003, 1, 'Greater Repeater Charm'), (777710003, 14, '[Tier 2] Fires two additional bolts instead of one.');

-- Master Repeater (777720003)
DELETE FROM `weenie` WHERE `class_Id` = 777720003;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`) VALUES (777720003, 'ilt_repeatercharm_level3', 38, '2026-04-18 00:00:00');
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES (777720003, 11, 1), (777720003, 13, 1), (777720003, 14, 1), (777720003, 63, 1), (777720003, 50000, 1);
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES (777720003, 1, 65536), (777720003, 16, 8), (777720003, 83, 2), (777720003, 50000, 6), (777720003, 50005, 3);
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES (777720003, 1, 33558517), (777720003, 8, 100676392), (777720003, 50, 100663297);
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES (777720003, 1, 'Master Repeater Charm'), (777720003, 14, '[Tier 3] Fires three additional bolts with increased speed.');
