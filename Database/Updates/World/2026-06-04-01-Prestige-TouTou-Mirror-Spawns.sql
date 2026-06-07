-- Clear all variation 11 spawns and links to keep it a blank slate
DELETE l FROM `landblock_instance_link` l
INNER JOIN `landblock_instance` p ON l.`parent_GUID` = p.`guid`
WHERE p.`variation_Id` = 11;

DELETE FROM `landblock_instance`
WHERE `variation_Id` = 11;
