# Daily Quest Toolkit

How to build a hand-play daily like **Pilfer's Hoard** (the Holtburg Scavenger Hunt) out of world data:

- a daily lock (once per account, at most N per IP address);
- a reward that grows with days in a row (the streak ladder);
- payment in **Marks of Favor**, which **Hollis the Favor Broker** exchanges for coin.

Everything here is emote data. No server code is needed per quest. The server features it relies on:

| Feature | Where it comes from |
|---|---|
| `InqAccountQuest` (EmoteType 155), `%aqt` / `%aqc` | ACECustom #612, see [ACCOUNT_LOCKED_CONTENT_GUIDE.md](ACCOUNT_LOCKED_CONTENT_GUIDE.md) |
| `InqIPQuest` (EmoteType 156), `%iqt` / `%iqc` | branch `feature/give-merge-stacks`, same guide, "IP-locked content" |
| Emote `Give` tops up stacks the player carries | branch `feature/give-merge-stacks`, server setting `emote_give_merge_stacks` (default on) |

The worked example is Pilfer's Hoard, `78780700_holtburg_scavenger_hunt_install.sql` in ACECustomSQLs (WCIDs
78780700-78780749). Read it next to this guide: every pattern below appears in it as emote rows you can copy.

---

## 1. The daily lock

Check the locks **every time the player talks to the quest giver**, the hand-in included, not only when the quest starts.
The quest is stamped at hand-in, so if the lock only ran at the start, two characters on one account could both start the
quest and both get paid.

| Category | Quest | Actions |
|---|---|---|
| Use (7) | | `InqAccountQuest` (155) `MyDaily` |
| QuestSuccess (12) | `MyDaily` | Tell "You've already helped today. Come back in %aqt." |
| QuestFailure (13) | `MyDaily` | `InqIPQuest` (156) `MyDaily@ip`, Amount 2 |
| QuestSuccess (12) | `MyDaily@ip` | Tell "Your household has done this twice today. Come back in %iqt." |
| QuestFailure (13) | `MyDaily@ip` | ...the quest: start it, hand it in... |

- The `quest` row for `MyDaily` sets the window: `min_Delta 72000` (20 h), `max_Solves -1`. Both locks use it.
- `StampQuest MyDaily` at the hand-in (after the reward, see section 2) is what closes both locks.
- The IP step needs its own `@comment` (`MyDaily@ip`): QuestSuccess/QuestFailure sets are chosen by the full quest
  string, so without it the IP step would run the account step's sets.
- Who counts for the IP lock: the player's own account, accounts with a character online from the same address, and
  accounts whose last login came from it. People sharing a connection share the limit; a VPN gets around it.
- `%iqt` and `%iqc` currently assume a limit of 2. With another Amount the lock is right but the time in the text is not.

## 2. Paying the reward

**Give first, stamps after, in the same emote set.** Before a set's Gives run, the server checks that the player can
carry all of them. If not, it tells them how many slots they need and stops the set right there. Everything after the Give
is skipped, so put every `StampQuest`, `SetQuestCompletions` and `IncrementQuest` after it. The player can then free space
and hand in again without losing the day or the streak.

| Order | Action |
|---|---|
| 1 | `Give` (3) the reward |
| 2 | Tell the thanks |
| 3 | `StampQuest` (22) `MyDaily`, then any other bookkeeping |

**Stacks.** With `emote_give_merge_stacks` on, a stackable reward first tops up matching stacks the player carries (main pack,
then side packs) and only makes a new stack for what is left. A player who already carries some Marks needs no free slot to
be paid. Rewards given with a Palette or Shade keep their own stack. Without the setting (or on an older server), every Give
makes a new stack and needs a free slot.

### Marks of Favor (78780702)

The shared daily currency. Pay it with `Give 78780702, stack N`.

- Bonded and attuned (int 33 = 1, int 114 = 1): it can't be traded, given or sold, so alts can't funnel it.
- MaxStackSize 25,000, with a PluralName ("Marks of Favor") so chat reads "gives you 15 Marks of Favor".
- Its weenie is in the hunt's SQL (`build_stackable` in the generator). Another daily can pay it without redefining it,
  as long as the hunt's SQL is installed.

### Hollis the Favor Broker (78780701)

A vendor whose currency is Marks instead of pyreals.

- `PropertyDataId.AlternateCurrency` is data id **57** = 78780702. (52 is an icon underlay: a first build used it and Hollis
  sold for pyreals.)
- `SellPrice` (float 38) = 1.0, so an item's price in Marks is its `Value` (int 19). `MerchandiseItemTypes` (int 74) = 0: he
  buys nothing.
- His shop is `weenie_properties_create_list` rows with `destination_Type 4` and `stack_Size -1`.

What he sells are **pouches**, Gems that turn into their contents when used (the "Box Of 50 Enlightened Coins" pattern):

| Property | Value |
|---|---|
| weenie type | 38 (Gem) |
| `UseCreateItem` (did 38) | the item it opens into (300004 Enlightened Coin, 20630 Trade Note) |
| `UseCreateQuantity` (int 269) | how many |
| `Value` (int 19) | price in Marks |
| Bonded / Attuned | int 33 = 1, int 114 = 1 |

Pouches exist because a vendor prices an item by its own Value, and a Trade Note's is fixed (250,000, sold at 1.15x). Current
shop: Coin Purse (1 coin, 1 Mark), Pouch of Enlightened Coins (5 coins, 5 Marks), Trade Note Bundle (8 notes, 1 Mark), Trade Note Crate (40 notes, 5 Marks). 8 MMD per Mark matches the player market (8-9 MMD per Enlightened Coin, 2026-11-23). To add
an item, add a line to `SHOP` in the generator.

## 3. The streak ladder

Pay more for each day in a row, hold at the top step, start over after a missed day. Pilfer's Hoard pays
15 / 20 / 25 / 30 / 35 / 40 / 45 / 50 Marks (half with 8+ wrong searches).

**"In a row"** is a hidden timer quest, re-stamped at every hand-in. `InqQuest` on it is QuestSuccess while its timer is
still running, so it means "the last hand-in was less than this long ago". 36 h works well with a 20 h lock: it gives a
16 h slot each day to keep the streak, and skipping a day breaks it. (24 h leaves a 4 h slot; 48 h lets players skip every
other day.)

Quest rows (both need `max_Solves -1`, see section 4):

| Quest | min_Delta | Holds |
|---|---|---|
| `MyDailyStreak` | 0 | the day number (completions) |
| `MyDailyStreakWindow` | 129600 (36 h) | the time of the last hand-in |

At hand-in, once the quest checks have passed:

| Category | Quest | Actions |
|---|---|---|
| (hand-in set) | | ... `InqQuest` (21) `MyDailyStreakWindow` |
| QuestFailure | `MyDailyStreakWindow` | **pay day 1**, then `SetQuestCompletions` (70) `MyDailyStreak` 1 |
| QuestSuccess | `MyDailyStreakWindow` | `InqQuestSolves` (30) `MyDailyStreak@1`, min 0, max 0 |
| QuestSuccess | `MyDailyStreak@1` | **pay day 1**, then `IncrementQuest` (33) `MyDailyStreak` |
| QuestFailure | `MyDailyStreak@1` | `InqQuestSolves` `MyDailyStreak@2`, min 1, max 1 |
| QuestSuccess | `MyDailyStreak@2` | **pay day 2**, then `IncrementQuest` `MyDailyStreak` |
| QuestFailure | `MyDailyStreak@2` | `InqQuestSolves` `MyDailyStreak@3`, min 2, max (empty) |
| QuestSuccess | `MyDailyStreak@3` | **pay day 3**, then `IncrementQuest` `MyDailyStreak` |
| QuestFailure | `MyDailyStreak@3` | **pay day 1**, then `SetQuestCompletions` `MyDailyStreak` 1 (no count: start over) |

That is a 3-step ladder. Each extra step adds one pair; only the last step has an empty max, which is what makes the top
step hold. The streak count keeps rising past the top, which is harmless.

**pay day N** = `Give` the day's reward, Tell, `StampQuest MyDaily`, `EraseQuest` (31) `MyDailyStreakWindow`,
`StampQuest MyDailyStreakWindow`, then the streak step from the table. The window is erased before it is stamped so its
time is always the latest hand-in.

The streak is per character, while the daily lock is per account. A player who switches characters from day to day
breaks the streak on each of them.

## 4. Rules learned the hard way

- **Keep helper quests out of QB.** Every quest name is worth up to 2 QB (1 when an account first gets it, 1 when its
  count first reaches exactly 1), so timers, counters and flags inflate a quest's QB. Start the quest row's `message`
  with `[NoQB]` and the server awards nothing for it (`QuestManager.NoQbMarker`). Give QB through a few deliberate
  stamps instead: Pilfer's Hoard awards 6 (started a hunt, a tidy hunt, reached the top of the streak), 2 each.
- **Helper quests need a `quest` row with `max_Solves -1`.** Without a row, the max solves read as 0 and every
  `SetQuestCompletions` / `SetQuestBitsOn` value is clamped to 0, so nothing sticks.
- **A branching step ends its set.** `InqQuest`, `InqAccountQuest`, `InqIPQuest`, `InqQuestSolves`, `InqQuestBitsOn/Off`
  and `Goto` hand off to other sets: put them last and continue in the set they branch to.
- **`Name@comment`** gives a quest step its own QuestSuccess/QuestFailure sets while reading the same quest record.
  Use a different comment for every branch point on the same quest.
- **Random pick:** `Goto` (67) a name, and give that name several GotoSet (32) sets with cumulative probabilities
  (0.125, 0.25, ... 1.0 for 8 equal choices).
- **Quest bits:** `SetQuestBitsOn` (106) / `SetQuestBitsOff` (107) / `InqQuestBitsOn` (102) / `InqQuestBitsOff` (103) store
  flags in a quest's completion count; 31 usable bits. The hunt uses bit 30 for "under way" and bits 0-29 for its spots.
- **`/qst stamp Quest N` sets** the count to N, it doesn't add. `/qst` acts on the creature you last appraised.
- **Every new quest name becomes a `/qb` stamp** the first time a character gets it, helper quests included.
- **`TakeItems` with stack -1** takes all of that item; use it to collect quest sheets at hand-in.
- **On a running server** after installing SQL: `/clearcache`, then `/reload-landblock` standing in the landblock.

## 5. Emote numbers used here

| Action | Type | Category | Number |
|---|---|---|---|
| Give | 3 | Use | 7 |
| Tell | 10 | Refuse (item handed to the NPC) | 1 |
| DirectBroadcast | 18 | QuestSuccess | 12 |
| InqQuest | 21 | QuestFailure | 13 |
| StampQuest | 22 | GotoSet | 32 |
| InqQuestSolves | 30 | | |
| EraseQuest | 31 | | |
| IncrementQuest | 33 | | |
| Goto | 67 | | |
| SetQuestCompletions | 70 | | |
| TakeItems | 74 | | |
| InqQuestBitsOn / Off | 102 / 103 | | |
| SetQuestBitsOn / Off | 106 / 107 | | |
| InqAccountQuest | 155 | | |
| InqIPQuest | 156 | | |

## Status

Pilfer's Hoard was played through in game on the local server on 2026-11-23: the account lock, every hunt path, payouts,
Hollis and the pouches. The IP lock, the stack merge and the streak ladder have unit tests (IP rule) and are waiting on
their first in-game run.
