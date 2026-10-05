"""Counts event kinds in a headless run's history.txt (tc2 run --out DIR). Usage: python histstats.py DIR [DIR...]"""
import collections
import sys

KEYS = ['was born', 'died of hunger', 'died in', 'married', 'saw the fire', 'burnt out', 'wildfire', 'fields of',
        'brought in', 'harvest failed', 'Hunger came', 'abandoned', 'Lightning']

for d in sys.argv[1:]:
    c = collections.Counter()
    fire_hours = []
    for line in open(d + '/history.txt', encoding='utf-8'):
        for k in KEYS:
            if k in line:
                c[k] += 1
        if 'burnt out after' in line:
            try:
                fire_hours.append(int(line.split('burnt out after ')[1].split(' hours')[0]))
            except (IndexError, ValueError):
                pass
    fh = sorted(fire_hours)
    med = fh[len(fh) // 2] if fh else 0
    print(d, dict(c), 'fire hours median', med, 'max', fh[-1] if fh else 0)
