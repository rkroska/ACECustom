/* Pet Breeding Sinks: Courtship Incense, Nurturing Draughts, and Chromatic Catalysts.
   Run against ace_world. Safe to re-run (deletes then inserts). */

/* ========================================================================= */
/* 1. Lesser Courtship Incense (98760410) - +2.5% mutation chance            */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 98760410;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (98760410, 'ace98760410-lessercourtshipincense', 44, '2026-09-12 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 98760410;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (98760410, 11, 1)  /* IgnoreCollisions */
     , (98760410, 13, 1)  /* Ethereal */
     , (98760410, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 98760410;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (98760410,   1,    128)  /* ItemType - Misc */
     , (98760410,   5,      5)  /* EncumbranceVal */
     , (98760410,   8,      5)  /* Mass */
     , (98760410,  11,    100)  /* MaxStackSize */
     , (98760410,  12,      1)  /* StackSize */
     , (98760410,  13,      1)  /* StackUnitEncumbrance */
     , (98760410,  14,      1)  /* StackUnitMass */
     , (98760410,  16, 524296)  /* ItemUseable - SourceContainedTargetContained */
     , (98760410,  18,     10)  /* UiEffects - Magical */
     , (98760410,  19, 100000)  /* Value - 100,000 Pyreals */
     , (98760410,  93,   1044)  /* PhysicsState */
     , (98760410,  94,    128); /* TargetType - Misc (PetDevice) */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 98760410;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (98760410,  1,  33558818) /* Setup */
     , (98760410,  8, 100670879);/* Icon */

DELETE FROM weenie_properties_string WHERE object_Id = 98760410;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760410,  1, 'Lesser Courtship Incense')
     , (98760410, 14, 'Use on a combat pet essence before breeding to prime it. Grants +2.5% mutation chance on the next breeding attempt.')
     , (98760410, 15, 'A fragrant ceremonial incense stick.')
     , (98760410, 16, 'A fragrant ceremonial incense stick.');

/* ========================================================================= */
/* 2. Refined Courtship Incense (98760411) - +5.0% mutation chance           */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 98760411;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (98760411, 'ace98760411-refinedcourtshipincense', 44, '2026-09-12 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 98760411;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (98760411, 11, 1)
     , (98760411, 13, 1)
     , (98760411, 14, 1);

DELETE FROM weenie_properties_int WHERE object_Id = 98760411;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (98760411,   1,    128)
     , (98760411,   5,      5)
     , (98760411,   8,      5)
     , (98760411,  11,    100)
     , (98760411,  12,      1)
     , (98760411,  13,      1)
     , (98760411,  14,      1)
     , (98760411,  16, 524296)
     , (98760411,  18,     10)
     , (98760411,  19, 500000)  /* Value - 500,000 Pyreals */
     , (98760411,  93,   1044)
     , (98760411,  94,    128);

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 98760411;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (98760411,  1,  33558818)
     , (98760411,  8, 100670879)
     , (98760411, 22, 872415275); /* IconOverlay */

DELETE FROM weenie_properties_string WHERE object_Id = 98760411;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760411,  1, 'Refined Courtship Incense')
     , (98760411, 14, 'Use on a combat pet essence before breeding to prime it. Grants +5.0% mutation chance on the next breeding attempt.')
     , (98760411, 15, 'A potent ceremonial incense stick.')
     , (98760411, 16, 'A potent ceremonial incense stick.');

/* ========================================================================= */
/* 3. Exquisite Courtship Incense (98760412) - +10.0% mutation chance        */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 98760412;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (98760412, 'ace98760412-exquisitecourtshipincense', 44, '2026-09-12 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 98760412;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (98760412, 11, 1)
     , (98760412, 13, 1)
     , (98760412, 14, 1);

DELETE FROM weenie_properties_int WHERE object_Id = 98760412;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (98760412,   1,     128)
     , (98760412,   5,       5)
     , (98760412,   8,       5)
     , (98760412,  11,     100)
     , (98760412,  12,       1)
     , (98760412,  13,       1)
     , (98760412,  14,       1)
     , (98760412,  16,  524296)
     , (98760412,  18,      10)
     , (98760412,  19, 2500000) /* Value - 2,500,000 Pyreals */
     , (98760412,  93,    1044)
     , (98760412,  94,     128);

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 98760412;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (98760412,  1,  33558818)
     , (98760412,  8, 100670879)
     , (98760412, 22, 100671392); /* IconOverlay */

DELETE FROM weenie_properties_string WHERE object_Id = 98760412;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760412,  1, 'Exquisite Courtship Incense')
     , (98760412, 14, 'Use on a combat pet essence before breeding to prime it. Grants +10.0% mutation chance on the next breeding attempt.')
     , (98760412, 15, 'An exquisite masterwork incense stick.')
     , (98760412, 16, 'An exquisite masterwork incense stick.');

/* ========================================================================= */
/* 4. Nurturing Draught (98760415) - 2x juvenile maturity XP                 */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 98760415;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (98760415, 'ace98760415-nurturingdraught', 44, '2026-09-12 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 98760415;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (98760415, 11, 1)
     , (98760415, 13, 1)
     , (98760415, 14, 1);

DELETE FROM weenie_properties_int WHERE object_Id = 98760415;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (98760415,   1,    128)
     , (98760415,   5,     10)
     , (98760415,   8,     10)
     , (98760415,  11,    100)
     , (98760415,  12,      1)
     , (98760415,  13,      1)
     , (98760415,  14,      1)
     , (98760415,  16, 524296)  /* ItemUseable - SourceContainedTargetContained */
     , (98760415,  18,     10)  /* UiEffects - Magical */
     , (98760415,  19, 500000)  /* Value - 500,000 Pyreals */
     , (98760415,  93,   1044)
     , (98760415,  94,    128); /* TargetType - Misc */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 98760415;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (98760415,  1,  33554446) /* Setup - Potion bottle */
     , (98760415,  8, 100668175);/* Icon - Elixir */

DELETE FROM weenie_properties_string WHERE object_Id = 98760415;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760415,  1, 'Nurturing Draught')
     , (98760415, 14, 'Feed to a juvenile combat pet essence. Doubles the maturity kill credit it earns until it reaches adulthood.')
     , (98760415, 15, 'A glowing elixir that stimulates pet growth.')
     , (98760415, 16, 'A glowing elixir that stimulates pet growth.');

/* ========================================================================= */
/* 5. Chromatic Catalyst (98760418) - Vibrant palette mutation filter        */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 98760418;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (98760418, 'ace98760418-chromaticcatalyst', 44, '2026-09-12 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 98760418;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (98760418, 11, 1)
     , (98760418, 13, 1)
     , (98760418, 14, 1);

DELETE FROM weenie_properties_int WHERE object_Id = 98760418;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (98760418,   1,     128)
     , (98760418,   5,      10)
     , (98760418,   8,      10)
     , (98760418,  11,     100)
     , (98760418,  12,       1)
     , (98760418,  13,       1)
     , (98760418,  14,       1)
     , (98760418,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (98760418,  18,      10)  /* UiEffects - Magical */
     , (98760418,  19, 1000000)  /* Value - 1,000,000 Pyreals */
     , (98760418,  93,    1044)
     , (98760418,  94,     128); /* TargetType - Misc */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 98760418;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (98760418,  1,  33558818)
     , (98760418,  8, 100670881);/* Icon - Prism/Catalyst */

DELETE FROM weenie_properties_string WHERE object_Id = 98760418;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760418,  1, 'Chromatic Catalyst')
     , (98760418, 14, 'Use on a combat pet essence before breeding. If a palette mutation occurs, it is guaranteed to select from rare, vibrant, high-saturation colors.')
     , (98760418, 15, 'An alchemical prism that refracts pure chroma.')
     , (98760418, 16, 'An alchemical prism that refracts pure chroma.');

/* ========================================================================= */
/* 5. Stock items on Ivo, Ruggan's Quartermaster (78780201) if present        */
/* ========================================================================= */
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780201 AND `weenie_Class_Id` IN (98760410, 98760411, 98760412, 98760415, 98760418);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
SELECT 78780201, 4, items.weenie_id, 1, 0, 0, 0
FROM (
    SELECT 98760410 AS weenie_id UNION ALL
    SELECT 98760411 UNION ALL
    SELECT 98760412 UNION ALL
    SELECT 98760415 UNION ALL
    SELECT 98760418
) AS items
WHERE EXISTS (SELECT 1 FROM `weenie` WHERE `class_Id` = 78780201);