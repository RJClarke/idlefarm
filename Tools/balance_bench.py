"""
Balance bench: max out a COPY of the save, run every crop in all four fields, and report how
long each run lasts and what it earns. Repeat it after any balance change to compare.

    python tools/balance_bench.py                      # all crops, defaults
    python tools/balance_bench.py --crops Radish,Corn --speed 10 --cap-minutes 120

Needs: the Unity editor open on this project, NOT in play mode (the script drives play mode
through the Temp/*.request bridges: PlayModeBridge + UIDriveBridge). Your real save and field
picks are backed up first and restored at the end, even if the bench fails part-way.

Writes docs/balance/bench-<date>.md (a table) and .csv (raw numbers).
"""
import argparse, csv, datetime, hashlib, json, os, re, shutil, sys, time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEMP = os.path.join(ROOT, "Temp")
SAVE_DIR = os.path.join(os.environ["USERPROFILE"], "AppData", "LocalLow", "DefaultCompany", "IdleFarm - Silo")
SAVE = os.path.join(SAVE_DIR, "gamedata.json")
CROPS = ["Radish", "Carrot", "Green Beans", "Tomato", "Corn", "Green Pepper", "Red Pepper", "Strawberry", "Blueberry"]
BASE_GAME_SPEED = 1.75  # GameConstants.baseGameSpeed: farm seconds per real second at 1x


def log(msg):
    print(time.strftime("%H:%M:%S"), msg, flush=True)


def md5(path):
    with open(path, "rb") as f:
        return hashlib.md5(f.read()).hexdigest()


def request(name, body="", wait=1.0):
    with open(os.path.join(TEMP, name), "w", encoding="utf-8") as f:
        f.write(body)
    time.sleep(wait)


def call(expr, timeout=15):
    """PlayModeBridge method caller: 'Type.Method|arg|arg' -> the result line."""
    result = os.path.join(TEMP, "open_popup_result.txt")
    if os.path.exists(result):
        os.remove(result)
    request("open_popup.request", expr, wait=0.3)
    end = time.time() + timeout
    while time.time() < end:
        if os.path.exists(result):
            time.sleep(0.2)
            with open(result, encoding="utf-8") as f:
                text = f.read().strip()
            return text.split("->", 1)[1].strip() if "->" in text else text
        time.sleep(0.3)
    raise RuntimeError(f"no answer from the editor for {expr!r} (is Unity open on this project?)")


def enter_play(settle=10, ready_timeout=120):
    """Enter play mode and wait until the game answers (a slow or idle editor can take a while)."""
    request("enter_play_mode.request", wait=settle)
    end = time.time() + ready_timeout
    while True:
        try:
            if "active=" in call("DevBench.Status", timeout=10):
                time.sleep(3)  # let the scene finish starting up
                return
        except RuntimeError:
            pass
        if time.time() > end:
            raise RuntimeError("the editor never entered play mode (is a popup or compile blocking it?)")


def exit_play(settle=8):
    request("exit_play_mode.request", wait=settle)


def dotnet_ticks_now():
    return int((time.time() + 62135596800) * 10**7)


def prepare_bench_save(src, animal):
    """A copy of the real save with no run in progress and no away-time (so no welcome-back)."""
    with open(src, encoding="utf-8") as f:
        d = json.load(f)
    d.update(runActive=False, money=0, lastSeenUtcTicks=dotnet_ticks_now(), collectModeOn=False)
    d["equippedAnimalID"] = animal
    with open(SAVE, "w", encoding="utf-8") as f:
        json.dump(d, f)


def parse_report(line):
    return {k: v for k, v in re.findall(r"(\w+)=(\S+)", line)}


def bench_crop(crop, speed, cap_seconds, poll):
    log(f"{crop}: {call(f'DevBench.Start|{crop}|{speed}')}")
    started = time.time()
    while True:
        time.sleep(poll)
        status = parse_report(call("DevBench.Status"))
        farm = float(status.get("farm", 0))
        if status.get("active") != "True":
            break
        if farm >= cap_seconds:
            call("DevBench.Stop")
            break
        log(f"  {crop}: farm {farm/60:.0f}m")
    report = parse_report(call("DevBench.Report"))
    report["crop"] = crop
    report["capped"] = str(float(report.get("farm", 0)) >= cap_seconds - 1)
    report["real_seconds"] = f"{time.time() - started:.0f}"
    log(f"  {crop}: {report}")
    return report


def fmt_time(seconds):
    s = int(float(seconds))
    return f"{s // 3600}h {s % 3600 // 60:02d}m" if s >= 3600 else f"{s // 60}m {s % 60:02d}s"


def write_reports(rows, args, started):
    out_dir = os.path.join(ROOT, "docs", "balance")
    os.makedirs(out_dir, exist_ok=True)
    stamp = started.strftime("%Y-%m-%d-%H%M")
    keys = ["crop", "farm", "capped", "bankrupt", "harvested", "planted", "money", "coins",
            "deer", "crows", "lightning", "dried", "rotted", "real_seconds"]
    with open(os.path.join(out_dir, f"bench-{stamp}-L{args.level_percent:g}.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)

    lines = [
        f"# Balance bench - {started:%Y-%m-%d %H:%M}", "",
        f"Everything at {args.level_percent:g}% of max (Research, permanent and farm upgrades, Barn skills;",
        f"levels rounded up, so all 4 fields are owned). One crop in every field, no equipment, animal: "
        f"{args.animal or 'none'}. Speed {args.speed}x; runs stop at {args.cap_minutes} farm-minutes.",
        "Per-run (Money-bought) upgrades are not bought. Farm time is in-game time.", "",
        "| Crop | Lasted | Ended | Harvested | Money/min | Coins | Coins/hr | Lost to deer / crows / dry / rot / lightning |",
        "|---|---|---|---|---|---|---|---|",
    ]
    for r in rows:
        farm = max(1.0, float(r.get("farm", 0)))
        ended = "still going (cap)" if r.get("capped") == "True" else ("bankrupt" if r.get("bankrupt") == "True" else "ended")
        lines.append(
            f"| {r['crop']} | {fmt_time(farm)} | {ended} | {int(r.get('harvested', 0)):,} | "
            f"{float(r.get('money', 0)) / farm * 60:,.0f} | {int(r.get('coins', 0)):,} | "
            f"{float(r.get('coins', 0)) / farm * 3600:,.0f} | "
            f"{r.get('deer')} / {r.get('crows')} / {r.get('dried')} / {r.get('rotted')} / {r.get('lightning')} |")
    path = os.path.join(out_dir, f"bench-{stamp}-L{args.level_percent:g}.md")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    return path


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--crops", default=",".join(CROPS), help="comma-separated crop names")
    ap.add_argument("--speed", type=float, default=20, help="game speed multiplier for the bench (dev speeds allowed)")
    ap.add_argument("--cap-minutes", type=float, default=60, help="stop a run after this many farm-minutes")
    ap.add_argument("--level-percent", type=float, default=100,
                    help="every research, upgrade and skill at this %% of its max (rounded up)")
    ap.add_argument("--animal", default="", help="animalID to equip (e.g. farm_dog, goose); empty = none")
    ap.add_argument("--poll", type=float, default=15, help="seconds between status checks")
    args = ap.parse_args()

    if not os.path.exists(SAVE):
        sys.exit(f"no save at {SAVE}")
    started = datetime.datetime.now()
    backup = os.path.join(SAVE_DIR, f"bench-backup-{started:%Y%m%d-%H%M%S}")
    os.makedirs(backup)
    for name in ("gamedata.json", "gamedata.json.bak"):
        if os.path.exists(os.path.join(SAVE_DIR, name)):
            shutil.copy2(os.path.join(SAVE_DIR, name), backup)
    real_md5 = md5(os.path.join(backup, "gamedata.json"))
    log(f"real save backed up to {backup}")

    rows = []
    try:
        prepare_bench_save(os.path.join(backup, "gamedata.json"), args.animal)
        enter_play()
        log(call(f"DevBench.MaxEverything|{args.level_percent}"))
        exit_play()
        enter_play()  # grid size and helper count are built at load
        for crop in [c.strip() for c in args.crops.split(",") if c.strip()]:
            rows.append(bench_crop(crop, args.speed, args.cap_minutes * 60, args.poll))
    finally:
        try:
            log(f"field picks: {call('DevBench.RestoreSelection')}")
        except Exception as e:  # editor may not be in play mode if it failed early
            log(f"could not restore field picks via the editor: {e}")
        exit_play()
        for name in ("gamedata.json", "gamedata.json.bak"):
            src = os.path.join(backup, name)
            if os.path.exists(src):
                shutil.copy2(src, os.path.join(SAVE_DIR, name))
        ok = md5(SAVE) == real_md5
        log(f"real save restored: {'OK' if ok else 'MISMATCH - check ' + backup}")

    if rows:
        log(f"report: {write_reports(rows, args, started)}")


if __name__ == "__main__":
    main()
