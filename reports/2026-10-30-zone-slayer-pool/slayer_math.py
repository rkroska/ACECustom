"""Read-only: real element resists, spawn weights and Slayer odds for a zone, from the local DBs."""
import json, re, sys, collections, pymysql

ZONE, VAR = sys.argv[1], int(sys.argv[2])
cfg = open(r"C:\Scripting\ACECustom\Source\ACE.Server\bin\x64\Release\net10.0\Config.js", encoding="utf-8").read()
cfg = re.sub(r"/\*.*?\*/", "", cfg, flags=re.S)
cfg = re.sub(r"(?m)^\s*//.*$", "", cfg)
cfg = json.loads(cfg)
def connect(k):
    s = cfg["MySql"][k]
    return pymysql.connect(host=s["Host"], port=int(s["Port"]), user=s["Username"], password=s["Password"], database=s["Database"]).cursor()

shard, w = connect("Shard"), connect("World")
shard.execute("select value from config_properties_string where `key`='zonecontrol_data'")
store = json.loads(shard.fetchone()[0])
area = next(a for a in store["areas"] if a["variation"] == VAR and a["name"].startswith(ZONE))
default = store["variationDefaults"][str(VAR)]["profile"]
prof = area["profile"]
over = prof.get("wcidOverrides") or {}
exempt = {int(x) for x in (prof.get("exemptWcids") or [])}
lbs = sorted(int(x) for x in area["landblocks"])

src = open(r"C:\Scripting\ACECustom\Source\ACE.Entity\Enum\Properties\PropertyInt.cs", encoding="utf-8").read()
MASK_ID = int(re.search(r"ElementalWeaknessMask\s*=\s*(\d+)", src).group(1))
src = open(r"C:\Scripting\ACECustom\Source\ACE.Entity\Enum\Properties\PropertyFloat.cs", encoding="utf-8").read()
FACTOR_ID = int(re.search(r"ElementalWeaknessFactor\s*=\s*(\d+)", src).group(1))
RESIST_IDS = {n: int(re.search(r"\bResist%s\s*=\s*(\d+)" % n, src).group(1)) for n in ["Slash", "Pierce", "Bludgeon", "Fire", "Cold", "Acid", "Electric", "Nether"]}
ct_src = open(r"C:\Scripting\ACECustom\Source\ACE.Entity\Enum\CreatureType.cs", encoding="utf-8").read()
body = ct_src[ct_src.index("{", ct_src.index("enum CreatureType")) + 1:]
body = body[:body.index("}")]
ct, nv = {}, 0
for line in body.splitlines():
    line = line.split("//")[0].strip().rstrip(",")
    if not line:
        continue
    m = re.match(r"(\w+)\s*(?:=\s*(\w+))?", line)
    if m.group(2):
        nv = int(m.group(2), 0)
    ct[nv] = m.group(1)
    nv += 1

# DamageType flags: Slash 1, Pierce 2, Bludgeon 4, Cold 8, Fire 16, Acid 32, Electric 64, Nether 1024
FLAGS = {"Slash": 1, "Pierce": 2, "Bludgeon": 4, "Cold": 8, "Fire": 16, "Acid": 32, "Electric": 64, "Nether": 1024}
EL7 = ["Slash", "Pierce", "Bludgeon", "Fire", "Cold", "Acid", "Electric"]

# population estimate: walk generators from the placed instances
w.execute("select weenie_Class_Id,count(*) from landblock_instance where landblock in (%s) and variation_Id=%%s group by 1" % ",".join(["%s"] * len(lbs)), lbs + [VAR])
pop = collections.Counter()
frontier = {r[0]: float(r[1]) for r in w.fetchall()}
for depth in range(6):
    if not frontier:
        break
    ids = list(frontier)
    ph = ",".join(["%s"] * len(ids))
    w.execute(f"select object_Id,probability,weenie_Class_Id,init_Create,max_Create from weenie_properties_generator where object_Id in ({ph})", ids)
    rows = collections.defaultdict(list)
    for o, pr, c, i, mx in w.fetchall():
        rows[o].append((pr, c, i, mx))
    w.execute(f"select object_Id,value from weenie_properties_int where type=82 and object_Id in ({ph})", ids)
    init = dict(w.fetchall())
    nxt = collections.Counter()
    for g, n in frontier.items():
        if g not in rows:
            pop[g] += n
            continue
        for pr, c, i, mx in rows[g]:
            nxt[c] += n * max(i or 0, 1 if (mx or 0) else 0, 1 if init.get(g) else 0)
    frontier = dict(nxt)

ids = list(pop)
ph = ",".join(["%s"] * len(ids))
w.execute(f"select w.class_Id,w.type,s.value from weenie w left join weenie_properties_string s on s.object_Id=w.class_Id and s.type=1 where w.class_Id in ({ph})", ids)
info = {r[0]: {"wtype": r[1], "name": r[2], "ct": None, "attackable": True, "resist": {}, "mask": None, "factor": None} for r in w.fetchall()}
w.execute(f"select object_Id,type,value from weenie_properties_int where type in (2,{MASK_ID}) and object_Id in ({ph})", ids)
for o, t, v in w.fetchall():
    if o in info:
        info[o]["ct" if t == 2 else "mask"] = v
w.execute(f"select object_Id,value from weenie_properties_bool where type=19 and object_Id in ({ph})", ids)
for o, v in w.fetchall():
    if o in info:
        info[o]["attackable"] = bool(v)
w.execute(f"select object_Id,type,value from weenie_properties_float where type in ({','.join(str(x) for x in list(RESIST_IDS.values())+[FACTOR_ID])}) and object_Id in ({ph})", ids)
rev = {v: k for k, v in RESIST_IDS.items()}
for o, t, v in w.fetchall():
    if o in info:
        if t == FACTOR_ID:
            info[o]["factor"] = v
        else:
            info[o]["resist"][rev[t]] = v

def stat(wcid, key):
    wo = over.get(str(wcid)) or {}
    for scope in (wo, prof.get("minion") or {}, prof, default.get("minion") or {}, default):
        s = (scope.get("stats") or {}).get(key)
        if s is not None:
            return s["base"]
    return None

def dstat(key, fb=None):
    s = (default.get("stats") or {}).get(key)
    return s["base"] if s else fb

mons = {}
for wcid, n in pop.items():
    i = info.get(wcid)
    if not i or not i["ct"] or not i["attackable"] or wcid in exempt or wcid not in over and i["ct"] == 35:
        continue
    res, arm = {}, {}
    for el in FLAGS:
        z = stat(wcid, "resist_" + el.lower())
        res[el] = z if z is not None else i["resist"].get(el, 1.0)
        a = stat(wcid, "armor_vs_" + el.lower())
        arm[el] = a if a is not None else 1.0
    wo = over.get(str(wcid)) or {}
    mask = (wo.get("propInts") or {}).get(str(MASK_ID), i["mask"])
    factor = (wo.get("propFloats") or {}).get(str(FACTOR_ID), i["factor"]) or 2.0
    if mask:
        for el, fl in FLAGS.items():
            if mask & fl:
                res[el] *= factor
    mons[wcid] = {"name": i["name"], "ct": ct.get(i["ct"], i["ct"]), "n": n, "res": res, "arm": arm, "mask": mask, "over": str(wcid) in over}

totn = sum(m["n"] for m in mons.values())
print(f"ZONE {area['name']} v{VAR}: {len(mons)} attackable monster weenies, est. population {totn:.0f}; {len(over)} wcid overrides")
print("armor_vs differs on:", sum(1 for m in mons.values() if len(set(m['arm'].values())) > 1))
print("slayer chance", dstat("weapon_slayer_chance"), "band", dstat("weapon_slayer_min"), dstat("weapon_slayer_max"), "| bag_odds", dstat("bag_odds"), "vengeance", dstat("bag_weight_vengeance"))
print("rend band", {k: dstat(k) for k in (default.get("stats") or {}) if "rend" in k and "armor" not in k and "spell" not in k})
print()
print(f"{'wcid':>10} {'monster':<28} {'species':<11} {'pop':>5} " + " ".join(f"{e[:4]:>5}" for e in FLAGS))
for wcid, m in sorted(mons.items(), key=lambda kv: (kv[1]["ct"] if isinstance(kv[1]["ct"], str) else "", -kv[1]["n"])):
    print(f"{wcid:>10} {(m['name'] or '')[:28]:<28} {str(m['ct'])[:11]:<11} {m['n']:>5.0f} " + " ".join(f"{m['res'][e]:>5.2f}" for e in FLAGS))

# species roll-up (population-weighted resist per element)
sp = collections.defaultdict(lambda: {"n": 0.0, "r": collections.Counter()})
for m in mons.values():
    s = sp[m["ct"]]
    s["n"] += m["n"]
    for e in EL7:
        s["r"][e] += m["n"] * m["res"][e]
for s in sp.values():
    s["r"] = {e: s["r"][e] / s["n"] for e in EL7}

print("\nSPECIES (pop share, resist by element, pop-weighted):")
print(f"{'species':<14} {'share':>6} {'best':>6} {'#>=best':>7} " + " ".join(f"{e[:4]:>5}" for e in EL7))
for name, s in sorted(sp.items(), key=lambda kv: -kv[1]["n"]):
    best = max(s["r"].values())
    print(f"{str(name)[:14]:<14} {s['n']/totn:>6.1%} {best:>6.2f} {sum(1 for v in s['r'].values() if abs(v-best)<1e-6):>7} " + " ".join(f"{s['r'][e]:>5.2f}" for e in EL7))

# rare element options, per monster weenie, population-weighted over the kill that dropped it
def rare_table(S, title):
    rows = collections.Counter()
    tot = 0.0
    bad = 0.0
    for m in mons.values():
        r = sorted((m["res"][e] for e in EL7), reverse=True)
        n = m["n"]
        tot += n
        best = r[0]
        rows["random"] += n * sum(r) / 7 / best
        rows["top3"] += n * sum(r[:3]) / 3 / best
        rows["weakest"] += n
        bad += n * sum(1 for v in r if v < best - 1e-6) / 7
    print(f"\n{title}: damage on the species it slays, relative to its best element (no Slayer difference: Slayer {S} multiplies all equally)")
    for k in ("random", "top3", "weakest"):
        print(f"  {k:<8} {rows[k]/tot:>6.1%}   with Slayer vs best-element no-Slayer: x{rows[k]/tot*S:.2f}")
    print(f"  random rare lands on a non-best element: {bad/tot:.1%}")
S = dstat("weapon_slayer_max", 2.0)
rare_table(S, "RARE ELEMENT")

# slayer drop economics. Weapons per corpse = 2.17 (regular), 300 kills/hr, 24.1% = one weapon group
W, KPH, GROUP = 2.17, 300, 0.241
ch = dstat("weapon_slayer_chance", 0.18)
shares = {k: s["n"] / totn for k, s in sp.items()}
weak = {k: {e for e, v in s["r"].items() if abs(v - max(s["r"].values())) < 1e-6} for k, s in sp.items()}
print(f"\nSLAYER PER HOUR, one weapon group, {KPH} kills/hr, {W} weapons/corpse, chance {ch} (before the modifier floor/cap):")
base = W * ch * GROUP * KPH
print(f"  group Slayer weapons per hour = {base:.1f}")
print(f"{'species':<14} {'share':>6} {'hunting it: today':>18} {'50/50':>7} {'pool':>7} | {'aligned today':>13} {'50/50':>7} {'pool':>7} {'pool+element':>13}")
for k, s in sorted(sp.items(), key=lambda kv: -kv[1]["n"]):
    p = shares[k]
    a = len(weak[k]) / 7
    # hunting species k only; pool draws by zone share
    today, blend, pool = base, base * (0.5 + 0.5 * p), base * p
    # element-matched pool: for each random element, choose among species weak to it, share-weighted
    filt = 0.0
    for e in EL7:
        cands = [x for x in sp if e in weak[x]]
        if k in cands:
            filt += (1 / 7) * shares[k] / sum(shares[x] for x in cands)
    print(f"{str(k)[:14]:<14} {p:>6.1%} {today:>18.1f} {blend:>7.1f} {pool:>7.1f} | {today*a:>13.2f} {blend*a:>7.2f} {pool*a:>7.2f} {base*filt:>13.2f}")
