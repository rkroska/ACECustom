"""Export EVERYTHING the pet breeding / Ruggan's Annex feature needs in ace_world, from this (test) server,
as one prod-ready SQL file.

    python deploy/export_annex_bundle.py        (writes deploy/Annex-Prod-Bundle.sql)

Read-only: it only SELECTs. The file it writes:
  * WEENIES - every weenie in 78780200-78780299 (the kits are 78780258-78780260) and the motel portal 98760388,
    copied row for row from test (all property tables, emotes and their actions, create lists). Nothing is
    cloned from retail templates at import time, so prod ends up identical to test.
  * PLACEMENTS - the annex (landblock 0x0106 variation 2), each with a fresh guid above the highest one
    already used in the landblock on the target, so nothing collides. The portal's placement is left as it
    is on the target.
  * Leaves out test-only NPCs (TEST_ONLY below).
  * Is safe to re-run: every weenie and placement it owns is deleted first, inside one transaction.
"""
import datetime, json, os, re, sys

import pymysql

CONFIG_JS = os.environ.get("ACE_CONFIG_JS", os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Source", "ACE.Server", "bin", "x64", "Release", "net10.0", "Config.js"))


def login():
    """The ace_world login from the server's Config.js (comments stripped; it is JSON with comments)."""
    txt = open(CONFIG_JS, encoding="utf-8-sig").read()
    txt = re.sub(r"/\*.*?\*/", "", txt, flags=re.S)
    txt = "\n".join(re.sub(r"(^|\s)//.*$", "", line) for line in txt.splitlines())
    return json.loads(txt)["MySql"]["World"]


ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "deploy", "Annex-Prod-Bundle.sql")

WEENIE_RANGES = [(78780200, 78780299), (98760388, 98760388)]
# 98760399-98760401 are NOT ours on prod (a generator, a portal and a creature). The pet kits moved to
# 78780258-78780260; never add the old ids back here.
TEST_ONLY = {78780241: "Scene Tester", 78780234: "Baby Candidate B", 78780235: "Baby Candidate C"}
# (label, landblock, variation or None, wcid filter as SQL, delete scope as SQL)
PLACEMENT_GROUPS = [
    ("the annex", 0x0106, 2, "BETWEEN 78780200 AND 78780299"),  # the cast and the exit portal 78780261
]
# The motel portal's PLACEMENT is deliberately not exported: prod already has 98760388 placed beside
# its own Prof. Ruggan (0xDB3B, a different spot than on test). The bundle replaces the portal weenie
# (new destination) and leaves prod's placement where it is.


def lit(v):
    if v is None:
        return "NULL"
    if isinstance(v, (bytes, bytearray)):
        return "True" if any(v) else "False"
    if isinstance(v, bool):
        return "True" if v else "False"
    if isinstance(v, str):
        return "'" + v.replace("\\", "\\\\").replace("'", "''") + "'"
    if isinstance(v, float):
        return repr(v)
    if isinstance(v, datetime.datetime):
        return "'" + v.strftime("%Y-%m-%d %H:%M:%S") + "'"
    return str(v)


def main():
    c = login()
    cn = pymysql.connect(host=c["Host"], port=int(c["Port"]), user=c["Username"], password=c["Password"],
                         database=c["Database"])
    cur = cn.cursor()

    def cols(table, skip=("id",)):
        cur.execute("SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=%s "
                    "ORDER BY ORDINAL_POSITION", (table,))
        return [r[0] for r in cur.fetchall() if r[0] not in skip]

    cond = " OR ".join("class_Id BETWEEN %d AND %d" % r for r in WEENIE_RANGES)
    cur.execute("SELECT class_Id, class_Name, type FROM weenie WHERE (%s) ORDER BY class_Id" % cond)
    weenies = [r for r in cur.fetchall() if r[0] not in TEST_ONLY]
    ids = [w[0] for w in weenies]
    idlist = ",".join(str(i) for i in ids)

    cur.execute("SELECT TABLE_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND COLUMN_NAME='object_Id' "
                "AND TABLE_NAME LIKE 'weenie\\_properties\\_%%' AND TABLE_NAME <> 'weenie_properties_emote' ORDER BY TABLE_NAME")
    prop_tables = [r[0] for r in cur.fetchall()]
    ecols = cols("weenie_properties_emote")
    acols = cols("weenie_properties_emote_action", skip=("id", "emote_Id"))

    L = []
    w = L.append
    w("/* =====================================================================================")
    w("   Pet Breeding / Ruggan's Annex - PROD BUNDLE for ace_world")
    w("   Exported %s from the test server by deploy/export_annex_bundle.py." % datetime.date.today().isoformat())
    w("   Re-run that script on test to refresh this file; never hand-edit it.")
    w("")
    w("   CREATES %d weenies, copied row for row from test:" % len(weenies))
    for a, b in WEENIE_RANGES:
        n = sum(1 for i in ids if a <= i <= b)
        w("     %d-%d  (%d)" % (a, b, n) if a != b else "     %d  (%d)" % (a, n))
    w("   Left out as test-only: " + ", ".join("%s (%d)" % (v, k) for k, v in sorted(TEST_ONLY.items())) + ".")
    w("   PLACES them: see the PLACEMENTS section. Guids are fresh on the target, never copied from test.")
    w("")
    w("   ONE TRANSACTION. Run it so an error stops before COMMIT:")
    w("       mysql --abort-source-on-error ace_world < deploy/Annex-Prod-Bundle.sql")
    w("   In Workbench, select ace_world first and watch the output: Workbench keeps going after errors.")
    w("   Re-runnable: everything it owns is deleted first.")
    w("")
    w("   AFTER IT (server running the new build):")
    w("     @modifylong pet_breeding_allowed_landblock 262      (0x0106)")
    w("     @modifylong pet_breeding_allowed_variant 2")
    w("     @clearcache, then @reload-landblock in the annex (or restart)")
    w("   Then read the V1-V4 results at the end.")
    w("   All text is 7-bit ASCII with LF line endings, per CLAUDE.md.")
    w("   ===================================================================================== */")
    w("")
    w("SET @__old_safe_updates = @@SQL_SAFE_UPDATES;")
    w("SET SQL_SAFE_UPDATES = 0;")
    w("START TRANSACTION;")
    w("")
    w("/* ---- WEENIES ---------------------------------------------------------------------- */")
    w("-- Clean slate: the weenie_properties_* FKs cascade, so this clears every property row too.")
    w("DELETE FROM `weenie` WHERE `class_Id` IN (%s);" % idlist)
    w("")

    for cid, cname, typ in weenies:
        cur.execute("SELECT value FROM weenie_properties_string WHERE object_Id=%s AND type=1", (cid,))
        r = cur.fetchone()
        w("-- %d  %s" % (cid, r[0] if r else cname))
        w("INSERT INTO `weenie` (`class_Id`,`class_Name`,`type`,`last_Modified`) VALUES (%d, %s, %d, NOW());" % (cid, lit(cname), typ))
        for t in prop_tables:
            tc = cols(t)
            order = "`id`" if "id" in cols(t, skip=()) else "1"
            cur.execute("SELECT %s FROM `%s` WHERE object_Id=%%s ORDER BY %s" % (",".join("`%s`" % x for x in tc), t, order), (cid,))
            rows = cur.fetchall()
            if rows:
                w("INSERT INTO `%s` (%s) VALUES" % (t, ",".join("`%s`" % x for x in tc)))
                w(",\n".join("  (" + ", ".join(lit(v) for v in row) + ")" for row in rows) + ";")
        cur.execute("SELECT id, %s FROM weenie_properties_emote WHERE object_Id=%%s ORDER BY id" % ",".join("`%s`" % x for x in ecols), (cid,))
        for erow in cur.fetchall():
            w("INSERT INTO `weenie_properties_emote` (%s) VALUES (%s);" % (",".join("`%s`" % x for x in ecols), ", ".join(lit(v) for v in erow[1:])))
            w("SET @e = LAST_INSERT_ID();")
            cur.execute("SELECT %s FROM weenie_properties_emote_action WHERE emote_Id=%%s ORDER BY `order`" % ",".join("`%s`" % x for x in acols), (erow[0],))
            acts = cur.fetchall()
            if acts:
                w("INSERT INTO `weenie_properties_emote_action` (`emote_Id`,%s) VALUES" % ",".join("`%s`" % x for x in acols))
                w(",\n".join("  (@e, " + ", ".join(lit(v) for v in a) + ")" for a in acts) + ";")
        w("")

    w("/* ---- PLACEMENTS ------------------------------------------------------------------- */")
    total_place = 0
    for label, lb, var, wfilter in PLACEMENT_GROUPS:
        where = "li.weenie_Class_Id %s" % wfilter
        if lb is not None:
            where += " AND li.landblock = %d AND li.variation_Id <=> %s" % (lb, "NULL" if var is None else var)
        cur.execute("""SELECT li.guid, li.weenie_Class_Id, s.value, li.obj_Cell_Id, li.origin_X, li.origin_Y, li.origin_Z,
                              li.angles_W, li.angles_X, li.angles_Y, li.angles_Z, li.variation_Id, li.landblock
                       FROM landblock_instance li
                       LEFT JOIN weenie_properties_string s ON s.object_Id = li.weenie_Class_Id AND s.type = 1
                       WHERE %s ORDER BY li.landblock, li.guid""" % where)
        rows = [r for r in cur.fetchall() if r[1] not in TEST_ONLY]
        cur.execute("SELECT COUNT(*) FROM landblock_instance_link WHERE parent_GUID IN %s OR child_GUID IN %s",
                    ([r[0] for r in rows] or [0], [r[0] for r in rows] or [0]))
        if cur.fetchone()[0]:
            sys.exit("%s has generator links; this exporter does not carry links" % label)
        w("")
        w("-- %s: %d placement(s)." % (label.capitalize(), len(rows)))
        if lb is not None:
            w("DELETE FROM `landblock_instance` WHERE `landblock` = 0x%04X AND `variation_Id` %s AND `weenie_Class_Id` %s;"
              % (lb, "IS NULL" if var is None else "= %d" % var, wfilter))
        else:
            w("DELETE FROM `landblock_instance` WHERE `weenie_Class_Id` %s;" % wfilter)
        for lbk in sorted({r[12] for r in rows}):
            lo = 0x70000000 | (lbk << 12)
            grp = [r for r in rows if r[12] == lbk]
            w("SET @g = (SELECT COALESCE(MAX(`guid`), 0x%08X) FROM `landblock_instance` WHERE `guid` BETWEEN 0x%08X AND 0x%08X);"
              % (lo - 1, lo, lo | 0xFFF))
            w("INSERT INTO `landblock_instance` (`guid`,`weenie_Class_Id`,`obj_Cell_Id`,`origin_X`,`origin_Y`,`origin_Z`,")
            w("  `angles_W`,`angles_X`,`angles_Y`,`angles_Z`,`is_Link_Child`,`last_Modified`,`variation_Id`) VALUES")
            vals = ["  (@g + %d, %d, 0x%08X, %s, %s, %s, %s, %s, %s, %s, False, NOW(), %s)" %
                    (i, r[1], r[3], lit(r[4]), lit(r[5]), lit(r[6]), lit(r[7]), lit(r[8]), lit(r[9]), lit(r[10]), lit(r[11]))
                    for i, r in enumerate(grp, 1)]
            for i, (v, r) in enumerate(zip(vals, grp)):
                w(v + ("," if i < len(vals) - 1 else ";") + " -- " + (r[2] or str(r[1])))
            total_place += len(grp)

    w("")
    w("COMMIT;")
    w("SET SQL_SAFE_UPDATES = @__old_safe_updates;")
    w("")
    w("/* V1. Expect %d. */" % len(weenies))
    w("SELECT COUNT(*) AS bundle_weenies FROM `weenie` WHERE `class_Id` IN (%s);" % idlist)
    w("")
    w("/* V2. Expect %d. */" % total_place)
    w("SELECT COUNT(*) AS bundle_placements FROM `landblock_instance` WHERE `landblock` = 0x0106 AND `variation_Id` = 2")
    w("  AND `weenie_Class_Id` BETWEEN 78780200 AND 78780299;")
    w("")
    w("/* V2b. The motel portal must still be placed (this bundle does not move it). Expect at least one row. */")
    w("SELECT HEX(`guid`) AS guid, HEX(`obj_Cell_Id`) AS cell, `origin_X`, `origin_Y`, `origin_Z` FROM `landblock_instance`")
    w("WHERE `weenie_Class_Id` = 98760388;")
    w("")
    w("/* V3. Placements of a weenie that does not exist. Expect ZERO rows. */")
    w("SELECT li.`weenie_Class_Id`, HEX(li.`landblock`) AS landblock, li.`variation_Id` FROM `landblock_instance` li")
    w("LEFT JOIN `weenie` w ON w.`class_Id` = li.`weenie_Class_Id`")
    w("WHERE w.`class_Id` IS NULL AND (li.`weenie_Class_Id` BETWEEN 78780200 AND 78780299 OR li.`weenie_Class_Id` = 98760388);")
    w("")
    w("/* V4. Annex NPCs placed anywhere OTHER than the annex (old test spots). Expect ZERO rows;")
    w("       remove any it lists with @removeinst <wcid> standing in that landblock. */")
    w("SELECT `weenie_Class_Id`, HEX(`landblock`) AS landblock, `variation_Id` FROM `landblock_instance`")
    w("WHERE `weenie_Class_Id` BETWEEN 78780200 AND 78780249 AND NOT (`landblock` = 0x0106 AND `variation_Id` = 2);")

    sql = "\n".join(L) + "\n"
    sql.encode("ascii")
    with open(OUT, "w", encoding="ascii", newline="\n") as fh:
        fh.write(sql)
    cn.close()
    print("wrote %s: %d weenies, %d placements" % (OUT, len(weenies), total_place))


if __name__ == "__main__":
    main()
