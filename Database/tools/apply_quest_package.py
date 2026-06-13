#!/usr/bin/env python3
"""
Apply a Quest Builder export folder (or .zip) to ace_world using .env.write.

  python apply_quest_package.py "C:\\Users\\...\\kill_turnin_quest_1_extracted"
  python apply_quest_package.py "C:\\Users\\...\\kill_turnin_quest (1).zip" --dry-run

Fails on first SQL error. Runs post-import verification queries.
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
ENV_WRITE = SCRIPT_DIR / ".env.write"
MYSQL_DEFAULT = r"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"

# From kill_turnin_quest package (override via --wcids)
DEFAULT_WCIDS = (78780090, 78780091, 78780094)
DEFAULT_STAMPS = ("kill_turnin_quest_pickup", "custom_quest_complete")
DEFAULT_TEMPLATES = (19849001, 78780023, 300004)


def load_env(path: Path) -> dict[str, str]:
    out: dict[str, str] = {}
    if not path.exists():
        print(f"Missing {path} — copy .env.write.example and set MYSQL_WORLD_PASSWORD.", file=sys.stderr)
        sys.exit(1)
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        if "=" in line:
            k, v = line.split("=", 1)
            out[k.strip()] = v.strip()
    if not out.get("MYSQL_WORLD_PASSWORD"):
        print("MYSQL_WORLD_PASSWORD is empty in .env.write", file=sys.stderr)
        sys.exit(1)
    return out


def mysql_exec_file(
    env: dict[str, str],
    sql_path: Path,
    mysql_exe: str,
    sql_text: str | None = None,
) -> subprocess.CompletedProcess:
    host = env.get("MYSQL_WORLD_HOST", "127.0.0.1")
    port = env.get("MYSQL_WORLD_PORT", "3306")
    user = env.get("MYSQL_WORLD_USER", "")
    password = env.get("MYSQL_WORLD_PASSWORD", "")
    db = env.get("MYSQL_WORLD_DATABASE", "ace_world")
    cmd = [
        mysql_exe,
        f"-h{host}",
        f"-P{port}",
        f"-u{user}",
        f"-p{password}",
        db,
        "--batch",
        "--raw",
    ]
    body = sql_text if sql_text is not None else sql_path.read_text(encoding="utf-8-sig", errors="replace")
    return subprocess.run(cmd, input=body, capture_output=True, text=True, encoding="utf-8", errors="replace")


def mysql_query(env: dict[str, str], sql: str, mysql_exe: str) -> list[list[str]]:
    host = env.get("MYSQL_WORLD_HOST", "127.0.0.1")
    port = env.get("MYSQL_WORLD_PORT", "3306")
    user = env.get("MYSQL_WORLD_USER", "")
    password = env.get("MYSQL_WORLD_PASSWORD", "")
    db = env.get("MYSQL_WORLD_DATABASE", "ace_world")
    cmd = [
        mysql_exe,
        f"-h{host}",
        f"-P{port}",
        f"-u{user}",
        f"-p{password}",
        db,
        "-N",
        "-B",
        "-e",
        sql,
    ]
    proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if proc.returncode != 0:
        err = proc.stderr.strip()
        raise RuntimeError(f"MySQL error:\n{err}\nSQL: {sql[:300]}")
    rows: list[list[str]] = []
    for line in proc.stdout.splitlines():
        if line.strip():
            rows.append(line.split("\t"))
    return rows


def patch_shell_sql(text: str) -> str:
    """Fix legacy shells that DELETE emote_action by object_Id (column does not exist)."""
    legacy = "DELETE FROM `weenie_properties_emote_action` WHERE `object_Id`"
    if legacy not in text:
        return text

    def repl(match: re.Match[str]) -> str:
        wcid = match.group(1)
        return (
            f"DELETE ea FROM `weenie_properties_emote_action` ea\n"
            f"INNER JOIN `weenie_properties_emote` e ON ea.`emote_Id` = e.`id`\n"
            f"WHERE e.`object_Id` = {wcid};"
        )

    text = re.sub(
        r"DELETE FROM `weenie_properties_emote_action` WHERE `object_Id` = (\d+);",
        repl,
        text,
    )
    text = text.replace(
        "INSERT INTO `weenie_properties_anim_part` (`object_Id`, `index`, `value`)",
        "INSERT INTO `weenie_properties_anim_part` (`object_Id`, `index`, `animation_Id`)",
    )
    text = text.replace(
        "`object_Id`, `sub_Palette_Id`, `offset`, `length`, `order`",
        "`object_Id`, `sub_Palette_Id`, `offset`, `length`",
    )
    text = re.sub(
        r"(SELECT \d+, `index`, )`value`( FROM `weenie_properties_anim_part`)",
        r"\1`animation_Id`\2",
        text,
    )
    text = re.sub(
        r"(SELECT \d+, `sub_Palette_Id`, `offset`, `length`), `order`( FROM `weenie_properties_palette`)",
        r"\1 FROM `weenie_properties_palette`",
        text,
    )
    return text


def ordered_sql_files(folder: Path) -> list[Path]:
    files = sorted(folder.glob("*.sql"))
    numbered = []
    unnumbered = []
    for f in files:
        m = re.match(r"^(\d+)_", f.name)
        if m:
            numbered.append((int(m.group(1)), f))
        else:
            unnumbered.append(f)
    if numbered:
        return [f for _, f in sorted(numbered)] + sorted(unnumbered)
    return sorted(files)


def safe_extract_zip(zf: zipfile.ZipFile, dest_dir: Path) -> None:
    root = dest_dir.resolve()
    for member in zf.infolist():
        member_path = Path(member.filename)
        if member_path.is_absolute() or member_path.drive:
            raise SystemExit(f"ZIP contains absolute path: {member.filename}")
        target = (root / member_path).resolve()
        if root not in target.parents and target != root:
            raise SystemExit(f"ZIP contains unsafe path: {member.filename}")
    zf.extractall(root)


def resolve_package(path: Path) -> tuple[Path, tempfile.TemporaryDirectory | None]:
    if path.is_dir():
        return path, None
    if path.suffix.lower() == ".zip":
        tmp = tempfile.TemporaryDirectory(prefix="quest_pkg_")
        try:
            with zipfile.ZipFile(path) as zf:
                safe_extract_zip(zf, Path(tmp.name))
        except BaseException:
            tmp.cleanup()
            raise
        return Path(tmp.name), tmp
    raise SystemExit(f"Not a directory or .zip: {path}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Apply quest package SQL to ace_world")
    parser.add_argument("package", type=Path, help="Folder or .zip from Quest Builder export")
    parser.add_argument("--dry-run", action="store_true", help="List files only, do not execute")
    parser.add_argument("--env", type=Path, default=ENV_WRITE, help="Path to .env.write")
    parser.add_argument("--mysql", default=MYSQL_DEFAULT, help="mysql.exe path")
    args = parser.parse_args()

    env = load_env(args.env)
    folder, tmp = resolve_package(args.package.resolve())
    try:
        sql_files = ordered_sql_files(folder)
        if not sql_files:
            print(f"No .sql files in {folder}", file=sys.stderr)
            return 1

        print(f"Package: {folder}")
        print(f"Database: {env.get('MYSQL_WORLD_USER')}@{env.get('MYSQL_WORLD_HOST')}/{env.get('MYSQL_WORLD_DATABASE')}")
        print(f"Files ({len(sql_files)}):")
        for f in sql_files:
            print(f"  - {f.name}")

        if args.dry_run:
            return 0

        print("\n--- Pre-flight (templates) ---")
        ids = ",".join(str(x) for x in DEFAULT_TEMPLATES)
        for row in mysql_query(env, f"SELECT class_Id, class_Name FROM weenie WHERE class_Id IN ({ids})", args.mysql):
            print(f"  OK template {row[0]} ({row[1]})")
        missing = set(DEFAULT_TEMPLATES) - {int(r[0]) for r in mysql_query(env, f"SELECT class_Id FROM weenie WHERE class_Id IN ({ids})", args.mysql)}
        if missing:
            print(f"  WARN missing templates: {sorted(missing)}")

        print("\n--- Applying SQL ---")
        for sql_path in sql_files:
            print(f"  >> {sql_path.name} ...", end=" ", flush=True)
            sql_text = sql_path.read_text(encoding="utf-8-sig", errors="replace")
            if "_shell_" in sql_path.name:
                sql_text = patch_shell_sql(sql_text)
            proc = mysql_exec_file(env, sql_path, args.mysql, sql_text=sql_text)
            if proc.returncode != 0:
                print("FAILED")
                print(proc.stderr or proc.stdout, file=sys.stderr)
                return 1
            print("OK")

        print("\n--- Post-checks ---")
        wcid_list = ",".join(str(w) for w in DEFAULT_WCIDS)
        for row in mysql_query(
            env,
            f"SELECT class_Id, class_Name, type FROM weenie WHERE class_Id IN ({wcid_list})",
            args.mysql,
        ):
            print(f"  weenie {row[0]}: {row[1]} (type {row[2]})")

        for wcid in DEFAULT_WCIDS:
            em = mysql_query(
                env,
                f"SELECT COUNT(*) FROM weenie_properties_emote WHERE object_Id = {wcid}",
                args.mysql,
            )[0][0]
            print(f"  emotes on {wcid}: {em}")

        for stamp in DEFAULT_STAMPS:
            q = mysql_query(
                env,
                f"SELECT name, min_Delta, max_Solves FROM quest WHERE name = '{stamp.replace(chr(39), chr(39)+chr(39))}'",
                args.mysql,
            )
            if q:
                print(f"  quest {stamp}: min_Delta={q[0][1]} max_Solves={q[0][2]}")
            else:
                print(f"  quest {stamp}: MISSING")

        print("\n--- In-game (manual) ---")
        print("  Restart ACE.Server so quest cache reloads.")
        print("  @createinst 78780090   # Quest Giver NPC")
        print("  @createinst 78780094   # Landscape pickup object")
        print("  Flow: Use 78780094 -> get 78780091 -> Give to 78780090")

        print("\nAll SQL applied successfully.")
        return 0
    finally:
        if tmp:
            tmp.cleanup()


if __name__ == "__main__":
    sys.exit(main())
