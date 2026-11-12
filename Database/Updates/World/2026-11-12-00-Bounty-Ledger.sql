/* Bounty Ledger - wcid 98760483 (ServerConfig item_bounty_ledger_wcid), a Book.
 *
 * The one item players use for item bounties (ItemBounties.cs, "The Bounty Ledger"):
 *   - a bounty used ON it starts (double-click the bounty, then click the ledger; standing in another tier or region,
 *     a second use on the ledger confirms);
 *   - double-clicked, it opens as a book: a summary page, then a page per bounty with its progress. The server writes the
 *     pages from the reader's own bounties every time it opens (like the Monster-Dex 78780005); none are stored here, and
 *     nothing can be written into it;
 *   - appraised by the player carrying it, its description lists their bounties going and their progress.
 * Nothing lives on the ledger, so losing it loses nothing: the server hands one to any player with bounties and no
 * ledger (when they use a bounty, and at login). Attuned and bonded: it can't be traded and stays through death.
 *
 * Model, icon and sounds are copied from the retail Book (364), which must exist first (it does on ILT and in any ACE
 * world database).
 * WCID: in the 98760423+ range - 98760423-456 Echo of the Paragon, 460-481 Shifting Stones, 482 proposed for the
 * Universal Ammo, 490-499 fishing. 98760483 is free in the repo and the project docs; check the live DB before importing.
 * If it must move, change item_bounty_ledger_wcid to match.
 * Every string is plain ASCII: the client draws nothing else.
 */
START TRANSACTION;

-- Narrow on purpose: removes only this weenie (id AND class_Name). If the id has been taken by something else, that
-- weenie is left alone and the INSERT below fails on the primary key instead of silently replacing it. Child rows follow
-- the parent (ON DELETE CASCADE).
DELETE FROM `weenie` WHERE `class_Id` = 98760483 AND `class_Name` = 'ilt-bounty-ledger';

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (98760483, 'ilt-bounty-ledger', 8, '2026-11-12 12:00:00') /* Book */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (98760483,   1, 8192) /* ItemType - Writable */
     , (98760483,   5,   25) /* EncumbranceVal */
     , (98760483,   8,   25) /* Mass */
     , (98760483,  16,    8) /* ItemUseable - Contained */
     , (98760483,  19,    0) /* Value */
     , (98760483,  33,    1) /* Bonded - Bonded */
     , (98760483,  93, 1044) /* PhysicsState - Ethereal, IgnoreCollisions, Gravity */
     , (98760483, 114,    1) /* Attuned - Attuned */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (98760483, 11, True)  /* IgnoreCollisions */
     , (98760483, 13, True)  /* Ethereal */
     , (98760483, 14, True)  /* GravityStatus */
     , (98760483, 22, False) /* Inscribable */
     , (98760483, 69, False) /* IsSellable */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (98760483,  1, 'Bounty Ledger')
     , (98760483, 14, 'Double-click to read your bounties and their progress. To start a bounty, double-click the bounty and then click this ledger.')
     , (98760483, 16, 'A record of the bounties you have going: what each one asks, where, how far along you are and what it pays. It writes itself from your own bounties every time you open it, so nothing is lost with it: if you lose it, use any bounty on yourself and a new one is added to your pack.');

/* model, icon and sounds from the retail Book (364) */
INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
SELECT 98760483, `type`, `value`
  FROM `weenie_properties_d_i_d`
 WHERE `object_Id` = 364 AND `type` IN (1, 2, 3, 6, 7, 8, 22, 50, 51, 52);

INSERT INTO `weenie_properties_book` (`object_Id`, `max_Num_Pages`, `max_Num_Chars_Per_Page`)
VALUES (98760483, 9, 1000) /* a summary page and up to 8 bounties */;

COMMIT;
