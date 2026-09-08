-- Owner ruling 2026-08-08: the Tou Tou mob layer was removed (mobs to be rebuilt from
-- scratch). Seven of the benched camp gens are wcids ILT ALSO has from an earlier sync,
-- orphaned on both sides (zero instance refs verified at migration) - delete them on ILT
-- at merge so no stale copies survive. Idempotent; on the local DB this is a no-op after
-- the 2026-08-08 prune. Archive: Migration pruned-archive\toutou-mobs-2026-08-08.
-- Referential check (2026-09-05): on the 2026-08-07 live dump and on the test shard, NO row in any table names these
-- seven ids - not landblock_instance, encounter, points_of_interest, treasure_wielded, nor any weenie_properties_*
-- generator / create_list / emote_action row. The deletes below therefore remove nothing today; they are here so
-- the weenie delete can never leave a dangling non-cascading reference if that ever changes.
DELETE FROM `landblock_instance` WHERE `weenie_Class_Id` IN (730000078, 730000079, 730000084, 730000085, 730000086, 730000087, 730000091);
DELETE FROM `encounter` WHERE `weenie_Class_Id` IN (730000078, 730000079, 730000084, 730000085, 730000086, 730000087, 730000091);
DELETE FROM `points_of_interest` WHERE `weenie_Class_Id` IN (730000078, 730000079, 730000084, 730000085, 730000086, 730000087, 730000091);
DELETE FROM `treasure_wielded` WHERE `weenie_Class_Id` IN (730000078, 730000079, 730000084, 730000085, 730000086, 730000087, 730000091);
DELETE FROM `weenie_properties_generator` WHERE `weenie_Class_Id` IN (730000078, 730000079, 730000084, 730000085, 730000086, 730000087, 730000091);
DELETE FROM `weenie_properties_create_list` WHERE `weenie_Class_Id` IN (730000078, 730000079, 730000084, 730000085, 730000086, 730000087, 730000091);
DELETE FROM `weenie`
WHERE `class_Id` IN (730000078, 730000079, 730000084, 730000085, 730000086, 730000087, 730000091);
