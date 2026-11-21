/* Skill Credit Gems (98760540-98760543): using one adds 1, 10, 50 or 100 skill credits to the character for good. The
   Ancient Statue of unknown origin (300002) sells them for 100 Enlightened Coins per credit: 100, 1,000, 5,000 and 10,000
   (its shop change is 2026-11-21-01-Ancient-Statue-Shop.sql). The Specialized Skill Augmentation Gems are unchanged:
   they still only raise the specialization limit.

   The code is in Player.UseSkillCreditGem (Player_SkillCreditGems.cs), reached from Gem.ActOnUse by WCID
   (Player.SkillCreditGems lists the four gems and their credits). It only takes a gem from the pack, uses it up first,
   then adds its credits to the available and total skill credits and to PropertyInt.SkillCreditsFromGems (50606).
   Enlightenment.RemoveSkills adds SkillCreditsFromGems back when it rebuilds the pool, so the credits are kept through
   enlightenment, and verify-skill-credits counts them. These weenies have no emotes. UnlimitedUse keeps Gem.UseGem from
   ever using one up, so on a server without the code a use does nothing and the gem is kept.

   Look: model, icon and the other data ids are copied at import time from the Specialized Skill Augmentation Gem of the
   same size (300021, 81000131, 81000141, 81000151); UiEffects BoostStamina instead of Magical gives them a different
   border. Weight, Attuned (no dropping, trading or giving) and death behavior (Bonded -1, Slippery) are those gems'. No
   level requirement and no cooldown: a character can use them right after enlightening.

   Convention: narrow DELETE (class_Id AND class_Name), child rows go with the weenie (ON DELETE CASCADE), all text is
   7-bit ASCII. Run against ace_world. Safe to re-run. WCIDs 98760540-98760543 are free in the 2026-11-09 dump; check
   live first:
   SELECT class_Id, class_Name FROM weenie WHERE class_Id BETWEEN 98760540 AND 98760543;
   Restart the server (or @clearcache) after importing. */

/* Skill Credit Gem: +1 (100 Enlightened Coins) */
DELETE FROM weenie WHERE class_Id = 98760540 AND class_Name = 'ace98760540-skillcreditgem';
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (98760540, 'ace98760540-skillcreditgem', 38 /* Gem */, '2026-11-21 00:00:00');

INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (98760540,   1,  2048) /* ItemType - Gem */
     , (98760540,   3,    80) /* PaletteTemplate */
     , (98760540,   5,    10) /* EncumbranceVal */
     , (98760540,   8,    10) /* Mass */
     , (98760540,  11,     1) /* MaxStackSize */
     , (98760540,  12,     1) /* StackSize */
     , (98760540,  16,     8) /* ItemUseable - Contained */
     , (98760540,  18,    16) /* UiEffects - BoostStamina */
     , (98760540,  19,   100) /* Value - 100 Enlightened Coins at the Ancient Statue (SellPrice 1.0) */
     , (98760540,  33,    -1) /* Bonded - Slippery, as the augmentation gems */
     , (98760540,  93,  1044) /* PhysicsState - Ethereal, IgnoreCollisions, Gravity */
     , (98760540,  94,    16) /* TargetType - Creature */
     , (98760540, 114,     1); /* Attuned - no trading or giving */

INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (98760540, 22, 1)  /* Inscribable */
     , (98760540, 63, 1); /* UnlimitedUse - Player.UseSkillCreditGem uses the gem up; Gem.UseGem never does */

INSERT INTO weenie_properties_float (object_Id, type, value)
VALUES (98760540, 76, 0.5); /* Translucency */

/* Model, icon and the other data ids of the 300021 Specialized Skill Augmentation Gem */
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
SELECT 98760540, type, value FROM weenie_properties_d_i_d WHERE object_Id = 300021;

INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760540,  1, 'Skill Credit Gem')
     , (98760540, 14, 'Double Click gem to gain 1 skill credit for good.')
     , (98760540, 15, 'A Gem, humming with lessons not yet learned.')
     , (98760540, 16, 'Using this gem adds 1 to your skill credits for good: skill credits from these gems stay with you through enlightenment.')
     , (98760540, 20, 'Skill Credit Gems');

/* 10x Skill Credit Gem: +10 (1,000 Enlightened Coins) */
DELETE FROM weenie WHERE class_Id = 98760541 AND class_Name = 'ace98760541-10xskillcreditgem';
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (98760541, 'ace98760541-10xskillcreditgem', 38 /* Gem */, '2026-11-21 00:00:00');

INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (98760541,   1,  2048) /* ItemType - Gem */
     , (98760541,   3,    80) /* PaletteTemplate */
     , (98760541,   5,    10) /* EncumbranceVal */
     , (98760541,   8,    10) /* Mass */
     , (98760541,  11,     1) /* MaxStackSize */
     , (98760541,  12,     1) /* StackSize */
     , (98760541,  16,     8) /* ItemUseable - Contained */
     , (98760541,  18,    16) /* UiEffects - BoostStamina */
     , (98760541,  19,  1000) /* Value - 1,000 Enlightened Coins at the Ancient Statue (SellPrice 1.0) */
     , (98760541,  33,    -1) /* Bonded - Slippery, as the augmentation gems */
     , (98760541,  93,  1044) /* PhysicsState - Ethereal, IgnoreCollisions, Gravity */
     , (98760541,  94,    16) /* TargetType - Creature */
     , (98760541, 114,     1); /* Attuned - no trading or giving */

INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (98760541, 22, 1)  /* Inscribable */
     , (98760541, 63, 1); /* UnlimitedUse - Player.UseSkillCreditGem uses the gem up; Gem.UseGem never does */

INSERT INTO weenie_properties_float (object_Id, type, value)
VALUES (98760541, 76, 0.5); /* Translucency */

/* Model, icon and the other data ids of the 81000131 Specialized Skill Augmentation Gem */
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
SELECT 98760541, type, value FROM weenie_properties_d_i_d WHERE object_Id = 81000131;

INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760541,  1, '10x Skill Credit Gem')
     , (98760541, 14, 'Double Click gem to gain 10 skill credits for good.')
     , (98760541, 15, 'A Gem, humming with lessons not yet learned.')
     , (98760541, 16, 'Using this gem adds 10 to your skill credits for good: skill credits from these gems stay with you through enlightenment.')
     , (98760541, 20, '10x Skill Credit Gems');

/* 50x Skill Credit Gem: +50 (5,000 Enlightened Coins) */
DELETE FROM weenie WHERE class_Id = 98760542 AND class_Name = 'ace98760542-50xskillcreditgem';
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (98760542, 'ace98760542-50xskillcreditgem', 38 /* Gem */, '2026-11-21 00:00:00');

INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (98760542,   1,  2048) /* ItemType - Gem */
     , (98760542,   3,    80) /* PaletteTemplate */
     , (98760542,   5,    10) /* EncumbranceVal */
     , (98760542,   8,    10) /* Mass */
     , (98760542,  11,     1) /* MaxStackSize */
     , (98760542,  12,     1) /* StackSize */
     , (98760542,  16,     8) /* ItemUseable - Contained */
     , (98760542,  18,    16) /* UiEffects - BoostStamina */
     , (98760542,  19,  5000) /* Value - 5,000 Enlightened Coins at the Ancient Statue (SellPrice 1.0) */
     , (98760542,  33,    -1) /* Bonded - Slippery, as the augmentation gems */
     , (98760542,  93,  1044) /* PhysicsState - Ethereal, IgnoreCollisions, Gravity */
     , (98760542,  94,    16) /* TargetType - Creature */
     , (98760542, 114,     1); /* Attuned - no trading or giving */

INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (98760542, 22, 1)  /* Inscribable */
     , (98760542, 63, 1); /* UnlimitedUse - Player.UseSkillCreditGem uses the gem up; Gem.UseGem never does */

INSERT INTO weenie_properties_float (object_Id, type, value)
VALUES (98760542, 76, 0.5); /* Translucency */

/* Model, icon and the other data ids of the 81000141 Specialized Skill Augmentation Gem */
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
SELECT 98760542, type, value FROM weenie_properties_d_i_d WHERE object_Id = 81000141;

INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760542,  1, '50x Skill Credit Gem')
     , (98760542, 14, 'Double Click gem to gain 50 skill credits for good.')
     , (98760542, 15, 'A Gem, humming with lessons not yet learned.')
     , (98760542, 16, 'Using this gem adds 50 to your skill credits for good: skill credits from these gems stay with you through enlightenment.')
     , (98760542, 20, '50x Skill Credit Gems');

/* 100x Skill Credit Gem: +100 (10,000 Enlightened Coins) */
DELETE FROM weenie WHERE class_Id = 98760543 AND class_Name = 'ace98760543-100xskillcreditgem';
INSERT INTO weenie (class_Id, class_Name, type, last_Modified)
VALUES (98760543, 'ace98760543-100xskillcreditgem', 38 /* Gem */, '2026-11-21 00:00:00');

INSERT INTO weenie_properties_int (object_Id, type, value)
VALUES (98760543,   1,  2048) /* ItemType - Gem */
     , (98760543,   3,    80) /* PaletteTemplate */
     , (98760543,   5,    10) /* EncumbranceVal */
     , (98760543,   8,    10) /* Mass */
     , (98760543,  11,     1) /* MaxStackSize */
     , (98760543,  12,     1) /* StackSize */
     , (98760543,  16,     8) /* ItemUseable - Contained */
     , (98760543,  18,    16) /* UiEffects - BoostStamina */
     , (98760543,  19, 10000) /* Value - 10,000 Enlightened Coins at the Ancient Statue (SellPrice 1.0) */
     , (98760543,  33,    -1) /* Bonded - Slippery, as the augmentation gems */
     , (98760543,  93,  1044) /* PhysicsState - Ethereal, IgnoreCollisions, Gravity */
     , (98760543,  94,    16) /* TargetType - Creature */
     , (98760543, 114,     1); /* Attuned - no trading or giving */

INSERT INTO weenie_properties_bool (object_Id, type, value)
VALUES (98760543, 22, 1)  /* Inscribable */
     , (98760543, 63, 1); /* UnlimitedUse - Player.UseSkillCreditGem uses the gem up; Gem.UseGem never does */

INSERT INTO weenie_properties_float (object_Id, type, value)
VALUES (98760543, 76, 0.5); /* Translucency */

/* Model, icon and the other data ids of the 81000151 Specialized Skill Augmentation Gem */
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
SELECT 98760543, type, value FROM weenie_properties_d_i_d WHERE object_Id = 81000151;

INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760543,  1, '100x Skill Credit Gem')
     , (98760543, 14, 'Double Click gem to gain 100 skill credits for good.')
     , (98760543, 15, 'A Gem, humming with lessons not yet learned.')
     , (98760543, 16, 'Using this gem adds 100 to your skill credits for good: skill credits from these gems stay with you through enlightenment.')
     , (98760543, 20, '100x Skill Credit Gems');
