import sys, math
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Polygon, Circle
# usage: python tc2/scripts/plotfoot.py out.png layout1.txt [layout2.txt ...] [--zoom R]
# (C13.3) a village's plan from `place --layout`: every building's ground as it stands - homes as their plan's rectangles turned to their
# bearing (front edge darker), works as discs; streets, lanes, the green / market place, the core's divided reach, the ring; overlapping
# homes outlined red.
args = sys.argv[1:]
zoom = None
if '--zoom' in args:
    i = args.index('--zoom'); zoom = float(args[i + 1]); del args[i:i + 2]
out = args[0]; files = args[1:]
n = len(files)
fig, axes = plt.subplots(1, n, figsize=(8 * n, 8))
if n == 1: axes = [axes]
cols = {0: '#c8a060', 1: '#a07848', 2: '#7a5a38', 3: '#d06040', 4: '#802020', 5: '#3060b0', 7: '#bbaa88', -1: '#999999'}

def corners(x, y, f, cx, cz, hx, hz):
    fx, fy = math.cos(f), math.sin(f); ux, uy = -fy, fx
    px, py = x + ux * cx + fx * cz, y + uy * cx + fy * cz
    pts = []
    for (a, b) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        pts.append((px + ux * a * hx + fx * b * hz, py + uy * a * hx + fy * b * hz))
    return pts

def sep(A, B):
    # separating axis test on two convex quads
    for P in (A, B):
        for i in range(4):
            x1, y1 = P[i]; x2, y2 = P[(i + 1) % 4]
            nx, ny = y2 - y1, x1 - x2
            pa = [nx * x + ny * y for (x, y) in A]; pb = [nx * x + ny * y for (x, y) in B]
            if max(pa) <= min(pb) or max(pb) <= min(pa): return True
    return False

for ax, f in zip(axes, files):
    V = None; S = []; B = []
    for line in open(f):
        t = line.split()
        if not t: continue
        if t[0] == 'V': V = t
        elif t[0] == 'S': S.append(float(t[1]))
        elif t[0] == 'B': B.append(t)
    sx, sy = float(V[4]), float(V[5])
    green = float(V[9]) if len(V) > 9 else 16.0
    core = float(V[10]) if len(V) > 10 else 0.0
    R = 40
    homes = []
    for t in B:
        k = int(t[1]); x = float(t[2]); y = float(t[3]); pl = int(t[4]); lived = int(t[5]); fa = float(t[6]); inopen = int(t[7])
        R = max(R, math.hypot(x - sx, y - sy) + 20)
        if t[8] == 'R':
            ax.add_patch(Circle((x, y), float(t[9]), color='#4060a0', alpha=0.55, zorder=2))
            continue
        rects = []
        vals = [float(v) for v in t[8:]]
        for i in range(0, len(vals), 4):
            rects.append(corners(x, y, fa, vals[i], vals[i + 1], vals[i + 2], vals[i + 3]))
        homes.append((k, x, y, pl, lived, fa, rects, inopen))
    if zoom: R = zoom
    # streets and their lanes (36 m out), the core's divided reach
    def band(ox, oy, ux, uy, t0, t1, half, col):
        nx, ny = -uy, ux
        ax.add_patch(Polygon([(ox + ux * t0 + nx * half, oy + uy * t0 + ny * half), (ox + ux * t1 + nx * half, oy + uy * t1 + ny * half),
                              (ox + ux * t1 - nx * half, oy + uy * t1 - ny * half), (ox + ux * t0 - nx * half, oy + uy * t0 - ny * half)], closed=True, color=col, zorder=1, lw=0))
    for a in S:
        ux, uy = math.cos(a), math.sin(a)
        band(sx, sy, ux, uy, 12, R * 1.5, 4.0, '#e2cfa8')
        for off in (36, -36):
            ox, oy = sx - uy * off, sy + ux * off
            band(ox, oy, ux, uy, 26, R * 1.5, 4.0, '#efe4cc')
        if core > 0:
            ax.plot([sx + ux * 12, sx + ux * core], [sy + uy * 12, sy + uy * core], color='#3060b0', lw=1.0, zorder=1)
    if int(V[6]) == 2: ax.add_patch(Circle((sx, sy), float(V[7]), fill=False, color='#604020', lw=1.5))
    ax.add_patch(Circle((sx, sy), green, color='#9cba70', zorder=0))
    # overlapping homes
    bad = set()
    for i in range(len(homes)):
        for j in range(i + 1, len(homes)):
            a, b = homes[i], homes[j]
            if math.hypot(a[1] - b[1], a[2] - b[2]) > 30: continue
            for ra in a[6]:
                for rb in b[6]:
                    if not sep(ra, rb): bad.add(i); bad.add(j)
    for i, (k, x, y, pl, lived, fa, rects, inopen) in enumerate(homes):
        for r in rects:
            ax.add_patch(Polygon(r, closed=True, facecolor=cols.get(pl, '#888') if k == 2 else '#bbaa88', edgecolor='red' if i in bad else ('#ff00ff' if inopen else ('k' if lived else '#777')),
                                 linewidth=1.2 if (i in bad or inopen) else 0.4, alpha=0.9, zorder=3))
        # the front edge
        r = rects[0]; ax.plot([r[2][0], r[3][0]], [r[2][1], r[3][1]], color='#202020', lw=0.9, zorder=4)
    ax.set_aspect('equal')
    ax.set_title(f"{V[2]} #{V[1]}: {V[3]} people, {V[8]} y, streets {len(S)}, green {green:.0f} m, core {core:.0f} m, overlapping {len(bad)}")
    ax.set_xlim(sx - R, sx + R); ax.set_ylim(sy - R, sy + R)
plt.tight_layout(); plt.savefig(out, dpi=90)
print("overlapping homes:", out)
