#!/usr/bin/env python3
"""
Progression estimate: how long until a player reaches 50% / 100% of Research, Farm upgrades,
permanent (Coin) upgrades, seeds and Barn skills, assuming NONSTOP play.

Reads the live assets (research, farm upgrades, permanent upgrades, crops, request catalog), so
re-run it after any tuning change:

    python tools/progression_estimate.py                    # default "steady" compost ramp
    python tools/progression_estimate.py --compost all      # compare every compost scenario
    python tools/progression_estimate.py --years 30

Model (hour by hour):
  * Coins/real hour = farm-hours per real hour x harvests per farm-hour x Coins per harvest x uptime.
      - farm-hours per real hour = baseGameSpeed (1.75) x unlocked Game Speed (1 + 0.25 per level);
        the player always runs at their fastest unlocked rung.
      - Coins per harvest is the exact in-game product (Coin research x Coin Yield x Soil Quality x
        Zone Level x luck from Bountiful Harvest / the Harvesting capstone).
      - Harvests per farm-hour is interpolated (log scale) between bench anchors by how far the
        THROUGHPUT items are (fields, helpers, grid, helper/growth speeds): 0% = STARTER_HARVESTS
        (an estimate), 50% and 100% come from docs/balance benches (average of all 9 crops).
  * Research: 4 slots (slot 2 bought with gems on day 1; slots 3-4 via their research). Each free
    slot takes the unfinished track whose next level is quickest (Research Speed, slot unlocks and
    Game Speed first). Levels are paid up front in Coins; an unaffordable level waits.
    Speed = (1 + Research Speed bonus) x compost boost multiplier.
  * Compost boosts: 12h tokens on every slot, bought greedily from the day's compost income
    (2x = 120, 3x = 360, 4x = 1000 per slot per 12h). The compost scenario ramps income up.
  * Every other Coin goes to the cheapest next level of any farm upgrade / permanent upgrade / seed
    packet (a typical idle-player pattern).
  * Barn skills come from delivery rep only: 3 board slots (30/60/90 rep, 8h cooldown each) =
    540 rep/day at best, regardless of Coins.
"""
import argparse, glob, heapq, math, os, re, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import research_extrapolate as rx  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
INT_MAX = 2_147_483_647

# ---- calibration (update after new benches) ----
BASE_GAME_SPEED = 1.75
STARTER_HARVESTS = 150.0   # harvests per farm-hour, 1 field / 1 helper / starter grid (estimate)
BENCH_HARVESTS_50 = 1755.0  # docs/balance/bench-2026-10-01-1127-L50.md, avg of 9 crops
BENCH_HARVESTS_100 = 3525.0  # docs/balance/bench-2026-10-01-0050.md (Coins / ~480 per harvest)
GOLDEN_CAPSTONE = 1.81     # luck at max beyond Bountiful (fits the maxed bench's ~480 Coins/harvest)
UPTIME = 0.9               # time actually farming (restarts, town trips)

BOOST_STEPS = [(2, 120), (3, 360), (4, 1000)]  # 12h token: multiplier, compost per slot

COMPOST = {  # compost income per day as a function of day number
    "none":   lambda d: 0,
    "slow":   lambda d: 50 + 20 * d,    # full 4x on 4 slots after ~a year
    "steady": lambda d: 100 + 60 * d,   # 2x by ~2 weeks, 3x by ~7 weeks, 4x by ~4.5 months
    "fast":   lambda d: 200 + 250 * d,  # 4x everywhere within a month
}


def num(text, key, default=0.0):
    m = re.search(rf'^\s*{re.escape(key)}:\s*([-\d.eE]+)\s*$', text, re.M)
    return float(m.group(1)) if m else default


def sval(text, key):
    m = re.search(rf'^\s*{re.escape(key)}:\s*(.+?)\s*$', text, re.M)
    return m.group(1).strip() if m else ""


class Track:
    """One levelled Coin sink: cost(level) for level 1..max."""
    def __init__(self, kind, tid, name, max_level, cost_fn, bonus=0.0):
        self.kind, self.id, self.name, self.max = kind, tid, name, int(max_level)
        self.cost_fn, self.bonus, self.level = cost_fn, bonus, 0

    def next_cost(self):
        return min(INT_MAX, self.cost_fn(self.level + 1)) if self.level < self.max else None

    def total_cost(self, upto):
        return sum(min(INT_MAX, self.cost_fn(L)) for L in range(1, upto + 1))


def load_farm_upgrades():
    out = []
    for fp in sorted(glob.glob(os.path.join(ROOT, "Assets", "Resources", "FarmUpgrades", "*.asset"))):
        s = open(fp, encoding="utf-8").read()
        base, g = num(s, "baseCoinCost"), max(1.0, num(s, "coinGrowthPerLevel", 1))
        every, bp = int(num(s, "coinBreakpointEvery")), num(s, "coinBreakpointMultiplier", 1)

        def cost(L, base=base, g=g, every=every, bp=bp):
            c = base * g ** (L - 1)
            if every > 0 and bp > 1:
                c *= bp ** ((L - 1) // every)
            return math.ceil(min(c, 9e18))
        out.append(Track("farm", sval(s, "upgradeID"), sval(s, "upgradeID"), num(s, "maxLevel"), cost,
                         num(s, "bonusPerLevel")))
    return out


def load_permanent_upgrades():
    out = []
    for fp in sorted(glob.glob(os.path.join(ROOT, "Assets", "Data", "Upgrades", "**", "*.asset"), recursive=True)):
        s = open(fp, encoding="utf-8").read()
        if "baseCoinCost:" not in s or "maxLevel:" not in s:
            continue  # legacy assets with no Coin pricing
        base, mult = num(s, "baseCoinCost"), num(s, "coinCostMultiplier", 2.5)

        def cost(L, base=base, mult=mult):
            return round(min(base * mult ** (L - 1), 9e18))
        out.append(Track("perm", sval(s, "upgradeID"), sval(s, "displayName"), num(s, "maxLevel"), cost))
    return out


def load_seeds():
    out = []
    for fp in sorted(glob.glob(os.path.join(ROOT, "Assets", "Data", "Crops", "Crop_*.asset"))):
        s = open(fp, encoding="utf-8").read()
        first = int(num(s, "unlockCost"))

        def cost(n, first=first):  # SeedShopRules.PacketPrice
            if n <= 1:
                return first
            second = first * 10 if first > 0 else 10
            return second << (n - 2)
        out.append(Track("seed", "seed_" + sval(s, "cropName"), sval(s, "cropName"), 4, cost))
    return out


def load_research():
    tun = rx.load_tuning()
    out = []
    for rd in rx.load_research():
        mx = rx.TIER_MAX[rd["tier"]]
        t = Track("research", rd["id"], rd["name"] or rd["id"], mx,
                  lambda L, rd=rd: rx.cost_at(rd, L, tun), rd["bonusPerLevel"])
        t.dur_fn = lambda L, rd=rd: rx.dur_at(rd, L, tun)
        t.partial = 0.0
        out.append(t)
    return out


def boost_multiplier(compost_per_day, slots):
    """Average boost multiplier when a day's compost buys 12h tokens greedily across all slots."""
    windows = 2 * slots
    left, mult, prev_cost, prev_m = compost_per_day, 1.0, 0, 1
    for m, c in BOOST_STEPS:
        step = (c - prev_cost) * windows
        frac = min(1.0, left / step) if step > 0 else 0
        mult += (m - prev_m) * frac
        left -= step * frac
        prev_cost, prev_m = c, m
        if frac < 1:
            break
    return mult


def done(tracks, frac):
    return all(t.level >= math.ceil(t.max * frac) for t in tracks)


def simulate(compost_name, years, verbose=False):
    research = load_research()
    farm = load_farm_upgrades()
    perm = load_permanent_upgrades()
    seeds = load_seeds()
    R = {t.id: t for t in research}
    F = {t.id: t for t in farm}
    P = {t.id: t for t in perm}
    seeds_by = {t.name: t for t in seeds}
    seeds_by["Radish"].level = 1  # starter crop

    def rlvl(i): return R[i].level if i in R else 0
    def flvl(i): return F[i].level if i in F else 0

    throughput = ([P[i] for i in ("zone_unlock_2", "zone_unlock_3", "zone_unlock_4", "helper_slot_unlock",
                                  "grid_size", "helper_harvesting_speed", "helper_move_speed",
                                  "helper_planting_speed", "helper_task_speed", "helper_watering_speed") if i in P]
                  + [R[i] for i in ("helper_harvest_speed", "helper_plant_speed", "helper_till_speed",
                                    "helper_water_speed", "crop_growth_speed") if i in R]
                  + [F[i] for i in ("growth_rate",) if i in F])

    def harvests_per_farm_hour():
        p = sum(t.level / t.max for t in throughput) / len(throughput)
        if p <= 0.5:
            return STARTER_HARVESTS * (BENCH_HARVESTS_50 / STARTER_HARVESTS) ** (p / 0.5)
        return BENCH_HARVESTS_50 * (BENCH_HARVESTS_100 / BENCH_HARVESTS_50) ** ((p - 0.5) / 0.5)

    def coins_per_harvest():
        zones = [flvl(f"zone_level_z{z}") for z in range(1, 5)]
        fields = 1 + sum(P[i].level for i in ("zone_unlock_2", "zone_unlock_3", "zone_unlock_4") if i in P)
        zone = sum(1 + 0.005 * zones[z] for z in range(fields)) / fields
        luck = (1 + 0.01 * flvl("bountiful_harvest"))
        c = ((1 + 0.005 * rlvl("crop_bonus_coin_amount")) * (1 + 0.01 * flvl("coin_yield"))
             * (1 + 0.005 * flvl("soil_quality")) * zone * luck)
        return max(1.0, c)

    def game_speed():
        return BASE_GAME_SPEED * (1 + 0.25 * rlvl("game_speed"))

    slots = 2  # slot 1 Coins, slot 2 gems: both day 1
    active = [None] * 4
    priority = ["research_speed", "slot_3_unlock", "slot_4_unlock", "game_speed", "crop_bonus_coin_amount"]

    def pick_research():
        busy = {t.id for t in active if t}
        for pid in priority:
            t = R.get(pid)
            if t and t.level < t.max and t.id not in busy:
                return t
        cands = [t for t in research if t.level < t.max and t.id not in busy]
        # Spread evenly: the track furthest behind (as a % of its max) goes next.
        return min(cands, key=lambda t: (t.level / t.max, t.dur_fn(t.level + 1))) if cands else None

    coins = 0.0
    lifetime = 0.0
    sinks = farm + perm + seeds
    milestones = {}
    long_ids = {t.id for t in farm if t.max >= 500}
    groups = {"Research": research,
              "Farm upgrades (short)": [t for t in farm if t.id not in long_ids],
              "Farm upgrades (500+ lvl)": [t for t in farm if t.id in long_ids],
              "Equipment/helpers/fields": [t for t in perm if t.id != "sprinkler_duration"],
              "Seeds": seeds}
    snapshots = []
    snap_days = [1, 7, 30, 90, 180, 365, 730, 1825]
    research_wait_hours = 0
    hours = int(years * 365 * 24)
    for h in range(hours):
        day = h / 24
        # income
        earned = UPTIME * game_speed() * harvests_per_farm_hour() * coins_per_harvest()
        coins += earned
        lifetime += earned
        # research
        speed = (1 + 0.01 * rlvl("research_speed")) * boost_multiplier(COMPOST[compost_name](day), slots)
        for s in range(slots):
            budget = 3600.0 * speed
            while budget > 0:
                t = active[s]
                if t is None:
                    t = pick_research()
                    if t is None:
                        break
                    cost = t.next_cost()
                    if coins < cost:
                        research_wait_hours += 1
                        break  # can't afford the next level yet; slot idles
                    coins -= cost
                    t.partial = 0.0
                    active[s] = t
                need = t.dur_fn(t.level + 1) - t.partial
                if budget >= need:
                    budget -= need
                    t.level += 1
                    active[s] = None
                    if t.id == "slot_3_unlock": slots = max(slots, 3)
                    if t.id == "slot_4_unlock": slots = max(slots, 4)
                else:
                    t.partial += budget
                    budget = 0
        # spend the rest on the cheapest next level anywhere (keep a small research reserve)
        reserve = max((t.next_cost() or 0) for t in research if t.level < t.max) if any(
            t.level < t.max for t in research) else 0
        reserve = min(reserve, coins * 0.5)
        while True:
            best = min((t for t in sinks if t.level < t.max), key=lambda t: t.next_cost(), default=None)
            if best is None or best.next_cost() > coins - reserve or best.next_cost() >= INT_MAX:
                break
            coins -= best.next_cost()
            best.level += 1
        # milestones
        for g, ts in groups.items():
            for f in (0.25, 0.5, 1.0):
                if (g, f) not in milestones and done(ts, f):
                    milestones[(g, f)] = (day, lifetime)
        if snap_days and day >= snap_days[0]:
            d = snap_days.pop(0)
            rpct = sum(t.level / t.max for t in research) / len(research)
            lng = [t.level for t in farm if t.id in long_ids]
            snapshots.append((d, rpct, sum(lng) / len(lng), coins_per_harvest(), earned, lifetime, slots, speed))
        if verbose and h % (24 * 30) == 0:
            print(f"  day {day:6.0f}: {earned:12,.0f} Coins/h  coins/harvest {coins_per_harvest():7.1f}  "
                  f"harvests/farm-h {harvests_per_farm_hour():6.0f}  speed {game_speed():.2f}  "
                  f"slots {slots}  research boost {speed:.2f}x")
    return milestones, groups, snapshots, research_wait_hours


def static_costs():
    rows = []
    for name, ts in (("Research", load_research()), ("Farm upgrades", load_farm_upgrades()),
                     ("Permanent upgrades", load_permanent_upgrades()), ("Seeds", load_seeds())):
        for f in (0.5, 1.0):
            c = sum(t.total_cost(math.ceil(t.max * f)) for t in ts)
            extra = ""
            if name == "Research":
                secs = sum(sum(t.dur_fn(L) for L in range(1, math.ceil(t.max * f) + 1)) for t in ts)
                extra = f"{secs / 86400:,.0f} slot-days ({secs / 86400 / 4:,.0f} days on 4 slots at 1x)"
            capped = sum(1 for t in ts for L in range(1, math.ceil(t.max * f) + 1) if t.cost_fn(L) >= INT_MAX)
            rows.append((name, f, c, capped, extra))
    return rows


def skills():
    # 7 tracks x 25 levels, one point each; Nth point costs 85 + 15N rep; best case 540 rep/day.
    out = []
    for f in (0.25, 0.5, 1.0):
        pts = math.ceil(175 * f)
        rep = sum(85 + 15 * n for n in range(1, pts + 1))
        out.append((f, pts, rep, rep / 540))
    return out


def fmt_days(d):
    if d is None:
        return "not reached"
    return f"{d:,.0f} days ({d / 365:.1f} yr)" if d >= 365 else f"{d:,.1f} days"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--compost", default="steady", help="none | slow | steady | fast | all")
    ap.add_argument("--years", type=float, default=40)
    ap.add_argument("--verbose", action="store_true")
    args = ap.parse_args()

    print("== Price tags (Coins to buy every track to that %; levels clamped at int.MaxValue) ==")
    for name, f, c, capped, extra in static_costs():
        note = f"  [{capped} levels cost int.MaxValue]" if capped else ""
        print(f"  {name:20} {f:>4.0%}: {c:>22,} Coins  {extra}{note}")
    print("\n== Barn skills (rep only, 540 rep/day best case) ==")
    for f, pts, rep, days in skills():
        print(f"  {f:>4.0%}: {pts:3} points, {rep:,} rep -> {days:,.0f} days")

    names = list(COMPOST) if args.compost == "all" else [args.compost]
    for n in names:
        print(f"\n== Nonstop simulation, compost '{n}' ==")
        ms, groups, snaps, wait = simulate(n, args.years, args.verbose)
        print(f"  {'day':>5} {'research':>9} {'500-lvl avg':>11} {'Coins/harvest':>13} {'Coins/hour':>12} {'lifetime Coins':>16} slots boost")
        for d, rp, lv, cph, cphr, life, sl, sp in snaps:
            print(f"  {d:>5} {rp:>8.0%} {lv:>11.0f} {cph:>13.1f} {cphr:>12,.0f} {life:>16,.0f} {sl:>5} {sp:4.1f}x")
        print(f"  research slot-hours spent waiting for Coins: {wait:,}")
        for g in groups:
            parts = []
            for f in (0.25, 0.5, 1.0):
                v = ms.get((g, f))
                parts.append(f"{f:.0%}: {fmt_days(v[0]) if v else 'not in ' + str(int(args.years)) + ' yr'}")
            print(f"  {g:20} " + " | ".join(parts))


if __name__ == "__main__":
    main()
