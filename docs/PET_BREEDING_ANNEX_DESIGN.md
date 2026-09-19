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

## 2. Where it goes

### 2.1 Landblock and variant: settled

The Seedy Motel is landblock `0x013A` (314 decimal), used through variation `3`. The portal
(98760388, `Database/Updates/World/2026-07-26-00-Seedy-Motel-Portal.sql`) drops players at cell
`0x013A02AE` in that variation, and the code defaults (`PropertyManager.cs`) are now
`pet_breeding_allowed_landblock` = `0x013A`, `pet_breeding_allowed_variant` = `3`. `0x016C`, which
older notes named, is the Marketplace.

One thing can still be wrong: a shard that stored 364 (`0x016C`) earlier keeps it, because stored
config overrides the code default. **Run `@showprops` before building** and `@modifylong` it if it
still says 364.

Build the annex inside the variation-3 instance. Variations are instanced copies of a landblock, so
the annex can be furnished without touching the retail dungeon, and the variant property already
pins breeding to that instance alone.

### 2.2 Which single landcell is the ritual floor

`CheckMultiplayerBreeding` (in `PetDevice_Breeding.cs`) requires **both summoned pets to share a
landcell**, and `MatchesBreedingArea` accepts a full raw cell as well as a 16-bit landblock:
a value above `0xFFFF` matches that exact cell only, a 16-bit value matches the whole landblock, and
0 means anywhere. This is covered by unit tests and works.

So you can gate breeding to exactly one room by setting `pet_breeding_allowed_landblock` to that
room's raw cell id (`0x013A02AE` is 20578990 decimal). That is the right call here: it makes the
Ritual Floor a real destination instead of "anywhere in the dungeon", and it stops two strangers in
a corridor accidentally pairing off. The same setting decides where players may heal their own pet,
so the exact-cell choice also means kits and heals only work on the floor.

**Build instruction:** stand in the candidate room and run `@breed-debug`. It prints
`Cell=0x........` and whether the current setting matches. Walk the whole room - every corner, both
sides of any pillar. If the cell id changes anywhere inside it, the room is more than one landcell and
two players standing on opposite sides of it will silently fail to breed. Either pick a smaller room,
or physically block players out of the second cell. Do not skip this check; the failure mode is
invisible.

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

**2. Quartermaster's nook.** A side alcove off the spine, not a blocker. Ivo sells the kits and
the breeding consumables.

**3. The Ritual Floor.** The breeding cell. This is the only room where the system works. Put the
holiday lights here at full density - it reads as a dance floor, which is exactly right, because
the ritual motion is literally `MotionCommand.DrudgeDance`. DJ Skulk, a drudge, stands on it. He is
not a joke bolted on; he is the mechanic wearing a costume. Keep this room open floor - the mating
guardian spawns just clear of one of the parent pets (their radii plus a 1.5 m gap) and the two pets need room to fight it.

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

Vendor (78780201). Stock: the Pet Neutering Kit (98760399) and the empty Pet Tailoring Kit
(98760400), plus the six breeding consumables (78780250-78780255: three Courtship Incense tiers,
Nurturing Draught, Chromatic Catalyst, Offering of Subjugation) once the sinks patch has run after
his weenie exists. The filled tailoring kit (98760401) is never sold; it only comes from extraction.
Deadpan.

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

The faction members are ordinary creature weenies made non-combatant. `Creature.IsNPC` is
`!(this is Player) && !Attackable && TargetingTactic == TargetingTactic.None`.

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

The two wings are not two independent timers talking past each other. The server supports a real
call-and-response chain, and the NPC patch uses it.

`EmoteType.LocalSignal` (88) calls `Landblock.EmitSignal`, which walks every object in the
landblock, and for each one with `HearLocalSignals` set and within `HearLocalSignalsRadius`,
fires `EmoteCategory.ReceiveLocalSignal` (37) with the signal name matched against the `quest`
column (`EmoteManager`).

**Property setup on every NPC that must hear:** `weenie_properties_int` type 290
(`HearLocalSignals`) = 1, type 291 (`HearLocalSignalsRadius`) = 60. Every annex NPC has both.

### What is wired (`2026-09-09-00-Ruggans-Annex-NPCs.sql`, part 4)

**Openers** (`category` 5 = HeartBeat, `probability` 0.03 each; on a 5 s heartbeat that is roughly
one opener every three minutes per speaker). Each says its line, then signals:

| Signal | Speaker | Line |
|---|---|---|
| annex_feud_a1 | Bexley | "A Browerk is brown. That is what the word means. That is the entire word." |
| annex_feud_a2 | Bexley | "Every creature in this lounge has papers..." |
| annex_feud_a3 | Splotch | "They call it purebred. I call it beige with a certificate." |
| annex_feud_a4 | Bexley | "Bloodline. Conformation. Restraint..." |
| annex_feud_a5 | Splotch | "The Professor called me an accident. Then he hung my portrait in his office." |
| annex_feud_a6 | Bexley | "I have been asked to stop using the phrase 'genetic vandalism.'..." |

**Answers** (`category` 37, `probability` 1, `quest` = the opener's signal; the `delay` on the Say
action is the comic beat, 3 s unless noted). Each answer signals a second-round name:

| Hears | Speaker | Line | Then signals |
|---|---|---|---|
| a1 | Splotch | "It means BORING. Say the quiet part, Bexley." | annex_feud_b1 |
| a2 | Splotch | "Papers? I have PALETTES." | annex_feud_b2 |
| a4 | Splotch | "We don't have a bloodline. We have a RANGE." | annex_feud_b2 |
| a6 | Splotch | "Ask them what colour they'll be next year. Go on. Ask them." | annex_feud_b1 |
| a3 | Bexley | "We do not acknowledge the ward." | annex_feud_b3 |
| b3 | Splotch | "He acknowledged us. Write that down." | annex_feud_b1 |
| a5 | Bexley (3.5 s) | "That is not a portrait. That is a case file." | annex_feud_b5 |

**Closers and choruses** on the second-round signals:

| Hears | Speaker | Probability | Delay | Line |
|---|---|---|---|---|
| b1 | Fenwick | 0.5 | 4.5 s | "Please. Both of you. There are customers." |
| b5 | Fenwick | 0.6 | 4.5 s | "It's a portrait. I framed it. I was told to frame it." |
| b1 | Gary | 0.25 | 5 s | "...I'm just here for the music." |
| b1 | each Ward creature (221-224) | 0.3 | 4 s | "HA." / "Say it louder, Splotch." / "That's the one..." / "Nobody over there has a nickname. Nobody." |
| b2 | each Registry creature (211-214) | 0.3 | 4 s | "Must he shout." / "One does not respond. One simply files." / "My papers are pending..." / "I have a lineage chart. It is very long." |

So `b1` means "the Ward won this round" and `b2` means "the Registry is being shouted at";
there is no `b4`. Six openers, two of them three-beat, is enough that a player standing through one
breeding session rarely hears a repeat.

The SQL shape, for adding a chain:

```sql
-- opener (HeartBeat): say, then signal
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780210, 5, 0.03, NULL);
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 0, 0, 'A Browerk is brown. That is what the word means.'),
  (@e, 1, 88, 0, 0, 'annex_feud_a1');

-- answer (ReceiveLocalSignal): quest = the signal name, delay = the beat
INSERT INTO `weenie_properties_emote` (`object_Id`,`category`,`probability`,`quest`)
VALUES (78780220, 37, 1, 'annex_feud_a1');
SET @e = LAST_INSERT_ID();
INSERT INTO `weenie_properties_emote_action` (`emote_Id`,`order`,`type`,`delay`,`extent`,`message`) VALUES
  (@e, 0, 8, 3, 0, 'It means BORING. Say the quiet part, Bexley.'),
  (@e, 1, 88, 0, 0, 'annex_feud_b1');
```

Useful emote action types for dressing these up: 8 Say, 88 LocalSignal, 5 Motion, 11 Turn,
9 Sound, 7 PhysScript, 13 TextDirect. `delay` is seconds before the action runs, `extent` widens
Say range when > 0.

**Idle lines** that belong to nobody's argument are also in the patch: three for Fenwick (0.02),
three for DJ Skulk (0.04), one for Gary (0.02) and one for Mrs. Ruggan (0.03).

**Two gating rules to design around**, both in `EmoteManager`:

- HeartBeat emotes are filtered by the object's current stance and motion. If you give an NPC a
  `style`/`substyle`, it only speaks while in that pose. Leave both NULL unless you want that.
- `HeartBeat()` returns early for any creature that is awake. Non-combatant NPCs stay asleep, so
  this is fine - but it is another reason the wing creatures must be built as proper NPCs and not
  as pacified monsters.

### Reacting to players

`Player.HandleActionTalk` fires `OnHearChat` on every creature within 96m when a player speaks. The
match on `weenie_properties_emote.quest` is an exact, whole-message, case-insensitive comparison,
with `quest` = NULL matching anything.

- `quest` NULL, low probability: Fenwick occasionally mutters "I heard that" at whatever a player
  says. Cheap, works forever.
- `quest` set to an exact phrase: a password gag. Put "say 'the teal incident' to Splotch" on a
  wall and let him have a whole monologue ready. Exact-match means it has to be posted somewhere
  the player can read it.

Neither of these is in the patch yet.

---

## 6. What a first-timer walks through

1. Steps out of the portal into the Drop. Fenwick, mid-sigh, gives them the whoa.
2. Walks the spine. Passes Ivo. Hears Bexley snipe at something out of sight, then hears something
   out of the *other* side snipe back. Has not seen either faction yet.
3. Reaches the Ritual Floor. Now both wings are visible at once, and the argument they have been
   overhearing has faces. Gary is in the middle of the floor.
4. DJ Skulk tells them to dance. They dance. Their partner dances. It works, and it works *because*
   of the thing the drudge told them, which retroactively makes the drudge correct.
5. On a mutation (with the guardian enabled), the spirit rises and the parents fight it on the dance
   floor, in front of both factions, for the Awakened Blessing. This is the money shot of the whole
   area and it is why the Ritual Floor is central and open rather than tucked in a back room. A
   player who bought an Offering of Subjugation from Ivo gets a short, weak spirit; everyone else
   gets the real fight.
6. Baby in hand, they look through the office doorway on the way out and see Splotch's portrait
   hanging on Ruggan's wall.

---

## 7. Build order

1. Run `@showprops` and confirm `pet_breeding_allowed_landblock` is 314 (0x013A) and
   `pet_breeding_allowed_variant` is 3. Fix a stale stored 364 with `@modifylong`.
2. Pick the Ritual Floor room and verify with `@breed-debug` that it is a single landcell
   (section 2.2). Record the raw cell id.
3. Set `pet_breeding_allowed_landblock` to that raw cell (decimal). Test a breed with two accounts
   before any decoration exists.
4. Load `2026-09-09-00-Ruggans-Annex-NPCs.sql`, then place Fenwick, Bexley, Splotch (part 5 of the
   patch: `@createinst <wcid>`, `@nudge`, `@rotate`, `@export-sql`). Stand in the room and confirm a
   three-beat exchange fires and the timing reads as a joke rather than three unrelated sentences.
5. Place the wing creatures, then Skulk, Ivo and Gary. Keep Bexley and Splotch within 60 m of each
   other and of Fenwick or the chains stop resolving.
6. Dress it: sign, barrels, lights, the office, the notes item, the portrait.
7. Turn on `pet_breeding_guardian_enabled` and watch one mutation fight on the floor. The guardian's
   incoming damage is self-normalising (about 15-30 pet hits, 10% per-hit cap) and its outgoing hit
   is 8% of the defending pet's health x `pet_breeding_guardian_damage_mult`, so
   `pet_breeding_guardian_health_mult` stretches the fight less than it looks. Tune from the
   `slain after Ns` log line; the Content Guide has the arithmetic.

### WCID block

`78780200`-`78780249` is reserved for the annex in `WCID_ALLOCATION_7878.md`. The fifteen
NPCs below are built in `Database/Updates/World/2026-09-09-00-Ruggans-Annex-NPCs.sql`; the patch
creates the weenies and does not place them.

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

Not built: `78780205` Ruggan's Notes and `78780206` the knocked-over sign (readable book items),
Gary's partial palette, the password gag, Fenwick's tutorial branch, and the placement rows.

The Ward's five mutation palettes are palette ids this server's breeding code has already
rolled (they are sitting in `ace_shard.biota_properties_int` type 9035), so they are known to
pass `PetMutationService.IsUsableCreaturePalette` rather than being guesses from the DAT range.

Gary is built plain. The joke is that he has one mutated limb, which needs a partial palette -
`weenie_properties_palette` rows take `offset` and `length`, so a range covering one body part
does it, but the offsets are model-specific and want eyeballing in the visualizer.

Existing items are placed or stocked, not created: 98760399 Neutering Kit, 98760400 Tailoring Kit,
98760401 Tailoring Kit (Filled), from
`Database/Updates/World/2026-09-09-01-Pet-Tailoring-and-Neutering-Kits.sql`, and the consumables
from `2026-09-12-00-Pet-Breeding-Sinks.sql`.

---

## 8. Traps

- **The multi-cell ritual room.** Covered in 2.2. This is the one that wastes a day.
- **A stale stored landblock.** The code default is right now, but `@showprops` wins. Check it.
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
- **`@breed-debug` is safe for players now.** It prints only the caller's own location and pet and a
  count of nearby candidates; the per-player list is admin-only. No need to restrict it.
