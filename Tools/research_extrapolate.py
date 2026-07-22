#!/usr/bin/env python3
"""
Research / economy extrapolation tool for IdleFarm - Silo.

Reads the LIVE ScriptableObject data (ResearchTuning + every Research_*.asset +
the animal assets) and applies the exact in-game formulas so the numbers always
match the build:

    cost(L)     = ceil(baseCostCoins * costDifficulty * costMultiplier * L^pCost)
    duration(L) = baseDurationSecs * timeDifficulty * timeMultiplier * L^pTime  / (1 + researchSpeedBonus)
    bonus(stat) = sum over researches( currentLevel * bonusPerLevel )
    egg cooldown = baseCooldownMin / (1 + chicken_cooldown bonus)
    egg reward   = round(baseReward * (1 + chicken_efficiency bonus))
    eggs/day     = 1440 / effectiveCooldownMin      (perfect collection)

Usage:
    python Tools/research_extrapolate.py                     # live values: chicken + egg + whole-tree
    python Tools/research_extrapolate.py --cost-mult 0.1     # sweep: pretend cost x0.1
    python Tools/research_extrapolate.py --time-mult 0.3
    python Tools/research_extrapolate.py --csv out.csv       # dump per-track table for graphing

Overrides only change THIS run's math; they do not write to the assets.
"""
import argparse, glob, math, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RESEARCH_DIR = os.path.join(ROOT, "Assets", "Resources", "Research")
TUNING_ASSET = os.path.join(ROOT, "Assets", "Data", "Research", "ResearchTuning.asset")
ANIMAL_DIR = os.path.join(ROOT, "Assets", "Data", "Animals")

TIER_MAX = {0: 1, 1: 10, 2: 25, 3: 100, 4: 100}  # Binary, Tier10, Tier25, Tier100Standard, Tier100Absurd


def _num(text, key, default=0.0):
    m = re.search(rf'^\s*{re.escape(key)}:\s*([-\d.eE]+)\s*$', text, re.M)
    return float(m.group(1)) if m else default


def _str(text, key, default=""):
    m = re.search(rf'^\s*{re.escape(key)}:\s*(.+?)\s*$', text, re.M)
    return m.group(1).strip().strip("'\"") if m else default


def load_tuning():
    t = open(TUNING_ASSET, encoding="utf-8").read()
    return {
        "pCost": _num(t, "pCost", 2.0),
        "pTime": _num(t, "pTime", 2.16),
        "costMultiplier": _num(t, "costMultiplier", 1.0),
        "timeMultiplier": _num(t, "timeMultiplier", 1.0),
    }


def load_research():
    out = []
    for fp in glob.glob(os.path.join(RESEARCH_DIR, "Research_*.asset")):
        s = open(fp, encoding="utf-8").read()
        out.append({
            "id": _str(s, "researchID"),
            "name": _str(s, "displayName"),
            "branch": _str(s, "branchID"),
            "stat": _str(s, "targetStatKey"),
            "tier": int(_num(s, "tier", 3)),
            "baseCost": _num(s, "baseCostCoins", 50),
            "baseDur": _num(s, "baseDurationSecs", 120),
            "costDiff": _num(s, "costDifficulty", 1),
            "timeDiff": _num(s, "timeDifficulty", 1),
            "binCost": _num(s, "binaryFixedCost", 0),
            "binDur": _num(s, "binaryFixedDurationSecs", 0),
            "bonusPerLevel": _num(s, "bonusPerLevel", 0),
        })
    return out


def load_animal(animal_id):
    for fp in glob.glob(os.path.join(ANIMAL_DIR, "Animal_*.asset")):
        s = open(fp, encoding="utf-8").read()
        if _str(s, "animalID") == animal_id:
            return {"cooldownMin": _num(s, "cooldownMinutes", 20), "reward": _num(s, "rewardCoins", 30)}
    return None


def cost_at(rd, L, tun):
    if rd["tier"] == 0:
        return int(rd["binCost"])
    return math.ceil(rd["baseCost"] * rd["costDiff"] * tun["costMultiplier"] * (L ** tun["pCost"]))


def dur_at(rd, L, tun, rs_bonus=0.0):
    if rd["tier"] == 0:
        return rd["binDur"]
    return rd["baseDur"] * rd["timeDiff"] * tun["timeMultiplier"] * (L ** tun["pTime"]) / max(0.01, 1 + rs_bonus)


def track_totals(rd, tun):
    mx = TIER_MAX[rd["tier"]]
    c = sum(cost_at(rd, L, tun) for L in range(1, mx + 1))
    t = sum(dur_at(rd, L, tun) for L in range(1, mx + 1))
    return mx, c, t


def rnd_half_even(x):  # Unity Mathf.RoundToInt
    f = math.floor(x); d = x - f
    if d < 0.5: return f
    if d > 0.5: return f + 1
    return f if f % 2 == 0 else f + 1


def fmt_time(sec):
    if sec < 3600: return f"{sec/60:.1f} min"
    if sec < 86400: return f"{sec/3600:.1f} h"
    return f"{sec/86400:.1f} d"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cost-mult", type=float, help="override tuning.costMultiplier for this run")
    ap.add_argument("--time-mult", type=float, help="override tuning.timeMultiplier for this run")
    ap.add_argument("--pcost", type=float, help="override pCost exponent")
    ap.add_argument("--ptime", type=float, help="override pTime exponent")
    ap.add_argument("--csv", help="write per-track table to CSV for graphing")
    args = ap.parse_args()

    tun = load_tuning()
    if args.cost_mult is not None: tun["costMultiplier"] = args.cost_mult
    if args.time_mult is not None: tun["timeMultiplier"] = args.time_mult
    if args.pcost is not None: tun["pCost"] = args.pcost
    if args.ptime is not None: tun["pTime"] = args.ptime

    print(f"Tuning: pCost={tun['pCost']}  pTime={tun['pTime']}  "
          f"costMult={tun['costMultiplier']}  timeMult={tun['timeMultiplier']}\n")

    research = {r["id"]: r for r in load_research()}

    # ---- Chicken egg economy ----
    chick = load_animal("chicken")
    cd = research.get("chicken_cooldown"); eff = research.get("chicken_efficiency")
    cd_max_bonus = TIER_MAX[cd["tier"]] * cd["bonusPerLevel"] if cd else 0
    eff_max_bonus = TIER_MAX[eff["tier"]] * eff["bonusPerLevel"] if eff else 0
    print("=== CHICKEN EGG ECONOMY (perfect collection, 1440 min/day) ===")
    print(f"{'Scenario':26} {'cooldown':>9} {'eggs/day':>9} {'coins/egg':>9} {'coins/day':>10}")
    scenarios = [("No upgrades", 0.0, 0.0),
                 ("Cooldown maxed only", cd_max_bonus, 0.0),
                 ("Efficiency maxed only", 0.0, eff_max_bonus),
                 ("Both maxed", cd_max_bonus, eff_max_bonus)]
    for label, cb, eb in scenarios:
        cool = chick["cooldownMin"] / (1 + cb)
        reward = rnd_half_even(chick["reward"] * (1 + eb))
        eggs = 1440 / cool
        print(f"{label:26} {cool:7.2f}m {eggs:9.1f} {reward:9d} {eggs*reward:10,.0f}")

    for rd in (cd, eff):
        if not rd: continue
        mx, c, t = track_totals(rd, tun)
        print(f"\n  {rd['name']}: max L{mx} (+{mx*rd['bonusPerLevel']*100:.1f}%)  "
              f"cost {c:,}  time {fmt_time(t)}")

    # ---- Whole tree ----
    rows = []
    total_c = total_t = 0
    for rd in research.values():
        mx, c, t = track_totals(rd, tun)
        rows.append((c, t, rd["name"] or rd["id"], rd["branch"], mx))
        total_c += c; total_t += t
    rows.sort(reverse=True)
    print(f"\n=== WHOLE TREE -- all {len(rows)} tracks fully maxed ===")
    print(f"  TOTAL COST: {total_c:,} coins")
    print(f"  TOTAL TIME: {fmt_time(total_t)} on ONE slot  |  ~{fmt_time(total_t/4)} across 4 slots")
    print("  Top 8 by cost:")
    for c, t, n, b, mx in rows[:8]:
        print(f"    {n:30} [{b:9}] L{mx:<3} {c:>13,} coins  {fmt_time(t):>8}")

    if args.csv:
        with open(args.csv, "w", encoding="utf-8") as f:
            f.write("name,branch,maxLevel,cost,time_seconds,time_days\n")
            for c, t, n, b, mx in sorted(rows, key=lambda r: -r[0]):
                f.write(f'"{n}",{b},{mx},{c},{t:.0f},{t/86400:.3f}\n')
        print(f"\nWrote per-track table -> {args.csv}")


if __name__ == "__main__":
    main()
