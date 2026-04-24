/* ============================================================
   Split Arrow Test Bow (WCID 900001)
   Based on yumi (WCID 363) with Split Arrow properties added
   ============================================================ */

DELETE FROM `weenie_properties_d_i_d`        WHERE `object_Id` = 900001;
DELETE FROM `weenie_properties_float`         WHERE `object_Id` = 900001;
DELETE FROM `weenie_properties_bool`          WHERE `object_Id` = 900001;
DELETE FROM `weenie_properties_int`           WHERE `object_Id` = 900001;
DELETE FROM `weenie_properties_string`        WHERE `object_Id` = 900001;
DELETE FROM `weenie`                          WHERE `class_Id`  = 900001;

/* --- Core Weenie (type 3 = MissileLauncher) --- */
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`)
VALUES (900001, 'split-arrow-bow', 3);

/* --- Strings --- */
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`) VALUES
(900001, 1,  'Split Arrow Bow'),
(900001, 16, 'A yumi imbued with the Split Arrow ability. Each shot can chain to up to 3 nearby additional targets.');

/* --- Ints (cloned from yumi WCID 363, with Split Arrow count added) --- */
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES
(900001, 1,   256),
(900001, 3,   20),
(900001, 5,   980),
(900001, 8,   140),
(900001, 9,   4194304),
(900001, 16,  1),
(900001, 19,  400),
(900001, 44,  0),
(900001, 46,  16),
(900001, 48,  47),
(900001, 49,  45),
(900001, 50,  1),
(900001, 51,  2),
(900001, 52,  2),
(900001, 53,  3),
(900001, 60,  192),
(900001, 93,  1044),
(900001, 150, 103),
(900001, 151, 2),
(900001, 169, 285737226),
(900001, 353, 8),
(900001, 9031, 3);   /* SplitArrowCount: 3 targets */

/* --- Bools --- */
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`) VALUES
(900001, 9030, 1);   /* SplitArrows: enabled */

/* --- Floats --- */
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`) VALUES
(900001, 9032, 8.0),   /* SplitArrowRange: 8 meters */
(900001, 9033, 0.6);   /* SplitArrowDamageMultiplier: 60% per split */

/* --- DIDs (exact copy from yumi WCID 363) --- */
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`) VALUES
(900001, 1,  33554728),
(900001, 3,  536870932),
(900001, 6,  67111919),
(900001, 7,  268435759),
(900001, 8,  100668815),
(900001, 22, 872415275),
(900001, 36, 234881053),
(900001, 46, 939524104);
