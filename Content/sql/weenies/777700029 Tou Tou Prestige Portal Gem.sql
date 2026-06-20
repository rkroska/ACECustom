DELETE FROM `weenie` WHERE `class_Id` = 777700029;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (777700029, '777700029TouTouPrestigeRecallGem', 38, '2026-06-04 23:25:00') /* Gem */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (777700029,   1,       2048) /* ItemType - Gem */
     , (777700029,   3,          8) /* PaletteTemplate */
     , (777700029,   5,         10) /* EncumbranceVal */
     , (777700029,   8,         10) /* Mass */
     , (777700029,   9,          0) /* ValidLocations - None */
     , (777700029,  11,          1) /* MaxStackSize */
     , (777700029,  12,          1) /* IsContainer - True */
     , (777700029,  13,         10) /* StackUnitEncumbrance */
     , (777700029,  14,         10) /* StackUnitMass */
     , (777700029,  15,         50) /* StackUnitValue */
     , (777700029,  16,          8) /* ItemUseable - Contained */
     , (777700029,  18,          1) /* UiEffects - Magical */
     , (777700029,  19,         50) /* Value */
     , (777700029,  33,          1) /* Bonded - Bonded */
     , (777700029,  93,       3092) /* PhysicsState */
     , (777700029,  94,         16) /* TargetType - Creature */
     , (777700029, 106,        210) /* ItemSpellcraft */
     , (777700029, 107,         50) /* ItemCurMana */
     , (777700029, 108,         50) /* ItemMaxMana */
     , (777700029, 114,          1) /* Attuned - Attuned */
     , (777700029, 150,        103) /* HookPlacement - Hook */
     , (777700029, 151,          2) /* HookType - Wall */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (777700029,  15, True ) /* LightsStatus */
     , (777700029,  63, True ) /* UnlimitedUse */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (777700029,   1, 'Tou Tou Prestige Portal Gem') /* Name */
     , (777700029,  16, 'Use this gem to be teleported to the Tou Tou Prestige Area.') /* LongDesc */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (777700029,   1, 0x02000C79) /* Setup */
     , (777700029,   3, 0x20000014) /* SoundTable */
     , (777700029,   6, 0x04000BEF) /* PaletteBase */
     , (777700029,   7, 0x1000010B) /* ClothingBase */
     , (777700029,   8, 0x060013CA) /* Icon */
     , (777700029,  22, 0x3400002B) /* PhysicsEffectTable */;

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (777700029, 7 /* Use */, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 99 /* TeleportTarget */, 0, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 11, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0xF559003D /* 0xF559003D [171.403397 113.162933 20.265999] -0.969513 0 0 0.245041 */, 171.403397, 113.162933, 20.265999, -0.969513, 0, 0, 0.245041);
