#!/usr/bin/env python
"""
Generator for Database/Updates/World/2026-10-09-00-Tiered-Colosseum.sql

Reads the LOCAL world database READ-ONLY (SELECT only) and writes the SQL file next to the other
world updates. Nothing from the retail Colosseum is hand-copied: property rows are cloned with
INSERT ... SELECT from the retail weenies, and the Arena Arbitrator's emote sets are computed by a
closure over the retail Master Arbitrator (34441) and emitted as literal rows read from the database.

Run:  python gen/tiered_colosseum_gen.py          (from Database/Updates/World)
Creds come from the server's Config.js (MySql.World).
All generated text is 7-bit ASCII (CLAUDE.md); the script asserts it.
"""
import json
import os
import re
import sys

import pymysql

CONFIG = r'C:\Scripting\ACECustom\Source\ACE.Server\bin\x64\Release\net10.0\Config.js'
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '2026-10-09-00-Tiered-Colosseum.sql')

# ---- ids (must match TieredDungeonRegistry.cs) -------------------------------------------------
ARBITRATOR, TICKET, SELLER, EXIT_PORTAL = 78780800, 78780801, 78780802, 78780803
ENTRY_PORTAL = {t: 78780800 + t for t in range(11, 16)}          # 78780811 .. 78780815

RETAIL_ARBITRATOR, RETAIL_TICKET, RETAIL_SELLER, RETAIL_PORTAL_TEMPLATE, RETAIL_COLO_PORTAL = 34441, 34449, 34442, 777705100, 34508
COLO_COIN, TRADE_NOTE_250K = 36518, 20630

MIN_WIELD_AUGS = {11: 2000, 12: 2500, 13: 3000, 14: 3500, 15: 4000}
MIN_WIELD_CREATURE = {11: 4000, 12: 4500, 13: 5000, 14: 5500, 15: 6000}

# enums (ACE.Entity/Enum)
CAT_HEARTBEAT, CAT_GIVE, CAT_USE = 5, 6, 7
CAT_QUEST_SUCCESS, CAT_QUEST_FAILURE = 12, 13
CAT_TEST_SUCCESS, CAT_TEST_FAILURE = 22, 23
CAT_EVENT_SUCCESS, CAT_EVENT_FAILURE = 27, 28
CAT_TEST_NO_QUALITY, CAT_QUEST_NO_FELLOW = 29, 30
CAT_TEST_NO_FELLOW, CAT_GOTO = 31, 32
CAT_NUM_FELLOWS_SUCCESS, CAT_NUM_FELLOWS_FAILURE = 33, 34

T_GIVE, T_TELL, T_TAKE_ITEMS, T_YESNO, T_OWNS_ITEMS, T_GOTO = 3, 10, 74, 75, 76, 67
T_TIER_PAY = 160
T_ADD_TITLE, T_AWARD_XP, T_AWARD_LEVEL_XP, T_AWARD_NOSHARE_XP, T_AWARD_SKILL_XP = 34, 2, 49, 62, 28

# EmoteManager.ExecuteEmote dispatch: emote type -> categories its message is looked up in (quest key = message)
_QUEST_INQ = [21, 80, 30, 82, 102, 103, 104, 105, 135, 151, 152, 20, 79]       # Inq(My)Quest, Solves, Bits, Server, PetRegistry, Update(My)Quest
_QUEST_INQ_FELLOW = [58, 60]                                                     # InqFellowQuest, UpdateFellowQuest
_TEST_INQ = [35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 114, 121, 76, 89, 75, 131]  # stat / skill / yesno / ownsitems / packspace / contracts / QuestCompletionCount
DISPATCH = {T_GOTO: [CAT_GOTO], 51: [CAT_EVENT_SUCCESS, CAT_EVENT_FAILURE],
            59: [CAT_TEST_NO_FELLOW, CAT_NUM_FELLOWS_SUCCESS, CAT_NUM_FELLOWS_FAILURE]}
for _t in _QUEST_INQ:
    DISPATCH[_t] = [CAT_QUEST_SUCCESS, CAT_QUEST_FAILURE]
for _t in _QUEST_INQ_FELLOW:
    DISPATCH[_t] = [CAT_QUEST_SUCCESS, CAT_QUEST_FAILURE, CAT_QUEST_NO_FELLOW]
for _t in _TEST_INQ:
    DISPATCH[_t] = [CAT_TEST_SUCCESS, CAT_TEST_FAILURE, CAT_TEST_NO_QUALITY]

# properties a tier portal must NOT inherit from the template (level / account / quest / use-count / gates)
PORTAL_DROP_INT = [26, 86, 87, 9019, 9020, 9021, 9022, 9023, 9024, 9040] + list(range(50112, 50121))
PORTAL_DROP_STRING = [33, 37]
PORTAL_DROP_BOOL = [43]
PORTAL_BITMASK_INT = 111
BITMASK_TIER_ENTRY = 0x01 | 0x10 | 0x20      # Unrestricted | NoSummon | NoRecall (NoRecall also blocks Portal Tie) = 49

PROP_TABLES = ['int', 'int64', 'float', 'bool', 'string', 'd_i_d', 'i_i_d', 'position', 'attribute', 'attribute_2nd',
               'skill', 'body_part', 'palette', 'texture_map', 'anim_part', 'spell_book', 'create_list', 'generator',
               'event_filter', 'book', 'book_page_data']


# ---- db ---------------------------------------------------------------------------------------
def connect():
    s = open(CONFIG, encoding='utf-8-sig').read()
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    s = re.sub(r'^\s*//.*$', '', s, flags=re.M)
    c = json.loads(s)['MySql']['World']
    return pymysql.connect(host=c['Host'], port=int(c['Port']), user=c['Username'], password=c['Password'],
                           database=c['Database'], autocommit=True)


CN = connect()


def q(sql, args=None):
    with CN.cursor() as cur:
        assert sql.lstrip().lower().startswith(('select', 'show')), 'generator is read-only'
        cur.execute(sql, args)
        cols = [d[0] for d in cur.description]
        return cols, cur.fetchall()


def columns(table):
    _, r = q("SELECT column_name FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name=%s "
             "ORDER BY ordinal_position", (table,))
    return [x[0] for x in r]


# ---- sql helpers ------------------------------------------------------------------------------
def lit(v):
    if v is None:
        return 'NULL'
    if isinstance(v, (bytes, bytearray)):
        return str(int.from_bytes(v, 'big'))
    if isinstance(v, bool):
        return 'True' if v else 'False'
    if isinstance(v, int):
        return str(v)
    if isinstance(v, float):
        return repr(v)
    s = str(v).replace('\\', '\\\\').replace("'", "''").replace('\r', '\\r').replace('\n', '\\n')
    return "'" + s + "'"


def check_ascii(text, what):
    bad = [(i, ch) for i, ch in enumerate(text) if ord(ch) > 126 or (ord(ch) < 32 and ch not in '\n\t')]
    if bad:
        raise SystemExit(f'non-ASCII / control character in {what}: {bad[:5]}')


out = []


def emit(s=''):
    out.append(s)


def weenie_header(new, retail, class_name, comment):
    _, r = q('SELECT type FROM weenie WHERE class_Id=%s', (retail,))
    assert r, f'retail weenie {retail} missing'
    emit(f'-- {comment}')
    emit(f'DELETE FROM `weenie` WHERE `class_Id` = {new};')
    emit(f'INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)')
    emit(f"VALUES ({new}, {lit(class_name)}, {r[0][0]}, NOW());")
    emit()


def clone_props(new, retail, skip_tables=(), drop=None):
    """INSERT ... SELECT of every property table of `retail` (not emotes), minus the `drop` {table: (type col, [types])}."""
    drop = drop or {}
    for t in PROP_TABLES:
        if t in skip_tables:
            continue
        table = f'weenie_properties_{t}'
        cols = [c for c in columns(table) if c not in ('id', 'object_Id')]
        if not cols:
            continue
        where = f'`object_Id` = {retail}'
        if t in drop:
            col, types = drop[t]
            where += f' AND `{col}` NOT IN ({", ".join(str(x) for x in types)})'
        _, cnt = q(f'SELECT COUNT(*) FROM `{table}` WHERE {where}')
        if not cnt[0][0]:
            continue
        cl = ', '.join(f'`{c}`' for c in cols)
        emit(f'INSERT INTO `{table}` (`object_Id`, {cl})')
        emit(f'  SELECT {new}, {cl} FROM `{table}` WHERE {where};')
    emit()


# ---- emotes -----------------------------------------------------------------------------------
EMOTE_COLS = ['category', 'probability', 'weenie_Class_Id', 'style', 'substyle', 'quest', 'vendor_Type',
              'min_Health', 'max_Health', 'damage_type']
ACTION_COLS = [c for c in columns('weenie_properties_emote_action') if c not in ('id', 'emote_Id')]


def emit_emote_set(new, es, actions, label):
    """es: dict of emote columns; actions: list of dict of action columns (order already set)."""
    emit(f'-- {label}')
    emit('INSERT INTO `weenie_properties_emote` (`object_Id`, ' + ', '.join(f'`{c}`' for c in EMOTE_COLS) + ')')
    emit(f'VALUES ({new}, ' + ', '.join(lit(es.get(c)) for c in EMOTE_COLS) + ');')
    emit('SET @parent_id = LAST_INSERT_ID();')
    emit('INSERT INTO `weenie_properties_emote_action` (`emote_Id`, ' + ', '.join(f'`{c}`' for c in ACTION_COLS) + ')')
    rows = []
    for a in actions:
        rows.append('(@parent_id, ' + ', '.join(lit(a.get(c)) for c in ACTION_COLS) + ')')
    emit('VALUES ' + '\n     , '.join(rows) + ';')
    emit()


def act(order, typ, delay=0.0, **kw):
    d = {'order': order, 'type': typ, 'delay': delay, 'extent': 1.0}
    d.update(kw)
    return d


def item_act(order, typ, wcid, stack, **kw):
    # same row shape as the retail Give / TakeItems / InqOwnsItems rows
    return act(order, typ, destination_Type=0, weenie_Class_Id=wcid, stack_Size=stack, palette=0, shade=0.0,
               try_To_Bond=0, **kw)


# ---- Arbitrator closure -----------------------------------------------------------------------
def arbitrator_sets():
    cols, sets = q('SELECT * FROM weenie_properties_emote WHERE object_Id=%s ORDER BY id', (RETAIL_ARBITRATOR,))
    sets = [dict(zip(cols, s)) for s in sets]
    acols, arows = q('SELECT a.* FROM weenie_properties_emote_action a JOIN weenie_properties_emote e ON e.id=a.emote_Id '
                     'WHERE e.object_Id=%s ORDER BY a.emote_Id, a.`order`', (RETAIL_ARBITRATOR,))
    acts = {}
    for r in arows:
        d = dict(zip(acols, r))
        acts.setdefault(d['emote_Id'], []).append(d)

    keep = {s['id'] for s in sets if (s['category'] == CAT_GIVE and s['weenie_Class_Id'] == RETAIL_TICKET)
            or s['category'] == CAT_HEARTBEAT}
    frontier = sorted(keep)
    unknown = set()
    while frontier:
        sid = frontier.pop()
        for a in acts.get(sid, []):
            cats = DISPATCH.get(a['type'])
            if cats is None:
                continue
            for s in sets:
                if s['id'] not in keep and s['category'] in cats and s['quest'] == a['message']:
                    keep.add(s['id'])
                    frontier.append(s['id'])
    kept = [s for s in sets if s['id'] in keep]

    # safety stops
    for s in kept:
        for a in acts.get(s['id'], []):
            if a['type'] in (T_ADD_TITLE, T_AWARD_XP, T_AWARD_LEVEL_XP, T_AWARD_NOSHARE_XP, T_AWARD_SKILL_XP, 113):
                raise SystemExit(f'STOP: closure pulled in a reward action type {a["type"]} (set {s["id"]} cat {s["category"]} quest {s["quest"]})')
            if a['weenie_Class_Id'] == COLO_COIN:
                raise SystemExit(f'STOP: closure pulled in Colosseum Coin give (set {s["id"]})')
    return kept, acts


def swap_ticket(v):
    return TICKET if v == RETAIL_TICKET else v


def build_arbitrator():
    weenie_header(ARBITRATOR, RETAIL_ARBITRATOR, 'tiered-colosseum-arbitrator',
                  'Arena Arbitrator (tier copy of the Master Arbitrator 34441; no rewards, pays through emote type 160)')
    clone_props(ARBITRATOR, RETAIL_ARBITRATOR)
    emit(f"UPDATE `weenie_properties_string` SET `value` = 'Arena Arbitrator' WHERE `object_Id` = {ARBITRATOR} AND `type` = 1;")
    emit()
    kept, acts = arbitrator_sets()
    summary = []
    for s in kept:
        es = {c: s[c] for c in EMOTE_COLS}
        if s['category'] == CAT_GIVE and s['weenie_Class_Id'] == RETAIL_TICKET:
            es['weenie_Class_Id'] = TICKET
        alist = []
        for a in acts.get(s['id'], []):
            d = {c: a[c] for c in ACTION_COLS}
            d['weenie_Class_Id'] = swap_ticket(d['weenie_Class_Id'])
            alist.append(d)
        assert es['weenie_Class_Id'] != RETAIL_TICKET and all(x['weenie_Class_Id'] != RETAIL_TICKET for x in alist)
        label = f'copied from {RETAIL_ARBITRATOR}: category {es["category"]} quest {es["quest"]!r}'
        emit_emote_set(ARBITRATOR, es, alist, label)
        summary.append((es['category'], es['quest'], [x['type'] for x in alist]))
    # the one new set: Use pays out
    emit_emote_set(ARBITRATOR, {'category': CAT_USE, 'probability': 1.0},
                   [act(0, T_TIER_PAY)], 'NEW: Use -> AwardTieredDungeonPay (160)')
    summary.append((CAT_USE, None, [T_TIER_PAY]))
    return summary


# ---- ticket / seller --------------------------------------------------------------------------
def build_ticket():
    weenie_header(TICKET, RETAIL_TICKET, 'tiered-colosseum-ticket', 'Tiered Colosseum Ticket (clone of 34449)')
    clone_props(TICKET, RETAIL_TICKET)
    emit(f"UPDATE `weenie_properties_string` SET `value` = 'Tiered Colosseum Ticket' WHERE `object_Id` = {TICKET} AND `type` = 1;")
    # string 14 = Use text, 16 = LongDesc (PropertyString.cs). Retail's text names the Colosseum Arbitrator.
    emit(f"UPDATE `weenie_properties_string` SET `value` = 'Give this to the Arena Arbitrator to start a tiered arena run for your fellowship.' WHERE `object_Id` = {TICKET} AND `type` = 14;")
    emit(f"UPDATE `weenie_properties_string` SET `value` = 'This ticket is given to the Arena Arbitrator to start a tiered arena run for your fellowship.' WHERE `object_Id` = {TICKET} AND `type` = 16;")
    emit()


def build_seller():
    weenie_header(SELLER, RETAIL_SELLER, 'tiered-colosseum-ticket-seller',
                  'Tiered Colosseum Ticket Seller (non-emote clone of 34442; emotes are new)')
    clone_props(SELLER, RETAIL_SELLER)
    emit(f"UPDATE `weenie_properties_string` SET `value` = 'Tiered Colosseum Ticket Seller' WHERE `object_Id` = {SELLER} AND `type` = 1;")
    emit()
    # Row shapes mirror weenie 34952 (Ley Line): InqYesNo message = key of the TestSuccess / TestFailure sets, test_String = question;
    # InqOwnsItems message = key, weenie_Class_Id + stack_Size = the required item; TakeItems weenie_Class_Id + stack_Size.
    k_yn, k_own = 'TierTicketYesNo', 'TierTicketOwns'
    emit_emote_set(SELLER, {'category': CAT_USE, 'probability': 1.0},
                   [act(0, T_YESNO, message=k_yn, test_String='Buy a Tiered Colosseum Ticket for 100 MMD trade notes?')],
                   'Use: ask yes/no')
    emit_emote_set(SELLER, {'category': CAT_TEST_FAILURE, 'probability': 1.0, 'quest': k_yn},
                   [act(0, T_TELL, message='Very well.')], 'No')
    emit_emote_set(SELLER, {'category': CAT_TEST_SUCCESS, 'probability': 1.0, 'quest': k_yn},
                   [item_act(0, T_OWNS_ITEMS, TRADE_NOTE_250K, 100, message=k_own)], 'Yes: does the player own 100 trade notes?')
    emit_emote_set(SELLER, {'category': CAT_TEST_FAILURE, 'probability': 1.0, 'quest': k_own},
                   [act(0, T_TELL, message='You need 100 MMD trade notes for a ticket.')], 'Not enough notes')
    # Give FIRST, then TakeItems: Give has an inventory-space / burden pre-check (EmoteManager Give case + DoEnqueue scan-ahead) that
    # aborts the whole chain before anything is taken; so a full pack never costs the player notes.
    emit_emote_set(SELLER, {'category': CAT_TEST_SUCCESS, 'probability': 1.0, 'quest': k_own},
                   [item_act(0, T_GIVE, TICKET, 1),
                    item_act(1, T_TAKE_ITEMS, TRADE_NOTE_250K, 100),
                    act(2, T_TELL, message='Here is your ticket. Give it to the Arena Arbitrator.')],
                   'Enough notes: give the ticket, take the notes')


# ---- portals ----------------------------------------------------------------------------------
def retail_position(wcid):
    cols, r = q('SELECT * FROM weenie_properties_position WHERE object_Id=%s AND position_Type=2', (wcid,))
    assert r, f'no destination on {wcid}'
    return dict(zip(cols, r[0]))


def emit_position(new, cell, x, y, z, w, ax, ay, az, variation):
    emit('INSERT INTO `weenie_properties_position` (`object_Id`, `position_Type`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, '
         '`angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `variation_Id`)')
    emit(f'VALUES ({new}, 2 /* Destination */, {cell} /* 0x{cell:08X} */, {lit(x)}, {lit(y)}, {lit(z)}, {lit(w)}, {lit(ax)}, {lit(ay)}, {lit(az)}, {lit(variation)});')
    emit()


PORTAL_DROP = {'position': ('position_Type', [2, 1, 3, 4, 5, 6, 7, 8, 9, 10]),
               'int': ('type', PORTAL_DROP_INT), 'string': ('type', PORTAL_DROP_STRING), 'bool': ('type', PORTAL_DROP_BOOL)}


def dropped_report():
    rep = []
    _, r = q('SELECT type, value FROM weenie_properties_int WHERE object_Id=%s AND type IN (%s) ORDER BY type'
             % ('%s', ','.join(str(x) for x in PORTAL_DROP_INT)), (RETAIL_PORTAL_TEMPLATE,))
    rep += [f'int {t}={v}' for t, v in r]
    _, r = q('SELECT position_Type FROM weenie_properties_position WHERE object_Id=%s', (RETAIL_PORTAL_TEMPLATE,))
    rep += [f'position type {t}' for (t,) in r]
    for tbl, types in (('string', PORTAL_DROP_STRING), ('bool', PORTAL_DROP_BOOL)):
        _, r = q(f'SELECT type FROM weenie_properties_{tbl} WHERE object_Id=%s AND type IN ({",".join(str(x) for x in types)})',
                 (RETAIL_PORTAL_TEMPLATE,))
        rep += [f'{tbl} {t}' for (t,) in r]
    return rep


def build_exit():
    weenie_header(EXIT_PORTAL, RETAIL_PORTAL_TEMPLATE, 'tiered-colosseum-exit',
                  f'Tier exit portal (clone of {RETAIL_PORTAL_TEMPLATE} without positions, level / quest / gate properties)')
    clone_props(EXIT_PORTAL, RETAIL_PORTAL_TEMPLATE, drop=PORTAL_DROP)
    emit(f"UPDATE `weenie_properties_string` SET `value` = 'Tou Tou' WHERE `object_Id` = {EXIT_PORTAL} AND `type` = 1;")
    emit()
    d = retail_position(RETAIL_PORTAL_TEMPLATE)
    # variation NULL: Portal.ActOnUse fills in the player's tier for this wcid
    emit_position(EXIT_PORTAL, 0xF559003D, d['origin_X'], d['origin_Y'], d['origin_Z'],
                  d['angles_W'], d['angles_X'], d['angles_Y'], d['angles_Z'], None)


def build_entries():
    d = retail_position(RETAIL_COLO_PORTAL)
    for tier in range(11, 16):
        wcid = ENTRY_PORTAL[tier]
        weenie_header(wcid, RETAIL_PORTAL_TEMPLATE, f'tiered-colosseum-entry-t{tier}',
                      f'Tiered Colosseum entry portal, tier {tier} (NOT placed in the world)')
        drop = dict(PORTAL_DROP)
        drop['int'] = ('type', PORTAL_DROP_INT + [PORTAL_BITMASK_INT])
        clone_props(wcid, RETAIL_PORTAL_TEMPLATE, drop=drop)
        emit(f"UPDATE `weenie_properties_string` SET `value` = 'Tiered Colosseum' WHERE `object_Id` = {wcid} AND `type` = 1;")
        emit('INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`) VALUES')
        emit(f'  ({wcid}, 111, {BITMASK_TIER_ENTRY}) /* PortalBitmask: Unrestricted | NoSummon | NoRecall (no tie) */')
        emit(f'  , ({wcid}, 9019, 1) /* PortalReqType = CreatureAug */')
        emit(f'  , ({wcid}, 9020, {MIN_WIELD_CREATURE[tier]}) /* PortalReqValue: creature augs >= MinWieldCreature */')
        emit(f'  , ({wcid}, 9022, 2) /* PortalReqType2 = ItemAug */')
        emit(f'  , ({wcid}, 9023, {MIN_WIELD_AUGS[tier]}); /* PortalReqValue2: item augs >= MinWieldAugs */')
        emit()
        emit_position(wcid, 0x00AF0118, d['origin_X'], d['origin_Y'], d['origin_Z'],
                      d['angles_W'], d['angles_X'], d['angles_Y'], d['angles_Z'], tier)


# ---- quests -----------------------------------------------------------------------------------
QUESTS = [
    ('TieredColoRooms', 0, -1, 'Tiered Colosseum: rooms credited this run (bit per room)'),
    ('TieredColoTopTier', 0, -1, 'Tiered Colosseum: highest tier credited this run'),
    ('TieredColoRun', 0, -1, 'Tiered Colosseum: start of the current run'),
    ('TieredColoPayTimer', 72000, -1, 'Tiered Colosseum: pay timer (20 hours, shared by every tier)'),
]


def build_quests():
    emit('-- Quest rows')
    emit('DELETE FROM `quest` WHERE `name` IN (' + ', '.join(lit(n) for n, *_ in QUESTS) + ');')
    emit('INSERT INTO `quest` (`name`, `min_Delta`, `max_Solves`, `message`, `last_Modified`, `is_ip_restricted`, `ip_loot_limit`)')
    emit('VALUES ' + '\n     , '.join(f'({lit(n)}, {d}, {m}, {lit(msg)}, NOW(), 0, 2)' for n, d, m, msg in QUESTS) + ';')
    emit()


# ---- main -------------------------------------------------------------------------------------
def main():
    body_start = len(out)
    summary = build_arbitrator()
    build_ticket()
    build_seller()
    build_exit()
    build_entries()
    build_quests()

    header = f"""-- =====================================================================================
-- Tiered Colosseum - world content  (GENERATED by gen/tiered_colosseum_gen.py - do not hand edit; re-run the generator)
--
-- Creates:
--   {ARBITRATOR}  tiered-colosseum-arbitrator   "Arena Arbitrator": clone of Master Arbitrator {RETAIL_ARBITRATOR}, only the ticket / heartbeat
--                 emote sets (reachable by closure), plus a Use set that runs AwardTieredDungeonPay (EmoteType 160)
--   {TICKET}  tiered-colosseum-ticket       "Tiered Colosseum Ticket": clone of Colosseum Ticket {RETAIL_TICKET}
--   {SELLER}  tiered-colosseum-ticket-seller "Tiered Colosseum Ticket Seller": clone of {RETAIL_SELLER}, new emotes (100 x Trade Note {TRADE_NOTE_250K} -> 1 ticket)
--   {EXIT_PORTAL}  tiered-colosseum-exit        "Tou Tou": exit portal to cell 0xF559003D (variation filled in by the server from the player's tier)
--   {ENTRY_PORTAL[11]}..{ENTRY_PORTAL[15]}  tiered-colosseum-entry-t11 .. -t15  "Tiered Colosseum": entry portals, variation = tier,
--                 gates: creature augs >= {MIN_WIELD_CREATURE[11]}..{MIN_WIELD_CREATURE[15]}, item augs >= {MIN_WIELD_AUGS[11]}..{MIN_WIELD_AUGS[15]} (T11..T15)
--   quest rows: {', '.join(n for n, *_ in QUESTS)}
--
-- NOT DONE HERE: the entry portals are NOT placed in the world (no landblock_instance rows). The owner
-- places them once a home is chosen. The seller, Arbitrator
-- and exit portal are placed at run time by TieredDungeonRegistry (WcidSwaps / LobbyExtraRows).
--
-- Cloning is INSERT ... SELECT from the retail rows, so run this file against a world database that still holds
-- the retail weenies {RETAIL_ARBITRATOR}, {RETAIL_TICKET}, {RETAIL_SELLER}, {RETAIL_PORTAL_TEMPLATE}. Emote quest / event names are copied verbatim:
-- the server tags them per tier at run time ("Name~v11").
-- All text is 7-bit ASCII per CLAUDE.md. Re-runnable: each DELETE cascades to every weenie_properties_* table.
-- Target database: ace_world
-- =====================================================================================
"""
    text = header + '\n'.join(out[body_start:]) + '\n'
    check_ascii(text, 'generated SQL')
    with open(OUT, 'w', encoding='ascii', newline='\n') as f:
        f.write(text)
    print('wrote', os.path.normpath(OUT), len(text), 'bytes')
    print('Arbitrator kept sets (category, quest, action types):')
    for c, qn, ts in summary:
        print(f'  {c:>3}  {qn!s:45}  {ts}')
    print('portal properties dropped from the clones:', dropped_report())


if __name__ == '__main__':
    main()
