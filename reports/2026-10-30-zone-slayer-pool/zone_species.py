"""Which creature species spawn in a Zone Control zone, from the world DB (read-only).

Sources: landblock_instance rows at the zone's variation (direct creatures + generators, walked
recursively through weenie_properties_generator), and outdoor encounters on the zone's landblocks.
"""
import json, re, sys, collections, pymysql

ZONE = sys.argv[1] if len(sys.argv) > 1 else "Tou Tou"
VAR = int(sys.argv[2]) if len(sys.argv) > 2 else 11

cfg = open(r"C:\Scripting\ACECustom\Source\ACE.Server\bin\x64\Release\net10.0\Config.js", encoding="utf-8").read()
cfg = re.sub(r"/\*.*?\*/", "", cfg, flags=re.S)
cfg = re.sub(r"(?m)^\s*//.*$", "", cfg)
cfg = json.loads(cfg)

def connect(k):
    s = cfg["MySql"][k]
    return pymysql.connect(host=s["Host"], port=int(s["Port"]), user=s["Username"], password=s["Password"], database=s["Database"])

shard = connect("Shard").cursor()
shard.execute("select value from config_properties_string where `key`='zonecontrol_data'")
data = json.loads(shard.fetchone()[0])
area = next(a for a in data["areas"] if a["variation"] == VAR and a["name"].startswith(ZONE))
lbs = sorted(int(x) for x in area["landblocks"])
print(f"zone {area['name']!r} v{VAR}: {len(lbs)} landblocks, exempt wcids {len(area.get('exemptWcids') or [])}")

w = connect("World").cursor()
ph = ",".join(["%s"] * len(lbs))

# CreatureType enum names from the source
ct_src = open(r"C:\Scripting\ACECustom\Source\ACE.Entity\Enum\CreatureType.cs", encoding="utf-8").read()
body = ct_src[ct_src.index("{", ct_src.index("enum CreatureType")) + 1:]
body = body[:body.index("}")]
ct_names, nextv = {}, 0
for line in body.splitlines():
    line = line.split("//")[0].strip().rstrip(",")
    if not line: continue
    m = re.match(r"(\w+)\s*(?:=\s*(\w+))?", line)
    if m.group(2): nextv = int(m.group(2), 0)
    ct_names[nextv] = m.group(1); nextv += 1

def weenie_info(wcids):
    if not wcids:
        return {}
    p = ",".join(["%s"] * len(wcids))
    w.execute(f"select w.class_Id, w.type, s.value from weenie w left join weenie_properties_string s on s.object_Id=w.class_Id and s.type=1 where w.class_Id in ({p})", list(wcids))
    info = {r[0]: {"wtype": r[1], "name": r[2], "ct": None} for r in w.fetchall()}
    w.execute(f"select object_Id, value from weenie_properties_int where type=2 and object_Id in ({p})", list(wcids))
    for oid, v in w.fetchall():
        info[oid]["ct"] = v
    return info

def gen_children(wcids):
    p = ",".join(["%s"] * len(wcids))
    w.execute(f"select object_Id, weenie_Class_Id from weenie_properties_generator where object_Id in ({p})", list(wcids))
    return w.fetchall()

w.execute(f"select weenie_Class_Id from landblock_instance where (landblock in ({ph})) and variation_Id=%s", lbs + [VAR])
roots = [r[0] for r in w.fetchall()]
print(f"landblock_instance rows at v{VAR}: {len(roots)}")

# outdoor encounters (no variation column)
w.execute(f"select weenie_Class_Id from encounter where landblock in ({ph})", lbs)
enc = [r[0] for r in w.fetchall()]
print(f"encounter rows: {len(enc)}")

if "--with-encounters" not in sys.argv: enc = []
seen, frontier, source = set(), set(roots) | set(enc), {}
for x in roots: source.setdefault(x, "instance")
for x in enc: source.setdefault(x, "encounter")
creatures = collections.Counter()
depth = 0
while frontier and depth < 6:
    info = weenie_info(frontier)
    seen |= frontier
    nxt = set()
    for wcid in frontier:
        i = info.get(wcid)
        if i and i["ct"]:
            creatures[wcid] += 1
    for parent, child in gen_children(frontier):
        if child not in seen:
            nxt.add(child)
            source.setdefault(child, source.get(parent, "?"))
    frontier = nxt
    depth += 1

info = weenie_info(set(creatures))
exempt = set(area.get("exemptWcids") or [])
by_ct = collections.defaultdict(list)
for wcid in creatures:
    i = info[wcid]
    by_ct[i["ct"]].append((wcid, i["name"], source.get(wcid), wcid in exempt))

print(f"\n{len(by_ct)} species across {len(creatures)} creature weenies:")
for ct, rows in sorted(by_ct.items(), key=lambda kv: -len(kv[1])):
    srcs = collections.Counter(r[2] for r in rows)
    ex = sum(r[3] for r in rows)
    sample = ", ".join(sorted({r[1] or str(r[0]) for r in rows})[:4])
    print(f"  {ct_names.get(ct, ct):<20} {len(rows):>3} weenies  {dict(srcs)}{'  exempt '+str(ex) if ex else ''}  e.g. {sample}")
