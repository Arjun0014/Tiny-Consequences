# Generates the UI atlas images (Stage 1.9): rounded panels, soft shadows and simple icons,
# drawn as white silhouettes (tinted per node in the GUI). Output: assets/ui/*.png (committed,
# so builds don't need Python). Run: PYTHONUTF8=1 python scripts/gen_ui.py
# Requires Pillow. All art here is original (06_PRESENTATION.md: generated assets only).
import math
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'assets', 'ui')
os.makedirs(OUT, exist_ok=True)
SS = 4          # supersampling
W = (255, 255, 255, 255)


def canvas(size):
    img = Image.new('RGBA', (size * SS, size * SS), (255, 255, 255, 0))
    return img, ImageDraw.Draw(img)


def save(img, name, size):
    img = img.resize((size, size), Image.LANCZOS)
    img.save(os.path.join(OUT, name + '.png'))


def S(v):
    return v * SS


def poly(d, pts, fill=W):
    d.polygon([(S(x), S(y)) for x, y in pts], fill=fill)


def circle(d, cx, cy, r, fill=W):
    d.ellipse([S(cx - r), S(cy - r), S(cx + r), S(cy + r)], fill=fill)


def rrect(d, x0, y0, x1, y1, r, fill=W):
    d.rounded_rectangle([S(x0), S(y0), S(x1), S(y1)], radius=S(r), fill=fill)


def line(d, pts, w, fill=W):
    d.line([(S(x), S(y)) for x, y in pts], fill=fill, width=int(round(S(w))), joint='curve')
    for x, y in (pts[0], pts[-1]):
        circle(d, x, y, w / 2, fill)


CLEAR = (255, 255, 255, 0)

# --- panels -------------------------------------------------------------------------------
img, d = canvas(48); rrect(d, 0, 0, 48, 48, 16); save(img, 'panel', 48)
img, d = canvas(48); rrect(d, 0, 0, 48, 48, 24); save(img, 'pill', 48)
img, d = canvas(16); rrect(d, 0, 0, 16, 16, 8); save(img, 'pill_s', 16)
Image.new('RGBA', (8, 8), (255, 255, 255, 255)).save(os.path.join(OUT, 'px.png'))
img, d = canvas(64); circle(d, 32, 32, 31); save(img, 'circle', 64)
img, d = canvas(64); circle(d, 32, 32, 30); circle(d, 32, 32, 24, CLEAR); save(img, 'ring', 64)
# soft shadow: blurred rounded rect with a 16 px falloff border
sh = Image.new('RGBA', (80, 80), (0, 0, 0, 0))
sd = ImageDraw.Draw(sh)
sd.rounded_rectangle([16, 16, 64, 64], radius=16, fill=(255, 255, 255, 255))
sh = sh.filter(ImageFilter.GaussianBlur(6))
sh.save(os.path.join(OUT, 'shadow.png'))

# --- icons (64 px, drawn on a 64 grid) --------------------------------------------------------
ICONS = {}


def icon(name):
    def deco(fn):
        ICONS[name] = fn
        return fn
    return deco


@icon('pause')
def _(d):
    rrect(d, 16, 12, 28, 52, 4); rrect(d, 36, 12, 48, 52, 4)


@icon('play')
def _(d):
    poly(d, [(20, 10), (52, 32), (20, 54)])


@icon('fast')
def _(d):
    poly(d, [(8, 14), (32, 32), (8, 50)]); poly(d, [(32, 14), (56, 32), (32, 50)])


@icon('faster')
def _(d):
    poly(d, [(2, 16), (22, 32), (2, 48)]); poly(d, [(22, 16), (42, 32), (22, 48)]); poly(d, [(42, 16), (62, 32), (42, 48)])


@icon('fast4')
def _(d):
    poly(d, [(2, 20), (16, 32), (2, 44)]); poly(d, [(16, 20), (30, 32), (16, 44)])
    poly(d, [(30, 20), (44, 32), (30, 44)]); poly(d, [(44, 20), (58, 32), (44, 44)])


@icon('cycle')
def _(d):
    d.arc([S(10), S(10), S(54), S(54)], 200, 500, fill=W, width=S(7))
    poly(d, [(42, 4), (58, 16), (40, 24)])


@icon('max')
def _(d):
    poly(d, [(4, 14), (28, 32), (4, 50)]); poly(d, [(26, 14), (50, 32), (26, 50)]); rrect(d, 51, 14, 59, 50, 3)


@icon('person')
def _(d):
    circle(d, 32, 18, 11)
    d.pieslice([S(12), S(32), S(52), S(72)], 180, 360, fill=W)


@icon('people')
def _(d):
    circle(d, 22, 20, 9); d.pieslice([S(6), S(34), S(38), S(66)], 180, 360, fill=W)
    circle(d, 44, 18, 9); d.pieslice([S(28), S(32), S(60), S(64)], 180, 360, fill=W)


@icon('house')
def _(d):
    poly(d, [(8, 30), (32, 8), (56, 30)]); rrect(d, 14, 28, 50, 56, 3)
    rrect(d, 27, 38, 37, 56, 2, CLEAR)


@icon('deer')
def _(d):
    rrect(d, 14, 26, 46, 40, 7)                       # body
    for x in (17, 24, 38, 44):
        rrect(d, x - 2, 36, x + 2, 56, 2)             # legs
    rrect(d, 42, 14, 48, 30, 3)                        # neck
    d.ellipse([S(42), S(8), S(56), S(20)], fill=W)     # head
    line(d, [(46, 10), (42, 2)], 2.5); line(d, [(51, 10), (56, 2)], 2.5)   # antlers
    line(d, [(43, 5), (39, 5)], 2); line(d, [(55, 5), (59, 5)], 2)


@icon('wolf')
def _(d):
    rrect(d, 10, 28, 46, 42, 7)
    for x in (13, 20, 36, 43):
        rrect(d, x - 2, 38, x + 2, 56, 2)
    poly(d, [(40, 30), (46, 16), (58, 24), (54, 32)])  # head
    poly(d, [(44, 20), (44, 10), (49, 18)])            # ear
    poly(d, [(12, 32), (2, 20), (6, 36)])              # tail


@icon('fish')
def _(d):
    d.ellipse([S(10), S(18), S(46), S(46)], fill=W)
    poly(d, [(40, 32), (58, 18), (58, 46)])
    circle(d, 20, 29, 3, CLEAR)


@icon('thermo')
def _(d):
    rrect(d, 25, 6, 39, 42, 7); circle(d, 32, 48, 11)
    rrect(d, 29, 12, 35, 44, 3, CLEAR); circle(d, 32, 48, 6, CLEAR)
    circle(d, 32, 48, 4.5); rrect(d, 30.5, 26, 33.5, 46, 1.5)


@icon('cloud')
def _(d):
    circle(d, 22, 36, 12); circle(d, 36, 28, 16); circle(d, 48, 38, 10); rrect(d, 12, 36, 56, 48, 6)


@icon('snow')
def _(d):
    for a in range(0, 180, 60):
        r = math.radians(a)
        dx, dy = math.cos(r) * 24, math.sin(r) * 24
        line(d, [(32 - dx, 32 - dy), (32 + dx, 32 + dy)], 5)
        for s in (-1, 1):
            bx, by = 32 + s * dx * 0.6, 32 + s * dy * 0.6
            for t in (-1, 1):
                rr = r + s * t * math.radians(40) + (math.pi if s < 0 else 0)
                line(d, [(bx, by), (bx + math.cos(rr) * 9, by + math.sin(rr) * 9)], 3.5)


@icon('wave')
def _(d):
    for yo in (22, 40):
        pts = [(4 + i * 2, yo + math.sin(i * 0.42) * 5) for i in range(29)]
        line(d, pts, 6)


@icon('globe')
def _(d):
    circle(d, 32, 32, 26); circle(d, 32, 32, 21, CLEAR)
    d.ellipse([S(22), S(6), S(42), S(58)], outline=W, width=int(S(4)))
    line(d, [(8, 32), (56, 32)], 4); line(d, [(12, 20), (52, 20)], 3.5); line(d, [(12, 44), (52, 44)], 3.5)


@icon('leaf')
def _(d):
    pts = []
    for i in range(21):
        t = i / 20
        x, y = 10 + 44 * t, 54 - 44 * t
        bulge = math.sin(t * math.pi) * 16
        pts.append((x - bulge * 0.7, y - bulge * 0.7))
    for i in range(20, -1, -1):
        t = i / 20
        x, y = 10 + 44 * t, 54 - 44 * t
        bulge = math.sin(t * math.pi) * 16
        pts.append((x + bulge * 0.7, y + bulge * 0.7))
    poly(d, pts)
    line(d, [(14, 50), (44, 20)], 3, CLEAR)


@icon('tree')
def _(d):
    circle(d, 32, 24, 17); circle(d, 20, 32, 12); circle(d, 44, 32, 12); rrect(d, 28, 36, 36, 58, 3)


@icon('drop')
def _(d):
    poly(d, [(32, 6), (48, 34), (16, 34)]); circle(d, 32, 40, 16)


@icon('sun')
def _(d):
    circle(d, 32, 32, 13)
    for a in range(0, 360, 45):
        r = math.radians(a)
        line(d, [(32 + math.cos(r) * 19, 32 + math.sin(r) * 19), (32 + math.cos(r) * 27, 32 + math.sin(r) * 27)], 5)


@icon('hourglass')
def _(d):
    rrect(d, 14, 6, 50, 12, 3); rrect(d, 14, 52, 50, 58, 3)
    poly(d, [(18, 12), (46, 12), (33, 32), (46, 52), (18, 52), (31, 32)])
    poly(d, [(24, 16), (40, 16), (32, 28)], CLEAR); poly(d, [(32, 38), (42, 50), (22, 50)], CLEAR)
    poly(d, [(27, 46), (37, 46), (32, 40)])


@icon('up')
def _(d):
    poly(d, [(32, 8), (54, 34), (40, 34), (40, 56), (24, 56), (24, 34), (10, 34)])


@icon('down')
def _(d):
    poly(d, [(32, 56), (54, 30), (40, 30), (40, 8), (24, 8), (24, 30), (10, 30)])


@icon('flat')
def _(d):
    poly(d, [(58, 32), (34, 12), (34, 24), (6, 24), (6, 40), (34, 40), (34, 52)])


@icon('close')
def _(d):
    line(d, [(16, 16), (48, 48)], 8); line(d, [(48, 16), (16, 48)], 8)


@icon('chev_right')
def _(d):
    line(d, [(22, 12), (42, 32), (22, 52)], 8)


@icon('chev_left')
def _(d):
    line(d, [(42, 12), (22, 32), (42, 52)], 8)


@icon('heart')
def _(d):
    circle(d, 22, 24, 13); circle(d, 42, 24, 13); poly(d, [(10, 30), (54, 30), (32, 56)])


@icon('bread')
def _(d):
    d.ellipse([S(6), S(12), S(58), S(44)], fill=W)
    rrect(d, 6, 28, 58, 52, 7)
    for x in (20, 32, 44):
        line(d, [(x - 4, 26), (x + 4, 18)], 3.5, CLEAR)


@icon('star')
def _(d):
    pts = []
    for i in range(10):
        r = 27 if i % 2 == 0 else 11
        a = math.radians(-90 + i * 36)
        pts.append((32 + math.cos(a) * r, 33 + math.sin(a) * r))
    poly(d, pts)


@icon('bubble')
def _(d):
    rrect(d, 6, 8, 58, 44, 14); poly(d, [(16, 40), (30, 42), (12, 58)])


@icon('fire')
def _(d):
    d.pieslice([S(12), S(22), S(52), S(62)], 0, 180, fill=W)
    poly(d, [(12, 42), (22, 18), (28, 30), (36, 4), (46, 26), (52, 42)])
    circle(d, 32, 46, 7, CLEAR)


@icon('ship')
def _(d):
    poly(d, [(4, 36), (60, 36), (50, 52), (12, 52)]); rrect(d, 34, 18, 50, 36, 3); rrect(d, 40, 8, 45, 18, 2)
    rrect(d, 12, 28, 32, 36, 2)


@icon('boat')
def _(d):
    poly(d, [(8, 40), (56, 40), (46, 54), (16, 54)]); line(d, [(32, 8), (32, 40)], 3); poly(d, [(34, 10), (54, 34), (34, 34)])


@icon('pin')
def _(d):
    circle(d, 32, 24, 18); poly(d, [(16, 32), (48, 32), (32, 58)]); circle(d, 32, 24, 7, CLEAR)


@icon('pickaxe')     # job: miner/builder placeholder
def _(d):
    line(d, [(18, 54), (44, 20)], 5); d.arc([S(14), S(6), S(62), S(46)], 200, 330, fill=W, width=int(S(6)))


@icon('wheat')
def _(d):
    line(d, [(32, 58), (32, 14)], 3.5)
    for y in (16, 26, 36):
        d.ellipse([S(20), S(y), S(31), S(y + 9)], fill=W); d.ellipse([S(33), S(y), S(44), S(y + 9)], fill=W)
    d.ellipse([S(27), S(6), S(37), S(18)], fill=W)


@icon('skull')
def _(d):
    circle(d, 32, 28, 20); rrect(d, 22, 40, 42, 54, 4)
    circle(d, 24, 28, 6, CLEAR); circle(d, 40, 28, 6, CLEAR); poly(d, [(32, 34), (36, 42), (28, 42)], CLEAR)


@icon('volcano')
def _(d):
    poly(d, [(4, 58), (24, 22), (40, 22), (60, 58)]); poly(d, [(24, 22), (40, 22), (36, 30), (28, 30)], CLEAR)
    circle(d, 26, 12, 5); circle(d, 36, 8, 6)


@icon('arrow_edge')
def _(d):
    poly(d, [(58, 32), (18, 8), (26, 32), (18, 56)])


@icon('info')
def _(d):
    circle(d, 32, 32, 28); circle(d, 32, 32, 22, CLEAR); circle(d, 32, 20, 4); rrect(d, 29, 28, 35, 46, 2)


# --- power icons (Stage 2.1) ----------------------------------------------------------------


@icon('bolt')
def _(d):
    poly(d, [(36, 2), (12, 36), (28, 36), (22, 62), (52, 24), (35, 24), (44, 2)])


@icon('rain')
def _(d):
    circle(d, 22, 26, 12); circle(d, 36, 20, 15); circle(d, 48, 28, 10); rrect(d, 12, 26, 56, 38, 6)
    for x in (18, 32, 46):
        line(d, [(x + 2, 44), (x - 3, 58)], 4)


@icon('drought')
def _(d):
    circle(d, 32, 26, 12)
    for k in range(8):
        a = k * math.pi / 4
        line(d, [(32 + math.cos(a) * 17, 26 + math.sin(a) * 17), (32 + math.cos(a) * 24, 26 + math.sin(a) * 24)], 4)
    line(d, [(6, 56), (22, 52), (30, 58), (42, 51), (58, 56)], 4)


@icon('heat')
def _(d):
    rrect(d, 26, 4, 38, 42, 6); circle(d, 32, 48, 12)
    for y in (12, 20, 28):
        line(d, [(42, y), (50, y)], 3)
    poly(d, [(8, 22), (14, 10), (18, 22)]); poly(d, [(46, 50), (54, 38), (58, 50)])


@icon('chill')
def _(d):
    for k in range(3):
        a = k * math.pi / 3
        line(d, [(32 + math.cos(a) * 26, 32 + math.sin(a) * 26), (32 - math.cos(a) * 26, 32 - math.sin(a) * 26)], 5)
    for k in range(6):
        a = k * math.pi / 3
        cx, cy = 32 + math.cos(a) * 17, 32 + math.sin(a) * 17
        line(d, [(cx, cy), (cx + math.cos(a + 0.8) * 7, cy + math.sin(a + 0.8) * 7)], 3)
        line(d, [(cx, cy), (cx + math.cos(a - 0.8) * 7, cy + math.sin(a - 0.8) * 7)], 3)


@icon('paw')
def _(d):
    circle(d, 32, 42, 13)
    circle(d, 15, 26, 7); circle(d, 25, 14, 7); circle(d, 39, 14, 7); circle(d, 49, 26, 7)


@icon('blight')
def _(d):
    circle(d, 32, 32, 16)
    for k in range(8):
        a = k * math.pi / 4
        line(d, [(32 + math.cos(a) * 16, 32 + math.sin(a) * 16), (32 + math.cos(a) * 26, 32 + math.sin(a) * 26)], 4)
        circle(d, 32 + math.cos(a) * 27, 32 + math.sin(a) * 27, 4)
    circle(d, 27, 28, 4, CLEAR); circle(d, 37, 36, 3, CLEAR)


@icon('oil')
def _(d):
    poly(d, [(32, 4), (48, 32), (16, 32)]); circle(d, 32, 38, 16)
    circle(d, 26, 38, 4, CLEAR)


@icon('meteor')
def _(d):
    circle(d, 40, 40, 14)
    line(d, [(6, 6), (30, 30)], 6); line(d, [(18, 4), (34, 22)], 4); line(d, [(4, 18), (22, 34)], 4)
    circle(d, 36, 36, 3, CLEAR); circle(d, 45, 44, 2, CLEAR)


@icon('settlers')
def _(d):
    poly(d, [(6, 42), (58, 42), (50, 56), (14, 56)])
    line(d, [(32, 8), (32, 42)], 3)
    poly(d, [(34, 10), (54, 36), (34, 36)])
    circle(d, 22, 30, 5)


for name, fn in ICONS.items():
    img, d = canvas(64)
    fn(d)
    save(img, 'i_' + name, 64)

# atlas file
lines = []
for f in sorted(os.listdir(OUT)):
    if f.endswith('.png'):
        lines.append('images {\n  image: "/assets/ui/%s"\n  sprite_trim_mode: SPRITE_TRIM_MODE_OFF\n}\n' % f)
lines.append('margin: 2\nextrude_borders: 2\ninner_padding: 0\n')
with open(os.path.join(ROOT, 'assets', 'ui.atlas'), 'w', newline='\n') as fh:
    fh.write(''.join(lines))
print('wrote %d images to %s' % (len(os.listdir(OUT)), OUT))
