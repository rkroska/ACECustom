-- =====================================================================================
-- Ruggan's Annex - prismatic monkey generator (78780264)
-- Target database: ace_world. Safe to re-run.
--
-- A copy of the retail TethBSDGeneratorNew (31015111): keeps 10 Nasty Brass Monkeys (260031)
-- scattered within 5 m, regenerating every 20 s. Two custom settings on top:
--   SpawnColourMutationChance (float 9061) = 0.5  - each monkey has a 50% chance of a random vivid
--                                                   mutation colour; a captured one keeps it
--   OnlyCombatPetsCanDamage   (bool 50057) = True - its monkeys only take damage from combat pets
-- The monkey model (0x02000964) is fully palette-based, so every coloured roll shows.
-- The retail 31015111 and its 34 placements in landblock 0x0103 are untouched.
--
-- Afterwards: @clearcache, then @reload-landblock inside the annex.
-- =====================================================================================

DELETE FROM `weenie` WHERE `class_Id` = 78780264;
INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780264, 'annex_prismatic_generator', 1, '2026-09-22 00:00:00');

DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780264;
INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780264,     1, True) /* Stuck */
     , (78780264,    11, True) /* IgnoreCollisions */
     , (78780264,    18, True) /* Visibility */
     , (78780264,   132, True) /* RandomizeSpawnTime */
     , (78780264, 50057, True) /* OnlyCombatPetsCanDamage - passed to every monkey it spawns */;

DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780264;
INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780264, 81,   10) /* MaxGeneratedObjects */
     , (78780264, 82,   10) /* InitGeneratedObjects */
     , (78780264, 93, 1044) /* PhysicsState */;

DELETE FROM `weenie_properties_float` WHERE `object_Id` = 78780264;
INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (78780264,   41, 20.0) /* RegenerationInterval */
     , (78780264,   43,  5.0) /* GeneratorRadius */
     , (78780264, 9061,  0.5) /* SpawnColourMutationChance - 50% per monkey */;

DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 78780264;
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780264, 1, 33555051) /* Setup - generator */
     , (78780264, 8, 100667494) /* Icon */;

DELETE FROM `weenie_properties_string` WHERE `object_Id` = 78780264;
INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780264, 1, 'Annex Prismatic Generator') /* Name */;

DELETE FROM `weenie_properties_generator` WHERE `object_Id` = 78780264;
INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (78780264, -1, 260031, 1, 10, 10, 1, 2, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0) /* Nasty Brass Monkey x10, scatter */;

-- The placement, with the next free static guid in landblock 0x0106.
DELETE FROM `landblock_instance` WHERE `weenie_Class_Id` = 78780264 AND `landblock` = 0x0106 AND `variation_Id` = 2;
SET @g = (SELECT COALESCE(MAX(`guid`), 0x70105FFF) FROM `landblock_instance` WHERE `guid` BETWEEN 0x70106000 AND 0x70106FFF);
-- Range full (1880125439 = 0x70106FFF): the two-row subquery raises error 1242 and stops the script.
SET @g = IF(@g + 1 > 1880125439, (SELECT 1 UNION SELECT 2), @g);
INSERT INTO `landblock_instance` (`guid`, `weenie_Class_Id`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `is_Link_Child`, `last_Modified`, `variation_Id`)
VALUES (@g + 1, 78780264, 0x01060163, 79.9978, -58.5984, -5.945, 0.999963, 0, 0, -0.00857348, False, NOW(), 2);
