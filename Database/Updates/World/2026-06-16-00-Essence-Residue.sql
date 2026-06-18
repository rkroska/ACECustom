/* Savage Echo — WCID 78780013 (7878 spare block).
 *
 * Player-facing name: Savage Echo
 * Role: tradeable stackable currency for combat pet Potency (body damage training).
 * Spend: Use stack on YOUR bond-attuned combat pet essence (+1 stored potency per successful use).
 * Cost: server-side curve (pet_potency_cost_base × level) — NOT an NPC shop price.
 *
 * See docs/PET_POTENCY_AND_STRAIN.md, docs/WCID_ALLOCATION_7878.md
 *
 * Deploy:
 *   mysql -h HOST -P PORT -u USER -p ace_world < Database/Updates/World/2026-06-16-00-Essence-Residue.sql
 *
 * Verify:
 *   SELECT class_Id, class_Name, type FROM weenie WHERE class_Id = 78780013;
 *   SELECT type, value FROM weenie_properties_string WHERE object_Id = 78780013;
 */
START TRANSACTION;

DELETE FROM `weenie_properties_emote` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_book_page_data` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_book` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_anim_part` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_attribute` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_attribute_2nd` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_body_part` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_bool` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_create_list` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_d_i_d` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_event_filter` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_float` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_generator` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_i_i_d` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_int` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_int64` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_palette` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_position` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_skill` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_spell_book` WHERE `object_Id` = 78780013;
DELETE FROM `weenie_properties_string` WHERE `object_Id` = 78780013;
DELETE FROM `weenie` WHERE `class_Id` = 78780013;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (78780013, 'ace78780013_savageecho', 44, NOW());
/* type 44 = Stackable (required for Use On targeting — same pattern as Charm Catalyst 777700010) */

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (78780013,    11, 1) /* IgnoreCollisions */
     , (78780013,    13, 1) /* Ethereal */
     , (78780013,    14, 1); /* GravityStatus */
/* Intentionally NO Attuned (114) or Bonded (33) — tradeable currency */

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (78780013,     1,    128) /* ItemType - Misc (0x80) */
     , (78780013,     5,      5) /* EncumbranceVal (total at stack 1; scales via StackUnit*) */
     , (78780013,     8,      5) /* Mass */
     , (78780013,    11,  10000) /* MaxStackSize */
     , (78780013,    12,      1) /* StackSize */
     , (78780013,    13,      1) /* StackUnitEncumbrance */
     , (78780013,    14,      1) /* StackUnitMass */
     , (78780013,    16, 524296) /* ItemUseable - SourceContainedTargetContained (0x80008) = Use On cursor */
     , (78780013,    18,     10) /* UiEffects - Magical */
     , (78780013,    19,      0) /* Value — no pawn value; economy is player trade + potency spend */
     , (78780013,    93,   1044) /* PhysicsState - Ethereal, IgnoreCollisions, Gravity, Inelastic */
     , (78780013,    94,    128); /* TargetType - Misc (same as Siphoned Essence 78780004 on pet devices) */

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (78780013,     1, 33558517)  /* Setup — crystalline powder (Charm Catalyst family) */
     , (78780013,     8, 100676392) /* Icon — placeholder; swap for custom Savage Echo art when ready */
     , (78780013,    22, 872415275); /* IconOverlay — subtle magical sheen (Hilt/Catalyst pattern) */

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (78780013,    1, 'Savage Echo')
     , (78780013,   14, 'Crystallized echoes of combat essence, shed when a bonded pet helps slay a creature — fiercer when the pet struck true and often. Use on your bond-attuned combat pet essence to raise Potency (stored body training). Active Potency is limited by your bond with that essence; resummon your pet after training. Each upgrade consumes Savage Echo from your pack; higher stored Potency costs more per level.')
     , (78780013,   15, 'A stack of savage echo crystals.')
     , (78780013,   16, 'Savage Echo');

COMMIT;
