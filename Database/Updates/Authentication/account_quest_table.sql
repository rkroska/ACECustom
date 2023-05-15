CREATE TABLE `ace_auth`.`account_quest` (
  `accountId` INT NOT NULL,
  `quest` VARCHAR(255) NOT NULL,
  PRIMARY KEY (`accountId`, `quest`));


  ALTER TABLE `ace_auth`.`account_quest` 
ADD COLUMN `num_Times_Completed` INT UNSIGNED NULL AFTER `quest`,
CHANGE COLUMN `accountId` `accountId` INT UNSIGNED NOT NULL ;
