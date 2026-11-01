#!/usr/bin/env python3
"""TC2's interface art (session 31): every icon, panel, slice and the logotype, generated - the game's look in one hand: earth, a medieval chronicle
and a miniature civilisation. Writes PNGs and their import settings into Game/Resources/UI. Rerun after any change; the contact sheets are for judging.

    python tc2/art/ui_kit.py [--out DIR] [--sheet FILE]

Icons are white on clear (the HUD tints them), drawn at 4x and reduced; 9-slices are plain images with the borders named in Hud.Skin.cs. Type: IM FELL
English and EB Garamond (OFL; CREDITS).
"""
import argparse
import hashlib
import math
import os

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "unity", "TinyConsequences2", "Assets", "TC2", "Game", "Resources"))
GOLD = (214, 176, 104)
GOLD_HI = (246, 222, 160)
GOLD_DK = (120, 92, 48)
INK = (13, 18, 28)
INK2 = (23, 30, 44)

SS = 4


def canvas(size):
    img = Image.new("L", (size * SS, size * SS), 0)
    return img, ImageDraw.Draw(img), size * SS / 100.0


def P(k, *pts):
    return [(x * k, y * k) for x, y in pts]


def poly(d, k, pts, fill=255):
    d.polygon(P(k, *pts), fill=fill)


def line(d, k, pts, w, fill=255):
    d.line(P(k, *pts), fill=fill, width=max(1, int(w * k)), joint="curve")
    for x, y in (pts[0], pts[-1]):
        r = w * k * 0.5
        d.ellipse((x * k - r, y * k - r, x * k + r, y * k + r), fill=fill)


def disc(d, k, x, y, r, fill=255):
    d.ellipse(((x - r) * k, (y - r) * k, (x + r) * k, (y + r) * k), fill=fill)


def ring(d, k, x, y, r, w, fill=255):
    d.ellipse(((x - r) * k, (y - r) * k, (x + r) * k, (y + r) * k), outline=fill, width=max(1, int(w * k)))


def arc(d, k, x, y, r, a0, a1, w, fill=255):
    d.arc(((x - r) * k, (y - r) * k, (x + r) * k, (y + r) * k), a0, a1, fill=fill, width=max(1, int(w * k)))


def cloud(d, k, cx=50, cy=44, s=1.0, fill=255):
    for ox, oy, r in ((-20, 4, 13), (-6, -6, 17), (12, -2, 14), (24, 6, 10)):
        disc(d, k, cx + ox * s, cy + oy * s, r * s, fill)
    d.rectangle(((cx - 22 * s) * k, (cy + 2 * s) * k, (cx + 26 * s) * k, (cy + 16 * s) * k), fill=fill)
    disc(d, k, cx - 24 * s, cy + 9 * s, 7 * s, fill)
    disc(d, k, cx + 28 * s, cy + 10 * s, 6 * s, fill)


def drop(d, k, x, y, s=1.0, fill=255):
    poly(d, k, [(x, y - 7 * s), (x + 3.8 * s, y + 0.5 * s), (x - 3.8 * s, y + 0.5 * s)], fill)
    disc(d, k, x, y + 1.5 * s, 4 * s, fill)


def wheat(d, k, cx, top, bottom, s=1.0, droop=0.0, fill=255):
    n = 7
    for i in range(n):
        t = i / (n - 1)
        y = top + t * (bottom - top) * 0.6
        x = cx + droop * (1 - t) * (1 - t) * 18
        dy = 5.2 * s
        for side in (-1, 1):
            ax, ay = x + side * 4.5 * s, y + 0
            poly(d, k, [(x, y + dy * 0.9), (ax, ay - dy * 0.25), (ax + side * 1.2 * s, ay + dy * 0.45), (x + side * 0.6, y + dy * 1.5)], fill)
        if i == 0:
            poly(d, k, [(x - 2.1 * s, y + 2 * s), (x, y - 5.8 * s), (x + 2.1 * s, y + 2 * s)], fill)
    pts = []
    for i in range(9):
        t = i / 8
        y = top + t * (bottom - top)
        pts.append((cx + droop * (1 - t) * (1 - t) * 18, y))
    line(d, k, pts, 2.2 * s, fill)


# ---------------------------------------------------------------------------- the powers
def i_lightning():
    img, d, k = canvas(96)
    poly(d, k, [(60, 6), (24, 54), (45, 54), (34, 94), (78, 38), (54, 38), (68, 6)])
    return img


def i_storm():
    img, d, k = canvas(96)
    # a gale's spiral: three arms winding in
    for a0, w in ((0, 6.4), (120, 6.4), (240, 6.4)):
        pts = []
        for i in range(28):
            t = i / 27
            r = 7 + t * 36
            a = math.radians(a0 + t * 300)
            pts.append((50 + r * math.cos(a), 50 + r * math.sin(a) * 0.86))
        for i in range(len(pts) - 1):
            ww = w * (0.45 + 0.55 * (1 - i / 27))
            line(d, k, pts[i:i + 2], ww)
    disc(d, k, 50, 50, 5.2, 0)
    ring(d, k, 50, 50, 5.8, 2.2)
    return img


def i_rain():
    img, d, k = canvas(96)
    cloud(d, k, 50, 34, 1.0)
    for x, y in ((30, 70), (50, 80), (70, 70), (40, 90), (60, 92)):
        drop(d, k, x, y - 3, 1.05)
    return img


def i_drought():
    img, d, k = canvas(96)
    disc(d, k, 50, 30, 13)
    for i in range(10):
        a = math.radians(i * 36 - 90)
        if math.sin(a) > 0.6:
            continue
        line(d, k, [(50 + 18 * math.cos(a), 30 + 18 * math.sin(a)), (50 + 25 * math.cos(a), 30 + 25 * math.sin(a))], 3.6)
    # the cracked ground
    poly(d, k, [(6, 62), (94, 62), (94, 94), (6, 94)])
    for pts in ([(20, 62), (28, 72), (22, 82), (30, 94)], [(50, 62), (44, 72), (54, 80), (48, 94)], [(76, 62), (70, 74), (80, 84), (74, 94)],
                [(28, 72), (44, 72)], [(54, 80), (70, 74)], [(6, 82), (22, 82)], [(80, 84), (94, 80)]):
        d.line(P(k, *pts), fill=0, width=int(2.4 * k))
    return img


def i_warmth():
    img, d, k = canvas(96)
    disc(d, k, 50, 50, 20)
    for i in range(12):
        a = math.radians(i * 30)
        if i % 2 == 0:
            poly(d, k, [(50 + 25 * math.cos(a - 0.17), 50 + 25 * math.sin(a - 0.17)), (50 + 43 * math.cos(a), 50 + 43 * math.sin(a)), (50 + 25 * math.cos(a + 0.17), 50 + 25 * math.sin(a + 0.17))])
        else:
            line(d, k, [(50 + 26 * math.cos(a), 50 + 26 * math.sin(a)), (50 + 35 * math.cos(a), 50 + 35 * math.sin(a))], 3.4)
    return img


def i_cold():
    img, d, k = canvas(96)
    for i in range(6):
        a = math.radians(i * 60 - 90)
        x1, y1 = 50 + 42 * math.cos(a), 50 + 42 * math.sin(a)
        line(d, k, [(50, 50), (x1, y1)], 4.6)
        for r, l in ((26, 12), (36, 8)):
            mx, my = 50 + r * math.cos(a), 50 + r * math.sin(a)
            for da in (-50, 50):
                b = a + math.radians(da)
                line(d, k, [(mx, my), (mx + l * math.cos(b), my + l * math.sin(b))], 3.4)
    disc(d, k, 50, 50, 6)
    return img


def i_quake():
    img, d, k = canvas(96)
    # the ground broken along a fault, one side thrown up
    poly(d, k, [(6, 44), (40, 44), (52, 56), (44, 68), (56, 82), (50, 94), (6, 94)])
    poly(d, k, [(94, 58), (60, 58), (64, 70), (56, 82), (62, 94), (94, 94)])
    # the shaking
    for x0, y0 in ((10, 30), (46, 34), (66, 42)):
        pts = [(x0 + i * 5, y0 + (-5 if i % 2 else 5) * (1 if 0 < i < 5 else 0)) for i in range(6)]
        line(d, k, pts, 3.2)
    return img


def i_eruption():
    img, d, k = canvas(96)
    poly(d, k, [(6, 94), (38, 40), (46, 46), (54, 46), (62, 40), (94, 94)])
    # the plume
    for x, y, r in ((50, 28, 9), (40, 20, 8), (60, 18, 9), (50, 10, 7), (32, 10, 5), (70, 8, 5)):
        disc(d, k, x, y, r)
    poly(d, k, [(46, 46), (45, 30), (55, 30), (54, 46)])
    # fire running down the cone, cut into it
    for pts in ([(50, 50), (46, 66), (50, 76), (44, 92)], [(54, 54), (62, 70), (60, 78), (68, 92)]):
        d.line(P(k, *pts), fill=0, width=int(3.2 * k), joint="curve")
    return img


def i_meteor():
    img, d, k = canvas(96)
    disc(d, k, 33, 67, 21)
    # the stone's pits, cut out
    for x, y, r in ((27, 62, 4.5), (40, 72, 3.2), (37, 56, 2.6)):
        disc(d, k, x, y, r, 0)
    # the trail
    for (x0, y0), (x1, y1), w in (((48, 49), (92, 6), 6.6), ((36, 45), (64, 16), 5.0), ((55, 62), (84, 34), 5.0), ((26, 44), (40, 28), 3.2), ((61, 73), (74, 60), 3.2)):
        line(d, k, [(x0, y0), (x1, y1)], w)
    return img


def i_plague():
    img, d, k = canvas(96)
    # a death's head
    disc(d, k, 50, 40, 30)
    poly(d, k, [(30, 56), (70, 56), (66, 82), (34, 82)])
    disc(d, k, 38, 42, 9.2, 0)
    disc(d, k, 62, 42, 9.2, 0)
    poly(d, k, [(50, 50), (55, 62), (45, 62)], 0)
    for x in (41, 50, 59):
        d.line(P(k, (x, 68), (x, 84)), fill=0, width=int(2.8 * k))
    return img


def i_blight():
    img, d, k = canvas(96)
    wheat(d, k, 40, 22, 94, 1.25, droop=1.6)
    # the rot: heavy spots falling from it
    for x, y, r in ((76, 36, 5.6), (88, 56, 4.2), (70, 62, 4.0), (84, 78, 4.8), (66, 88, 3.4)):
        disc(d, k, x, y, r)
    return img


def i_bounty():
    img, d, k = canvas(96)
    # a sheaf: three ears bound
    for cx, top, s in ((30, 16, 0.86), (70, 16, 0.86), (50, 6, 1.0)):
        wheat(d, k, cx, top, 74, s)
    d.rectangle((26 * k, 64 * k, 74 * k, 72 * k), fill=255)
    for x0, x1 in ((34, 26), (50, 50), (66, 74)):
        line(d, k, [(50 + (x0 - 50) * 0.4, 72), (x1, 94)], 3.2)
    return img


def i_cull():
    img, d, k = canvas(96)
    # a wolf's head under a falling blade: the hunters thinned
    poly(d, k, [(18, 26), (34, 44), (66, 44), (82, 26), (84, 62), (66, 76), (58, 96), (42, 96), (34, 76), (16, 62)])
    disc(d, k, 38, 58, 4.4, 0)
    disc(d, k, 62, 58, 4.4, 0)
    poly(d, k, [(43, 82), (57, 82), (50, 90)], 0)
    # the blade across its brow
    poly(d, k, [(10, 8), (92, 8), (92, 18), (10, 18)])
    return img


def i_bring():
    img, d, k = canvas(96)
    # a stag's head with its antlers
    poly(d, k, [(36, 46), (64, 46), (60, 72), (54, 92), (46, 92), (40, 72)])
    poly(d, k, [(36, 48), (22, 42), (26, 54), (38, 56)])
    poly(d, k, [(64, 48), (78, 42), (74, 54), (62, 56)])
    disc(d, k, 43, 58, 2.6, 0)
    disc(d, k, 57, 58, 2.6, 0)
    for side in (-1, 1):
        cx = 50
        pts = [(cx + side * 8, 46), (cx + side * 12, 30), (cx + side * 22, 18), (cx + side * 30, 6)]
        line(d, k, pts, 3.6)
        line(d, k, [(cx + side * 12, 30), (cx + side * 4, 14)], 3.0)
        line(d, k, [(cx + side * 18, 23), (cx + side * 14, 6)], 3.0)
        line(d, k, [(cx + side * 22, 18), (cx + side * 36, 22)], 3.0)
    return img


def i_ore():
    img, d, k = canvas(96)
    # a vein's crystal under a pick
    poly(d, k, [(52, 92), (20, 72), (28, 50), (60, 44), (84, 66), (78, 86)])
    for pts in ([(28, 50), (46, 66), (52, 92)], [(60, 44), (46, 66)], [(84, 66), (46, 66)]):
        d.line(P(k, *pts), fill=0, width=int(2.6 * k))
    # the pick
    line(d, k, [(22, 44), (58, 8)], 6.0)
    pts = [(38, 4), (58, 10), (72, 26)]
    d.line(P(k, *pts), fill=255, width=int(7.4 * k), joint="curve")
    poly(d, k, [(38, 1), (43, 8), (32, 7)])
    poly(d, k, [(75, 30), (68, 25), (70, 36)])
    return img


# ---------------------------------------------------------------------------- the controls
def i_pause():
    img, d, k = canvas(96)
    d.rounded_rectangle((26 * k, 18 * k, 42 * k, 82 * k), 3 * k, fill=255)
    d.rounded_rectangle((58 * k, 18 * k, 74 * k, 82 * k), 3 * k, fill=255)
    return img


def i_play():
    img, d, k = canvas(96)
    poly(d, k, [(28, 14), (84, 50), (28, 86)])
    return img


def i_hourglass():
    img, d, k = canvas(96)
    d.rounded_rectangle((20 * k, 8 * k, 80 * k, 17 * k), 3 * k, fill=255)
    d.rounded_rectangle((20 * k, 83 * k, 80 * k, 92 * k), 3 * k, fill=255)
    poly(d, k, [(27, 17), (73, 17), (73, 26), (54, 50), (73, 74), (73, 83), (27, 83), (27, 74), (46, 50), (27, 26)])
    # the glass, hollow above, with the sand's heap below and its fall
    poly(d, k, [(33, 20), (67, 20), (67, 25), (51, 47), (49, 47), (33, 25)], 0)
    poly(d, k, [(38, 28), (62, 28), (52, 42), (48, 42)], 255)
    poly(d, k, [(33, 80), (67, 80), (67, 75), (51, 53), (49, 53), (33, 75)], 0)
    poly(d, k, [(35, 80), (65, 80), (50, 63)], 255)
    return img


def i_book():
    img, d, k = canvas(96)
    # an open book, its leaves curving from the spine
    poly(d, k, [(50, 26), (40, 20), (22, 18), (6, 24), (6, 82), (22, 76), (40, 78), (50, 86)])
    poly(d, k, [(50, 26), (60, 20), (78, 18), (94, 24), (94, 82), (78, 76), (60, 78), (50, 86)])
    d.line(P(k, (50, 24), (50, 86)), fill=0, width=int(3 * k))
    for y in (32, 42, 52, 62):
        d.line(P(k, (14, y - 2), (26, y - 4), (41, y - 1)), fill=0, width=int(2.2 * k))
        d.line(P(k, (86, y - 2), (74, y - 4), (59, y - 1)), fill=0, width=int(2.2 * k))
    return img


def i_banner():
    img, d, k = canvas(96)
    line(d, k, [(26, 6), (26, 94)], 5)
    disc(d, k, 26, 6, 5)
    # a swallow-tailed banner
    poly(d, k, [(29, 14), (72, 14), (88, 14), (74, 32), (88, 50), (29, 50)])
    d.line(P(k, (42, 22), (42, 42)), fill=0, width=int(2.6 * k))
    d.line(P(k, (35, 32), (49, 32)), fill=0, width=int(2.6 * k))
    return img


def i_globe():
    img, d, k = canvas(96)
    ring(d, k, 50, 50, 42, 5)
    d.ellipse((30 * k, 8 * k, 70 * k, 92 * k), outline=255, width=int(3.2 * k))
    d.line(P(k, (50, 8), (50, 92)), fill=255, width=int(3.2 * k))
    d.line(P(k, (8, 50), (92, 50)), fill=255, width=int(3.2 * k))
    arc(d, k, 50, 2, 44, 28, 152, 3.2)
    arc(d, k, 50, 98, 44, 208, 332, 3.2)
    return img


def i_scroll():
    img, d, k = canvas(96)
    # a scroll with lines of writing
    d.rounded_rectangle((20 * k, 14 * k, 80 * k, 86 * k), 4 * k, fill=255)
    disc(d, k, 20, 20, 9)
    disc(d, k, 80, 80, 9)
    for y, x1 in ((30, 68), (42, 72), (54, 62), (66, 70)):
        d.line(P(k, (30, y), (x1, y)), fill=0, width=int(3.6 * k))
    return img


def i_sound():
    img, d, k = canvas(96)
    poly(d, k, [(10, 38), (28, 38), (50, 18), (50, 82), (28, 62), (10, 62)])
    arc(d, k, 50, 50, 18, -48, 48, 5.2)
    arc(d, k, 50, 50, 32, -48, 48, 5.2)
    return img


def i_soundoff():
    img, d, k = canvas(96)
    poly(d, k, [(10, 38), (28, 38), (50, 18), (50, 82), (28, 62), (10, 62)])
    line(d, k, [(62, 36), (88, 64)], 5.4)
    line(d, k, [(88, 36), (62, 64)], 5.4)
    return img


def i_menu():
    img, d, k = canvas(96)
    for y in (24, 50, 76):
        d.rounded_rectangle((16 * k, (y - 5) * k, 84 * k, (y + 5) * k), 3 * k, fill=255)
    return img


def i_close():
    img, d, k = canvas(96)
    line(d, k, [(24, 24), (76, 76)], 9)
    line(d, k, [(76, 24), (24, 76)], 9)
    return img


def i_back():
    img, d, k = canvas(96)
    line(d, k, [(62, 18), (32, 50), (62, 82)], 10)
    return img


def i_next():
    img, d, k = canvas(96)
    line(d, k, [(38, 18), (68, 50), (38, 82)], 10)
    return img


def i_eye():
    img, d, k = canvas(96)
    d.ellipse((6 * k, 22 * k, 94 * k, 78 * k), fill=255)
    d.ellipse((12 * k, 28 * k, 88 * k, 72 * k), fill=0)
    disc(d, k, 50, 50, 18)
    disc(d, k, 50, 50, 7, 0)
    return img


def i_person():
    img, d, k = canvas(96)
    disc(d, k, 50, 30, 19)
    d.pieslice((14 * k, 54 * k, 86 * k, 126 * k), 180, 360, fill=255)
    return img


def i_house():
    img, d, k = canvas(96)
    poly(d, k, [(50, 10), (94, 50), (82, 50), (82, 88), (18, 88), (18, 50), (6, 50)])
    d.rectangle((42 * k, 60 * k, 58 * k, 88 * k), fill=0)
    return img


def i_gear():
    img, d, k = canvas(96)
    for i in range(8):
        a = math.radians(i * 45)
        poly(d, k, [(50 + 28 * math.cos(a - 0.34), 50 + 28 * math.sin(a - 0.34)), (50 + 44 * math.cos(a - 0.18), 50 + 44 * math.sin(a - 0.18)),
                    (50 + 44 * math.cos(a + 0.18), 50 + 44 * math.sin(a + 0.18)), (50 + 28 * math.cos(a + 0.34), 50 + 28 * math.sin(a + 0.34))])
    disc(d, k, 50, 50, 32)
    disc(d, k, 50, 50, 14, 0)
    return img


def i_sun():
    img, d, k = canvas(96)
    disc(d, k, 50, 50, 20)
    for i in range(8):
        a = math.radians(i * 45)
        line(d, k, [(50 + 28 * math.cos(a), 50 + 28 * math.sin(a)), (50 + 40 * math.cos(a), 50 + 40 * math.sin(a))], 5)
    return img


def i_moon():
    img, d, k = canvas(96)
    disc(d, k, 50, 50, 34)
    disc(d, k, 64, 42, 30, 0)
    return img


def i_why():
    img, d, k = canvas(96)
    # a chain's links: cause to cause
    for x, y in ((28, 30), (50, 50), (72, 70)):
        ring(d, k, x, y, 15, 6.4)
    return img


def i_ring():
    # a soft halo of gold, the guide's mark over a thing (a disc's rim, 9-sliced to any box in Hud.Guide.cs)
    img, d, k = canvas(96)
    ring(d, k, 50, 50, 44, 5)
    return img


ICONS = dict(
    lightning=i_lightning, storm=i_storm, rain=i_rain, drought=i_drought, warmth=i_warmth, cold=i_cold, quake=i_quake, eruption=i_eruption, meteor=i_meteor,
    plague=i_plague, blight=i_blight, bounty=i_bounty, cull=i_cull, bring=i_bring, ore=i_ore,
    pause=i_pause, play=i_play, hourglass=i_hourglass, book=i_book, banner=i_banner, globe=i_globe, scroll=i_scroll, sound=i_sound, soundoff=i_soundoff,
    menu=i_menu, close=i_close, back=i_back, next=i_next, eye=i_eye, person=i_person, house=i_house, gear=i_gear, sun=i_sun, moon=i_moon, why=i_why,
)


def icon_png(fn, size=64):
    mask = fn().resize((size, size), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    out.putalpha(mask)
    return out


# ---------------------------------------------------------------------------- slices
def rounded(size, r, fill, edge=None, edge_w=1, inner=None, inner_at=3):
    """A rounded box at 4x, reduced: its fill, its edge, a second fine rule inside."""
    s = size * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, s - 1, s - 1), r * SS, fill=fill, outline=edge, width=edge_w * SS if edge else 1)
    if inner:
        a = inner_at * SS
        d.rounded_rectangle((a, a, s - 1 - a, s - 1 - a), max(1, r - inner_at) * SS, outline=inner, width=max(1, SS))
    return img.resize((size, size), Image.LANCZOS)


def grain(img, amount=4, seed=7):
    """A little noise in the fill, so a panel is not a flat colour on a large screen."""
    a = np.asarray(img).astype(np.float32)
    rng = np.random.default_rng(seed)
    n = rng.normal(0, amount, a.shape[:2])
    for c in range(3):
        a[..., c] = np.clip(a[..., c] + n, 0, 255)
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def panel():
    """The HUD's panel: night-blue vellum, a hairline of gold with a finer one inside, small corner marks. 9-slice border 18."""
    base = rounded(96, 7, INK + (232,), GOLD + (205,), 1, GOLD_DK + (120,), 4)
    base = grain(base, 2.6)
    s = 96 * SS
    over = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    # the corners' marks: a small lozenge of gold just inside each
    for cx, cy in ((9, 9), (87, 9), (9, 87), (87, 87)):
        k = SS
        d.polygon([(cx * k, (cy - 2.6) * k), ((cx + 2.6) * k, cy * k), (cx * k, (cy + 2.6) * k), ((cx - 2.6) * k, cy * k)], fill=GOLD + (210,))
    return Image.alpha_composite(base, over.resize((96, 96), Image.LANCZOS))


def tag():
    """A tag under the pointer: smaller and lighter. 9-slice border 7."""
    return rounded(32, 4, INK + (240,), GOLD + (170,), 1)


def button(kind):
    """A button's three states. 9-slice border 8."""
    if kind == "normal":
        img = rounded(32, 6, INK2 + (222,), GOLD_DK + (170,), 1)
    elif kind == "hover":
        img = rounded(32, 6, (44, 40, 34, 240), GOLD + (235,), 1)
    else:
        img = rounded(32, 6, (98, 72, 30, 245), GOLD_HI + (255,), 1)
    return img


def seal(kind):
    """A power's seal: a round disc of dark enamel with a gold ring (hover brighter, on lit, spent dull). 72 px."""
    s = 72 * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    fills = {"normal": (18, 24, 36, 236), "hover": (38, 40, 44, 248), "on": (96, 70, 28, 252), "spent": (16, 19, 26, 214)}
    rims = {"normal": GOLD + (225,), "hover": GOLD_HI + (255,), "on": GOLD_HI + (255,), "spent": GOLD_DK + (160,)}
    c = s / 2
    r = c - 3 * SS
    # the shadow it stands on
    shadow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse((c - r, c - r + 3 * SS, c + r, c + r + 3 * SS), fill=(0, 0, 0, 120))
    img = Image.alpha_composite(img, shadow.filter(ImageFilter.GaussianBlur(2 * SS)))
    d = ImageDraw.Draw(img)
    d.ellipse((c - r, c - r, c + r, c + r), fill=fills[kind], outline=rims[kind], width=int(1.6 * SS))
    r2 = r - 3.5 * SS
    d.ellipse((c - r2, c - r2, c + r2, c + r2), outline=rims[kind][:3] + (rims[kind][3] // 3,), width=int(0.8 * SS))
    # a lit edge at its head, as enamel takes the light
    if kind != "spent":
        arc_box = (c - r + 2 * SS, c - r + 2 * SS, c + r - 2 * SS, c + r - 2 * SS)
        d.arc(arc_box, 205, 335, fill=(255, 255, 255, 40), width=int(1.4 * SS))
    return img.resize((72, 72), Image.LANCZOS)


def disc_mask():
    """A plain soft disc (a power's coming-back fill; the guide's glow)."""
    s = 64
    y, x = np.mgrid[0:s, 0:s]
    d = np.sqrt((x - s / 2 + 0.5) ** 2 + (y - s / 2 + 0.5) ** 2) / (s / 2)
    a = np.clip((1.0 - d) * 18, 0, 1) * 255
    out = np.zeros((s, s, 4), np.uint8)
    out[..., :3] = 255
    out[..., 3] = a.astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def glow():
    """A soft glow, white on clear: behind a lit seal and a highlighted word."""
    s = 128
    y, x = np.mgrid[0:s, 0:s]
    d = np.sqrt((x - s / 2 + 0.5) ** 2 + (y - s / 2 + 0.5) ** 2) / (s / 2)
    a = np.clip(1.0 - d, 0, 1) ** 2.2 * 255
    out = np.zeros((s, s, 4), np.uint8)
    out[..., :3] = 255
    out[..., 3] = a.astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def halo():
    """The guide's ring round a thing on the screen: a gold rim with a glow outside and in. 9-slice border 30."""
    s = 96
    y, x = np.mgrid[0:s, 0:s]
    # a rounded box's distance
    qx = np.abs(x - s / 2 + 0.5) - (s / 2 - 24)
    qy = np.abs(y - s / 2 + 0.5) - (s / 2 - 24)
    dist = np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2) + np.minimum(np.maximum(qx, qy), 0) - 8
    rim = np.clip(1.6 - np.abs(dist), 0, 1)
    gl = np.exp(-np.maximum(dist, 0) / 5.5) * 0.65 * (dist > -1) + np.exp(np.minimum(dist, 0) / 4.0) * 0.28 * (dist <= -1)
    out = np.zeros((s, s, 4), np.uint8)
    out[..., 0] = GOLD_HI[0]
    out[..., 1] = GOLD_HI[1]
    out[..., 2] = GOLD_HI[2]
    out[..., 3] = (np.clip(rim + gl, 0, 1) * 255).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def vignette_left():
    """The title's shade: dark at the left, clear past half way (1 row stretched over the screen). 256x4."""
    w, h = 256, 4
    x = np.linspace(0, 1, w)
    a = (1 - np.clip(x / 0.62, 0, 1)) ** 1.5 * 0.86 + 0.0
    out = np.zeros((h, w, 4), np.uint8)
    out[..., 0], out[..., 1], out[..., 2] = 5, 7, 12
    out[..., 3] = (np.tile(a, (h, 1)) * 255).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def vignette_foot():
    """A shade rising from the screen's foot (a wash under the world's name lines). 4x128."""
    w, h = 4, 128
    y = np.linspace(0, 1, h)
    a = np.clip(y, 0, 1) ** 2.0 * 0.75
    out = np.zeros((h, w, 4), np.uint8)
    out[..., 0], out[..., 1], out[..., 2] = 5, 7, 12
    out[..., 3] = (np.tile(a[:, None], (1, w)) * 255).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def track():
    """A slider's track. 9-slice border 4: 16x8."""
    return rounded(16, 3, (6, 9, 14, 230), GOLD_DK + (200,), 1).resize((16, 16), Image.LANCZOS)


def thumb(kind="normal"):
    """A slider's thumb: a small gold lozenge."""
    s = 24 * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = s / 2
    r = 9.5 * SS
    col = GOLD if kind == "normal" else GOLD_HI
    d.polygon([(c, c - r), (c + r, c), (c, c + r), (c - r, c)], fill=col + (255,), outline=(60, 44, 18, 255))
    return img.resize((24, 24), Image.LANCZOS)


def sbar(kind):
    """A thin scrollbar on a page, and its thumb, in ink. 9-slice border 3."""
    if kind == "track":
        return rounded(12, 3, (90, 66, 40, 34))
    return rounded(12, 3, (110, 74, 42, 170))


def sbar_dark(kind):
    if kind == "track":
        return rounded(12, 3, (255, 240, 200, 18))
    return rounded(12, 3, GOLD + (150,))


def leather():
    """The book's binding: dark leather with a tooled gold rule. 9-slice border 24."""
    s = 96
    rng = np.random.default_rng(11)
    n = rng.normal(0, 1, (s, s))
    # a soft mottling (blurred noise)
    blur = np.asarray(Image.fromarray(((n * 40 + 128).clip(0, 255)).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.6))).astype(np.float32) / 255 - 0.5
    out = np.zeros((s, s, 4), np.float32)
    base = np.array([58, 30, 18], np.float32)
    for c in range(3):
        out[..., c] = base[c] * (1 + blur * 0.55)
    out[..., 3] = 255
    img = Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA")
    d = ImageDraw.Draw(img)
    d.rectangle((3, 3, s - 4, s - 4), outline=(190, 150, 84, 190), width=1)
    d.rectangle((5, 5, s - 6, s - 6), outline=(120, 86, 44, 150), width=1)
    d.rectangle((0, 0, s - 1, s - 1), outline=(22, 11, 6, 255), width=1)
    return img


def divider(w=512, h=28):
    """An ornamental rule: a gold line thinning to its ends, a small globe at its heart, lozenges beside it."""
    s = SS
    img = Image.new("RGBA", (w * s, h * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cy = h * s / 2
    for x in range(0, w * s):
        t = abs(x - w * s / 2) / (w * s / 2)
        if t < 0.055:
            continue
        a = int(235 * (1 - t) ** 0.8)
        d.line([(x, cy - 0.6 * s), (x, cy + 0.6 * s)], fill=GOLD + (a,))
    c = w * s / 2
    r = 8.5 * s
    d.ellipse((c - r, cy - r, c + r, cy + r), outline=GOLD_HI + (255,), width=int(1.3 * s))
    d.ellipse((c - r * 0.45, cy - r, c + r * 0.45, cy + r), outline=GOLD_HI + (210,), width=int(0.8 * s))
    d.line([(c - r, cy), (c + r, cy)], fill=GOLD_HI + (210,), width=int(0.8 * s))
    for sx in (-1, 1):
        lx = c + sx * 20 * s
        q = 3.2 * s
        d.polygon([(lx, cy - q), (lx + q, cy), (lx, cy + q), (lx - q, cy)], fill=GOLD + (240,))
    return img.resize((w, h), Image.LANCZOS)


# ---------------------------------------------------------------------------- the logotype
def gold_fill(h):
    """A vertical wash of old gold: bright at the head, deep at the foot, with a burnished band."""
    y = np.linspace(0, 1, h)
    stops = [(0.0, (255, 241, 196)), (0.32, (240, 208, 136)), (0.52, (205, 156, 80)), (0.56, (250, 224, 164)), (0.74, (214, 168, 92)), (1.0, (150, 104, 48))]
    rgb = np.zeros((h, 3))
    for c in range(3):
        rgb[:, c] = np.interp(y, [s[0] for s in stops], [s[1][c] for s in stops])
    return rgb


def logo():
    """The title: "Tiny Consequences" in IM FELL English, gold with a dark edge; a small globe for the o of Consequences - the world in its name; a fine rule."""
    fonts = os.path.join(RES, "Fonts")
    S = 3
    W, H = 1800 * S, 330 * S
    font = ImageFont.truetype(os.path.join(fonts, "IMFellEnglish.ttf"), 170 * S)
    small = ImageFont.truetype(os.path.join(fonts, "IMFellEnglish.ttf"), 44 * S)
    text = "Tiny Consequences"
    track_px = 5 * S
    # measure, with the globe standing for the o
    total = 0
    xs = []
    prev = None
    o_at = text.index("o")
    for i, ch in enumerate(text):
        kern = 0
        if prev is not None:
            kern = font.getlength(prev + ch) - font.getlength(prev) - font.getlength(ch)
        total += kern
        xs.append(total)
        total += font.getlength(ch) + track_px
        prev = ch
    x0 = 12 * S
    base = 186 * S
    mask = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(mask)
    for i, ch in enumerate(text):
        if i == o_at:
            continue
        d.text((x0 + xs[i], base), ch, font=font, fill=255, anchor="ls")
    # the globe: the o's place - a ring, its meridians and its parallel in gold, its sea filled below
    ol = font.getlength("o")
    cx = x0 + xs[o_at] + ol / 2
    x_height = font.getbbox("x", anchor="ls")
    r = abs(x_height[1]) * 0.56
    cy = base - abs(x_height[1]) * 0.50
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=255, width=int(7 * S))
    d.ellipse((cx - r * 0.46, cy - r, cx + r * 0.46, cy + r), outline=255, width=int(3 * S))
    d.line((cx, cy - r, cx, cy + r), fill=255, width=int(3 * S))
    d.line((cx - r, cy, cx + r, cy), fill=255, width=int(3 * S))
    for f in (0.5,):
        half = r * math.sqrt(1 - f * f)
        d.line((cx - half, cy - r * f, cx + half, cy - r * f), fill=255, width=int(2.4 * S))
        d.line((cx - half, cy + r * f, cx + half, cy + r * f), fill=255, width=int(2.4 * S))
    # the world's deep behind its lines
    deep = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(deep).ellipse((cx - r + 2 * S, cy - r + 2 * S, cx + r - 2 * S, cy + r - 2 * S), fill=(34, 76, 104, 255))
    land = Image.new("L", (W, H), 0)
    ld = ImageDraw.Draw(land)
    for ox, oy, rr in ((-0.25, -0.25, 0.36), (0.1, -0.45, 0.25), (0.35, 0.15, 0.34), (-0.1, 0.42, 0.28)):
        ld.ellipse((cx + (ox - rr) * r, cy + (oy - rr) * r, cx + (ox + rr) * r, cy + (oy + rr) * r), fill=255)
    land = land.filter(ImageFilter.GaussianBlur(3 * S))
    clip = Image.new("L", (W, H), 0)
    ImageDraw.Draw(clip).ellipse((cx - r + 2 * S, cy - r + 2 * S, cx + r - 2 * S, cy + r - 2 * S), fill=255)
    land = ImageChops.multiply(land.point(lambda v: 255 if v > 120 else 0), clip)
    deep.paste((70, 104, 62, 255), (0, 0), land)
    # the gold
    rows = np.zeros((H, W, 3))
    top, foot = int(base - 130 * S), int(base + 56 * S)
    washed = gold_fill(foot - top)
    rows[:top, :] = washed[0]
    rows[top:foot, :] = washed[:, None, :]
    rows[foot:, :] = washed[-1]
    a = np.asarray(mask).astype(np.float32) / 255
    # a chiselled edge: lighter up and to the left, darker down and to the right
    sh = np.asarray(mask.filter(ImageFilter.GaussianBlur(1.6 * S))).astype(np.float32) / 255
    emb = np.roll(np.roll(sh, S, 0), S, 1) - np.roll(np.roll(sh, -S, 0), -S, 1)
    lit = np.clip(rows * (1 - 0.55 * np.clip(emb, 0, 1))[:, :, None] + np.clip(-emb, 0, 1)[:, :, None] * 90, 0, 255)
    gold = np.dstack([lit, a * 255]).astype(np.uint8)
    goldi = Image.fromarray(gold, "RGBA")
    # the dark edge and soft shadow, so it reads over cloud and sea alike
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    edge = mask.filter(ImageFilter.MaxFilter(2 * S + 1)).filter(ImageFilter.GaussianBlur(1.2 * S))
    shadow.paste((10, 8, 8, 235), (0, 0), edge)
    soft = mask.filter(ImageFilter.GaussianBlur(9 * S))
    soft_img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    soft_img.paste((0, 0, 0, 215), (0, 0), soft)
    out = Image.alpha_composite(soft_img, shadow)
    out = Image.alpha_composite(out, deep)
    out = Image.alpha_composite(out, goldi)
    # the second part's numeral: small, raised, in rubric with a gold edge
    nx = x0 + total + 12 * S
    d2 = ImageDraw.Draw(out)
    d2.text((nx, base - 84 * S), "II", font=small, fill=(232, 184, 110, 255), anchor="ls", stroke_width=2 * S, stroke_fill=(16, 10, 8, 235))
    # cropped close, scaled to its size
    bbox = out.getbbox()
    out = out.crop((max(0, bbox[0] - 6), max(0, bbox[1] - 6), min(W, bbox[2] + 6), min(H, bbox[3] + 6)))
    w2 = 1100
    h2 = int(out.height * w2 / out.width)
    return out.resize((w2, h2), Image.LANCZOS)


# ---------------------------------------------------------------------------- import settings
META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 2
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupportCheck: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomDataBlocks: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def guid_of(name):
    return hashlib.md5(("tc2-ui-" + name).encode()).hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(RES, "UI"))
    ap.add_argument("--sheet", default=None)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    meta_dir = a.out + ".meta"
    if not os.path.exists(meta_dir):
        with open(meta_dir, "w", newline="\n") as f:
            f.write(FOLDER_META.format(guid=guid_of("folder")))
    made = {}
    for name, fn in ICONS.items():
        made["i_" + name] = icon_png(fn)
    for name, img in dict(panel=panel(), tag=tag(), btn=button("normal"), btn_hover=button("hover"), btn_on=button("on"),
                          seal=seal("normal"), seal_hover=seal("hover"), seal_on=seal("on"), seal_spent=seal("spent"), disc=disc_mask(), glow=glow(), halo=halo(),
                          shade_left=vignette_left(), shade_foot=vignette_foot(), track=track(), thumb=thumb(), thumb_hover=thumb("hover"),
                          sbar_track=sbar("track"), sbar_thumb=sbar("thumb"), sbar_track_dark=sbar_dark("track"), sbar_thumb_dark=sbar_dark("thumb"),
                          leather=leather(), divider=divider(), logo=logo()).items():
        made[name] = img
    for name, img in made.items():
        path = os.path.join(a.out, name + ".png")
        img.save(path, optimize=True)
        with open(path + ".meta", "w", newline="\n") as f:
            f.write(META.format(guid=guid_of(name)))
    print(len(made), "images in", a.out, "-", sum(os.path.getsize(os.path.join(a.out, n + ".png")) for n in made) // 1024, "KB")
    if a.sheet:
        names = [n for n in made if n.startswith("i_")]
        cols = 9
        rows = (len(names) + cols - 1) // cols
        cell = 96
        sheet = Image.new("RGBA", (cols * cell, rows * cell + 330), (30, 38, 52, 255))
        d = ImageDraw.Draw(sheet)
        for i, n in enumerate(names):
            x, y = (i % cols) * cell, (i // cols) * cell
            se = made["seal"].resize((80, 80), Image.LANCZOS)
            sheet.alpha_composite(se, (x + 8, y + 4))
            ic = made[n].resize((42, 42), Image.LANCZOS)
            tint = Image.new("RGBA", ic.size, (246, 226, 170, 0))
            tint.putalpha(ic.getchannel("A"))
            sheet.alpha_composite(tint, (x + 27, y + 23))
        sy = rows * cell + 8
        sheet.alpha_composite(made["logo"].resize((760, int(made["logo"].height * 760 / made["logo"].width)), Image.LANCZOS), (10, sy))
        sheet.alpha_composite(made["divider"], (10, sy + 170))
        sheet.alpha_composite(made["panel"], (540, sy + 170))
        sheet.alpha_composite(made["btn"].resize((64, 32)), (650, sy + 170))
        sheet.alpha_composite(made["btn_hover"].resize((64, 32)), (720, sy + 170))
        sheet.alpha_composite(made["btn_on"].resize((64, 32)), (790, sy + 170))
        sheet.alpha_composite(made["thumb"], (650, sy + 210))
        sheet.alpha_composite(made["halo"], (700, sy + 210))
        sheet.save(a.sheet)
        print("sheet", a.sheet)


if __name__ == "__main__":
    main()
