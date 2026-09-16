/* Pet Breeding Sinks: Courtship Incense, Nurturing Draughts, and Chromatic Catalysts.
   Run against ace_world. Safe to re-run (deletes then inserts). */

/* ========================================================================= */
/* 1. Lesser Courtship Incense (78780250) - +2.5% mutation chance            */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780250;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780250, 'ace78780250-lessercourtshipincense', 44, '2026-09-12 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780250;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780250, 11, 1)  /* IgnoreCollisions */
     , (78780250, 13, 1)  /* Ethereal */
     , (78780250, 14, 1); /* GravityStatus */

DELETE FROM weenie_properties_int WHERE object_Id = 78780250;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780250,   1,    128)  /* ItemType - Misc */
     , (78780250,   5,      5)  /* EncumbranceVal */
     , (78780250,   8,      5)  /* Mass */
     , (78780250,  11,    100)  /* MaxStackSize */
     , (78780250,  12,      1)  /* StackSize */
     , (78780250,  13,      1)  /* StackUnitEncumbrance */
     , (78780250,  14,      1)  /* StackUnitMass */
     , (78780250,  16, 524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780250,  18,     10)  /* UiEffects - Magical */
     , (78780250,  19, 100000)  /* Value - 100,000 Pyreals */
     , (78780250,  93,   1044)  /* PhysicsState */
     , (78780250,  94,    128); /* TargetType - Misc (PetDevice) */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780250;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780250,  1,  33558818) /* Setup */
     , (78780250,  8, 100670879);/* Icon */

DELETE FROM weenie_properties_string WHERE object_Id = 78780250;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780250,  1, 'Lesser Courtship Incense')
     , (78780250, 14, 'Use on a combat pet essence before breeding to prime it. Grants +2.5% mutation chance on the next breeding attempt.')
     , (78780250, 15, 'A fragrant ceremonial incense stick.')
     , (78780250, 16, 'A fragrant ceremonial incense stick.');

/* ========================================================================= */
/* 2. Refined Courtship Incense (78780251) - +5.0% mutation chance           */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780251;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780251, 'ace78780251-refinedcourtshipincense', 44, '2026-09-12 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780251;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780251, 11, 1)
     , (78780251, 13, 1)
     , (78780251, 14, 1);

DELETE FROM weenie_properties_int WHERE object_Id = 78780251;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780251,   1,    128)
     , (78780251,   5,      5)
     , (78780251,   8,      5)
     , (78780251,  11,    100)
     , (78780251,  12,      1)
     , (78780251,  13,      1)
     , (78780251,  14,      1)
     , (78780251,  16, 524296)
     , (78780251,  18,     10)
     , (78780251,  19, 500000)  /* Value - 500,000 Pyreals */
     , (78780251,  93,   1044)
     , (78780251,  94,    128);

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780251;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780251,  1,  33558818)
     , (78780251,  8, 100670879)
     , (78780251, 22, 872415275); /* IconOverlay */

DELETE FROM weenie_properties_string WHERE object_Id = 78780251;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780251,  1, 'Refined Courtship Incense')
     , (78780251, 14, 'Use on a combat pet essence before breeding to prime it. Grants +5.0% mutation chance on the next breeding attempt.')
     , (78780251, 15, 'A potent ceremonial incense stick.')
     , (78780251, 16, 'A potent ceremonial incense stick.');

/* ========================================================================= */
/* 3. Exquisite Courtship Incense (78780252) - +10.0% mutation chance        */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780252;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780252, 'ace78780252-exquisitecourtshipincense', 44, '2026-09-12 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780252;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780252, 11, 1)
     , (78780252, 13, 1)
     , (78780252, 14, 1);

DELETE FROM weenie_properties_int WHERE object_Id = 78780252;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780252,   1,     128)
     , (78780252,   5,       5)
     , (78780252,   8,       5)
     , (78780252,  11,     100)
     , (78780252,  12,       1)
     , (78780252,  13,       1)
     , (78780252,  14,       1)
     , (78780252,  16,  524296)
     , (78780252,  18,      10)
     , (78780252,  19, 2500000) /* Value - 2,500,000 Pyreals */
     , (78780252,  93,    1044)
     , (78780252,  94,     128);

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780252;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780252,  1,  33558818)
     , (78780252,  8, 100670879)
     , (78780252, 22, 100671392); /* IconOverlay */

DELETE FROM weenie_properties_string WHERE object_Id = 78780252;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780252,  1, 'Exquisite Courtship Incense')
     , (78780252, 14, 'Use on a combat pet essence before breeding to prime it. Grants +10.0% mutation chance on the next breeding attempt.')
     , (78780252, 15, 'An exquisite masterwork incense stick.')
     , (78780252, 16, 'An exquisite masterwork incense stick.');

/* ========================================================================= */
/* 4. Nurturing Draught (78780253) - 2x juvenile maturity XP                 */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780253;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780253, 'ace78780253-nurturingdraught', 44, '2026-09-12 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780253;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780253, 11, 1)
     , (78780253, 13, 1)
     , (78780253, 14, 1);

DELETE FROM weenie_properties_int WHERE object_Id = 78780253;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780253,   1,    128)
     , (78780253,   5,     10)
     , (78780253,   8,     10)
     , (78780253,  11,    100)
     , (78780253,  12,      1)
     , (78780253,  13,      1)
     , (78780253,  14,      1)
     , (78780253,  16, 524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780253,  18,     10)  /* UiEffects - Magical */
     , (78780253,  19, 500000)  /* Value - 500,000 Pyreals */
     , (78780253,  93,   1044)
     , (78780253,  94,    128); /* TargetType - Misc */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780253;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780253,  1,  33554446) /* Setup - Potion bottle */
     , (78780253,  8, 100668175);/* Icon - Elixir */

DELETE FROM weenie_properties_string WHERE object_Id = 78780253;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780253,  1, 'Nurturing Draught')
     , (78780253, 14, 'Feed to a juvenile combat pet essence. Doubles the maturity kill credit it earns until it reaches adulthood.')
     , (78780253, 15, 'A glowing elixir that stimulates pet growth.')
     , (78780253, 16, 'A glowing elixir that stimulates pet growth.');

/* ========================================================================= */
/* 5. Chromatic Catalyst (78780254) - Vibrant palette mutation filter        */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780254;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780254, 'ace78780254-chromaticcatalyst', 44, '2026-09-12 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780254;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780254, 11, 1)
     , (78780254, 13, 1)
     , (78780254, 14, 1);

DELETE FROM weenie_properties_int WHERE object_Id = 78780254;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780254,   1,     128)
     , (78780254,   5,      10)
     , (78780254,   8,      10)
     , (78780254,  11,     100)
     , (78780254,  12,       1)
     , (78780254,  13,       1)
     , (78780254,  14,       1)
     , (78780254,  16,  524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780254,  18,      10)  /* UiEffects - Magical */
     , (78780254,  19, 1000000)  /* Value - 1,000,000 Pyreals */
     , (78780254,  93,    1044)
     , (78780254,  94,     128); /* TargetType - Misc */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780254;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780254,  1,  33558818)
     , (78780254,  8, 100670881);/* Icon - Prism/Catalyst */

DELETE FROM weenie_properties_string WHERE object_Id = 78780254;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780254,  1, 'Chromatic Catalyst')
     , (78780254, 14, 'Use on a combat pet essence before breeding. If a palette mutation occurs, it is guaranteed to select from rare, vibrant, high-saturation colors.')
     , (78780254, 15, 'An alchemical prism that refracts pure chroma.')
     , (78780254, 16, 'An alchemical prism that refracts pure chroma.');

/* ========================================================================= */
/* 6. Offering of Subjugation (78780255) - Weakens mating guardian           */
/* ========================================================================= */
DELETE FROM weenie WHERE class_Id = 78780255;
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (78780255, 'ace78780255-offeringofsubjugation', 44, '2026-09-12 00:00:00');

DELETE FROM weenie_properties_bool WHERE object_Id = 78780255;
INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (78780255, 11, 1)
     , (78780255, 13, 1)
     , (78780255, 14, 1);

DELETE FROM weenie_properties_int WHERE object_Id = 78780255;
INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (78780255,   1,    128)  /* ItemType - Misc */
     , (78780255,   5,     10)
     , (78780255,   8,     10)
     , (78780255,  11,    100)  /* MaxStackSize */
     , (78780255,  12,      1)  /* StackSize */
     , (78780255,  13,      1)
     , (78780255,  14,      1)
     , (78780255,  16, 524296)  /* ItemUseable - SourceContainedTargetContained */
     , (78780255,  18,     10)  /* UiEffects - Magical */
     , (78780255,  19, 500000)  /* Value - 500,000 Pyreals */
     , (78780255,  93,   1044)
     , (78780255,  94,    128); /* TargetType - Misc */

DELETE FROM weenie_properties_d_i_d WHERE object_Id = 78780255;
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
VALUES (78780255,  1,  33558818)
     , (78780255,  8, 100670879);/* Icon */

DELETE FROM weenie_properties_string WHERE object_Id = 78780255;
INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (78780255,  1, 'Offering of Subjugation')
     , (78780255, 14, 'Cast into the hearth or anoint onto a pet device before breeding to weaken its mating guardian and guarantee victory.')
     , (78780255, 15, 'Cast into the hearth or anoint onto a pet device before breeding to weaken its mating guardian and guarantee victory.')
     , (78780255, 16, 'Cast into the hearth or anoint onto a pet device before breeding to weaken its mating guardian and guarantee victory.');

/* ========================================================================= */
/* 7. Stock items on Ivo, Ruggan's Quartermaster (78780201) if present        */
/* ========================================================================= */
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780201 AND `weenie_Class_Id` IN (78780250, 78780251, 78780252, 78780253, 78780254, 78780255);
INSERT INTO `weenie_properties_create_list` (`object_Id`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`)
SELECT 78780201, 4, items.weenie_id, 1, 0, 0, 0
FROM (
    SELECT 78780250 AS weenie_id UNION ALL
    SELECT 78780251 UNION ALL
    SELECT 78780252 UNION ALL
    SELECT 78780253 UNION ALL
    SELECT 78780254 UNION ALL
    SELECT 78780255
) AS items
WHERE EXISTS (SELECT 1 FROM `weenie` WHERE `class_Id` = 78780201);
