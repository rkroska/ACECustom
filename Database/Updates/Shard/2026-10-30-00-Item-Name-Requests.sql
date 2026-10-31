-- Item rename (owner 2026-10-30): @item-name requests share the pet_name_requests table and the portal's
-- Name Approvals page with @pet-name.
--
--   kind          0 = pet (every existing row), 1 = item (a weapon or caster)
--   fee_paid      pyreals charged when the request was made (item_rename_fee); 0 for pets
--   fee_refunded  1 once the fee has been given back (deny, auto-deny, replaced, or a failed submit). Claimed with
--                 WHERE fee_refunded = 0 before the bank is credited, so a fee is never refunded twice.
--
-- The server also adds these columns itself the first time it touches the table (NameRequestStore.EnsureSchema), so
-- running this file is optional. Safe to run with the server up; re-running is harmless (each column is added only
-- when it is missing).

DROP PROCEDURE IF EXISTS `add_name_request_column`;
DELIMITER $$
CREATE PROCEDURE `add_name_request_column`(IN col VARCHAR(64), IN ddl VARCHAR(255))
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema = DATABASE() AND table_name = 'pet_name_requests' AND column_name = col) THEN
        SET @sql = CONCAT('ALTER TABLE `pet_name_requests` ADD COLUMN ', ddl);
        PREPARE stmt FROM @sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END$$
DELIMITER ;

CALL `add_name_request_column`('kind',         '`kind` tinyint NOT NULL DEFAULT 0 AFTER `id`');
CALL `add_name_request_column`('fee_paid',     '`fee_paid` bigint NOT NULL DEFAULT 0');
CALL `add_name_request_column`('fee_refunded', '`fee_refunded` tinyint NOT NULL DEFAULT 0');

DROP PROCEDURE `add_name_request_column`;
