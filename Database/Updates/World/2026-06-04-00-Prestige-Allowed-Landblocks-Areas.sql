-- Add area name and custom boundary fields to prestige_allowed_landblocks table
ALTER TABLE `prestige_allowed_landblocks`
ADD COLUMN `area_name` varchar(100) NOT NULL DEFAULT 'Default',
ADD COLUMN `boundary_wcid` int(11) NULL DEFAULT NULL,
ADD COLUMN `boundary_scale` float NULL DEFAULT NULL,
ADD COLUMN `boundary_script_id` int(11) NULL DEFAULT NULL,
ADD COLUMN `is_wiped` tinyint(1) NOT NULL DEFAULT 0;

-- Update the existing Tou Tou v11 landblocks to use 'Tou Tou' as their area_name
UPDATE `prestige_allowed_landblocks`
SET `area_name` = 'Tou Tou'
WHERE `tier` = 1 AND `landblock` IN (
    62809, 63064, 63320, 63576, 63575, 63319, 63318, 63062, 63063, 62807, 
    62806, 62551, 62552, 62297, 62296, 62295, 62039, 62040, 62041, 62042, 
    61786, 61787, 62043, 62299, 62300, 62557, 62558, 62814, 62815, 63071, 
    63327, 63583, 63839, 63838, 63837, 63836, 63835, 63579, 63578, 63577, 
    61782, 61783, 61784, 61785, 61788, 61789, 61790, 61791, 62038, 62044, 
    62045, 62046, 62047, 62294, 62298, 62301, 62302, 62303, 62550, 62553, 
    62554, 62555, 62556, 62559, 62808, 62810, 62811, 62812, 62813, 63065, 
    63066, 63067, 63068, 63069, 63070, 63321, 63322, 63323, 63324, 63325, 
    63326, 63574, 63580, 63581, 63582, 63830, 63831, 63832, 63833, 63834
);
