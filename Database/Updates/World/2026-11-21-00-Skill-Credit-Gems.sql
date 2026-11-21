/* Skill Credit Gems (98760540-98760543): using one adds 1, 10, 50 or 100 to the character's available (and total) skill
   credits. The Ancient Statue of unknown origin (300002) sells them for 100 Enlightened Coins per credit: 100, 1,000, 5,000
   and 10,000 (its shop change is 2026-11-21-01-Ancient-Statue-Shop.sql). The Specialized Skill Augmentation Gems are
   unchanged: they still only raise the specialization limit.

   Data only, no code. The gem's Use emote awards the credits (AwardTrainingCredits -> Player.AddSkillCredits, which
   says "You have been awarded N additional skill credits.") and then takes one of these gems from the pack (TakeItems).
   Both steps have delay 0, so they run in the same call, before Gem.ActOnUse (the Use emote runs first, in
   WorldObject.OnActivate). A player can't drop an Attuned gem, put it in a chest, trade it or give it, so a gem a player
   uses is in their pack and TakeItems finds it. (A gem an admin @creates on the ground could be used without being taken:
   hand them out with @ci, into the pack.) UnlimitedUse keeps Gem.UseGem from taking a second gem when TakeItems took
   another of the same kind (UseGem already skips a gem that is gone). The Specialized Skill Augmentation Gems are used up
   by their emote the same way (PromptAddAugment, also with UnlimitedUse).

   Enlightenment: these credits are part of the available pool, which Enlightenment.RemoveSkills rebuilds from the
   heritage and quest credits, so they are not kept through enlightenment. The appraisal text says so.

   Look: model, icon and the other data ids are copied at import time from the Specialized Skill Augmentation Gem of the
   same size (300021, 81000131, 81000141, 81000151); UiEffects BoostStamina instead of Magical gives them a different
   border. Weight, Attuned (no trading or giving) and death behavior (Bonded -1, Slippery) are those gems'. No level
   requirement and no cooldown: a character can use them right after enlightening.

   Convention: narrow DELETE (class_Id AND class_Name), child rows go with the weenie (ON DELETE CASCADE), only @parent_id
   is used, all text is 7-bit ASCII. Run against ace_world. Safe to re-run. WCIDs 98760540-98760543 are free in the
   2026-11-09 dump; check live first:
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
     , (98760540, 63, 1); /* UnlimitedUse - the Use emote takes the gem; Gem.UseGem must not take another */

INSERT INTO weenie_properties_float (object_Id, type, value)
VALUES (98760540, 76, 0.5); /* Translucency */

/* Model, icon and the other data ids of the 300021 Specialized Skill Augmentation Gem */
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
SELECT 98760540, type, value FROM weenie_properties_d_i_d WHERE object_Id = 300021;

INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760540,  1, 'Skill Credit Gem')
     , (98760540, 14, 'Double Click gem to gain 1 skill credit.')
     , (98760540, 15, 'A Gem, humming with lessons not yet learned.')
     , (98760540, 16, 'Using this gem adds 1 to your available skill credits. Skill credits from these gems are not kept through enlightenment.')
     , (98760540, 20, 'Skill Credit Gems');

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id, style, substyle, quest, vendor_Type, min_Health, max_Health)
VALUES (98760540, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, amount, weenie_Class_Id, stack_Size)
VALUES (@parent_id, 0, 47 /* AwardTrainingCredits */, 0, 1,   1, NULL, NULL)
     , (@parent_id, 1, 74 /* TakeItems */,            0, 1, NULL, 98760540, 1);

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
     , (98760541, 63, 1); /* UnlimitedUse - the Use emote takes the gem; Gem.UseGem must not take another */

INSERT INTO weenie_properties_float (object_Id, type, value)
VALUES (98760541, 76, 0.5); /* Translucency */

/* Model, icon and the other data ids of the 81000131 Specialized Skill Augmentation Gem */
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
SELECT 98760541, type, value FROM weenie_properties_d_i_d WHERE object_Id = 81000131;

INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760541,  1, '10x Skill Credit Gem')
     , (98760541, 14, 'Double Click gem to gain 10 skill credits.')
     , (98760541, 15, 'A Gem, humming with lessons not yet learned.')
     , (98760541, 16, 'Using this gem adds 10 to your available skill credits. Skill credits from these gems are not kept through enlightenment.')
     , (98760541, 20, '10x Skill Credit Gems');

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id, style, substyle, quest, vendor_Type, min_Health, max_Health)
VALUES (98760541, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, amount, weenie_Class_Id, stack_Size)
VALUES (@parent_id, 0, 47 /* AwardTrainingCredits */, 0, 1,  10, NULL, NULL)
     , (@parent_id, 1, 74 /* TakeItems */,            0, 1, NULL, 98760541, 1);

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
     , (98760542, 63, 1); /* UnlimitedUse - the Use emote takes the gem; Gem.UseGem must not take another */

INSERT INTO weenie_properties_float (object_Id, type, value)
VALUES (98760542, 76, 0.5); /* Translucency */

/* Model, icon and the other data ids of the 81000141 Specialized Skill Augmentation Gem */
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
SELECT 98760542, type, value FROM weenie_properties_d_i_d WHERE object_Id = 81000141;

INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760542,  1, '50x Skill Credit Gem')
     , (98760542, 14, 'Double Click gem to gain 50 skill credits.')
     , (98760542, 15, 'A Gem, humming with lessons not yet learned.')
     , (98760542, 16, 'Using this gem adds 50 to your available skill credits. Skill credits from these gems are not kept through enlightenment.')
     , (98760542, 20, '50x Skill Credit Gems');

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id, style, substyle, quest, vendor_Type, min_Health, max_Health)
VALUES (98760542, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, amount, weenie_Class_Id, stack_Size)
VALUES (@parent_id, 0, 47 /* AwardTrainingCredits */, 0, 1,  50, NULL, NULL)
     , (@parent_id, 1, 74 /* TakeItems */,            0, 1, NULL, 98760542, 1);

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
     , (98760543, 63, 1); /* UnlimitedUse - the Use emote takes the gem; Gem.UseGem must not take another */

INSERT INTO weenie_properties_float (object_Id, type, value)
VALUES (98760543, 76, 0.5); /* Translucency */

/* Model, icon and the other data ids of the 81000151 Specialized Skill Augmentation Gem */
INSERT INTO weenie_properties_d_i_d (object_Id, type, value)
SELECT 98760543, type, value FROM weenie_properties_d_i_d WHERE object_Id = 81000151;

INSERT INTO weenie_properties_string (object_Id, type, value)
VALUES (98760543,  1, '100x Skill Credit Gem')
     , (98760543, 14, 'Double Click gem to gain 100 skill credits.')
     , (98760543, 15, 'A Gem, humming with lessons not yet learned.')
     , (98760543, 16, 'Using this gem adds 100 to your available skill credits. Skill credits from these gems are not kept through enlightenment.')
     , (98760543, 20, '100x Skill Credit Gems');

INSERT INTO weenie_properties_emote (object_Id, category, probability, weenie_Class_Id, style, substyle, quest, vendor_Type, min_Health, max_Health)
VALUES (98760543, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO weenie_properties_emote_action (emote_Id, `order`, type, delay, extent, amount, weenie_Class_Id, stack_Size)
VALUES (@parent_id, 0, 47 /* AwardTrainingCredits */, 0, 1, 100, NULL, NULL)
     , (@parent_id, 1, 74 /* TakeItems */,            0, 1, NULL, 98760543, 1);
