# Ruggan's Annex - Breeding Area Design

Audience: whoever builds this in the world. Companion to `PET_BREEDING_CONTENT_GUIDE.md`
(the tunables) and `PET_BREEDING_PLAYER_GUIDE.md` (the rules as players read them).

All NPC text in this document is 7-bit ASCII on purpose. It gets loaded into
`weenie_properties_emote_action.message` and drawn by the AC client. See the rule in `CLAUDE.md`.

---

## 1. The premise

Professor Ruggan registers captured essences in Lin. That is the sanctioned half of his work.
The other half is behind a door he does not talk about, staffed by one intern, and it is where
the essences he was only supposed to catalogue started producing offspring in colours that do
not occur in nature.

The creatures he made have opinions about this. Two factions share one hallway and cannot stand
each other:

- **The Registry** - plain, correctly-coloured creatures with paperwork, insufferable about it.
- **The Ward** - mutation-palette creatures, louder, prouder, convinced they are the future.

Players walk in between them and breed on the floor in the middle. The comedy is that nobody
involved thinks this is a good facility, including the person who built it.

---

## 2. Two decisions to make before anyone places an object

### 2.1 Which landblock and variant

Right now these disagree and one of them is wrong:

| Source | Says |
|---|---|
| `PropertyManager.cs:528` default | landblock `0x016C` (364 decimal) |
| `Database/Updates/World/2026-07-26-00-Seedy-Motel-Portal.sql` | portal destination cell `0x013A02AE`, `variation_Id` 3 |

`0x013A02AE` is landblock `0x013A`, not `0x016C`. Either the live shard has
`pet_breeding_allowed_landblock` modified away from the code default, or the portal drops players
somewhere breeding does not work. **Confirm with `@showprops` before building anything.** The
answer decides where the annex goes.

Recommendation: build the annex inside the **variation instance** the portal already uses
(`variation_Id` 3). Variations are instanced copies of a landblock, so the annex can be furnished
without touching the retail dungeon, and `pet_breeding_allowed_variant` can pin breeding to that
instance alone.

### 2.2 Which single landcell is the ritual floor

This is the one that will bite you. `PetDevice_Breeding.cs:193` requires **both summoned pets to
share a landcell**, and `IsInBreedingArea` accepts a full raw cell as well as a 16-bit landblock:

```csharp
var targetLb = allowedLandblock > 0xFFFF ? (allowedLandblock >> 16) : allowedLandblock;
var locValid = allowedLandblock == 0
    || player.Location.Landblock == targetLb
    || player.Location.Cell == allowedLandblock;
```

So you can gate breeding to exactly one room by setting the property to that room's raw cell id.
That is the right call here: it makes the Ritual Floor a real destination instead of "anywhere in
the dungeon", and it stops two strangers in a corridor accidentally pairing off.

**Build instruction:** stand in the candidate room and run `@breed-debug`. It prints
`Cell=0x........`. Walk the whole room - every corner, both sides of any pillar. If the cell id
changes anywhere inside it, the room is more than one landcell and two players standing on
opposite sides of it will silently fail to breed. Either pick a smaller room, or physically block
players out of the second cell. Do not skip this check; the failure mode is invisible.

---

## 3. Floorplan

Six zones. A single spine with two wings off it, so a player on the Ritual Floor can see and hear
both factions at once. NPC speech carries 96m (`WorldObject.LocalBroadcastRange`), which is most
of a dungeon landblock, so geometry is not a constraint - sightlines are what matter.

```
                      [ 6. Ruggan's Office ]
                          (door, locked)
                               |
   [ 4. The Registry ] --- [ 3. RITUAL FLOOR ] --- [ 5. The Ward ]
      purebreds, tidy         one landcell            mutants, chaos
                               |
                      [ 2. Quartermaster ]
                               |
                      [ 1. THE DROP - Fenwick ]
                               ^
                          portal in
```

**1. The Drop.** Where the portal lands. Fenwick and nothing else, so the first thing a player
sees is a tired man in front of a mess. One sign, knocked over. A garbage barrel. The motel's
existing holiday lights start here and get worse as you go in.

**2. Quartermaster's nook.** A side alcove off the spine, not a blocker. Sells the three kits.

**3. The Ritual Floor.** The breeding cell. This is the only room where the system works. Put the
holiday lights here at full density - it reads as a dance floor, which is exactly right, because
the ritual motion is literally `MotionCommand.DrudgeDance` (`Player.cs:990`). DJ Skulk, a drudge,
stands on it. He is not a joke bolted on; he is the mechanic wearing a costume. Keep this room
open floor - the mating guardian spawns here and the two parent pets need room to fight it.

**4. The Registry (left wing).** Clean, symmetrical, a velvet-rope line of plainly-coloured
creature NPCs standing at attention. Bexley the Registrar behind a desk stacked with paper.

**5. The Ward (right wing).** Same species, mutation palettes, standing in no order at all.
Splotch out front. Overturned furniture. One creature facing the wall.

**6. Ruggan's Office.** Locked, at the back, visible through a doorway. The Professor is "in the
field." A readable notes item and a portrait of Splotch on the wall. This room exists to be
looked into and not entered - it is the punchline to Fenwick's whole shift.

---

## 4. Cast

### Fenwick, Kennel Intern - The Drop

The "whoa, Professor Ruggan really went overboard" energy, played as a man who has explained this
four hundred times. He never gets excited; the room does that for him.

- "Whoa. Whoa. Okay. Yes. This is the place. Yes, the Professor did this."
- "No, I don't know why the Ursuin is pink. I have stopped needing to know why the Ursuin is pink."
- "Professor Ruggan is a genius. I want that on the record before I say anything else."
- "He said, and I quote, 'let's see what happens.' Something always happens. That is the problem
  with the sentence."
- "Rules are on the sign. The sign is on the floor. The floor is where the Professor put it."
- "Mrs. Ruggan sent another letter. I told her he was in the field. He is under a desk."
- "Don't feed them. Don't pet them. And do not tell the left side what the right side said."

His second dialogue branch is the actual tutorial: the nine points from section 3 of the Content
Guide, in that order. Keep the comedy in the greeting and the rules in the branch, so a player who
wants to learn can get to it without wading through bits.

### Bexley, Keeper of the Registry - left wing

Prim, wounded, speaks only of bloodlines and standards. Refuses to acknowledge the Ward exists,
which he does by talking about it constantly.

- "Every creature in this lounge has papers. Every creature in that ward has... a situation."
- "A Browerk is brown. That is what the word means. That is the entire word."
- "We do not acknowledge the ward."
- "Bloodline. Conformation. Restraint. Three concepts, none of them represented over there."
- "I have been asked to stop using the phrase 'genetic vandalism.' I have not agreed to stop."

### Splotch - right wing

The first mutant Ruggan ever bred. Self-appointed spokesman. Six colours, no shame.

- "Papers? I have PALETTES."
- "They call it purebred. I call it beige with a certificate."
- "The Professor called me an accident. Then he hung my portrait in his office."
- "Ask them what colour they'll be next year. Go on. Ask them."
- "We don't have a bloodline. We have a RANGE."

### DJ Skulk - Ritual Floor

A drudge. Three lines, forever, and that is the bit.

- "You. Yes. Dance. That is the entire ritual. I did not design it."
- "Everyone dances here. Even the Registrar. Once. He does not discuss it."
- "Both of you. Together. Now."

### Ivo, Ruggan's Quartermaster - alcove

Vendor for WCIDs 98760399 / 98760400 / 98760401. Deadpan.

- On the Tailoring Kit: "It takes the look off one and puts it on another. The first one does not
  survive that. The price reflects it."
- On the Neutering Kit: "This one is for people who have made a decision. I don't ask which."

### Gary - standing in the middle of the Ritual Floor

One ordinary creature with exactly one mutated limb. Belongs to neither faction. Both sides claim
him when convenient and disown him the rest of the time. He has one line and it is
"...I'm just here for the music." Build Gary. Gary is the cheapest joke in the whole area and it
will be the one people mention.

### Mrs. Ruggan - occasional

Ties the annex back to her existing lens errand in Lin. Place her at the Drop on an event, or on a
generator with a long respawn so she shows up rarely.

- "Where is he."
- "He told me this was a filing annex."
- "There is a pink Ursuin in my husband's filing annex."

### The wing creatures

The faction members are ordinary creature weenies made non-combatant. `Creature.cs:153`:

```csharp
public bool IsNPC => !(this is Player) && !Attackable && TargetingTactic == TargetingTactic.None;
```

So each wing creature needs `weenie_properties_bool` type 19 (`Attackable`) = False and
`weenie_properties_int` type 68 (`TargetingTactic`) = 0. Set type 67 (`Tolerance`) = 0 as well.

- **Registry side:** stock palettes, stiff names. "Registered Browerk, Champion Line."
  "Certified Shreth, Third Generation." "Pedigreed Ursuin (Papers Pending)."
- **Ward side:** same species with a `PaletteTemplate` taken from the mutation pool, so they render
  exactly like a real bred mutation. Names: "Splotch." "The Teal Incident." "Ursuin, Unregistered."
  "Nine-Colour Shreth."

Because Ward creatures use the same palette pool the breeding roll uses, a player who breeds a
mutation will sometimes get a pet that matches one standing in the Ward. That is free continuity
and costs nothing to build.

---

## 5. The feud engine

The two wings should not be two independent timers talking past each other. The server already
supports a real call-and-response chain, and it is worth using.

`EmoteType.LocalSignal` (88) calls `Landblock.EmitSignal`, which walks every object in the
landblock, and for each one with `HearLocalSignals` set and within `HearLocalSignalsRadius`,
fires `EmoteCategory.ReceiveLocalSignal` (37) with the signal name in the `Quest` column
(`Landblock.cs:1487-1502`, `EmoteManager.cs:4248`).

So: Bexley opens on his heartbeat, signals; Splotch hears the signal and answers three seconds
later; Splotch signals again and Fenwick closes it out. One joke, three NPCs, no code.

**Property setup on every NPC that must hear:** `weenie_properties_int` type 290
(`HearLocalSignals`) = 1, type 291 (`HearLocalSignalsRadius`) = 60.

**Opener - Bexley** (`category` 5 = HeartBeat, `probability` 0.04; at the default 5s heartbeat that
is roughly one exchange every two minutes, which is about right for a room people stand in):

```sql
-- emote header
INSERT INTO `weenie_properties_emote`
  (`object_Id`, `category`, `probability`, `quest`)
VALUES
  (78780210, 5, 0.04, NULL);

-- actions: say the line, then signal the other wing
INSERT INTO `weenie_properties_emote_action`
  (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES
  (@bexley_emote, 0, 8,  0.0, 0.0, 'A Browerk is brown. That is what the word means.'),
  (@bexley_emote, 1, 88, 0.0, 0.0, 'annex_feud_a1');
```

**Answer - Splotch** (`category` 37 = ReceiveLocalSignal, `quest` matches the signal name,
`probability` 1.0; the `delay` on the first action is the comic beat):

```sql
INSERT INTO `weenie_properties_emote`
  (`object_Id`, `category`, `probability`, `quest`)
VALUES
  (78780220, 37, 1.0, 'annex_feud_a1');

INSERT INTO `weenie_properties_emote_action`
  (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES
  (@splotch_emote, 0, 8,  3.0, 0.0, 'It means BORING. Say the quiet part, Bexley.'),
  (@splotch_emote, 1, 88, 0.0, 0.0, 'annex_feud_c1');
```

**Closer - Fenwick** (`probability` 0.5, so he only sometimes bothers):

```sql
INSERT INTO `weenie_properties_emote`
  (`object_Id`, `category`, `probability`, `quest`)
VALUES
  (78780200, 37, 0.5, 'annex_feud_c1');

INSERT INTO `weenie_properties_emote_action`
  (`emote_Id`, `order`, `type`, `delay`, `extent`, `message`)
VALUES
  (@fenwick_emote, 0, 8, 3.5, 0.0, 'Please. Both of you. There are customers.');
```

Write five or six of these chains (`annex_feud_a1` through `a6`) and let the heartbeat pick one at
random. Six three-beat exchanges is enough that a player standing through one breeding session
never hears a repeat.

Useful emote action types for dressing these up: 8 Say, 88 LocalSignal, 5 Motion, 11 Turn,
9 Sound, 7 PhysScript, 13 TextDirect. `delay` is seconds before the action runs, `extent` widens
Say range when > 0.

**Two gating rules to design around**, both in `EmoteManager.cs`:

- HeartBeat emotes are filtered by the object's current stance and motion (line 3438-3444). If you
  give an NPC a `style`/`substyle`, it only speaks while in that pose. Leave both NULL unless you
  want that.
- `HeartBeat()` returns early for any creature where `IsAwake` is true (line 3888). Non-combatant
  NPCs stay asleep, so this is fine - but it is another reason the wing creatures must be built as
  proper NPCs and not as pacified monsters.

### Reacting to players

`Player.cs:1019` fires `OnHearChat` on every creature within 96m when a player speaks. The match on
`weenie_properties_emote.quest` is an exact, whole-message comparison, with `quest` = NULL matching
anything (`EmoteManager.cs:3429`).

- `quest` NULL, low probability: Fenwick occasionally mutters "I heard that" at whatever a player
  says. Cheap, works forever.
- `quest` set to an exact phrase: a password gag. Put "say 'the teal incident' to Splotch" on a
  wall and let him have a whole monologue ready. Exact-match means it has to be posted somewhere
  the player can read it.

---

## 6. What a first-timer walks through

1. Steps out of the portal into the Drop. Fenwick, mid-sigh, gives them the whoa.
2. Walks the spine. Passes Ivo. Hears Bexley snipe at something out of sight, then hears something
   out of the *other* side snipe back. Has not seen either faction yet.
3. Reaches the Ritual Floor. Now both wings are visible at once, and the argument they have been
   overhearing has faces. Gary is in the middle of the floor.
4. DJ Skulk tells them to dance. They dance. Their partner dances. It works, and it works *because*
   of the thing the drudge told them, which retroactively makes the drudge correct.
5. On a mutation, the spirit rises and the parents fight it on the dance floor, in front of both
   factions. This is the money shot of the whole area and it is why the Ritual Floor is central and
   open rather than tucked in a back room.
6. Baby in hand, they look through the office doorway on the way out and see Splotch's portrait
   hanging on Ruggan's wall.

---

## 7. Build order

1. Run `@showprops` and settle section 2.1. Nothing else is worth doing until the landblock and
   variant are known to be correct.
2. Pick the Ritual Floor room and verify with `@breed-debug` that it is a single landcell
   (section 2.2). Record the raw cell id.
3. Set `pet_breeding_allowed_landblock` to that raw cell and `pet_breeding_allowed_variant` to the
   annex variant. Test a breed with two accounts before any decoration exists.
4. Place Fenwick, Bexley, Splotch. Wire one feud chain. Stand in the room and confirm the three-beat
   exchange fires and the timing reads as a joke rather than three unrelated sentences.
5. Add the remaining five chains, then the wing creatures, then Skulk, Ivo and Gary.
6. Dress it: sign, barrels, lights, the office, the notes item, the portrait.
7. Turn on `pet_breeding_guardian_enabled` and watch one mutation fight on the floor. Tune
   `pet_breeding_guardian_health_mult` from the `slain after Ns` log line. Default 1.0 dies in
   seconds; the Content Guide suggests ~10 for a real fight.

### WCID block

`78780200`-`78780249` is reserved for the annex in `WCID_ALLOCATION_7878.md`. The fifteen
NPCs below are built and ready to load:
`Database/Updates/World/2026-09-09-00-Ruggans-Annex-NPCs.sql`.

| WCID | Object | Built from |
|---|---|---|
| 78780200 | Fenwick, Kennel Intern | 42720 Ealdred |
| 78780201 | Ivo, Ruggan's Quartermaster | 46425 Marid (vendor) |
| 78780202 | DJ Skulk | 5595 drudgeskulkerdancer |
| 78780203 | Gary | 29008 Browerk |
| 78780204 | Mrs. Ruggan | 3920 collectorsho |
| 78780210 | Bexley, Keeper of the Registry | 42720 Ealdred |
| 78780211 | Registered Browerk, Champion Line | 29008 Browerk |
| 78780212 | Certified Shreth, Third Generation | 4108 Gnawer Shreth |
| 78780213 | Pedigreed Ursuin (Papers Pending) | 7990 Field Ursuin |
| 78780214 | Drudge Skulker of Record | 7 Drudge Skulker |
| 78780220 | Splotch | 29008 Browerk + mutation palette |
| 78780221 | The Teal Incident | 4108 Gnawer Shreth + mutation palette |
| 78780222 | Ursuin, Unregistered | 7990 Field Ursuin + mutation palette |
| 78780223 | Nine-Colour Shreth | 4110 Blood Shreth + mutation palette |
| 78780224 | Subject Twelve | 7 Drudge Skulker + mutation palette |

Still to build: `78780205` Ruggan's Notes and `78780206` the knocked-over sign, both readable
book items rather than NPCs.

The Ward's five mutation palettes are palette ids this server's breeding code has already
rolled (they are sitting in `ace_shard.biota_properties_int` type 9035), so they are known to
pass `PetMutationService.IsUsableCreaturePalette` rather than being guesses from the DAT range.

Gary is built plain. The joke is that he has one mutated limb, which needs a partial palette -
`weenie_properties_palette` rows take `offset` and `length`, so a range covering one body part
does it, but the offsets are model-specific and want eyeballing in the visualizer.

Existing items are placed, not created: 98760399 Neutering Kit, 98760400 Tailoring Kit,
98760401 Tailoring Kit (Primed), from
`Content/sql/weenies/98760399-98760401 Pet Tailoring and Neutering Kits.sql`.

---

## 8. Traps

- **The multi-cell ritual room.** Covered in 2.2. This is the one that wastes a day.
- **Do not make the wing creatures attackable.** The mating guardian is the only fight in the
  annex, it only fights pets, and a player who kills a Registry Browerk breaks the joke and the
  respawn. `Attackable` = False, `TargetingTactic` = 0.
- **Do not put the wings in the same landcell as the ritual floor.** Anything summoned in that cell
  is a breeding candidate. Give the Ritual Floor its own cell with nothing else in it.
- **Ward palettes come from the mutation pool, not from invention.** Use the palette list the
  breeding roll draws from so the Ward looks like real offspring. The visualizer showroom previews
  them before you commit a weenie.
- **ASCII only in every emote message.** Apostrophes are fine; curly quotes, em dashes and ellipsis
  characters are not. The client draws them as garbage.
- **`@breed-debug` is a player-level command that currently prints every online player's location.**
  If the annex is going live to players, restrict its player list to the breeding landblock first.
