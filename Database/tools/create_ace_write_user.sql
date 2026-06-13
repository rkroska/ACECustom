-- Create local dev user for applying Quest Builder / world SQL packages.
-- Run as MySQL root (or another account with CREATE USER / GRANT).
--
-- 1) Replace YOUR_PASSWORD_HERE below with your chosen password.
-- 2) Execute this entire script in MySQL Workbench (or mysql CLI).
-- 3) Put the same password in Database/tools/.env.write (MYSQL_WORLD_PASSWORD).

-- Optional: remove old user if you are recreating it
-- DROP USER IF EXISTS 'ace_write'@'127.0.0.1';
-- DROP USER IF EXISTS 'ace_write'@'localhost';

CREATE USER IF NOT EXISTS 'ace_write'@'127.0.0.1' IDENTIFIED BY 'YOUR_PASSWORD_HERE';
CREATE USER IF NOT EXISTS 'ace_write'@'localhost' IDENTIFIED BY 'YOUR_PASSWORD_HERE';

-- Quest packages touch ace_world only (weenies, quest, emotes, landblock_instance, etc.)
GRANT SELECT, INSERT, UPDATE, DELETE ON ace_world.* TO 'ace_write'@'127.0.0.1';
GRANT SELECT, INSERT, UPDATE, DELETE ON ace_world.* TO 'ace_write'@'localhost';

FLUSH PRIVILEGES;

-- Verify (run separately as ace_write if desired):
-- SHOW GRANTS FOR 'ace_write'@'127.0.0.1';
