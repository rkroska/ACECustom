-- Carry-over: instance deletions replicating the test server state.
-- (a) 11 explicit-v0 rows on 0x0010 (variation-unattackable fix, 2026-07). These are ILT's
--     live Week 8/9 event content - owner decision 2026-08-08 (option 2): delete the
--     explicit-v0 originals AND RE-ADD THE SAME 11 OBJECTS AT NULL VARIATION below, so the
--     content stays visible in the base world once the 0-equals-null engine fix makes
--     explicit-v0 unreachable. Same guids, same positions, only variation_Id changes 0->NULL.
-- (b) 12 T10 gen rows in the Tou Tou F-range removed by the v11 restructure (kept deleted:
--     the island is the custom content area; new mob design replaces spawns there).
DELETE FROM `landblock_instance` WHERE `guid` IN (
  0x70010171, 0x70010172, 0x70010173, 0x70010174, 0x70010175, 0x70010177,
  0x70010178, 0x70010179, 0x7001017A, 0x7001017B, 0x7001017C,
  0x7F562014, 0x7F563015, 0x7F65900A, 0x7F65A004, 0x7F65B008, 0x7F65C3FC,
  0x7F762024, 0x7F76B139, 0x7F76C020, 0x7F86301A, 0x7F963081, 0x7F965398
);

INSERT INTO `landblock_instance` (`guid`, `weenie_Class_Id`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `is_Link_Child`, `last_Modified`, `variation_Id`)
VALUES (1879114097,3110099,1049337,193.099,-280.065,-5.945,-0.726731,0,0,-0.686923,0,'2026-02-15 20:46:32',NULL)
     , (1879114098,3110099,1049334,189.985,-264.346,-5.945,0.0219561,0,0,0.999759,0,'2026-02-15 20:46:47',NULL)
     , (1879114099,98760041,1049337,189.136,-279.958,-5.945,-0.722136,0,0,0.691751,0,'2026-02-15 20:47:02',NULL)
     , (1879114100,98760020,1049338,189.981,-275.434,-5.945,-0.999954,0,0,0.00958709,0,'2026-02-15 20:47:14',NULL)
     , (1879114101,98760020,1049338,190.208,-276.219,-5.945,-0.999954,0,0,0.00958709,0,'2026-02-15 20:47:19',NULL)
     , (1879114103,694200710,1049337,190.881,-279.088,-5.9785,-0.656027,0,0,0.754738,0,'2026-02-15 21:08:32',NULL)
     , (1879114104,3110099,1049427,85.299,-230.12,0.055,-0.70278,0,0,-0.711407,0,'2026-02-15 22:23:24',NULL)
     , (1879114105,3110099,1049414,71.9618,-229.955,0.055,-0.690583,0,0,0.723254,0,'2026-02-15 22:23:38',NULL)
     , (1879114106,15774,1049423,82.8518,-225.997,0.005,0.343091,0,0,0.939302,0,'2026-02-15 22:24:06',NULL)
     , (1879114107,98760044,1049423,82.9664,-229.886,-0.063,-0.686704,0,0,0.726937,0,'2026-02-15 22:25:08',NULL)
     , (1879114108,98760043,1049336,190.488,-265.555,-5.995,0.0344509,0,0,0.999406,0,'2026-02-16 01:15:20',NULL);
