-- Owner ruling 2026-08-08: the prestige system keeps NO allowed landblocks except the single
-- 0xEAEA (60138) dev/test stub row per tier. Zone Control bounded zones are the sole boundary
-- authority everywhere else. The inactive stub per tier is load-bearing twice over:
--   1. A tier with any DB row loads ONLY its active rows -> empty set -> "no restrictions"
--      (PrestigeManager.IsLandblockAllowed), so prestige boundary enforcement is inert.
--   2. A tier with ZERO rows falls back to the in-code default [0xEAEA] ACTIVE (would confine
--      players), and EnsureTierSeededFromEffectiveSet re-seeds empty tiers. The stub blocks both.
-- Tier 1 (v11 / Tou Tou) had no stub before this script - it gets one here.
DELETE FROM `prestige_allowed_landblocks` WHERE `landblock` <> 60138;
INSERT INTO `prestige_allowed_landblocks` (`tier`, `landblock`, `is_active`, `updated_at`)
VALUES ('1','60138','0',UTC_TIMESTAMP())
ON DUPLICATE KEY UPDATE `is_active` = 0;
