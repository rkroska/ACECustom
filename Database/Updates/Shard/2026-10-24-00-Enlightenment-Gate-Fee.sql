-- Enlightenment gate fee (owner 2026-10-24).
--
-- The 150 and 300 enlightenment gates now also accept a flag bought with a fee, as an expensive way around the
-- Paragon. quest_stamp may list several flags separated by '|'; any one of them passes the tier.
--
--   targets 151-300 : ParagonEnlCompleted   or EnlGate150Bypass
--   targets 301+    : ParagonArmorCompleted or EnlGate300Bypass
--
-- The fee itself lives in config_properties_long (enl_gate_150_* / enl_gate_300_*) and the on/off switch in
-- enl_gate_fee_enabled; until changed with /modifylong or /modifybool they use the defaults in code.
--
-- Safe to run with the server up. Afterwards run @reload-enlightenment-tiers in game (or restart) so the
-- server reads the new rows. Re-running is harmless: the WHERE clauses only match the old values.

UPDATE `config_enlightenment_tier`
SET `quest_stamp` = 'ParagonEnlCompleted|EnlGate150Bypass'
WHERE `quest_stamp` = 'ParagonEnlCompleted';

UPDATE `config_enlightenment_tier`
SET `quest_stamp` = 'ParagonArmorCompleted|EnlGate300Bypass'
WHERE `quest_stamp` = 'ParagonArmorCompleted';
