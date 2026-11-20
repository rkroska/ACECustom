# Account-Locked Content - Content Guide

Audience: the people who build NPCs, chests, portals and quests. No code required: everything below is a row
in a weenie SQL file or in the `quest` table.

Related: `Source/ACE.Server/Managers/AccountQuestLimits.cs` (how it works) and
`Source/ACE.Server.Tests/AccountQuestLimitsTests.cs` (the rules, as tests).

---

## 1. What it is, in one paragraph

`InqAccountQuest` (emote type **155**) is `InqQuest` (type 21) asked of the whole account instead of one
character. `InqQuest` asks "has this character done the quest, and is its timer still running?"
`InqAccountQuest` asks "has any character on this account done it, and is the account's timer still
running?" It branches the same way: `QuestSuccess` = already done, so deny; `QuestFailure` = go ahead.
Nothing else changes. `StampQuest` still stamps the character who did it, and the account check adds up
the stamps of every character on the account. So any content that already gates on `InqQuest` becomes
account-locked by changing that one step from 21 to 155.

---

## 2. The three pieces

| Piece | Where | What goes in it |
|---|---|---|
| The rule | `quest` table | `min_Delta` (seconds between solves) and `max_Solves` (-1 = unlimited). With `InqAccountQuest` they apply to the whole account. |
| The gate | the weenie's emote, usually `7 /* Use */` | one action of type `155 /* InqAccountQuest */` with the quest name in `message` |
| The outcomes | emote sets `12 /* QuestSuccess */` and `13 /* QuestFailure */` whose `quest` is the quest name | QuestSuccess: the "come back later" text. QuestFailure: the reward, then `22 /* StampQuest */` |

Common settings for the rule:

| You want | `min_Delta` | `max_Solves` |
|---|---|---|
| Once per account every 20 hours | 72000 | -1 |
| Once per account per week | 604800 | -1 |
| Once per account, ever | 0 | 1 |
| Three times per account, ever | 0 | 3 |
| Once per account every 20 hours, five times in total | 72000 | 5 |

The timer runs from the newest completion by any character on the account. `max_Solves` counts the
account's total. `quest_mindelta_rate` scales `min_Delta` exactly as it does for normal quests (quests
named `ColoArena...` are never scaled).

"Ever", and any timer longer than the server's purge delay, has one gap: a deleted character counts only
until it is purged (section 7).

No `quest` row at all makes it an account-wide flag, as with `InqQuest`: once any character on the
account has the stamp, the account answers QuestSuccess for good. That suits "has anyone on this account
done X?" checks. For a timer or a solve limit, add the row.

---

## 3. Adding it to a weenie SQL file

In a weenie SQL file (the format in `Content/sql/weenies/` and in `Database/Updates/World/` patches), the
gate is the `weenie_properties_emote_action` row whose `type` is `21 /* InqQuest */`. Change that number
to 155. The column list and the rest of the row stay as they are; the quest name stays in `message`.

Before (one per character):

```sql
VALUES (@parent_id,  0,  21 /* InqQuest */, 0, 1, NULL, 'MyDailyGift', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
```

After (one per account):

```sql
VALUES (@parent_id,  0, 155 /* InqAccountQuest */, 0, 1, NULL, 'MyDailyGift', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
```

The Chests of Chance are a good model: their Use emote is a single `InqQuest` step on the chest's quest,
the QuestSuccess set says "You have played your odds too recently", and each QuestFailure set gives a
reward and stamps the quest. Making them one per account is that one number and nothing else.

To load an edited file from `Content/sql/weenies/` on a dev server, use `@import-sql <wcid>`. Objects
already in the world keep the emotes they spawned with, so restart (or let the landblock reload) before
testing.

---

## 4. A complete example: one gift per account per day

Short column lists are fine: every column not named takes its default.

```sql
SET @npc  = 0;   -- your NPC's WCID
SET @gift = 0;   -- the WCID of the item it gives

-- The rule: once per account every 20 hours
DELETE FROM `quest` WHERE `name` = 'ExampleAccountGift';
INSERT INTO `quest` (`name`, `min_Delta`, `max_Solves`, `message`, `last_Modified`)
VALUES ('ExampleAccountGift', 72000, -1, 'Example: one gift per account per day', NOW());

-- Use: ask the account
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `quest`)
VALUES (@npc, 7 /* Use */, 1, NULL);
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 155 /* InqAccountQuest */, 0, 1, 'ExampleAccountGift');

-- QuestSuccess: a character on this account already has it
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `quest`)
VALUES (@npc, 12 /* QuestSuccess */, 1, 'ExampleAccountGift');
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES (@parent_id, 0, 10 /* Tell */, 0, 1, 'Your account has already claimed today''s gift. Come back in %aqt.');

-- QuestFailure: go ahead. Give the reward, then stamp the character
INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `quest`)
VALUES (@npc, 13 /* QuestFailure */, 1, 'ExampleAccountGift');
SET @parent_id = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`, `weenie_Class_Id`, `stack_Size`)
VALUES (@parent_id, 0, 10 /* Tell */, 0, 1, 'Here is your gift.', NULL, NULL)
     , (@parent_id, 1, 3 /* Give */, 0, 1, NULL, @gift, 1)
     , (@parent_id, 2, 22 /* StampQuest */, 0, 1, 'ExampleAccountGift', NULL, NULL);
```

Stamp in the same set that pays out, after the reward, as the Chests of Chance do.

---

## 5. Converting content that is already live

For content that exists only in the live world database, ship a patch in `Database/Updates/World/` that
flips the gate in place:

```sql
UPDATE `weenie_properties_emote_action` a
JOIN `weenie_properties_emote` e ON e.`id` = a.`emote_Id`
SET a.`type` = 155 /* InqAccountQuest */
WHERE e.`object_Id` = 12345678          -- the weenie
  AND a.`type` = 21 /* InqQuest */
  AND a.`message` = 'MyDailyGift';      -- the quest it gates on
```

It is safe to run twice, and the undo is the same statement with 155 and 21 swapped. Restart afterwards:
objects already in the world keep the emotes they spawned with.

---

## 6. Showing the account's timer: `%aqt` and `%aqc`

| Text | Shows | The per-character version |
|---|---|---|
| `%aqt` | time until the account can do it again | `%tqt` |
| `%aqc` | times the account has done it, all characters added up | `%tqc` |
| `%tqm` | the quest's `max_Solves` (the same for both) | |

- Use `%aqt` in account-locked text. `%tqt` and `%CDtime` still show the clicking character's own timer,
  which for an alt that never did it reads as if it could go ahead.
- Like `%tqt`, they work in emote sets that have a quest (the QuestSuccess and QuestFailure sets), and
  `%aqt` is meant for the QuestSuccess text. In a QuestFailure set it reads "negative time", and once
  `max_Solves` is reached it shows a very long time, so write once-ever text without it.
- While another use on the account is still being rewarded (its claim holds the gate and its stamp hasn't
  landed yet), `%aqt` reads "a moment", so "Come back in %aqt." still makes sense.
- The account is only read when the text contains `%aqt` or `%aqc`.

---

## 7. What counts as "the account"

- Every character on the player's login account: online, offline, and deleted but not yet purged.
  Deleting the character that did it does not reset the lock.
- Purging does. If the server purges deleted characters (`PurgeDeletedCharacters` in the server config,
  after `PurgeDeletedCharactersDays`, 30 by default), a purged character's stamps go with it. A player
  could claim, hand the reward to another character, delete the one that claimed it and claim again
  after the purge. That reopens a once-ever lock (`max_Solves`), and a timed lock whose cooldown
  (`min_Delta` x `quest_mindelta_rate`; just `min_Delta` for quests named `ColoArena...`, which never
  scale) is longer than the purge delay. At the defaults (purge after 30 days, rate 1), daily and weekly
  locks are safe; a higher rate can push them past it. While purging is on, don't put rewards worth that
  behind such locks.
- It is the login account, not the computer or IP. Two accounts on one PC are separate.
- A character that is still logging out counts right away, so swapping characters quickly doesn't slip
  past the lock.

---

## 8. Things to know

- **Deploy the server build before the content.** A server without type 155 skips the step: neither
  QuestSuccess nor QuestFailure runs, so the object does nothing when used.
- **Stamps stay per character.** `StampQuest`, `EraseQuest`, `IncrementQuest`, `SetQuestCompletions` and
  the rest act on the character using the content. The account check only reads them. To clear an
  account's lock for support, erase the quest on every character of the account that has it (`@qst list`
  and `@qst erase <quest>`, with each character selected), then try the gate again: a stamp left on any
  character still counts. A deleted character can't be selected for `@qst`, so its stamp counts until the
  character is purged (section 7). To clear it sooner, an admin deletes that character's row for the quest
  from the shard's `character_properties_quest_registry` table (the character is offline, so the change
  counts at once).
- **Keep the gate and the stamp together.** Give the `InqAccountQuest` step, the reward and the
  `StampQuest` no delay, as the Chests of Chance do, and if the object deletes itself (`DeleteSelf`), do
  that after the `StampQuest`. The stamp still lands a while after the gate: the QuestSuccess or
  QuestFailure set runs on the next server tick, as with `InqQuest`, and some steps make the next one wait
  even with no delay (an NPC turns to face the player before a `Give` unless it has
  `DontTurnOrMoveWhenGiving`, bool 82; `Motion`, `Turn`, `TurnToTarget`, `MoveHome`, `CastSpell` and
  `AwardSkillXP` wait too). So when the gate lets a character through, the account claims the quest's
  gate: it stays closed for the whole account until the object has finished its emotes (branches inside
  the outcome, such as a nested `InqQuest` or a `Goto`, included), the reward and the stamp, or a stop
  such as a full pack. The object normally stays busy (refusing new uses) until then; `OnPortal` and
  `OnDeath` can clear that early, but the claim still keeps the gate closed for that account and quest.
  There is no timer, however long the emotes wait. If the object leaves the world (a despawn), or the
  player's character leaves it to log out (a stamp would no longer be saved), before the emotes have done
  anything, they stop and another use can go ahead; if that happens after they started, or they stop on a
  server error, the gate stays closed for that account until the server restarts, so it fails closed. An
  account has one login at a time (a second login boots the first or is refused), so no other character on
  the account can be playing meanwhile. After a stop with no stamp, the gate opens again as soon as the
  object is idle, so the player can simply try again.
- **A treasure isn't counted by the full-pack check.** The check that stops a reward set on a full pack,
  so the player can free space and retry, counts `Give` steps but not `CreateTreasure`. A treasure can
  take the last free slot, and a later `Give` then stops the set before its `StampQuest`, so the player
  could repeat it for another treasure (an `InqQuest` gate has the same gap). With `CreateTreasure` in a
  reward set, put it last, right after the `StampQuest`: a full pack then costs the player the treasure
  instead of letting them repeat the set.
- **It fails closed.** If the shard or world database can't be read, or the emote names no quest, the
  account is treated as having done it (QuestSuccess), so a problem never hands out extra rewards. Those errors are
  logged at most once a minute.
- **A small database read per use**, plus one more when the text shows `%aqt` or `%aqc`. Indexed and
  cheap, and fine for Use, Talk and Give on NPCs and chests. Don't hang it on something that fires
  constantly, like a heartbeat.
- **Players only.** If the emote's target isn't a player, the step does nothing.
- **Mixing is fine.** `InqQuest` and `InqAccountQuest` on the same quest answer different questions, and
  quest names with an `@comment` work the same way in both.

---

## 9. Testing it locally

1. Make two characters on one account, A and B.
2. A uses the content: reward, and the quest is stamped on A.
3. B uses it: the QuestSuccess text, with `%aqt` counting down from A's stamp.
4. `@modifydouble quest_mindelta_rate 0.001` turns 20 hours into 72 seconds. After a minute and a half
   B can go ahead. Set it back to 1 afterwards.
5. A character on a different account is unaffected.
6. A uses it and logs out; log B in right away: still QuestSuccess.
