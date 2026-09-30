# Summarise tools/dread_ab.lua runs: python tools/dread_ab.py out/dread_*.csv
# Per arm (dread on / off), pooled over seeds and per seed: famine and hunger rates, starvation,
# stores and storage, sowing, spoilage, emigration; the response to a failed harvest; and, within
# the "on" arm, how villages that remember hunger differ from those that don't.
import csv, sys, glob, collections, math

rows = []
for pat in sys.argv[1:] or ["out/dread_*.csv"]:
    for fn in glob.glob(pat):
        with open(fn, newline="") as f:
            for r in csv.DictReader(f):
                for k, v in r.items():
                    if k != "arm":
                        r[k] = float(v)
                rows.append(r)

def summary(rs):
    vy = len(rs)
    py = sum(r["pop"] for r in rs) or 1
    harv = sum(r["harvest"] for r in rs) or 1
    need = sum(r["need"] for r in rs) or 1
    s = collections.OrderedDict()
    s["village-years"] = vy
    s["famines/100vy"] = 100 * sum(r["famine"] for r in rs) / max(vy, 1)
    s["rationing/100vy"] = 100 * sum(r["rationing"] for r in rs) / max(vy, 1)
    s["seed eaten/100vy"] = 100 * sum(r["seed_eaten"] for r in rs) / max(vy, 1)
    s["crop fail/100vy"] = 100 * sum(r["crop_failure"] for r in rs) / max(vy, 1)
    s["starved/1000py"] = 1000 * sum(r["starved"] for r in rs) / py
    s["deaths/1000py"] = 1000 * sum(r["deaths"] for r in rs) / py
    s["emigrants/1000py"] = 1000 * sum(r["mig_out"] for r in rs) / py
    s["store yrs"] = sum(r["store_y"] * r["pop"] for r in rs) / py
    s["capacity yrs"] = sum(r["cap_y"] * r["pop"] for r in rs) / py
    s["at capacity"] = sum(r["at_cap"] * r["pop"] for r in rs) / py
    s["harvest/need"] = harv / need
    s["spoiled/harvest"] = sum(r["spoil_g"] for r in rs) / harv
    s["fields/100p"] = 100 * sum(r["fields"] for r in rs) / py
    s["dread"] = sum(r["dread"] * r["pop"] for r in rs) / py
    s["granaries/100p"] = 100 * sum(r.get("granaries", 0) for r in rs) / py
    s["dread granaries"] = sum(r.get("granary_dread", 0) for r in rs)
    s["gifts out/need"] = sum(r.get("gift_out", 0) for r in rs) / need
    s["feasts/need"] = sum(r.get("feast", 0) for r in rs) / need
    s["relief in/need"] = sum(r.get("relief_in", 0) for r in rs) / need
    return s

def recurrence(rs, lo=2, hi=30):
    # after a famine in a village: another famine there within (lo, hi] years? (the memory window)
    by = collections.defaultdict(dict)
    for r in rs:
        by[(r["seed"], r["village"])][int(r["year"])] = r
    n = hit = 0
    for key, ys in by.items():
        last = max(ys)
        for y, r in ys.items():
            if r["famine"] > 0 and y + hi <= last:
                n += 1
                if any(ys.get(y + k, {}).get("famine", 0) > 0 for k in range(lo + 1, hi + 1)):
                    hit += 1
    return n, hit

def resilience(rs):
    # after a crop failure: a famine in the same village within 3 years?
    by = collections.defaultdict(dict)
    for r in rs:
        by[(r["seed"], r["village"])][int(r["year"])] = r
    n = hit = 0
    for key, ys in by.items():
        for y, r in ys.items():
            if r["crop_failure"] > 0:
                n += 1
                if any(ys.get(y + k, {}).get("famine", 0) > 0 for k in range(0, 4)):
                    hit += 1
    return n, hit

arms = collections.defaultdict(list)
seeds = collections.defaultdict(lambda: collections.defaultdict(list))
for r in rows:
    arms[r["arm"]].append(r)
    seeds[int(r["seed"])][r["arm"]].append(r)

keys = list(summary(rows).keys())
print("%-18s %10s %10s" % ("pooled", "on", "off"))
son, soff = summary(arms["on"]), summary(arms["off"])
for k in keys:
    print("%-18s %10.3f %10.3f" % (k, son[k], soff[k]))
for arm in ("on", "off"):
    n, hit = resilience(arms[arm])
    print("%s: %d crop failures, %d followed by famine within 3 y (%.0f%%)" % (arm, n, hit, 100 * hit / max(n, 1)))
    n, hit = recurrence(arms[arm])
    print("%s: %d famines (with 30 years after them in the run), %d followed by another famine 3-30 y later (%.0f%%)" % (arm, n, hit, 100 * hit / max(n, 1)))
print()
print("per seed (famines/100vy, starved/1000py, store yrs, at capacity, spoiled/harvest, emigrants/1000py):")
for sd in sorted(seeds):
    parts = []
    for arm in ("on", "off"):
        s = summary(seeds[sd][arm]) if seeds[sd][arm] else None
        if s:
            parts.append("%s %.2f %.2f %.2f %.2f %.2f %.1f" % (arm, s["famines/100vy"], s["starved/1000py"], s["store yrs"], s["at capacity"], s["spoiled/harvest"], s["emigrants/1000py"]))
    print("  seed %d: %s" % (sd, " | ".join(parts)))
# within the "on" arm: villages that remember hunger vs not
hi = [r for r in arms["on"] if r["dread"] >= 0.3]
lo = [r for r in arms["on"] if r["dread"] < 0.1]
if hi and lo:
    a, b = summary(hi), summary(lo)
    print()
    print("'on' arm, dread >= 0.3 (%d vy) vs < 0.1 (%d vy):" % (len(hi), len(lo)))
    for k in ("store yrs", "capacity yrs", "at capacity", "fields/100p", "harvest/need", "spoiled/harvest", "famines/100vy", "starved/1000py"):
        print("  %-18s %8.3f %8.3f" % (k, a[k], b[k]))
