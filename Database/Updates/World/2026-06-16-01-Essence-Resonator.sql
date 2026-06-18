/* Essence Resonator — WCID 78780014 (7878 spare block).
 *
 * Reusable Use-on-Target tool: click spare Siphoned (78780004) or Hollow (78780006) captured
 * essences in your pack to salvage them into Savage Echo (78780013). Yield is server-side only
 * (pet_residue_salvage_* ServerConfig) — not a cook_book recipe.
 *
 * See docs/PET_POTENCY_AND_STRAIN.md, docs/WCID_ALLOCATION_7878.md
 *
 * Deploy:
 *   mysql -h HOST -P PORT -u USER -p ace_world < Database/Updates/World/2026-06-16-01-Essence-Resonator.sql
 *
 * Verify:
 *   SELECT class_Id, class_Name, type FROM weenie WHERE class_Id = 78780014;
 */
START TRANSACTION;

DELETE FROM `weenie_properties_emote` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_book_page_data` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_book` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_anim_part` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_attribute` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_body_part` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_event_filter` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_float` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_generator` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_i_i_d` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_int64` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_palette` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_position` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_skill` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_spell_book` WHERE `object_Id` = 78780014;
DELETE FROM `weenie_properties_string` WHERE `object_Id` = 78780014;
DELETE FROM `weenie` WHERE `class_Id` = 78780014;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780014, 'ace78780014_essenceresonator', 44, NOW());
/* type 44 = Stackable / CraftTool — Use On targeting cursor (Charm Catalyst pattern) */

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780014,    11, 1) /* IgnoreCollisions */
     , (78780014,    13, 1) /* Ethereal */
     , (78780014,    14, 1); /* GravityStatus */
/* Reusable vendor tool — not attuned/bonded (tradeable like Ust) */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780014,     1,    128) /* ItemType - Misc (0x80) */
     , (78780014,     5,     25) /* EncumbranceVal */
     , (78780014,     8,     25) /* Mass */
     , (78780014,    11,      1) /* MaxStackSize — reusable single tool */
     , (78780014,    12,      1) /* StackSize */
     , (78780014,    16, 524296) /* ItemUseable - SourceContainedTargetContained (0x80008) */
     , (78780014,    18,     10) /* UiEffects - Magical */
     , (78780014,    19,    500) /* Value — vendor hint only */
     , (78780014,    93,   1044) /* PhysicsState */
     , (78780014,    94,   2048); /* TargetType - Gem (0x800) — Siphoned/Hollow captured essences */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780014,     1, 33558517)  /* Setup — crystalline tool (Catalyst family) */
     , (78780014,     8, 100676392) /* Icon — placeholder */
     , (78780014,    22, 872415275); /* IconOverlay */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780014,    1, 'Essence Resonator')
     , (78780014,   14, 'A resonant crystalline tool that extracts Savage Echo from spare captured essences. Use this on a Siphoned or Hollow essence in your pack to salvage it. Applied essences on pet devices cannot be salvaged. Yield depends on the captured creature; hollow essences return less than siphoned.')
     , (78780014,   15, 'An essence resonator.')
     , (78780014,   16, 'A well-crafted essence resonator.');

COMMIT;
