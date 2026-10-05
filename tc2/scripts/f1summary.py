"""Summarise a planet run's CSV (headless `planet --csv`): climate, ice, sea, relief and life per millennium.
Usage: python tc2/scripts/f1summary.py tc2/out/f1/run_42i.csv [tc2/out/f1/run_42i.txt]"""
import csv, sys

rows = list(csv.DictReader(open(sys.argv[1])))
f = lambda r, k: float(r[k])
print(f"{len(rows)} rows, years {f(rows[0],'year'):.0f}-{f(rows[-1],'year'):.0f}")
keys = ['Tglob', 'landT', 'icePct', 'seaNow', 'co2', 'forest', 'landFrac', 'maxE', 'landE', 'contFrac', 'species', 'landAnimals', 'seaAnimals']
print('millennium  ' + '  '.join(f'{k:>17}' for k in keys))
for m in range(0, int(f(rows[-1], 'year')) // 1000 + 1):
    seg = [r for r in rows if m * 1000 <= f(r, 'year') < (m + 1) * 1000 or (m * 1000 == f(r, 'year'))]
    if not seg: continue
    cells = []
    for k in keys:
        v = [f(r, k) for r in seg]
        cells.append(f'{min(v):8.4g}..{max(v):<8.4g}')
    print(f'{m*1000:>5}-{(m+1)*1000:<5} ' + '  '.join(cells))
# glacial episodes from the glac flag
eps, start = [], None
for r in rows:
    g = int(r['glac'])
    if g and start is None: start = f(r, 'year')
    if not g and start is not None: eps.append((start, f(r, 'year'))); start = None
if start is not None: eps.append((start, None))
print('glacial episodes (flag):', ', '.join(f'{a:.0f}-{"end" if b is None else f"{b:.0f}"}' for a, b in eps) or 'none')
sea = [f(r, 'seaNow') for r in rows]; ice = [f(r, 'icePct') for r in rows]; co2 = [f(r, 'co2') for r in rows]
print(f'sea level {min(sea):.1f}..{max(sea):.1f} m; ice {min(ice):.1f}..{max(ice):.1f} % of land; CO2 {min(co2):.0f}..{max(co2):.0f} ppm')
print(f'eruptions total {rows[-1]["eruptions"]}, VEI4+ {rows[-1]["vei4plus"]}; vents {min(int(r["vents"]) for r in rows)}..{max(int(r["vents"]) for r in rows)}')
if len(sys.argv) > 2:
    txt = open(sys.argv[2]).read().splitlines()
    i = next((j for j, l in enumerate(txt) if l.startswith('epochal events:')), None)
    if i is not None:
        print('\n'.join(txt[i:i + 60]))
    for l in txt:
        if l.startswith('ran '): print(l)
