"""Read-only: the real damage-type mix of T11+ weapon drops per weapon group, then the Slayer + matching rend rates."""
import re, collections, pymysql
from _paths import load_config, read_source, source_path
WEAPONS = ("ACE.Server", "Factories", "Tables", "Wcids", "Weapons")
enum_src = read_source("ACE.Server", "Factories", "Enum", "WeenieClassName.cs")
enum = {m.group(1): int(m.group(2)) for m in re.finditer(r"^\s*(\w+)\s*=\s*(\d+)", enum_src, flags=re.M)}

s = load_config()["MySql"]["World"]
c = pymysql.connect(host=s["Host"], port=int(s["Port"]), user=s["Username"], password=s["Password"], database=s["Database"]).cursor()
c.execute("select object_Id,value from weenie_properties_int where type=45")
dmg = dict(c.fetchall())

def tables(fn):
    src = source_path(*WEAPONS, fn).read_text(encoding="utf-8")
    out = {}
    for m in re.finditer(r"ChanceTable<WeenieClassName>\s+(\w+)\s*=\s*new ChanceTable<WeenieClassName>[^{]*\{(.*?)\};", src, flags=re.S):
        rows = re.findall(r"WeenieClassName\.(\w+)\s*,\s*([\d.]+)f?", m.group(2))
        out[m.group(1)] = [(a, float(b)) for a, b in rows]
    return out

NAMES = {1: "Slash", 2: "Pierce", 4: "Bludgeon", 8: "Cold", 16: "Fire", 32: "Acid", 64: "Electric", 1024: "Nether", 0: "Undef"}
missing = collections.Counter()
def mix(rows):
    """element shares of one table; Slash|Pierce splits by its rend, chosen at random (GetMatchingRends)"""
    out = collections.Counter()
    total = sum(w for _, w in rows)
    for name, w in rows:
        wcid = enum.get(name)
        t = dmg.get(wcid, 0) if wcid is not None else None
        if t is None:
            missing[name] += 1
            continue
        bits = [b for b in NAMES if b and t & b]
        if not bits:
            out["Undef"] += w / total
        else:
            for b in bits:
                out[NAMES[b]] += w / total / len(bits)
    return out

def avg(mixes):
    out = collections.Counter()
    for m in mixes:
        for k, v in m.items():
            out[k] += v / len(mixes)
    return out

groups = {}
one = []
for fn in ("HeavyWeaponWcids.cs", "LightWeaponWcids.cs", "FinesseWeaponWcids.cs"):
    t = tables(fn)
    print(fn, "tables", len(t))
    one.append(avg([mix(r) for r in t.values() if r]))
groups["1H melee"] = avg(one)
t = tables("TwoHandedWeaponWcids.cs")
print("TwoHanded tables", len(t))
groups["2H melee"] = avg([mix(r) for r in t.values() if r])
bow = avg([mix(tables(f)["T10_Chances"]) for f in ("BowWcids_Aluvian.cs", "BowWcids_Gharundim.cs", "BowWcids_Sho.cs")])
groups["Missile"] = avg([bow, mix(tables("CrossbowWcids.cs")["T10_Chances"]), mix(tables("AtlatlWcids.cs")["T10_Chances"])])
ct = tables("CasterWcids.cs")
key = [k for k in ct if "10" in k][0]
war = [(a, b) for a, b in ct[key] if "nether" not in a.lower()]
groups["War"] = mix(war)
print("missing wcid names:", list(missing)[:8])

EL = ["Slash", "Pierce", "Bludgeon", "Cold", "Fire", "Acid", "Electric", "Nether", "Undef"]
print("\nELEMENT MIX PER GROUP")
print(f"{'group':<10}" + "".join(f"{e[:6]:>8}" for e in EL))
for g, m in groups.items():
    print(f"{g:<10}" + "".join(f"{m[e]*100:>7.1f}%" for e in EL))

# population by weak element, from slayer_math (estimates)
sp = {"Thrungus": (1382, "Bludgeon"), "Ursuin": (1352, "Pierce"), "Shadow": (1348, "Slash"), "Banderling": (1348, "Electric"), "Moarsman": (1204, "Pierce"),
      "Reedshark": (1204, "Cold"), "Sleech": (1204, "Acid"), "Grievver": (1204, "Bludgeon"), "Undead": (640, "Fire"), "Skeleton": (320, "Cold"),
      "Remoran": (9, "Acid")}
el = collections.Counter()
for n, e in list(sp.values()) + [(403, "Electric"), (35, "Slash"), (28, "Slash"), (28, "Fire")]:
    el[e] += n
T = sum(el.values())
print("\nweak-element share of monsters:", {e: round(v / T * 100, 1) for e, v in el.items()})

W, K, GS, SL = 2.17, 300, 0.241, {"1H melee": .2088, "2H melee": .2088, "Missile": .2088, "War": .235}
print("\nMINUTES per Slayer + matching rend weapon, hunting that species: today | at share 0.5 | hours while killing something else at 0.5")
print(f"{'species':<11}{'weak':<9}" + "".join(f"{g:>26}" for g in groups))
for name, (n, e) in sp.items():
    row = f"{name:<11}{e:<9}"
    for g, m in groups.items():
        base = W * SL[g] * GS * K * m[e]
        # the shipped pool ignores the element: any zone species, by its share of the zone
        other = 0.5 * base * n / T
        hunt = 0.5 * base + other
        row += f"{60/base if base else 0:>9.0f}m {60/hunt if hunt else 0:>6.0f}m {1/other if other else 0:>7.1f}h"
    print(row)

print("\nPER GROUP: per hour, all species")
for g, m in groups.items():
    base = W * SL[g] * GS * K
    today = sum(m[e] * el[e] / T for e in el)
    print(f"{g:<10} slayer+rend/hr {base:5.1f}  matching today(zone mix) {base*today:5.2f}  at any share {base*today:5.2f} (the pool moves species, not this total)")

print("\nRARE matches its Slayer's species today (zone mix) per group, vs 1/7 = 14.3%:")
for g, m in groups.items():
    print(f"{g:<10} {sum(m[e]*el[e]/T for e in el):.1%}   average damage vs its Slayer target {sum(m[e]*el[e]/T for e in el)*1+(1-sum(m[e]*el[e]/T for e in el))*.25:.1%} of best")
