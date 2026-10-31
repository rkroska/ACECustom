"""Read-only: resist pattern per zone/tier, bag settings, and the real Slayer chance after the card floor/cap."""
import json, re, random, collections, pymysql

cfg = open(r"C:\Scripting\ACECustom\Source\ACE.Server\bin\x64\Release\net10.0\Config.js", encoding="utf-8").read()
cfg = re.sub(r"/\*.*?\*/", "", cfg, flags=re.S)
cfg = re.sub(r"(?m)^\s*//.*$", "", cfg)
s = json.loads(cfg)["MySql"]["Shard"]
c = pymysql.connect(host=s["Host"], port=int(s["Port"]), user=s["Username"], password=s["Password"], database=s["Database"]).cursor()
c.execute("select value from config_properties_string where `key`='zonecontrol_data'")
store = json.loads(c.fetchone()[0])

EL = ["slash", "pierce", "bludgeon", "fire", "cold", "acid", "electric"]
print("RESIST PATTERN per zone (overridden monsters only)")
print(f"{'zone':<12} {'var':>3} {'with resists':>12} {'1 weak':>7} {'2+ weak':>8} {'lo':>5} {'hi':>5} nether values")
for a in sorted(store["areas"], key=lambda a: a["variation"]):
    over = a["profile"].get("wcidOverrides") or {}
    one = many = n = 0
    lows, highs, neth = set(), set(), collections.Counter()
    for wo in over.values():
        st = wo.get("stats") or {}
        vals = {e: (st.get("resist_" + e) or {"base": 1.0})["base"] for e in EL}
        if not any("resist_" + e in st for e in EL):
            continue
        n += 1
        top = max(vals.values())
        k = sum(1 for v in vals.values() if v == top)
        one += k == 1
        many += k > 1
        lows.add(min(vals.values()))
        highs.add(top)
        neth[(st.get("resist_nether") or {"base": 1.0})["base"]] += 1
    print(f"{a['name'][:12]:<12} {a['variation']:>3} {n:>12} {one:>7} {many:>8} {min(lows) if lows else 0:>5} {max(highs) if highs else 0:>5} {dict(neth)}")

def find(o, want, path=""):
    if isinstance(o, dict):
        for k, v in o.items():
            if k in want and isinstance(v, dict) and "base" in v:
                yield path + "." + k, v["base"]
            yield from find(v, want, path + "." + str(k))
    elif isinstance(o, list):
        for i, v in enumerate(o):
            yield from find(v, want, f"{path}[{i}]")

print("\nBAG settings found in the store:")
hits = list(find(store, {"bag_odds", "bag_weight_vengeance", "bag_weight_memory"}))
print(" count", len(hits))
for h in hits[:8]:
    print(" ", h)

CARDS = ["weapon_imbue_chance", "weapon_slayer_chance", "weapon_bite_chance", "weapon_crush_chance", "weapon_armor_rend_chance",
         "weapon_shield_cleave_chance", "weapon_cleave_chance", "weapon_split_chance", "weapon_proc_arc_chance", "weapon_proc_ring_chance", "weapon_spell_armor_rend_chance"]
ELIG = {"melee": [1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 0], "missile": [1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0], "caster": [1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1]}
print("\nSLAYER CHANCE after floor/cap (simulated, uniform drop / top-up from losers):")
print(f"{'var':>3} {'chance':>7} {'min':>4} {'cap':>4} {'band':>11} | melee missile caster | rare S")
for v in (11, 14, 18, 25):
    st = store["variationDefaults"][str(v)]["profile"].get("stats") or {}
    g = lambda k, d=0.0: st[k]["base"] if k in st else d
    # tier-suffixed values
    gt = lambda k, d=0.0: st[f"{k}_t{v}"]["base"] if f"{k}_t{v}" in st else g(k, d)
    ch = [gt(x) for x in CARDS]
    mn, cap = gt("weapon_modifier_min"), gt("weapon_modifier_cap")
    out = []
    for cls, el in ELIG.items():
        N, hit = 200000, 0
        for _ in range(N):
            won = [i for i in range(11) if el[i] and random.random() < ch[i]]
            if len(won) > cap:
                won = random.sample(won, int(cap))
            elif len(won) < mn:
                losers = [i for i in range(11) if el[i] and ch[i] > 0 and i not in won]
                random.shuffle(losers)
                won += losers[: int(mn) - len(won)]
            hit += 1 in won
        out.append(hit / N)
    print(f"{v:>3} {gt('weapon_slayer_chance'):>7.3f} {mn:>4.0f} {cap:>4.0f} {gt('weapon_slayer_min'):>5.2f}-{gt('weapon_slayer_max'):<5.2f} | {out[0]:>5.1%} {out[1]:>7.1%} {out[2]:>6.1%}")
    print("     card chances", {x.replace('weapon_', '').replace('_chance', ''): round(y, 3) for x, y in zip(CARDS, ch)})
    print("     rend", gt("weapon_rend_power_min"), gt("weapon_rend_power_max"))
