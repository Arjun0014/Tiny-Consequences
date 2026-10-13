"""
TC2 asset kit (D2-026): stylised low-poly models for the miniature world, generated with Blender (headless).

    blender -b --factory-startup --python tc2/art/kit.py -- --out unity/TinyConsequences2/Assets/TC2/Game/Resources/TC2Models --preview tc2/out/kit

Every model is built from code (no external assets): flat-shaded faces with soft bevels on the big forms, colours as
linear vertex colours per face (alpha 1 marks parts the game may tint per instance - clothes, roofs), origin at the
centre of the base, metres, exported as FBX with Y up baked in. Deterministic (fixed seeds).
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector


def srgb(r, g, b, tint=0.0):
    """sRGB colour -> linear, with the tint mask in alpha."""
    def lin(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b), tint)


class Kit:
    def __init__(self, name, seed=1):
        self.name = name
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.float_color.new("Col")
        self.rng = random.Random(seed)

    def paint(self, faces, color):
        for f in faces:
            for loop in f.loops:
                loop[self.col] = color

    def _new_faces(self, before):
        return [f for f in self.bm.faces if f.index == -1 or f not in before]

    def add(self, geom_fn, color, matrix=Matrix()):
        before = set(self.bm.faces)
        ret = geom_fn(self.bm)
        verts = ret["verts"]
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        new = [f for f in self.bm.faces if f not in before]
        self.paint(new, color)
        return new, verts

    # ---- primitives (all placed by matrices; z is up in Blender) ----
    def box(self, centre, size, color, rot_z=0.0, bevel=0.0):
        m = Matrix.Translation(Vector(centre)) @ Matrix.Rotation(rot_z, 4, "Z") @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        faces, verts = self.add(lambda bm: bmesh.ops.create_cube(bm, size=1.0), color, m)
        if bevel > 0:
            edges = list({e for f in faces for e in f.edges})
            res = bmesh.ops.bevel(self.bm, geom=edges + verts, offset=bevel, segments=1, affect="EDGES", profile=0.5)
            self.paint(res.get("faces", []), color)
        return faces

    def cyl(self, base, r1, r2, h, color, seg=8, rot=None, cap=True):
        m = Matrix.Translation(Vector(base) + Vector((0, 0, h / 2.0)))
        if rot is not None:
            m = Matrix.Translation(Vector(base)) @ rot @ Matrix.Translation(Vector((0, 0, h / 2.0)))
        return self.add(lambda bm: bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=h), color, m)[0]

    def blob(self, centre, radius, color, subdiv=1, jitter=0.18, squash=1.0, smooth=False):
        m = Matrix.Translation(Vector(centre)) @ Matrix.Diagonal((1.0, 1.0, squash, 1.0))
        faces, verts = self.add(lambda bm: bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius), color, m)
        c = Vector(centre)
        for v in verts:
            d = v.co - c
            v.co = c + d * (1.0 + self.rng.uniform(-jitter, jitter))
        if smooth:
            for f in faces:
                f.smooth = True
        return faces

    def ellipsoid(self, centre, r, color, subdiv=2, rot=None, smooth=True):
        m = Matrix.Translation(Vector(centre))
        if rot is not None:
            m = m @ rot
        m = m @ Matrix.Diagonal((r[0], r[1], r[2], 1.0))
        faces, verts = self.add(lambda bm: bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0), color, m)
        if smooth:
            for f in faces:
                f.smooth = True
        return faces

    def bowl(self, centre, r, color, cut_z, subdiv=2):
        """An ellipsoid with its top removed above cut_z (Blender z): an open hull or a bowl."""
        faces = self.ellipsoid(centre, r, color, subdiv=subdiv, smooth=False)
        gone = [f for f in faces if all(v.co.z > cut_z for v in f.verts)]
        bmesh.ops.delete(self.bm, geom=gone, context="FACES")

    def limb(self, a, b, r1, r2, color, seg=6):
        """A tapered cylinder from point a to point b."""
        a, b = Vector(a), Vector(b)
        d = b - a
        h = d.length
        rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        m = Matrix.Translation(a) @ rot @ Matrix.Translation(Vector((0, 0, h / 2.0)))
        return self.add(lambda bm: bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=h), color, m)[0]

    def quad_strip(self, pts, color):
        verts = [self.bm.verts.new(p) for p in pts]
        f = self.bm.faces.new(verts)
        self.paint([f], color)
        return f

    def finish(self, sheets=False):
        """sheets: open double-sided leaves (grass): keep both sides as built (no merging, no normal fixing)."""
        bm = self.bm
        if not sheets:
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(self.name)
        bm.to_mesh(mesh)
        bm.free()
        for p in mesh.polygons:
            p.use_smooth = False
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        if mesh.color_attributes:
            mesh.color_attributes.active_color = mesh.color_attributes[0]
        return obj


# --------------------------------------------------------------------------- palette
WALL = srgb(0.88, 0.84, 0.74)
WALL2 = srgb(0.84, 0.76, 0.62)
TIMBER = srgb(0.33, 0.22, 0.14)
THATCH = srgb(0.66, 0.53, 0.32, 1.0)
THATCH_DARK = srgb(0.52, 0.41, 0.24, 1.0)
TILE = srgb(0.66, 0.30, 0.19, 1.0)
STONE = srgb(0.55, 0.53, 0.49)
STONE_DARK = srgb(0.42, 0.40, 0.38)
DOOR = srgb(0.40, 0.25, 0.13)
GLASS = srgb(0.16, 0.19, 0.24, 0.5)   # alpha 0.5: a window (it glows at night when someone is home)
WELL_WATER = srgb(0.10, 0.16, 0.22)
CHIMNEY = srgb(0.56, 0.52, 0.49)


def gable_roof(k, length, width, eave_z, pitch_deg, color, overhang=0.35, thick=0.2, ridge_color=None):
    """Two slabs meeting at a ridge along x; eaves at eave_z."""
    half_w = width / 2.0 + overhang
    rise = half_w * math.tan(math.radians(pitch_deg))
    L = length / 2.0 + overhang
    ridge = eave_z + rise
    for side in (-1, 1):
        # slab from the eave (y = side*half_w, z = eave) to the ridge (y = 0, z = ridge), with thickness
        e0 = Vector((-L, side * half_w, eave_z)); e1 = Vector((L, side * half_w, eave_z))
        r0 = Vector((-L, 0.0, ridge)); r1 = Vector((L, 0.0, ridge))
        n = Vector((0.0, side * math.sin(math.radians(pitch_deg)), math.cos(math.radians(pitch_deg)))) * thick
        top = [e0 + n, e1 + n, r1 + n, r0 + n]
        bot = [e0, e1, r1, r0]
        vt = [k.bm.verts.new(p) for p in top]
        vb = [k.bm.verts.new(p) for p in bot]
        faces = [k.bm.faces.new(vt), k.bm.faces.new(list(reversed(vb)))]
        for i in range(4):
            j = (i + 1) % 4
            faces.append(k.bm.faces.new([vt[i], vb[i], vb[j], vt[j]]))
        k.paint(faces, color)
    if ridge_color is not None:
        k.box((0, 0, ridge + thick * 0.6), (length + 2 * overhang, 0.32, 0.22), ridge_color)
    return ridge


def gable_walls(k, length, width, wall_h, pitch_deg, color):
    """The triangular gable ends above the walls."""
    half_w = width / 2.0
    rise = half_w * math.tan(math.radians(pitch_deg))
    for side in (-1, 1):
        x = side * length / 2.0
        pts = [Vector((x, -half_w, wall_h)), Vector((x, half_w, wall_h)), Vector((x, 0.0, wall_h + rise))]
        if side < 0:
            pts = list(reversed(pts))
        k.quad_strip(pts, color)


def windows_and_door(k, length, width, wall_h, door_side=-1, n_front=2, glass=GLASS, door=DOOR):
    y = door_side * (width / 2.0 + 0.03)
    k.box((0.0, y, 0.95), (0.95, 0.08, 1.9), door)
    for i in range(n_front):
        x = (-1 if i % 2 == 0 else 1) * length * (0.27 + 0.08 * (i // 2))
        k.box((x, y, 1.45), (0.72, 0.08, 0.62), glass)
        k.box((x, y, 1.08), (0.86, 0.12, 0.08), TIMBER)  # sill
    k.box((length / 2.0 + 0.03, 0.0, 1.45), (0.08, 0.7, 0.6), glass)


def house(name, length, width, wall_h, pitch, roof, wall=WALL, frame=False, chimney=True, seed=1, plinth=True):
    k = Kit(name, seed)
    if plinth:
        k.box((0, 0, 0.12), (length + 0.25, width + 0.25, 0.24), STONE, bevel=0.03)
    k.box((0, 0, 0.24 + (wall_h - 0.24) / 2.0), (length, width, wall_h - 0.24), wall, bevel=0.04)
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.box((sx * length / 2.0, sy * width / 2.0, wall_h / 2.0 + 0.1), (0.2, 0.2, wall_h - 0.1), TIMBER)
    if frame:
        for x in (-length / 4.0, 0.0, length / 4.0):
            for sy in (-1, 1):
                k.box((x, sy * (width / 2.0 + 0.02), wall_h / 2.0 + 0.1), (0.16, 0.06, wall_h - 0.2), TIMBER)
        for sy in (-1, 1):
            k.box((0.0, sy * (width / 2.0 + 0.02), wall_h * 0.55), (length, 0.06, 0.14), TIMBER)
    gable_walls(k, length, width, wall_h, pitch, wall)
    windows_and_door(k, length, width, wall_h)
    ridge = gable_roof(k, length, width, wall_h, pitch, roof, ridge_color=THATCH_DARK if roof == THATCH else srgb(0.5, 0.22, 0.14, 1.0))
    if chimney:
        k.box((length * 0.32, width * 0.12, (ridge + 0.9) / 2.0 + 0.2), (0.6, 0.6, ridge + 0.5), CHIMNEY, bevel=0.03)
    return k.finish()


def round_hut(name, radius, wall_h, roof_h, seed=3):
    k = Kit(name, seed)
    k.cyl((0, 0, 0), radius + 0.15, radius + 0.15, 0.22, STONE, seg=12)
    k.cyl((0, 0, 0.22), radius, radius, wall_h - 0.22, WALL2, seg=12)
    k.cyl((0, 0, wall_h - 0.1), radius + 0.6, 0.08, roof_h, THATCH, seg=12)
    k.box((0, -radius - 0.02, 0.9), (0.8, 0.1, 1.75), DOOR)
    return k.finish()


def boat(name, seed=10):
    """A small wooden rowing boat (a village's fishing boat): an open 4 m hull, the floor inside it, two thwarts across the
    gunwales, the oars shipped."""
    k = Kit(name, seed)
    wood, inner, dark = srgb(0.52, 0.37, 0.22), srgb(0.38, 0.27, 0.16), srgb(0.30, 0.21, 0.13)
    k.bowl((0, 0, 0.42), (0.75, 2.0, 0.42), wood, cut_z=0.5, subdiv=3)    # the hull, open above its widest
    k.box((0, 0, 0.30), (0.95, 2.6, 0.05), inner)                          # the floor boards inside it
    for sx in (-1, 1):
        k.ellipsoid((sx * 0.72, 0, 0.5), (0.06, 1.85, 0.05), dark, subdiv=1)   # the gunwales
    for y in (-0.5, 0.45):
        k.box((0, y, 0.48), (1.4, 0.16, 0.05), dark)                        # thwarts
    for sx in (-1, 1):
        k.box((sx * 0.4, -0.1, 0.55), (0.06, 2.2, 0.05), dark, rot_z=sx * 0.05)   # oars shipped on the thwarts
    return k.finish()


def ruin(name, seed=9):
    """An abandoned house (08 §4, D2-052): stone footings and broken wall stumps of a 7 x 5 m house, roofless, the doorway
    open, rubble at the corners and a fallen beam. The game lowers and greens it as the centuries pass."""
    k = Kit(name, seed)
    rng = random.Random(seed)
    stone, dark, beam = srgb(0.60, 0.57, 0.51), srgb(0.45, 0.43, 0.39), srgb(0.36, 0.28, 0.20)
    L, W, t = 7.0, 5.0, 0.35
    # the walls in stretches of broken height; the doorway on the front (-y) side left open
    def stretch(x0, x1, y0, y1, door=None):
        n = 4
        for i in range(n):
            a0, a1 = i / n, (i + 1) / n
            mid = (a0 + a1) / 2
            if door is not None and abs(mid - door) < 0.13:
                continue
            h = 0.35 + 1.25 * rng.random() ** 1.4
            cx, cy = x0 + (x1 - x0) * mid, y0 + (y1 - y0) * mid
            sx = abs(x1 - x0) / n + (t if x0 == x1 else 0.02)
            sy = abs(y1 - y0) / n + (t if y0 == y1 else 0.02)
            k.box((cx, cy, h / 2), (max(sx, t), max(sy, t), h), stone if rng.random() < 0.7 else dark, bevel=0.03)
    stretch(-L / 2, L / 2, -W / 2, -W / 2, door=0.5)
    stretch(-L / 2, L / 2, W / 2, W / 2)
    stretch(-L / 2, -L / 2, -W / 2, W / 2)
    stretch(L / 2, L / 2, -W / 2, W / 2)
    # rubble where the walls fell, and a roof beam across the floor
    for cx, cy in ((-L / 2 + 0.6, -W / 2 + 0.6), (L / 2 - 0.7, W / 2 - 0.5), (0.8, W / 2 - 0.6)):
        k.blob((cx, cy, 0.15), 0.45 + 0.2 * rng.random(), dark, subdiv=1, jitter=0.25, squash=0.45)
    k.box((0.3, 0.2, 0.12), (4.6, 0.22, 0.2), beam, rot_z=0.35)
    return k.finish()


def tent(name, seed=8):
    """A camp's shelter (D2-045): hides over a cone of poles, the poles crossing above, a flap open at the front."""
    k = Kit(name, seed)
    hide = srgb(0.62, 0.50, 0.36)
    pole = srgb(0.42, 0.32, 0.22)
    k.cyl((0, 0, 0), 1.7, 0.12, 2.6, hide, seg=9)
    k.cyl((0, 0, 0.02), 1.72, 1.72, 0.05, srgb(0.50, 0.40, 0.29), seg=9)
    for i in range(5):
        a = i * 2.0 * math.pi / 5.0 + 0.3
        k.cyl((math.cos(a) * 0.22, math.sin(a) * 0.22, 2.35), 0.035, 0.03, 0.75, pole, seg=4, rot=Matrix.Rotation(0.4, 4, Vector((-math.sin(a), math.cos(a), 0.0))))
    k.box((0, -1.38, 0.62), (0.72, 0.08, 1.15), srgb(0.24, 0.18, 0.13))
    return k.finish()


def conifer(name, height, seed):
    k = Kit(name, seed)
    r = k.rng
    k.cyl((0, 0, 0), 0.2, 0.14, height * 0.22, srgb(0.36, 0.25, 0.16), seg=6)
    tiers = 4
    for t in range(tiers):
        f = t / (tiers - 1.0)
        rad = (1.0 - 0.72 * f) * height * 0.24
        h = height * 0.33
        z = height * 0.16 + f * height * 0.55
        g = 0.24 + 0.05 * r.random()
        col = srgb(0.14 + 0.03 * r.random(), g + 0.06 * f, 0.15 + 0.03 * r.random())
        rot = Matrix.Rotation(r.uniform(0, 6.28), 4, "Z") @ Matrix.Rotation(r.uniform(-0.05, 0.05), 4, "X")
        k.cyl((r.uniform(-0.05, 0.05), r.uniform(-0.05, 0.05), z), rad, 0.05, h, col, seg=7, rot=rot)
    return k.finish()


def broadleaf(name, height, seed, palette=((0.30, 0.47, 0.20), (0.36, 0.53, 0.23), (0.26, 0.42, 0.18))):
    k = Kit(name, seed)
    r = k.rng
    trunk = srgb(0.38, 0.28, 0.19)
    k.cyl((0, 0, 0), 0.26, 0.17, height * 0.42, trunk, seg=7)
    for b in range(2):
        a = r.uniform(0, 6.28)
        rot = Matrix.Rotation(a, 4, "Z") @ Matrix.Rotation(0.55, 4, "X")
        k.cyl((0, 0, height * 0.40), 0.11, 0.05, height * 0.20, trunk, seg=5, rot=rot)
    top = height * 0.64
    n = 7
    for i in range(n):
        a = i / n * 6.283 + r.uniform(-0.3, 0.3)
        d = height * r.uniform(0.11, 0.19)
        c = (math.cos(a) * d, math.sin(a) * d, top + r.uniform(-0.10, 0.16) * height)
        p = palette[i % len(palette)]
        k.blob(c, height * r.uniform(0.16, 0.22), srgb(p[0] + r.uniform(-0.03, 0.03), p[1] + r.uniform(-0.03, 0.03), p[2], 1.0), subdiv=2, jitter=0.10, squash=0.85, smooth=True)
    k.blob((0, 0, top + height * 0.15), height * 0.23, srgb(palette[1][0], palette[1][1], palette[1][2], 1.0), subdiv=2, jitter=0.08, squash=0.8, smooth=True)
    return k.finish()


def birch(name, height, seed):
    k = Kit(name, seed)
    r = k.rng
    k.cyl((0, 0, 0), 0.13, 0.08, height * 0.75, srgb(0.86, 0.85, 0.80), seg=6)
    for i in range(4):
        z = height * (0.15 + 0.15 * i)
        k.box((0, 0, z), (0.27, 0.27, 0.06), srgb(0.2, 0.2, 0.2), rot_z=r.uniform(0, 3))
    for i in range(4):
        z = height * (0.55 + 0.12 * i)
        k.blob((r.uniform(-0.3, 0.3), r.uniform(-0.3, 0.3), z), height * (0.16 - 0.02 * i), srgb(0.48, 0.62, 0.26, 1.0), subdiv=2, jitter=0.12, squash=1.3, smooth=True)
    return k.finish()


def bush(name, seed):
    k = Kit(name, seed)
    r = k.rng
    for i in range(4):
        a = i * 1.7
        k.blob((math.cos(a) * 0.4, math.sin(a) * 0.4, 0.45 + r.uniform(0, 0.2)), r.uniform(0.45, 0.65), srgb(0.28 + r.uniform(0, 0.05), 0.44 + r.uniform(0, 0.06), 0.19, 1.0), subdiv=2, jitter=0.12, squash=0.8, smooth=True)
    return k.finish()


def person(name, seed=9):
    k = Kit(name, seed)
    skin = srgb(0.86, 0.67, 0.53)
    hair = srgb(0.30, 0.20, 0.13)
    cloth = srgb(1.0, 1.0, 1.0, 1.0)      # tinted per person by the game (alpha 1)
    legs = srgb(0.30, 0.27, 0.24)
    feet = srgb(0.22, 0.16, 0.11)
    for sx in (-1, 1):
        k.box((sx * 0.09, 0, 0.05), (0.12, 0.2, 0.1), feet)
        k.box((sx * 0.09, 0, 0.47), (0.13, 0.15, 0.8), legs, bevel=0.02)
    k.box((0, 0, 0.88 + 0.33), (0.42, 0.24, 0.66), cloth, bevel=0.04)          # tunic
    k.box((0, 0, 0.94), (0.46, 0.27, 0.1), srgb(0.36, 0.25, 0.15))               # belt
    for sx in (-1, 1):
        rot = Matrix.Rotation(sx * 0.12, 4, "Y")
        k.cyl((sx * 0.27, 0, 0.95), 0.06, 0.055, 0.58, cloth, seg=6, rot=rot)
        k.blob((sx * 0.30, 0, 0.92), 0.06, skin, subdiv=1, jitter=0.0)
    k.cyl((0, 0, 1.52), 0.06, 0.06, 0.1, skin, seg=6)                           # neck
    k.blob((0, 0, 1.72), 0.13, skin, subdiv=2, jitter=0.0, squash=1.1)
    k.blob((0, 0.02, 1.78), 0.135, hair, subdiv=1, jitter=0.0, squash=0.75)
    return k.finish()


def quadruped(name, L, H, leg, body, belly=None, hoof=(0.12, 0.10, 0.09), width=None, head=0.22, neck=0.35, neck_up=40,
              ears=0.0, tail=0.25, bushy=False, tail_tip=None, antlers=0.0, horns=0.0, horn_back=True, tusks=False,
              snout=0.5, hump=0.0, seed=21):
    """A low-poly four-legged animal facing +Y (Unity +Z): shoulder height H, body length L, legs `leg` long."""
    k = Kit(name, seed)
    col = srgb(*body)
    bel = srgb(*(belly or body))
    hf = srgb(*hoof)
    bone = srgb(0.86, 0.80, 0.66)
    w = width or L * 0.36
    bh = (H - leg) * 1.0                     # the body's depth
    zc = leg + bh * 0.5
    # body: chest forward and higher, rump behind
    k.ellipsoid((0, L * 0.12, zc + bh * 0.04), (w * 0.5, L * 0.36, bh * 0.55), col)
    k.ellipsoid((0, -L * 0.18, zc - bh * 0.02), (w * 0.47, L * 0.32, bh * 0.5), col)
    k.ellipsoid((0, -L * 0.02, zc - bh * 0.28), (w * 0.38, L * 0.34, bh * 0.25), bel)
    if hump > 0:
        k.ellipsoid((0, L * 0.18, zc + bh * 0.42), (w * 0.35, L * 0.2, bh * hump), col)
    # legs, hooves or paws
    for sx in (-1, 1):
        for sy, yy in ((1, L * 0.3), (-1, -L * 0.3)):
            x = sx * w * 0.3
            top = (x, yy, zc - bh * 0.05)
            knee = (x, yy + sy * 0.03 * L, leg * 0.45)
            foot = (x, yy, leg * 0.06)
            k.limb(top, knee, w * 0.16, w * 0.1, col)
            k.limb(knee, foot, w * 0.1, w * 0.07, col)
            k.box((x, yy + 0.02 * L, leg * 0.03), (w * 0.17, w * 0.22, leg * 0.06), hf)
    # neck and head
    a = math.radians(neck_up)
    base = Vector((0, L * 0.38, zc + bh * 0.2))
    tip = base + Vector((0, math.cos(a) * neck, math.sin(a) * neck))
    k.limb(base, tip, w * 0.24, w * 0.17, col)
    hc = tip + Vector((0, head * 0.45, head * 0.05))
    k.ellipsoid(tuple(hc), (head * 0.42, head * 0.62, head * 0.45), col)
    k.ellipsoid(tuple(hc + Vector((0, head * 0.55, -head * 0.12))), (head * 0.28 * (0.6 + snout), head * 0.4 * (0.6 + snout), head * 0.26), col)
    k.ellipsoid(tuple(hc + Vector((0, head * (0.72 + 0.35 * snout), -head * 0.12))), (head * 0.12, head * 0.06, head * 0.1), srgb(0.08, 0.06, 0.05), subdiv=1)
    for sx in (-1, 1):
        k.ellipsoid(tuple(hc + Vector((sx * head * 0.3, head * 0.18, head * 0.18))), (head * 0.05, head * 0.05, head * 0.05), srgb(0.05, 0.04, 0.04), subdiv=1)
        if ears > 0:
            e0 = hc + Vector((sx * head * 0.28, -head * 0.15, head * 0.3))
            k.limb(tuple(e0), tuple(e0 + Vector((sx * ears * 0.35, -ears * 0.2, ears))), head * 0.12, head * 0.02, col, seg=4)
        if antlers > 0:
            b0 = hc + Vector((sx * head * 0.18, -head * 0.1, head * 0.38))
            b1 = b0 + Vector((sx * antlers * 0.35, -antlers * 0.2, antlers * 0.6))
            b2 = b1 + Vector((sx * antlers * 0.15, antlers * 0.15, antlers * 0.45))
            k.limb(tuple(b0), tuple(b1), 0.025, 0.02, bone, seg=4)
            k.limb(tuple(b1), tuple(b2), 0.02, 0.012, bone, seg=4)
            for t in (0.4, 0.75):
                p0 = b0.lerp(b1, t) if t < 0.6 else b1.lerp(b2, (t - 0.5) * 2)
                k.limb(tuple(p0), tuple(p0 + Vector((sx * antlers * 0.1, antlers * 0.25, antlers * 0.25))), 0.015, 0.008, bone, seg=4)
        if horns > 0:
            h0 = hc + Vector((sx * head * 0.22, -head * 0.05, head * 0.35))
            if horn_back:
                pts = [h0, h0 + Vector((sx * horns * 0.15, -horns * 0.45, horns * 0.55)), h0 + Vector((sx * horns * 0.25, -horns * 1.0, horns * 0.45))]
            else:
                pts = [h0, h0 + Vector((sx * horns * 0.6, horns * 0.05, horns * 0.15)), h0 + Vector((sx * horns * 0.75, horns * 0.25, horns * 0.55))]
            k.limb(tuple(pts[0]), tuple(pts[1]), horns * 0.12, horns * 0.08, bone, seg=5)
            k.limb(tuple(pts[1]), tuple(pts[2]), horns * 0.08, horns * 0.02, bone, seg=5)
        if tusks:
            t0 = hc + Vector((sx * head * 0.18, head * 0.75, -head * 0.2))
            k.limb(tuple(t0), tuple(t0 + Vector((sx * head * 0.08, head * 0.12, head * 0.22))), head * 0.04, head * 0.01, bone, seg=4)
    # tail
    t0 = Vector((0, -L * 0.48, zc + bh * 0.15))
    if bushy:
        # a brush slanting down and back from the rump; its tip (if any) at the far end along the same slant
        rot = Matrix.Rotation(0.6, 4, "X")
        axis = rot @ Vector((0, 1, 0))
        centre = t0 - axis * (tail * 0.5)
        k.ellipsoid(tuple(centre), (w * 0.14, tail * 0.55, w * 0.14), col, rot=rot)
        if tail_tip:
            k.ellipsoid(tuple(centre - axis * (tail * 0.45)), (w * 0.11, tail * 0.18, w * 0.11), srgb(*tail_tip), rot=rot)
    elif tail > 0:
        k.limb(tuple(t0), tuple(t0 + Vector((0, -tail * 0.3, -tail))), w * 0.05, w * 0.03, col, seg=4)
    return k.finish()


def seal(name, seed=40):
    """A seal hauled out, head up, facing +Y (Unity +Z): 1.6 m long, origin under its belly."""
    k = Kit(name, seed)
    back = srgb(0.40, 0.38, 0.36)
    belly = srgb(0.62, 0.60, 0.56)
    dark = srgb(0.07, 0.06, 0.06)
    # the body: a long tapering drop resting on the ground, chest raised a little
    k.ellipsoid((0, 0.18, 0.24), (0.26, 0.52, 0.24), back)
    k.ellipsoid((0, -0.38, 0.17), (0.2, 0.42, 0.17), back)
    k.ellipsoid((0, 0.0, 0.1), (0.22, 0.6, 0.1), belly)
    # the neck and head, lifted to look about
    k.limb((0, 0.52, 0.32), (0, 0.72, 0.5), 0.17, 0.13, back)
    k.ellipsoid((0, 0.8, 0.54), (0.13, 0.17, 0.12), back)
    k.ellipsoid((0, 0.95, 0.5), (0.07, 0.08, 0.06), back)
    k.ellipsoid((0, 1.02, 0.5), (0.03, 0.02, 0.025), dark, subdiv=1)
    for sx in (-1, 1):
        k.ellipsoid((sx * 0.08, 0.86, 0.6), (0.025, 0.025, 0.025), dark, subdiv=1)
        # fore flippers flat on the ground, hind flippers spread in a V behind
        k.ellipsoid((sx * 0.27, 0.32, 0.06), (0.12, 0.07, 0.03), back, rot=Matrix.Rotation(sx * 0.5, 4, "Z"))
        k.ellipsoid((sx * 0.1, -0.86, 0.07), (0.06, 0.16, 0.03), back, rot=Matrix.Rotation(sx * 0.35, 4, "Z"))
    return k.finish()


def fish(name, back=(0.30, 0.42, 0.52), seed=41):
    """A small shoaling fish facing +Y (Unity +Z), 0.3 m long, origin at its middle; its back is tintable."""
    k = Kit(name, seed)
    top = srgb(*back, tint=1.0)
    side = srgb(0.80, 0.84, 0.86)
    fin = srgb(back[0] * 0.8, back[1] * 0.8, back[2] * 0.8, tint=1.0)
    k.ellipsoid((0, 0.0, 0.012), (0.035, 0.13, 0.03), top, subdiv=2)
    k.ellipsoid((0, 0.0, -0.008), (0.037, 0.125, 0.03), side, subdiv=2)
    # the tail: two vanes in a fork
    for sz in (-1, 1):
        k.ellipsoid((0, -0.15, sz * 0.025), (0.006, 0.045, 0.022), fin, subdiv=1, rot=Matrix.Rotation(sz * -0.6, 4, "X"))
    k.ellipsoid((0, -0.01, 0.045), (0.005, 0.04, 0.018), fin, subdiv=1)   # the dorsal fin
    k.ellipsoid((0, 0.12, 0.012), (0.012, 0.01, 0.01), srgb(0.05, 0.05, 0.06), subdiv=1)   # the eyes, as one dark band
    return k.finish()


def fence(name, seed=5):
    k = Kit(name, seed)
    wood = srgb(0.48, 0.36, 0.24)
    for x in (-1.0, 1.0):
        k.box((x, 0, 0.55), (0.12, 0.12, 1.1), wood)
    for z in (0.45, 0.85):
        k.box((0, 0, z), (2.1, 0.07, 0.1), wood)
    return k.finish()


def haystack(name, seed=6):
    k = Kit(name, seed)
    k.blob((0, 0, 0.55), 1.1, srgb(0.80, 0.68, 0.38), subdiv=2, jitter=0.06, squash=0.85)
    k.cyl((0, 0, 1.6), 0.05, 0.03, 0.6, srgb(0.4, 0.3, 0.2), seg=4)
    return k.finish()


def rick(name, seed=71):
    """A corn rick (C3.7b): the sheaves built into a round stack on staddle stones (mushroom stones under a timber floor, against rats and damp),
    the butts outward in courses swelling a little to the eaves, a thatched cone pegged down with a straw finial. ~3.6 m across, 4.3 m high."""
    k = Kit(name, seed)
    sheaf = srgb(0.80, 0.67, 0.36)
    course = srgb(0.70, 0.57, 0.30)
    thatch = srgb(0.62, 0.52, 0.32)
    for i in range(6):                                                         # the staddles: a stone stem, a stone cap
        a = i * math.tau / 6.0 + 0.3
        x, y = 1.25 * math.cos(a), 1.25 * math.sin(a)
        k.cyl((x, y, 0), 0.16, 0.11, 0.42, STONE, seg=6)
        k.cyl((x, y, 0.42), 0.30, 0.26, 0.12, STONE_DARK, seg=7)
    k.cyl((0, 0, 0.54), 1.62, 1.62, 0.12, TIMBER, seg=12)                      # the floor
    z, r = 0.66, 1.62
    for i in range(5):                                                         # the sheaves in courses, swelling to the eaves
        r1 = 1.62 + 0.05 * i
        k.cyl((0, 0, z), r1, r1 + 0.05, 0.40, sheaf if i % 2 == 0 else course, seg=14, cap=False)
        z += 0.40; r = r1 + 0.05
    k.cyl((0, 0, z), r + 0.12, 0.10, 1.75, thatch, seg=14)                    # the thatched cone, its eaves over the courses
    k.cyl((0, 0, z + 1.70), 0.10, 0.02, 0.45, course, seg=5)                  # the straw finial
    for i in range(7):                                                         # the thatch's rope ties, pegged at the eaves
        a = i * math.tau / 7.0
        k.limb((0.0, 0.0, z + 1.55), ((r + 0.08) * math.cos(a), (r + 0.08) * math.sin(a), z + 0.05), 0.025, 0.025, srgb(0.42, 0.33, 0.20), seg=3)
    return k.finish()


def grave(name, seed=73):
    """A fresh grave (D2-151): a low mound of earth (tinted: bare earth) with a wooden board at its head, ~1.9 m long."""
    k = Kit(name, seed)
    k.ellipsoid((0, 0, 0.02), (0.95, 0.40, 0.26), srgb(0.80, 0.80, 0.80, 1.0), subdiv=2, smooth=False)
    k.box((0.98, 0, 0.36), (0.06, 0.30, 0.72), TIMBER, bevel=0.01)
    k.box((0.98, 0, 0.58), (0.07, 0.44, 0.07), TIMBER)
    return k.finish()


def grave_old(name, seed=74):
    """An old grave (D2-151): a low grassed mound (tinted: grass) and a small rough stone at its head."""
    k = Kit(name, seed)
    k.ellipsoid((0, 0, -0.02), (0.95, 0.42, 0.16), srgb(0.80, 0.80, 0.80, 1.0), subdiv=2, smooth=False)
    k.blob((0.95, 0.02, 0.16), 0.16, STONE, subdiv=1, jitter=0.18, squash=1.4)
    return k.finish()


def standard(name, seed=75):
    """The first household's standard (D2-152): a tall ash pole on a stone foot with a cross-bar and a long hanging cloth (tinted: its polity's
    colour), a finial on top. ~6 m."""
    k = Kit(name, seed)
    k.cyl((0, 0, 0), 0.32, 0.26, 0.22, STONE, seg=6)
    k.cyl((0, 0, 0.2), 0.06, 0.045, 5.6, TIMBER, seg=6)
    k.box((0.0, 0, 5.35), (0.08, 1.30, 0.08), TIMBER)
    k.box((0.03, 0, 4.45), (0.03, 1.20, 1.75), srgb(0.80, 0.80, 0.80, 1.0))
    for sy in (-0.42, 0.0, 0.42):                                        # the cloth's swallowtail ends
        k.box((0.03, sy, 3.40), (0.03, 0.30, 0.40), srgb(0.80, 0.80, 0.80, 1.0))
    k.blob((0, 0, 5.85), 0.12, srgb(0.70, 0.58, 0.25), subdiv=1, jitter=0.05)
    return k.finish()


def muck_heap(name, seed=76):
    """A yard's muck heap (C3.7c): a low dark mound of dung and trodden straw, wet at its foot, a fork stuck in it. ~2.2 m across."""
    k = Kit(name, seed)
    k.blob((0, 0, 0.12), 1.05, srgb(0.28, 0.21, 0.14), subdiv=2, jitter=0.12, squash=0.42)
    k.blob((0.35, -0.25, 0.38), 0.55, srgb(0.36, 0.29, 0.17), subdiv=1, jitter=0.18, squash=0.6)
    for i in range(6):                                                    # straw in it
        a = i * 1.05
        k.box((0.55 * math.cos(a), 0.55 * math.sin(a), 0.42), (0.35, 0.03, 0.02), srgb(0.70, 0.60, 0.34), rot_z=a + 0.7)
    k.limb((-0.2, 0.1, 0.35), (-0.45, 0.25, 1.35), 0.025, 0.02, TIMBER, seg=4)   # the fork's shaft
    return k.finish()


def well(name, seed=7):
    k = Kit(name, seed)
    k.cyl((0, 0, 0), 0.9, 0.9, 0.8, STONE, seg=10)
    k.cyl((0, 0, 0.75), 0.62, 0.62, 0.06, WELL_WATER, seg=10, cap=True)
    for sx in (-1, 1):
        k.box((sx * 0.75, 0, 1.4), (0.12, 0.12, 1.4), TIMBER)
    gable_roof(k, 1.0, 1.4, 2.0, 38, THATCH, overhang=0.2, thick=0.1)
    return k.finish()


def blade(k, base, angle, lean, height, width, col_base, col_tip):
    """One blade of grass: a thin bent triangle strip (two segments), double sided."""
    ca, sa = math.cos(angle), math.sin(angle)
    def p(t, side):
        x = base[0] + ca * lean * t * t + (-sa) * width * side * (1 - t)
        y = base[1] + sa * lean * t * t + ca * width * side * (1 - t)
        return Vector((x, y, base[2] + height * t))
    pts = [(p(0, -1), p(0, 1)), (p(0.55, -1), p(0.55, 1)), (p(1.0, 0), p(1.0, 0))]
    for i in range(2):
        a0, a1 = pts[i]; b0, b1 = pts[i + 1]
        col = col_base if i == 0 else col_tip
        if i == 1:
            f = k.quad_strip([a0, a1, b0], col)
            k.quad_strip([b0, a1, a0], col)
        else:
            k.quad_strip([a0, a1, b1, b0], col)
            k.quad_strip([b0, b1, a1, a0], col)


def grass_tuft(name, seed):
    k = Kit(name, seed)
    r = k.rng
    base_c = srgb(0.62, 0.70, 0.40, 1.0)   # tinted per instance to the ground's green
    tip_c = srgb(0.86, 0.92, 0.62, 1.0)
    for i in range(7):
        a = i / 7.0 * 6.283 + r.uniform(-0.4, 0.4)
        off = (math.cos(a) * 0.05, math.sin(a) * 0.05, 0.0)
        blade(k, off, a, r.uniform(0.08, 0.18), r.uniform(0.28, 0.48), 0.035, base_c, tip_c)
    return k.finish(sheets=True)


def flowers(name, seed):
    k = Kit(name, seed)
    r = k.rng
    stem = srgb(0.36, 0.50, 0.24)
    head = srgb(1.0, 1.0, 1.0, 1.0)        # petals take the instance colour
    for i in range(5):
        a = i * 1.26 + r.uniform(-0.3, 0.3)
        x, y = math.cos(a) * 0.09, math.sin(a) * 0.09
        h = r.uniform(0.22, 0.38)
        k.cyl((x, y, 0), 0.008, 0.006, h, stem, seg=3)
        k.blob((x, y, h), 0.035, head, subdiv=1, jitter=0.0, squash=0.6)
    for i in range(4):
        a = i * 1.57 + 0.4
        blade(k, (0, 0, 0), a, 0.12, 0.25, 0.03, srgb(0.40, 0.55, 0.26), srgb(0.52, 0.66, 0.32))
    return k.finish(sheets=True)


def reeds(name, seed):
    k = Kit(name, seed)
    r = k.rng
    for i in range(9):
        a = r.uniform(0, 6.283)
        d = r.uniform(0.0, 0.25)
        blade(k, (math.cos(a) * d, math.sin(a) * d, 0), a, r.uniform(0.05, 0.15), r.uniform(0.9, 1.5), 0.03, srgb(0.42, 0.50, 0.26), srgb(0.66, 0.62, 0.36))
    for i in range(2):
        a = r.uniform(0, 6.283)
        x, y = math.cos(a) * 0.1, math.sin(a) * 0.1
        k.cyl((x, y, 0), 0.01, 0.008, 1.3, srgb(0.45, 0.48, 0.28), seg=3)
        k.cyl((x, y, 1.3), 0.035, 0.035, 0.18, srgb(0.38, 0.24, 0.14), seg=5)  # bulrush head
    return k.finish(sheets=True)


def boulder(name, seed):
    k = Kit(name, seed)
    k.blob((0, 0, 0.35), 0.6, srgb(0.52, 0.50, 0.47), subdiv=1, jitter=0.22, squash=0.65)
    k.blob((0.45, 0.2, 0.18), 0.32, srgb(0.47, 0.45, 0.43), subdiv=1, jitter=0.25, squash=0.6)
    return k.finish()


# --------------------------------------------------------------------------- the civilisation kit (D2-089, C1)
CLOTH = srgb(0.80, 0.74, 0.62, 1.0)    # awnings and sacking (tinted per instance)
CHARCOAL = srgb(0.12, 0.11, 0.10)
IRON = srgb(0.24, 0.25, 0.27)
STRAW = srgb(0.80, 0.68, 0.40)
ROCK = srgb(0.58, 0.56, 0.52)
ROCK_CUT = srgb(0.70, 0.68, 0.63)


def quarry_face(name, seed=63):
    """A quarry (C1.3): a rock face cut in steps, loose blocks at its foot, a sled for hauling."""
    k = Kit(name, seed)
    for i, (h, y) in enumerate(((4.2, 2.6), (2.9, 1.3), (1.6, 0.1))):           # the face: the uncut rock behind, steps cut down toward the front
        k.box((0, y, h / 2.0), (9.0 - i * 1.6, 1.4, h), ROCK if i == 0 else ROCK_CUT, bevel=0.06)
    for sx in (-1, 1):                                                        # the hillside either side of the cut
        k.blob((sx * 5.0, 2.4, 1.2), 1.6, ROCK, subdiv=2, jitter=0.18, squash=0.75)
        k.blob((sx * 4.2, 3.4, 2.4), 1.3, ROCK, subdiv=2, jitter=0.18, squash=0.8)
    for i in range(7):                                                        # blocks cut and waiting
        k.box((k.rng.uniform(-3.5, 3.5), k.rng.uniform(-2.8, -0.4), 0.25), (k.rng.uniform(0.5, 0.9), k.rng.uniform(0.4, 0.7), 0.5), ROCK_CUT, rot_z=k.rng.uniform(0, 1.5), bevel=0.03)
    for sx in (-1, 1):                                                        # a sled
        k.box((2.4, -3.6 + sx * 0.45, 0.12), (2.2, 0.12, 0.12), TIMBER)
    for x in (1.6, 2.4, 3.2):
        k.box((x, -3.6, 0.22), (0.12, 1.0, 0.08), TIMBER)
    return k.finish()


def mine_pit(name, seed=67):
    """A copper mine (C5.1): a shaft in the rock with a timber collar and a windlass over it, a spoil heap of broken rock beside it,
    the ore sorted out on a floor of flat stones - green and blue stone - and a lean-to for the miners' tools."""
    k = Kit(name, seed)
    ORE_GREEN, ORE_BLUE = srgb(0.22, 0.52, 0.40), srgb(0.20, 0.38, 0.55)
    SPOIL = srgb(0.50, 0.40, 0.33)
    k.box((0, 0, 0.12), (3.4, 3.4, 0.24), srgb(0.40, 0.33, 0.27), bevel=0.04)       # the trodden ground round the shaft
    k.box((0, 0, 0.16), (1.7, 1.7, 0.2), srgb(0.05, 0.05, 0.05))                    # the shaft's dark mouth
    for sx in (-1, 1):                                                               # its timber collar
        k.box((sx * 0.95, 0, 0.3), (0.2, 2.1, 0.22), TIMBER)
        k.box((0, sx * 0.95, 0.3), (2.1, 0.2, 0.22), TIMBER)
    for sx in (-1, 1):                                                               # the windlass: two posts, a drum, a crank
        k.limb((sx * 1.15, -0.25, 0.2), (sx * 1.0, 0.0, 1.9), 0.09, 0.08, TIMBER)
        k.limb((sx * 1.15, 0.25, 0.2), (sx * 1.0, 0.0, 1.9), 0.09, 0.08, TIMBER)
    k.limb((-1.25, 0.0, 1.85), (1.25, 0.0, 1.85), 0.16, 0.16, srgb(0.42, 0.30, 0.19), seg=8)
    k.limb((1.25, 0.0, 1.85), (1.25, -0.45, 1.85), 0.04, 0.04, TIMBER)
    k.limb((0.0, 0.0, 1.7), (0.0, 0.0, 0.35), 0.025, 0.025, srgb(0.70, 0.62, 0.45), seg=4)   # the rope
    k.cyl((0.0, 0.0, 0.3), 0.28, 0.32, 0.4, srgb(0.45, 0.32, 0.20), seg=8)          # a bucket at the mouth
    for i in range(6):                                                               # the spoil heap
        k.blob((2.8 + k.rng.uniform(-0.7, 0.7), 1.6 + k.rng.uniform(-0.8, 0.8), 0.35), 0.9 + 0.2 * (i % 3), SPOIL if i % 2 else ROCK, subdiv=2, jitter=0.2, squash=0.55)
    k.box((-2.6, -1.4, 0.06), (1.8, 1.4, 0.12), ROCK_CUT, bevel=0.02)               # the sorting floor
    for i in range(11):                                                              # ore picked out: green and blue stone
        k.blob((-2.6 + k.rng.uniform(-0.65, 0.65), -1.4 + k.rng.uniform(-0.45, 0.45), 0.2), 0.16 + 0.05 * (i % 3), ORE_GREEN if i % 3 else ORE_BLUE, jitter=0.25, squash=0.7)
    k.box((-2.4, 1.9, 0.7), (1.6, 1.0, 1.4), TIMBER)                                 # the lean-to
    k.box((-2.4, 1.6, 1.45), (2.0, 1.6, 0.14), THATCH, rot_z=0.0, bevel=0.02)
    return k.finish()


def charcoal_clamp(name, seed=68):
    """A charcoal hearth (C5.2a): a blackened floor at the woods, a clamp - billets stacked round a flue and earthed over with turf - with
    its vent at the top, split billets stacked waiting, and a rake leaning on them."""
    k = Kit(name, seed)
    TURF, EARTH = srgb(0.36, 0.40, 0.22), srgb(0.30, 0.24, 0.18)
    k.cyl((0, 0, 0), 4.2, 4.2, 0.06, CHARCOAL, seg=14)                            # the hearth floor, black with old burns
    k.blob((0, 0, 0.2), 2.2, EARTH, subdiv=2, jitter=0.05, squash=0.62)           # the clamp, earthed over
    for i in range(7):                                                             # turf laid on it
        a = i * 0.9
        k.blob((math.cos(a) * 1.2, math.sin(a) * 1.2, 0.95), 0.75, TURF, subdiv=1, jitter=0.1, squash=0.45)
    k.cyl((0, 0, 1.35), 0.22, 0.16, 0.25, CHARCOAL, seg=6)                         # the vent at its crown
    for row in range(3):                                                           # billets stacked waiting
        for j in range(5):
            k.limb((2.6, -1.0 + j * 0.42, 0.18 + row * 0.34), (3.9, -1.0 + j * 0.42 + 0.05, 0.18 + row * 0.34), 0.16, 0.15, srgb(0.50, 0.38, 0.25), seg=6)
    k.limb((2.5, 1.2, 0.0), (3.0, 0.4, 1.6), 0.04, 0.04, TIMBER)                   # a rake
    for i in range(4):                                                             # charcoal raked out
        k.blob((-2.8 + k.rng.uniform(-0.4, 0.4), -1.6 + k.rng.uniform(-0.4, 0.4), 0.12), 0.35, CHARCOAL, jitter=0.3, squash=0.5)
    return k.finish()


def bog_pits(name, seed=69):
    """Bog-ore pits (C5.2b): shallow pits cut in wet ground with brown water standing in them, the turf stacked aside, a heap of
    rust-red ore lumps drying on a floor of boards, and a roasting fire of logs to drive the water off before the bloomery."""
    k = Kit(name, seed)
    WATER, TURF, RUST = srgb(0.20, 0.17, 0.10), srgb(0.32, 0.38, 0.20), srgb(0.55, 0.27, 0.14)
    k.cyl((0, 0, 0), 4.0, 4.0, 0.05, srgb(0.30, 0.26, 0.18), seg=12)              # the trodden wet ground
    for i, (x, y, r) in enumerate(((-1.4, 0.8, 0.9), (0.6, 1.3, 0.75), (1.2, -0.6, 0.85), (-0.6, -1.3, 0.7))):
        k.cyl((x, y, 0.02), r, r * 0.9, 0.06, WATER, seg=9)                          # pits with water standing in them
    for i in range(5):                                                              # turf cut and stacked
        k.box((2.8, -2.0 + i * 0.5, 0.2), (0.7, 0.4, 0.4), TURF, rot_z=0.1 * i, bevel=0.02)
    k.box((-2.6, -1.8, 0.06), (1.8, 1.2, 0.1), srgb(0.48, 0.36, 0.24))               # boards
    for i in range(12):                                                             # ore lumps drying
        k.blob((-2.6 + k.rng.uniform(-0.7, 0.7), -1.8 + k.rng.uniform(-0.45, 0.45), 0.24), 0.15 + 0.05 * (i % 3), RUST, jitter=0.3, squash=0.7)
    for i in range(4):                                                              # the roasting fire's logs
        a = i * 0.8
        k.limb((-2.2 + math.cos(a) * 0.5, 2.0 + math.sin(a) * 0.5, 0.12), (-2.2 - math.cos(a) * 0.5, 2.0 - math.sin(a) * 0.5, 0.12), 0.1, 0.1, srgb(0.36, 0.26, 0.17))
    k.blob((-2.2, 2.0, 0.2), 0.35, RUST, jitter=0.2, squash=0.5)
    k.limb((1.8, 2.2, 0.0), (2.2, 1.9, 1.3), 0.035, 0.035, TIMBER)                 # a spade
    return k.finish()


def stone_pile(name, seed=64):
    """Stone carried home and stacked (C1.3): a low heap of rough blocks."""
    k = Kit(name, seed)
    for i in range(9):
        a = i * 2.4
        r = 0.35 + 0.25 * (i % 3)
        k.box((math.cos(a) * r, math.sin(a) * r, 0.18 + 0.28 * (i // 4)), (0.55, 0.42, 0.34), ROCK_CUT if i % 2 else ROCK, rot_z=a, bevel=0.03)
    return k.finish()


def training_ground(name, seed=65):
    """A training ground (C1.5): straw marks on posts, a rack of practice spears, a ring of low posts round beaten earth."""
    k = Kit(name, seed)
    for i in range(14):                                                        # the ring of posts
        a = i / 14.0 * 2 * math.pi
        k.cyl((math.cos(a) * 7.5, math.sin(a) * 5.5, 0), 0.09, 0.07, 0.8, TIMBER, seg=5)
    for x in (-3.5, -1.2, 1.2, 3.5):                                           # the marks: a post, a straw body, a cross-arm
        k.cyl((x, 2.5, 0), 0.1, 0.08, 1.9, TIMBER, seg=5)
        k.ellipsoid((x, 2.5, 1.35), (0.32, 0.26, 0.5), STRAW, subdiv=1, smooth=False)
        k.box((x, 2.5, 1.55), (1.0, 0.1, 0.1), TIMBER)
    k.box((0, -3.2, 0.9), (2.4, 0.12, 0.12), TIMBER)                           # the spear rack
    for sx in (-1, 1):
        k.box((sx * 1.1, -3.2, 0.5), (0.12, 0.12, 1.0), TIMBER)
    for i in range(6):
        x = -0.9 + i * 0.36
        k.cyl((x, -3.12, 0), 0.03, 0.025, 2.2, srgb(0.55, 0.42, 0.26), seg=4, rot=Matrix.Rotation(math.radians(8), 4, "X"))
    return k.finish()


LOG_OUT = srgb(0.36, 0.26, 0.17)      # a trunk's outside, dressed and charred
LOG_IN = srgb(0.62, 0.47, 0.30)       # the pale hewn wood inside a hollowed trunk


def dugout(name, seed=11):
    """A dugout (C9.1): a single trunk hollowed into a 5 m hull - thick blunt ends left solid, the inside hewn pale, a paddle in it."""
    k = Kit(name, seed)
    k.bowl((0, 0, 0.34), (0.38, 2.5, 0.34), LOG_OUT, cut_z=0.52, subdiv=3)      # the trunk's round belly, open above
    k.box((0, 0, 0.20), (0.50, 3.9, 0.05), LOG_IN)                              # the hewn floor inside
    for sy in (-1, 1):
        k.ellipsoid((0, sy * 2.15, 0.40), (0.30, 0.42, 0.16), LOG_OUT, subdiv=2, smooth=False)   # the solid ends
    for sx in (-1, 1):
        k.ellipsoid((sx * 0.33, 0, 0.52), (0.05, 2.2, 0.04), LOG_IN, subdiv=1)  # the cut rim of the hollow
    k.box((0.12, 0.4, 0.30), (0.10, 1.5, 0.03), TIMBER, rot_z=0.06)             # a paddle lying in it
    k.box((0.12, 1.2, 0.30), (0.20, 0.36, 0.035), TIMBER, rot_z=0.06)           # its blade
    return k.finish()


def boat_shed(name, seed=12):
    """A boat shed (C9.2, a naust): a long low timber shed at the head of the beach, its seaward gable open, the roof sweeping low over
    plank walls on a footing of stones; slipway timbers running out of the mouth toward the water."""
    k = Kit(name, seed)
    L, W, H = 9.0, 4.6, 1.6                                                       # length along x: the mouth at -x faces the water
    k.box((0, 0, 0.1), (L + 0.3, W + 0.3, 0.2), STONE_DARK, bevel=0.02)          # the stone footing
    for sy in (-1, 1):
        k.box((0.0, sy * W / 2.0, H / 2.0 + 0.2), (L, 0.16, H), WALL2, bevel=0.03)   # the long walls of planks
    k.box((L / 2.0, 0, H / 2.0 + 0.2), (0.16, W, H), WALL2, bevel=0.03)         # the landward end wall
    gable_walls(k, L, W, H + 0.2, 48, WALL2)                                      # (the landward gable shows; the seaward is open)
    k.box((-L / 2.0, 0, H + 0.2), (0.18, W + 0.1, 0.18), TIMBER)                 # the tie beam over the mouth
    for x in (-L / 2.0, -L / 6.0, L / 6.0, L / 2.0):                             # the frame's posts
        for sy in (-1, 1):
            k.box((x, sy * (W / 2.0 + 0.06), H / 2.0 + 0.2), (0.2, 0.08, H), TIMBER)
    gable_roof(k, L, W, H + 0.2, 48, THATCH, overhang=0.5, thick=0.26, ridge_color=THATCH_DARK)
    for sy in (-1, 1):                                                            # slipway timbers out of the mouth
        k.box((-L / 2.0 - 1.6, sy * 0.55, 0.06), (3.6, 0.18, 0.12), TIMBER)
    for x in (-L / 2.0 - 0.6, -L / 2.0 - 1.8, -L / 2.0 - 3.0):                     # its rollers
        k.cyl((x, 0.75, 0.12), 0.08, 0.08, 1.5, TIMBER, seg=6, rot=Matrix.Rotation(math.pi / 2.0, 4, "X"))
    # lengthwise along y with the mouth at +y, as the boats lie (+y comes into the game as -z; the view turns it to the water)
    bmesh.ops.rotate(k.bm, verts=k.bm.verts, cent=(0.0, 0.0, 0.0), matrix=Matrix.Rotation(-math.pi / 2.0, 3, "Z"))
    return k.finish()


# --------------------------------------------------------------------------- architecture (D2-114)
# Homes in families, never one repeated box: five plans (a one-room cottage, a cottage, a longhouse with its byre, a two-storey jettied
# house, a hall-house with its cross wing) under three roof forms (gable, hipped, half-hipped). Walls are painted for the second tint
# channel (vertex alpha 0.25: daub, limewash, logs, stone - the game's wall colour), roofs for the first (alpha 1: thatch, shingle, tile,
# slate); timber, doors and windows keep their colours. What a house is made of and how it is roofed is read from the world (the
# builders' tradition, their means, the village's ground and knowledge) and drawn by tinting; the overlays (timber framing, a chimney,
# a stone plinth, a lean-to) are separate models composed per house.
ROOF_T = srgb(0.86, 0.86, 0.86, 1.0)      # a roof's cover, tinted per instance
RIDGE_T = srgb(0.60, 0.60, 0.60, 1.0)     # its ridge, a shade darker under the same tint
WALL_T = srgb(0.90, 0.90, 0.90, 0.25)     # walls, tinted per instance by their material
BEAM = srgb(0.30, 0.20, 0.12)             # framing, sills, bands
SHUTTER = srgb(0.36, 0.30, 0.22)

PLANS = {
    #          L     W    eave  pitch
    "small":  (4.8, 3.8, 2.2, 47),
    "cottage": (7.0, 5.0, 2.5, 45),
    "long":   (12.0, 5.4, 2.3, 47),
    "two":    (6.6, 5.4, 5.1, 50),
    "hall":   (10.0, 6.2, 3.4, 50),
    "row":    (4.6, 6.2, 5.6, 55),    # (C13.3, D2-118) the narrow town house: 4.6 m of frontage (x), 6.2 deep (y), gable to the street
}


def slab(k, pts, thick, color):
    """A roof plane as a slab lying on the plane through pts (its underside: the walls' top meets it) and `thick` above it."""
    top = [k.bm.verts.new(Vector(p) + Vector((0, 0, thick))) for p in pts]
    bot = [k.bm.verts.new(Vector(p)) for p in pts]
    faces = [k.bm.faces.new(top), k.bm.faces.new(list(reversed(bot)))]
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        faces.append(k.bm.faces.new([top[i], bot[i], bot[j], top[j]]))
    k.paint(faces, color)
    return faces


def roof_form(k, L, W, eave, pitch, form, overhang=0.38, thick=0.24, cx=0.0, cy=0.0, along_y=False):
    """A roof over an L x W plan (ridge along x, or along y): its planes pass through the walls' top at eave. form: g gable, h hipped,
    j half-hipped. Returns the ridge height."""
    t = math.tan(math.radians(pitch))
    Lo, Wo = L / 2.0 + overhang, W / 2.0 + overhang
    zlo = eave - overhang * t
    R = eave + (W / 2.0) * t

    def P(x, y, z):
        return (cx + y, cy + x, z) if along_y else (cx + x, cy + y, z)

    planes = []
    if form == "g":
        for s in (-1, 1):
            planes.append([P(-Lo, s * Wo, zlo), P(Lo, s * Wo, zlo), P(Lo, 0, R), P(-Lo, 0, R)])
    elif form == "h":
        d = min(Wo, Lo)
        for s in (-1, 1):
            planes.append([P(-Lo, s * Wo, zlo), P(Lo, s * Wo, zlo), P(Lo - d, 0, R), P(-(Lo - d), 0, R)])
        for e in (-1, 1):
            planes.append([P(e * Lo, -Wo, zlo), P(e * Lo, Wo, zlo), P(e * (Lo - d), 0, R)])
    else:   # half-hipped: the gable's top 40 % hipped back
        f = 0.6
        yc = (W / 2.0) * (1 - f)
        zc = eave + f * (W / 2.0) * t
        d = (1 - f) * (W / 2.0)
        for s in (-1, 1):
            planes.append([P(-Lo, s * Wo, zlo), P(Lo, s * Wo, zlo), P(Lo, s * yc, zc), P(Lo - d, 0, R), P(-(Lo - d), 0, R), P(-Lo, s * yc, zc)])
        for e in (-1, 1):
            planes.append([P(e * Lo, -yc, zc), P(e * Lo, yc, zc), P(e * (Lo - d), 0, R)])
    for pts in planes:
        slab(k, pts, thick, ROOF_T)
    # the ridge, a shade darker
    rl = 2 * Lo if form == "g" else (2 * (Lo - min(Wo, Lo)) if form == "h" else 2 * (Lo - (0.4 * W / 2.0)))
    if rl > 0.3:
        size = (0.30, rl, 0.20) if along_y else (rl, 0.30, 0.20)
        k.box((cx, cy, R + thick * 1.05), size, RIDGE_T)
    return R


def end_walls(k, L, W, eave, pitch, form, cx=0.0, cy=0.0, along_y=False, color=WALL_T):
    """The walls under a gable (a triangle) or a half-hip (a trapezoid); a hipped roof has none."""
    if form == "h":
        return
    t = math.tan(math.radians(pitch))
    for e in (-1, 1):
        x = e * L / 2.0
        if form == "g":
            pts = [(x, -W / 2.0, eave), (x, W / 2.0, eave), (x, 0.0, eave + (W / 2.0) * t)]
        else:
            f = 0.6
            yc = (W / 2.0) * (1 - f)
            zc = eave + f * (W / 2.0) * t
            pts = [(x, -W / 2.0, eave), (x, W / 2.0, eave), (x, yc, zc), (x, -yc, zc)]
        if along_y:
            pts = [(cx + p[1], cy + p[0], p[2]) for p in pts]
        else:
            pts = [(cx + p[0], cy + p[1], p[2]) for p in pts]
        if e < 0:
            pts = list(reversed(pts))
        k.quad_strip([Vector(p) for p in pts], color)


def window(k, x, y, z, w=0.7, h=0.62, side=-1, shutters=True):
    """A window in a long wall (side -1 front, +1 back), its sill and shutters."""
    yy = side * y
    k.box((x, yy, z), (w, 0.08, h), GLASS)
    k.box((x, yy + side * 0.04, z - h / 2.0 - 0.05), (w + 0.16, 0.1, 0.08), BEAM)
    if shutters:
        for sx in (-1, 1):
            k.box((x + sx * (w / 2.0 + 0.14), yy + side * 0.03, z), (0.24, 0.05, h), SHUTTER)


def door(k, x, y, w=0.95, h=1.95, side=-1):
    k.box((x, side * y, h / 2.0), (w, 0.1, h), DOOR)
    k.box((x, side * (y + 0.03), h + 0.06), (w + 0.2, 0.1, 0.12), BEAM)


def corner_posts(k, L, W, h, z0=0.0):
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.box((sx * L / 2.0, sy * W / 2.0, z0 + h / 2.0), (0.2, 0.2, h), BEAM)


def home(name, plan, form, seed=20):
    """A home of one plan under one roof form (D2-114)."""
    k = Kit(name, seed)
    L, W, H, pitch = PLANS[plan]
    y = W / 2.0 + 0.03
    if plan == "row":
        # (C13.3, D2-118) a narrow town house on a divided plot: tall and deep, its gable to the street (the front, -y), the upper storeys jettied
        # out 0.4 m over the street on their joists, the door and the shop window in the gable end, a window in each storey's front and the
        # attic's; its ridge runs back from the street
        h1 = 2.7
        k.box((0, 0, h1 / 2.0), (L, W, h1), WALL_T, bevel=0.03)
        corner_posts(k, L, W, h1)
        J = 0.4
        W2 = W + J
        cy2 = -J / 2.0
        k.box((0, cy2 - 0.02, h1 + 0.1), (L + 0.12, W2 + 0.1, 0.2), BEAM)   # the jetty's bressumer and joist ends
        k.box((0, cy2, h1 + 0.2 + (H - h1 - 0.2) / 2.0), (L + 0.06, W2, H - h1 - 0.2), WALL_T, bevel=0.03)
        for sx in (-1, 1):
            for sy in (-1, 1):
                k.box((sx * (L + 0.06) / 2.0, cy2 + sy * W2 / 2.0, (h1 + H) / 2.0 + 0.1), (0.2, 0.2, H - h1 - 0.2), BEAM)
        fy = W / 2.0 + 0.03                                   # the ground storey's front
        fy2 = W2 / 2.0 + 0.03 - cy2                           # the upper storeys' front (jettied)
        k.box((-0.95, -fy, 0.98), (0.95, 0.1, 1.95), DOOR)
        k.box((-0.95, -(fy + 0.03), 2.0), (1.15, 0.1, 0.12), BEAM)
        k.box((0.95, -fy, 1.35), (1.3, 0.08, 0.9), GLASS)    # the shop window by the door, its board below
        k.box((0.95, -(fy + 0.05), 0.8), (1.5, 0.14, 0.1), BEAM)
        for x in (-1.05, 1.05):
            window(k, x, fy2, 3.75, w=0.62, h=0.75)
        window(k, 0.0, fy2, 6.25, w=0.5, h=0.55, shutters=False)   # (the attic's, high in the gable)
        window(k, 0.0, W2 / 2.0 + 0.03 + cy2, 3.75, w=0.6, h=0.7, side=1)
        for yy in (-1.0, 1.4):
            k.box((L / 2.0 + 0.06, yy, 3.75), (0.08, 0.55, 0.62), GLASS)   # a window on its side, above its neighbour's eaves
        end_walls(k, W2, L + 0.06, H, pitch, form, cy=cy2, along_y=True)
        roof_form(k, W2, L + 0.06, H, pitch, form, cy=cy2, along_y=True)
        return k.finish()
    if plan == "two":
        # a two-storey house: the upper storey jettied out over the street front on its joists
        h1 = 2.6
        k.box((0, 0, h1 / 2.0), (L, W, h1), WALL_T, bevel=0.03)
        corner_posts(k, L, W, h1)
        L2, W2 = L + 0.2, W + 0.5
        cy2 = -0.25
        k.box((0, cy2, h1 + 0.1), (L2 + 0.1, W2 + 0.1, 0.2), BEAM)   # the jetty's bressumer and joist ends
        k.box((0, cy2, h1 + 0.2 + (H - h1 - 0.2) / 2.0), (L2, W2, H - h1 - 0.2), WALL_T, bevel=0.03)
        for sx in (-1, 1):
            for sy in (-1, 1):
                k.box((sx * L2 / 2.0, cy2 + sy * W2 / 2.0, (h1 + H) / 2.0 + 0.1), (0.2, 0.2, H - h1 - 0.2), BEAM)
        door(k, -L * 0.2, y)
        window(k, L * 0.24, y, 1.45)
        for x in (-L * 0.3, 0.0, L * 0.3):
            window(k, x, W2 / 2.0 + 0.03 - cy2, 3.9, w=0.62, h=0.7)
        window(k, 0.0, W2 / 2.0 + 0.03 + cy2, 3.9, w=0.62, h=0.7, side=1)
        k.box((L2 / 2.0 + 0.03, cy2, 3.9), (0.08, 0.6, 0.62), GLASS)
        end_walls(k, L2, W2, H, pitch, form, cy=cy2)
        roof_form(k, L2, W2, H, pitch, form, cy=cy2)
        return k.finish()
    if plan == "hall":
        # a hall-house: the open hall, tall-roofed with its porch, and a two-storey cross wing at one end (the chamber over the store)
        k.box((0, 0, H / 2.0), (L, W, H), WALL_T, bevel=0.04)
        corner_posts(k, L, W, H)
        door(k, -L * 0.28, y, w=1.1, h=2.2)
        for x in (-L * 0.02, L * 0.2):
            window(k, x, y, 1.9, w=0.8, h=1.3)
        window(k, -L * 0.1, y, 1.9, w=0.8, h=1.3, side=1)
        # the porch over the door
        k.box((-L * 0.28, -(W / 2.0 + 0.9), 1.15), (1.9, 1.8, 2.3), WALL_T, bevel=0.03)
        k.box((-L * 0.28, -(W / 2.0 + 1.82), 0.95), (0.95, 0.06, 1.9), DOOR)
        end_walls(k, 1.8, 1.9, 2.3, 50, "g", cx=-L * 0.28, cy=-(W / 2.0 + 0.9), along_y=True)
        roof_form(k, 1.8, 1.9, 2.3, 50, "g", overhang=0.2, thick=0.16, cx=-L * 0.28, cy=-(W / 2.0 + 0.9), along_y=True)
        end_walls(k, L, W, H, pitch, form)
        roof_form(k, L, W, H, pitch, form)
        # the cross wing at +x: two storeys, its ridge across the hall's
        wl, ww, wh = 8.6, 5.2, 5.4
        cx = L / 2.0 + ww / 2.0 - 0.3
        k.box((cx, 0, wh / 2.0), (ww, wl, wh), WALL_T, bevel=0.04)
        for sx in (-1, 1):
            for sy in (-1, 1):
                k.box((cx + sx * ww / 2.0, sy * wl / 2.0, wh / 2.0), (0.2, 0.2, wh), BEAM)
        k.box((cx, 0, 2.7), (ww + 0.08, wl + 0.08, 0.16), BEAM)   # the floor between its storeys
        for z in (1.5, 4.1):
            for sy in (-1, 1):
                k.box((cx + ww / 2.0 + 0.03, sy * wl * 0.25, z), (0.08, 0.7, 0.7), GLASS)
        k.box((cx, -(wl / 2.0 + 0.03), 4.1), (0.8, 0.08, 0.8), GLASS)
        end_walls(k, wl, ww, wh, 50, "g" if form != "h" else "j", cx=cx, along_y=True)
        roof_form(k, wl, ww, wh, 50, "g" if form != "h" else "j", cx=cx, along_y=True)
        return k.finish()
    # one storey: a one-room cottage, a cottage, a longhouse
    k.box((0, 0, H / 2.0), (L, W, H), WALL_T, bevel=0.04)
    corner_posts(k, L, W, H)
    if plan == "long":
        # the dwelling at -x, the byre at +x: its wide door in the end wall, a hatch over it, no windows on the byre's side
        door(k, -L * 0.12, y)
        for x in (-L * 0.36, -L * 0.28 + 2.5):
            window(k, x, y, 1.4, w=0.6, h=0.55)
        window(k, -L * 0.3, y, 1.4, w=0.6, h=0.55, side=1)
        k.box((L / 2.0 + 0.04, 0, 0.95), (0.1, 1.6, 1.9), DOOR)
        k.box((L / 2.0 + 0.06, 0, 2.0), (0.1, 1.9, 0.14), BEAM)
        for x in (L * 0.12, L * 0.32):
            k.box((x, -(W / 2.0 + 0.03), 1.75), (0.5, 0.06, 0.25), BEAM)   # the byre's slit vents
    elif plan == "small":
        door(k, -L * 0.16, y, w=0.85, h=1.8)
        window(k, L * 0.24, y, 1.35, w=0.5, h=0.45, shutters=False)
    else:
        door(k, -L * 0.05, y)
        for x in (-L * 0.32, L * 0.28):
            window(k, x, y, 1.45)
        window(k, 0.0, y, 1.45, side=1)
        k.box((L / 2.0 + 0.03, 0.0, 1.45), (0.08, 0.7, 0.6), GLASS)
    end_walls(k, L, W, H, pitch, form)
    roof_form(k, L, W, H, pitch, form)
    return k.finish()


def framing(name, plan, seed=21):
    """The timber frame of a framed house (an overlay drawn over its walls): posts, rails, braces on the long and end walls."""
    k = Kit(name, seed)
    L, W, H, pitch = PLANS[plan]
    off = 0.045

    def wall_frame(L0, W0, z0, h, cy=0.0, cx=0.0, along_y=False):
        n = max(2, int(round(L0 / 1.25)))
        for s in (-1, 1):
            for i in range(n + 1):
                x = -L0 / 2.0 + i * L0 / n
                p = (cx + s * (W0 / 2.0 + off), cy + x, z0 + h / 2.0) if along_y else (cx + x, cy + s * (W0 / 2.0 + off), z0 + h / 2.0)
                k.box(p, (0.06, 0.14, h) if along_y else (0.14, 0.06, h), BEAM)
            for z in (z0 + h * 0.5, z0 + h - 0.06):
                p = (cx + s * (W0 / 2.0 + off), cy, z) if along_y else (cx, cy + s * (W0 / 2.0 + off), z)
                k.box(p, (0.06, L0, 0.12) if along_y else (L0, 0.06, 0.12), BEAM)
        m = max(1, int(round(W0 / 1.4)))
        for e in (-1, 1):
            for i in range(1, m):
                y = -W0 / 2.0 + i * W0 / m
                p = (cx + y, cy + e * (L0 / 2.0 + off), z0 + h / 2.0) if along_y else (cx + e * (L0 / 2.0 + off), cy + y, z0 + h / 2.0)
                k.box(p, (0.14, 0.06, h) if along_y else (0.06, 0.14, h), BEAM)
            p = (cx, cy + e * (L0 / 2.0 + off), z0 + h * 0.5) if along_y else (cx + e * (L0 / 2.0 + off), cy, z0 + h * 0.5)
            k.box(p, (W0, 0.06, 0.12) if along_y else (0.06, W0, 0.12), BEAM)

    if plan == "row":
        wall_frame(W, L, 0.0, 2.7, along_y=True)
        wall_frame(W + 0.4, L + 0.06, 2.9, H - 2.9, cy=-0.2, along_y=True)
    elif plan == "two":
        wall_frame(L, W, 0.0, 2.6)
        wall_frame(L + 0.2, W + 0.5, 2.8, H - 2.8, cy=-0.25)
    elif plan == "hall":
        wall_frame(L, W, 0.0, H)
        wall_frame(8.6, 5.2, 0.0, 2.7, cx=L / 2.0 + 2.6 - 0.3, along_y=True)
        wall_frame(8.6, 5.2, 2.7, 2.7, cx=L / 2.0 + 2.6 - 0.3, along_y=True)
    else:
        wall_frame(L, W, 0.0, H)
    return k.finish()


def chimney(name, seed=22):
    """A stone chimney stack (unit height 1: the game scales it to the house), its cap and pot."""
    k = Kit(name, seed)
    k.box((0, 0, 0.5), (0.75, 0.75, 1.0), STONE, bevel=0.03)
    k.box((0, 0, 1.0), (0.9, 0.9, 0.08), STONE_DARK)
    return k.finish()


def plinth(name, seed=23):
    """A stone footing (unit 1 x 1 plan, 0.32 m: the game scales it to the house's plan)."""
    k = Kit(name, seed)
    k.box((0, 0, 0.16), (1.0, 1.0, 0.32), STONE, bevel=0.02)
    return k.finish()


def outshut(name, seed=24):
    """A lean-to along a house's back wall (a unit 1 m of it: the game scales it along the wall): a low wall and a roof sloping away."""
    k = Kit(name, seed)
    d, hi, lo = 2.4, 2.1, 1.3
    k.box((0, d / 2.0, lo / 2.0), (1.0, d, lo), WALL_T)
    t = (hi - lo) / d
    slab(k, [(-0.5, -0.1, hi + 0.05), (0.5, -0.1, hi + 0.05), (0.5, d + 0.35, lo - 0.35 * t), (-0.5, d + 0.35, lo - 0.35 * t)], 0.18, ROOF_T)
    for s in (-1, 1):
        k.quad_strip([Vector(p) for p in ((s * 0.5, 0.0, lo), (s * 0.5, d, lo), (s * 0.5, 0.0, hi))][:: (1 if s > 0 else -1)], WALL_T)
    return k.finish()


def garden(name, seed=27):
    """A bed of a household's kitchen garden (D2-114, C3.7a): raised beds of dug earth in rows of greens, 6 x 4.4 m; the game tiles its croft with
    them and fences the croft's outline with `hurdle` (D2-145: hurdles round each bed made a croft a stack of crates)."""
    k = Kit(name, seed)
    k.box((0, 0, 0.025), (6.0, 4.4, 0.05), srgb(0.44, 0.34, 0.24))            # the plot's trodden earth
    greens = (srgb(0.30, 0.47, 0.20), srgb(0.38, 0.52, 0.24), srgb(0.27, 0.40, 0.22), srgb(0.46, 0.55, 0.30))
    for i in range(4):
        y = -1.45 + i * 0.97
        k.box((0, y, 0.08), (5.3, 0.62, 0.08), srgb(0.33, 0.25, 0.18))       # a raised bed
        g = greens[i % len(greens)]
        for j in range(7):
            x = -2.3 + j * 0.77 + k.rng.uniform(-0.08, 0.08)
            h = k.rng.uniform(0.22, 0.36) if i != 2 else k.rng.uniform(0.45, 0.6)   # (a row of beans on short poles)
            k.box((x, y + k.rng.uniform(-0.06, 0.06), 0.12 + h / 2.0), (0.34, 0.34, h), g, rot_z=k.rng.uniform(0, 1.2))
    return k.finish()


def garden_bare(name, seed=28):
    """The same garden out of leaf (C3.7a, the bed's season): its beds and clods tinted per instance - fresh dug earth in the sowing and once
    gathered, a weedy rough where nobody dug it this year. 6 x 4.4 m, as `garden`."""
    k = Kit(name, seed)
    k.box((0, 0, 0.025), (6.0, 4.4, 0.05), srgb(0.44, 0.34, 0.24))            # the plot's trodden earth
    bed = srgb(0.90, 0.90, 0.90, 1.0)                                         # tinted per instance: the bed's earth as it is this season
    for i in range(4):
        y = -1.45 + i * 0.97
        k.box((0, y, 0.08), (5.3, 0.62, 0.08), bed)                           # a raised bed
        for r in (-0.16, 0.16):
            k.box((0, y + r, 0.13), (5.1, 0.12, 0.04), bed)                   # its ridges, turned by the spade
        for j in range(6):
            x = -2.2 + j * 0.88 + k.rng.uniform(-0.15, 0.15)
            k.box((x, y + k.rng.uniform(-0.18, 0.18), 0.15), (0.22, 0.18, 0.08), bed, rot_z=k.rng.uniform(0, 1.5))   # clods
    return k.finish()


def hurdle(name, seed=29):
    """A wattle hurdle (D2-145): a 2 m panel of hazel rods woven between upright sails, a stake at each end, 0.75 m high - the game sets them
    end to end along a croft's open outline (scaled along x to fit), a garden's fence as against a pen's rails (`fence`)."""
    k = Kit(name, seed)
    rod = (srgb(0.47, 0.36, 0.23), srgb(0.40, 0.30, 0.19))
    for i in range(3):
        k.box((0, 0.015 * (1 if i % 2 else -1), 0.2 + i * 0.21), (1.96, 0.05, 0.19), rod[i % 2])   # the weave in three courses (cheap: a croft has many)
    for x in (-0.98, 0.98):
        k.box((x, 0, 0.45), (0.08, 0.08, 0.9), srgb(0.33, 0.25, 0.17))           # the stakes
    return k.finish()


def homes():
    objs = []
    for plan in ("small", "cottage", "long", "two", "hall", "row"):
        for form in ("g", "h", "j"):
            objs.append(home("home_%s_%s" % (plan, form), plan, form))
        objs.append(framing("frame_%s" % plan, plan))
    objs.append(chimney("chimney"))
    objs.append(plinth("plinth"))
    objs.append(outshut("outshut"))
    objs.append(garden("garden"))
    objs.append(garden_bare("garden_bare"))
    objs.append(hurdle("hurdle"))
    return objs


# --------------------------------------------------------------------------- the village's works, each its own silhouette (D2-114)
BOARD = srgb(0.30, 0.22, 0.15)        # weatherboard, tarred dark (barns, granaries)
BOARD_SEAM = srgb(0.22, 0.16, 0.11)
GLOW = srgb(0.95, 0.42, 0.10, 0.5)    # coals in a forge (alpha 0.5: lit as a window is)


def barn(name, seed=61):
    """A great aisled barn (C1.1, D2-114): long and low-walled under a huge half-hipped roof that is most of it, tarred weatherboard, a tall
    cart porch (the midstrey) on the front with doors a loaded wagon goes through; no windows, no chimney."""
    k = Kit(name, seed)
    L, W, H, pitch = 15.0, 8.4, 2.5, 52
    k.box((0, 0, 0.15), (L + 0.3, W + 0.3, 0.3), STONE, bevel=0.03)
    k.box((0, 0, 0.3 + (H - 0.3) / 2.0), (L, W, H - 0.3), BOARD, bevel=0.03)
    x = -L / 2.0 + 0.5
    while x < L / 2.0:                                                        # the boards' battens
        for sy in (-1, 1):
            k.box((x, sy * (W / 2.0 + 0.02), H / 2.0 + 0.15), (0.07, 0.05, H - 0.3), BOARD_SEAM)
        x += 0.75
    end_walls(k, L, W, H, pitch, "j", color=BOARD)
    roof_form(k, L, W, H, pitch, "j", overhang=0.5, thick=0.3)
    # the midstrey: a tall gabled porch on the front long side, its ridge running into the great roof
    pw, pd, ph = 4.4, 3.0, 4.4
    py = -(W / 2.0 + pd / 2.0 - 0.2)
    k.box((0, py, ph / 2.0), (pw, pd, ph), BOARD, bevel=0.03)
    end_walls(k, pd + 1.6, pw, ph, 50, "g", cy=py - 0.8, along_y=True, color=BOARD)
    roof_form(k, pd + 1.6, pw, ph, 50, "g", overhang=0.35, thick=0.26, cy=py - 0.8, along_y=True)
    for sx in (-1, 1):                                                        # its great doors, a loaded wagon's height
        k.box((sx * 0.92, py - pd / 2.0 - 0.05, 1.9), (1.75, 0.1, 3.8), DOOR)
    k.box((0, py - pd / 2.0 - 0.09, 3.9), (4.0, 0.14, 0.2), BEAM)
    return k.finish()


def granary(name, seed=4):
    """A granary (C1.1, D2-114): a small boarded store lifted off the ground on staddle stones against rats and damp, steps to its door, a
    hipped roof - a raised box nobody would take for a home."""
    k = Kit(name, seed)
    for sx in (-1.3, 0.0, 1.3):
        for sy in (-1.0, 1.0):
            k.cyl((sx, sy, 0), 0.16, 0.10, 0.62, STONE, seg=6)              # the staddle's stem
            k.cyl((sx, sy, 0.62), 0.36, 0.30, 0.14, STONE, seg=8)           # and its cap
    k.box((0, 0, 0.82), (3.5, 2.7, 0.12), BEAM)
    k.box((0, 0, 0.88 + 1.05), (3.2, 2.4, 2.1), BOARD, bevel=0.03)
    for xx in (-1.2, -0.4, 0.4, 1.2):
        for sy in (-1, 1):
            k.box((xx, sy * 1.22, 1.93), (0.06, 0.04, 2.0), BOARD_SEAM)
    k.box((0.6, -1.23, 1.75), (0.75, 0.08, 1.4), DOOR)
    for i in range(3):                                                        # the steps up to it
        k.box((0.6, -1.65 - i * 0.3, 0.12 + (2 - i) * 0.22), (0.9, 0.32, 0.24 + (2 - i) * 0.44), STONE)
    roof_form(k, 3.2, 2.4, 2.98, 48, "h", overhang=0.4, thick=0.22)
    return k.finish()


def smithy(name, seed=62):
    """A smithy (C1.4, D2-114): an open-fronted forge shed under a low roof, dominated by its great stone chimney over the hearth; the anvil on its
    stump in the open front, the quench trough, a grindstone, iron bars racked, charcoal heaped by the wall."""
    k = Kit(name, seed)
    L, W, H, pitch = 6.4, 4.8, 2.4, 32
    k.box((0, 0, 0.06), (L + 1.4, W + 1.4, 0.12), STONE_DARK)                 # a floor of trodden stones
    k.box((0, W / 2.0 - 0.12, H / 2.0), (L, 0.24, H), WALL_T, bevel=0.03)    # the back wall
    k.box((L / 2.0 - 0.12, 0.2, H / 2.0), (0.24, W - 0.4, H), WALL_T, bevel=0.03)   # one side wall; the other end is the chimney's
    for sx in (-1, 1):
        k.box((sx * (L / 2.0 - 0.1), -W / 2.0 + 0.1, H / 2.0), (0.22, 0.22, H), BEAM)   # the open front's posts
    k.box((0, -W / 2.0 + 0.1, H - 0.1), (L, 0.24, 0.2), BEAM)
    end_walls(k, L, W, H, pitch, "g")
    roof_form(k, L, W, H, pitch, "g", overhang=0.45, thick=0.2)
    # the chimney: a stone stack at the hearth's end, standing well above the ridge
    cx = -L / 2.0 + 0.2
    k.box((cx, 0.6, 3.2), (1.5, 1.6, 6.4), STONE, bevel=0.05)
    k.box((cx, 0.6, 6.45), (1.7, 1.8, 0.14), STONE_DARK)
    k.box((cx + 1.1, 0.6, 0.5), (1.0, 1.4, 1.0), STONE, bevel=0.04)           # the hearth in the shed
    k.box((cx + 1.1, 0.6, 1.02), (0.8, 1.1, 0.06), GLOW)                      # its coals
    k.cyl((cx + 1.1, 1.75, 0.5), 0.38, 0.22, 0.6, srgb(0.45, 0.30, 0.18), seg=6, rot=Matrix.Rotation(math.pi / 2.0, 4, "X"))   # the bellows
    k.cyl((0.6, -W / 2.0 - 0.4, 0.0), 0.34, 0.32, 0.62, BEAM, seg=8)         # the anvil's stump, out in the open front
    k.box((0.6, -W / 2.0 - 0.4, 0.74), (0.66, 0.26, 0.26), IRON, bevel=0.02)  # the anvil
    k.box((1.8, -W / 2.0 - 0.2, 0.3), (1.1, 0.5, 0.6), BEAM)                  # the quench trough
    k.box((1.8, -W / 2.0 - 0.2, 0.58), (0.94, 0.36, 0.04), WELL_WATER)
    k.cyl((L / 2.0 + 0.6, -0.8, 0.55), 0.55, 0.55, 0.16, ROCK_CUT, seg=10, rot=Matrix.Rotation(math.pi / 2.0, 4, "Y"))   # a grindstone
    for i in range(4):                                                        # iron bars racked against the side wall
        k.box((L / 2.0 + 0.2, 0.6 + i * 0.18, 0.9), (0.06, 0.06, 1.8), IRON, rot_z=0.0)
    for i in range(5):                                                        # charcoal heaped by the back wall
        k.blob((-0.6 + i * 0.5, W / 2.0 + 0.6, 0.25), 0.45, CHARCOAL, jitter=0.3, squash=0.6)
    return k.finish()


def loom(k, x, y, rot=0.0):
    """A broad treadle loom (C3.1) standing at (x, y): two side frames, the breast and back beams, the cloth on the beam, a weaver's bench."""
    c, s = math.cos(rot), math.sin(rot)

    def P(px, py):
        return (x + px * c - py * s, y + px * s + py * c)
    for sx in (-0.85, 0.85):                                                  # the side frames
        for sy in (-0.55, 0.55):
            k.box((*P(sx, sy), 0.75), (0.09, 0.09, 1.5), BEAM, rot_z=rot)
        k.box((*P(sx, 0.0), 1.45), (0.09, 1.2, 0.09), BEAM, rot_z=rot)
    k.box((*P(0.0, -0.55), 0.85), (1.8, 0.1, 0.1), BEAM, rot_z=rot)         # the breast beam
    k.box((*P(0.0, 0.55), 0.85), (1.8, 0.1, 0.1), BEAM, rot_z=rot)          # the back beam
    k.box((*P(0.0, 0.0), 0.84), (1.5, 1.0, 0.02), CLOTH_W, rot_z=rot)       # the warp and the cloth woven on it
    k.box((*P(0.0, -0.55), 0.95), (1.5, 0.16, 0.14), CLOTH_W, rot_z=rot)    # cloth rolled on the beam
    k.box((*P(0.0, -1.0), 0.45), (1.2, 0.3, 0.08), BEAM, rot_z=rot)         # the bench
    for sx in (-0.5, 0.5):
        k.box((*P(sx, -1.0), 0.22), (0.07, 0.25, 0.44), BEAM, rot_z=rot)


CLOTH_W = srgb(0.84, 0.80, 0.70)       # undyed woollen cloth and warp
DYE_BLUE = srgb(0.20, 0.30, 0.52)      # woad
DYE_RED = srgb(0.62, 0.20, 0.16)       # madder
DYE_YELLOW = srgb(0.78, 0.64, 0.24)    # weld


def weavers_shed(name, seed=70):
    """A weaver's shed (C3.1): a long low timber shed whose front is open bays between posts - the light the looms want - with broad treadle
    looms seen inside, a lean-to over the warping frame at one end, a dye vat on its hearth and hanks of dyed yarn on a rack outside. Walls and
    roof are tinted per instance (what its builders build with: D2-114). The front (open bays) faces -y."""
    k = Kit(name, seed)
    L, W, H, pitch = 10.0, 5.0, 2.6, 45
    k.box((0, 0, 0.08), (L + 0.4, W + 0.4, 0.16), STONE_DARK)                 # a sill of stones, the floor beaten inside
    k.box((0, W / 2.0 - 0.12, H / 2.0), (L, 0.24, H), WALL_T, bevel=0.03)    # the back wall
    for sx in (-1, 1):
        k.box((sx * (L / 2.0 - 0.12), 0.0, H / 2.0), (0.24, W, H), WALL_T, bevel=0.03)   # the end walls
    for x in (-L / 2.0 + 0.1, -L / 6.0, L / 6.0, L / 2.0 - 0.1):             # the open front: posts and a wall plate, low sill walls
        k.box((x, -W / 2.0 + 0.1, H / 2.0), (0.24, 0.24, H), BEAM)
    k.box((0, -W / 2.0 + 0.1, H - 0.1), (L, 0.26, 0.22), BEAM)
    for x0, x1 in ((-L / 2.0, -L / 6.0), (L / 6.0, L / 2.0)):
        k.box(((x0 + x1) / 2.0, -W / 2.0 + 0.1, 0.4), (x1 - x0 - 0.2, 0.22, 0.8), WALL_T)
    end_walls(k, L, W, H, pitch, "g")
    roof_form(k, L, W, H, pitch, "g", overhang=0.5, thick=0.22)
    for i, x in enumerate((-3.0, 0.0, 3.0)):                                   # the looms, seen through the bays
        loom(k, x, 0.35, 0.0)
    # the lean-to over the warping frame at the east end
    lx = L / 2.0 + 1.1
    for sy in (-1.6, 1.6):
        k.box((lx + 0.9, sy, 0.95), (0.16, 0.16, 1.9), BEAM)
    slab(k, [(L / 2.0, -2.0, 2.3), (L / 2.0 + 2.3, -2.0, 1.75), (L / 2.0 + 2.3, 2.0, 1.75), (L / 2.0, 2.0, 2.3)], 0.14, ROOF_T)
    k.box((lx, 0.0, 0.85), (0.08, 2.6, 1.7), BEAM)                           # the warping frame: its uprights' pegs and the threads
    for z in (0.4, 0.8, 1.2, 1.55):
        k.box((lx - 0.05, 0.0, z), (0.04, 2.5, 0.03), CLOTH_W)
    # the dye vat on its hearth, out front by the west end
    vx, vy = -L / 2.0 + 0.9, -W / 2.0 - 1.4
    k.box((vx, vy, 0.25), (1.3, 1.3, 0.5), STONE, bevel=0.04)
    k.cyl((vx, vy, 0.5), 0.55, 0.6, 0.55, BOARD, seg=10)
    k.cyl((vx, vy, 1.0), 0.48, 0.48, 0.04, DYE_BLUE, seg=10)
    # hanks of dyed yarn drying on a rack
    rx, ry = 1.6, -W / 2.0 - 1.3
    for sx in (-1.0, 1.0):
        k.box((rx + sx, ry, 0.8), (0.1, 0.1, 1.6), BEAM)
    k.box((rx, ry, 1.6), (2.2, 0.08, 0.08), BEAM)
    for j, col in enumerate((DYE_BLUE, DYE_RED, DYE_YELLOW, CLOTH_W, DYE_BLUE)):
        k.box((rx - 0.8 + j * 0.4, ry, 1.25), (0.16, 0.12, 0.62), col)
    return k.finish()


def tenter(name, seed=71):
    """A tenter frame (C3.1): a length of fulled cloth stretched on hooks between two rails on posts to dry square - drawn by a weaver's shed while
    its village has wares (the cloth tinted per instance: its dye)."""
    k = Kit(name, seed)
    L = 6.0
    for x in (-L / 2.0, -L / 6.0, L / 6.0, L / 2.0):
        k.box((x, 0.0, 0.75), (0.12, 0.12, 1.5), BEAM)
    for z in (0.45, 1.45):
        k.box((0.0, 0.0, z), (L + 0.2, 0.1, 0.08), BEAM)
    k.box((0.0, 0.02, 0.95), (L - 0.3, 0.03, 0.92), CLOTH)                   # the cloth, its dye the instance's tint
    return k.finish()


FIRED = srgb(0.74, 0.45, 0.28)         # fired earthenware: a warm terracotta
FIRED_DARK = srgb(0.58, 0.34, 0.22)
CLAY_RAW = srgb(0.70, 0.60, 0.43)      # raw clay, grey-brown, unfired pots drying
CLAY_WET = srgb(0.40, 0.35, 0.28)      # clay puddled and wet in the pit
KILN_DAUB = srgb(0.66, 0.47, 0.34)     # the kiln's dome: clay daubed over stone, burnt to a rosy brown
SOOT = srgb(0.17, 0.15, 0.14)


def pot(k, x, y, z, h, r, col, neck=0.42):
    """A pot standing at (x, y, z): its swelling body and a neck with a rolled rim."""
    k.ellipsoid((x, y, z + h * 0.44), (r, r, h * 0.46), col, subdiv=2)
    k.cyl((x, y, z + h * 0.80), r * neck, r * (neck + 0.12), h * 0.2, col, seg=10)


def kiln(name, seed=72):
    """A potter's kiln (C3.2): an updraught kiln - a beehive dome of clay daubed over stone on a stone footing, its stoke-hole at the front black
    with soot, a vent at its crown - and beside it an open drying shed of posts under a roof (tinted per instance: what its builders roof with,
    D2-114) with shelves of unfired pots, the potter's wheel under its eave, a heap of fuel by the kiln. The front faces -y."""
    k = Kit(name, seed)
    k.cyl((0, 0, 0), 5.2, 5.2, 0.05, srgb(0.50, 0.41, 0.31), seg=16)          # the trodden ground, clay-stained
    kx, ky = -2.1, 0.2
    k.cyl((kx, ky, 0.0), 1.75, 1.7, 0.3, STONE, seg=12)                         # the stone footing
    k.cyl((kx, ky, 0.3), 1.55, 1.45, 0.9, KILN_DAUB, seg=12)                    # the firing chamber's wall
    k.ellipsoid((kx, ky, 1.2), (1.45, 1.45, 1.15), KILN_DAUB, subdiv=2)         # the dome over the ware
    k.cyl((kx, ky, 2.25), 0.34, 0.26, 0.32, SOOT, seg=8)                        # the vent at its crown
    k.box((kx, ky - 1.5, 0.48), (0.8, 0.5, 0.8), SOOT)                          # the stoke-hole's mouth
    k.box((kx, ky - 1.62, 0.94), (1.15, 0.3, 0.16), STONE)                      # its lintel
    for i in range(3):                                                          # soot licked up the dome over it
        k.box((kx + (i - 1) * 0.25, ky - 1.45, 1.3 + 0.12 * i), (0.32, 0.12, 0.5), SOOT, rot_z=0.0)
    for row in range(3):                                                        # billets stacked for the firing
        for j in range(5):
            k.limb((kx - 1.4 + j * 0.42, ky - 2.6, 0.18 + row * 0.34), (kx - 1.4 + j * 0.42 + 0.05, ky - 3.9, 0.18 + row * 0.34), 0.16, 0.15, srgb(0.50, 0.38, 0.25), seg=6)
    # the drying shed: an open shed of posts, its roof's ridge along x
    sx0, L, W, H = 2.0, 4.0, 2.8, 2.1
    for px in (sx0 - L / 2.0 + 0.1, sx0, sx0 + L / 2.0 - 0.1):
        for py in (-W / 2.0 + 0.1, W / 2.0 - 0.1):
            k.box((px, py, H / 2.0), (0.18, 0.18, H), BEAM)
    for py in (-W / 2.0 + 0.1, W / 2.0 - 0.1):
        k.box((sx0, py, H - 0.08), (L, 0.2, 0.18), BEAM)
    roof_form(k, L, W, H, 34, "g", overhang=0.45, thick=0.2, cx=sx0)
    for z in (0.55, 1.15):                                                      # shelves along its back, pots drying on them
        k.box((sx0, W / 2.0 - 0.45, z), (L - 0.4, 0.55, 0.06), BOARD)
        for j in range(7):
            pot(k, sx0 - L / 2.0 + 0.55 + j * 0.48, W / 2.0 - 0.45, z + 0.03, 0.34, 0.15, CLAY_RAW)
    # the wheel under the eave at the front: its flywheel, the head, the potter's seat, a lump of clay on it
    wx, wy = sx0 - 0.6, -W / 2.0 + 0.5
    k.cyl((wx, wy, 0.05), 0.42, 0.42, 0.1, BEAM, seg=12)
    k.cyl((wx, wy, 0.15), 0.05, 0.05, 0.5, BEAM, seg=6)
    k.cyl((wx, wy, 0.65), 0.24, 0.24, 0.06, BEAM, seg=10)
    k.ellipsoid((wx, wy, 0.78), (0.13, 0.13, 0.1), CLAY_RAW, subdiv=1)
    k.box((wx, wy + 0.6, 0.25), (0.5, 0.35, 0.5), BEAM)
    # fired ware set out by the kiln's mouth: storage jars and pots, cooling
    for j, (x, y, h, r) in enumerate(((-0.5, -1.8, 0.95, 0.36), (0.25, -2.2, 0.8, 0.3), (-0.2, -2.8, 0.42, 0.18), (0.55, -2.9, 0.36, 0.16))):
        pot(k, x, y, 0.05, h, r, FIRED if j % 2 == 0 else FIRED_DARK)
    return k.finish()


def clay_pit(name, seed=73):
    """The clay pit by a kiln (C3.2): a hollow dug in the bank, its floor wet clay with water standing in it, the dug clay heaped beside, a
    puddling trough where it is trodden, a spade - drawn larger as the ware fired at its kiln grows."""
    k = Kit(name, seed)
    k.cyl((0, 0, 0), 2.6, 2.4, 0.08, CLAY_RAW, seg=12)                          # the pit's lip, clay trodden out
    k.cyl((0, 0, 0.02), 2.0, 1.7, 0.08, CLAY_WET, seg=12)                       # its floor, wet
    k.cyl((0.4, -0.3, 0.06), 0.8, 0.7, 0.05, WELL_WATER, seg=9)                 # water standing in it
    for i in range(6):                                                          # the dug clay heaped beside it
        k.blob((2.4 + k.rng.uniform(-0.5, 0.5), 1.0 + k.rng.uniform(-0.6, 0.6), 0.3), 0.55 + 0.1 * (i % 3), CLAY_RAW, subdiv=2, jitter=0.15, squash=0.55)
    k.box((-2.4, 1.2, 0.2), (1.6, 0.9, 0.4), BOARD)                             # the puddling trough
    k.box((-2.4, 1.2, 0.41), (1.4, 0.7, 0.04), CLAY_WET)
    k.limb((-1.6, -1.8, 0.0), (-1.2, -2.2, 1.3), 0.035, 0.035, TIMBER)          # a spade
    return k.finish()


def storage_jars(name, seed=74):
    """Storage jars (C3.2): great jars of fired clay, lidded and sealed, standing by a granary - drawn while its village's jars keep its grain."""
    k = Kit(name, seed)
    for j, (x, y, h, r) in enumerate(((0.0, 0.0, 1.15, 0.42), (0.85, 0.15, 1.05, 0.38), (0.4, 0.8, 1.1, 0.4), (-0.6, 0.6, 0.6, 0.24))):
        col = FIRED if j % 2 == 0 else FIRED_DARK
        pot(k, x, y, 0.0, h, r, col, neck=0.5)
        k.cyl((x, y, h * 0.99), r * 0.52, r * 0.42, 0.06, CLAY_RAW, seg=10)      # its sealing lid of clay
    return k.finish()


def ware_stack(name, seed=75):
    """Ware for sale (C3.2): fired pots stacked in straw, bowls nested, a pair of jars - set out by a kiln while its village holds ware beyond what
    it keeps."""
    k = Kit(name, seed)
    k.box((0, 0, 0.06), (2.2, 1.4, 0.12), STRAW)                                # straw laid down
    for i in range(3):
        for j in range(2):
            pot(k, -0.7 + i * 0.6, -0.3 + j * 0.6, 0.12, 0.42, 0.2, FIRED if (i + j) % 2 == 0 else FIRED_DARK)
    for i in range(3):                                                          # bowls nested in a stack
        k.cyl((0.95, 0.4, 0.12 + i * 0.1), 0.22, 0.3, 0.12, FIRED, seg=10)
    pot(k, 0.95, -0.35, 0.12, 0.7, 0.26, FIRED_DARK)
    return k.finish()


CART_WOOD = srgb(0.58, 0.43, 0.27)      # a cart's planks, weathered


def wheel(k, x, y, z, r, spokes=6, width=0.12):
    """A wooden wheel standing upright at (x, y, z) - its axle along x: a felloe rim, spokes, a hub."""
    rot = Matrix.Rotation(math.pi / 2.0, 4, "Y")
    k.cyl((x - width / 2.0, y, z), r, r, width, BEAM, seg=12, rot=rot)                 # the rim (a solid disc reads at a distance)
    k.cyl((x - width * 0.6, y, z), r * 0.82, r * 0.82, width * 1.2, BOARD, seg=12, rot=rot)   # the inner face, darker between the spokes
    for i in range(spokes):
        a = i * math.pi / spokes
        k.limb((x, y + math.cos(a) * r * 0.85, z + math.sin(a) * r * 0.85), (x, y - math.cos(a) * r * 0.85, z - math.sin(a) * r * 0.85), 0.035, 0.035, TIMBER, seg=4)
    k.cyl((x - width, y, z), r * 0.16, r * 0.16, width * 2.0, TIMBER, seg=8, rot=rot)   # the hub


def handcart(name, seed=76):
    """A handcart (C4.2): a bed of planks with low sides on two wheels and an axle, two shafts forward for the one who pushes or pulls it. Its
    front (the shafts) faces -y; parked, the shafts rest on the ground."""
    k = Kit(name, seed)
    L, W, z = 1.6, 0.95, 0.55
    k.box((0, 0.1, z), (W, L, 0.08), CART_WOOD)                                    # the bed
    for sx in (-1, 1):
        k.box((sx * (W / 2.0 - 0.03), 0.1, z + 0.17), (0.05, L, 0.28), CART_WOOD)  # its sides
    k.box((0, 0.1 + L / 2.0 - 0.03, z + 0.17), (W, 0.05, 0.28), CART_WOOD)        # the tailboard
    k.box((0, 0.25, z - 0.08), (W + 0.3, 0.09, 0.09), TIMBER)                  # the axle
    for sx in (-1, 1):
        wheel(k, sx * (W / 2.0 + 0.12), 0.25, 0.45, 0.45)
        k.limb((sx * 0.32, -L / 2.0 + 0.1, z), (sx * 0.30, -L / 2.0 - 1.2, 0.08), 0.035, 0.03, TIMBER, seg=5)   # the shafts, resting on the ground
    return k.finish()


def ox_cart(name, seed=77):
    """An ox cart (C4.2): a long bed with rails on two tall wheels, a pole forward to the yoke of the ox (drawn apart). Front -y."""
    k = Kit(name, seed)
    L, W, z = 2.6, 1.35, 0.95
    k.box((0, 0.2, z), (W, L, 0.1), CART_WOOD)                                     # the bed
    for sx in (-1, 1):                                                          # the rails on stakes
        k.box((sx * (W / 2.0 - 0.04), 0.2, z + 0.45), (0.06, L, 0.06), BEAM)
        for j in range(4):
            k.box((sx * (W / 2.0 - 0.04), 0.2 - L / 2.0 + 0.2 + j * (L - 0.4) / 3.0, z + 0.22), (0.05, 0.05, 0.45), BEAM)
    k.box((0, 0.35, z - 0.1), (W + 0.4, 0.12, 0.12), TIMBER)                   # the axle
    for sx in (-1, 1):
        wheel(k, sx * (W / 2.0 + 0.16), 0.35, 0.72, 0.72, spokes=8, width=0.14)
    k.limb((0, -L / 2.0 + 0.3, z - 0.02), (0, -L / 2.0 - 2.0, 0.85), 0.06, 0.05, TIMBER, seg=6)   # the pole
    k.box((0, -L / 2.0 - 2.0, 0.9), (1.2, 0.12, 0.12), TIMBER)                 # the yoke
    return k.finish()


def cart_load(name, seed=78):
    """A cart's load (C4.2): sacks and a bundle tied on - drawn on a cart that carries more than a back's load."""
    k = Kit(name, seed)
    for i, (x, y) in enumerate(((-0.2, -0.3), (0.2, -0.25), (-0.15, 0.3), (0.2, 0.32))):
        k.ellipsoid((x, y, 0.2), (0.22, 0.3, 0.2), CLOTH_W if i % 2 == 0 else srgb(0.62, 0.52, 0.36), subdiv=2)
    k.box((0, 0.0, 0.42), (0.55, 0.9, 0.18), srgb(0.55, 0.42, 0.26), bevel=0.04)   # a bundle on top
    return k.finish()


def market_stall(name, seed=66):
    """A market stall (C1.6, D2-114): a trestle of goods under a dyed awning on four poles - the game sets out as many as traders come to the
    market, each awning its own dye (alpha 1: tinted per instance)."""
    k = Kit(name, seed)
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.box((sx * 1.3, sy * 0.95, 1.15 if sy < 0 else 1.0), (0.1, 0.1, 2.3 if sy < 0 else 2.0), BEAM)
    slab(k, [(-1.5, -1.2, 2.3), (1.5, -1.2, 2.3), (1.5, 1.2, 1.95), (-1.5, 1.2, 1.95)], 0.05, CLOTH)   # the awning, sloped to the back
    for sx in (-1, 1):                                                         # its valance
        pass
    k.box((0, -1.22, 2.18), (3.0, 0.04, 0.26), CLOTH)
    k.box((0, -0.55, 0.8), (2.5, 0.75, 0.08), BEAM)                            # the trestle
    for sx in (-1, 1):
        k.box((sx * 1.0, -0.55, 0.4), (0.08, 0.6, 0.8), BEAM)
    goods = (STRAW, srgb(0.62, 0.36, 0.22), WALL2, srgb(0.42, 0.50, 0.30))
    for j in range(4):
        k.box((-0.9 + j * 0.6, -0.55, 0.96), (0.42, 0.4, 0.24), goods[(j + seed) % 4])
    k.cyl((1.7, 0.6, 0), 0.3, 0.3, 0.75, srgb(0.45, 0.30, 0.17), seg=8)       # a barrel
    k.box((-1.75, 0.6, 0.22), (0.5, 0.5, 0.44), BEAM)                          # a crate
    return k.finish()


def market_cross(name, seed=67):
    """A market cross (C1.6, D2-114): a stone shaft and head on stepped stone, where a market is held - the centre of a market green."""
    k = Kit(name, seed)
    for i, (r, h) in enumerate(((1.8, 0.3), (1.35, 0.3), (0.9, 0.3))):
        k.box((0, 0, i * 0.3 + h / 2.0), (r * 2, r * 2, h), STONE, bevel=0.03)
    k.box((0, 0, 0.9 + 0.2), (0.7, 0.7, 0.4), STONE_DARK, bevel=0.03)
    k.box((0, 0, 1.3 + 1.6), (0.32, 0.32, 3.2), STONE, bevel=0.02)
    k.box((0, 0, 4.2), (1.1, 0.26, 0.26), STONE, bevel=0.02)
    k.box((0, 0, 4.55), (0.28, 0.28, 0.5), STONE, bevel=0.02)
    return k.finish()


def build_all():
    objs = []
    objs.extend(homes())                   # the house families (D2-114; the four old house_* models are gone)
    objs.append(round_hut("hut_round", 2.3, 2.1, 2.6))
    objs.append(tent("tent"))
    objs.append(ruin("ruin"))
    objs.append(boat("boat"))
    objs.append(dugout("dugout"))          # the dugout (C9.1); "boat" is the plank boat (C9.2)
    objs.append(boat_shed("boat_shed"))    # the boat shed at a landing (C9.2)
    # the wild animals (D2-035): one per species kind, at adult size
    objs.append(quadruped("animal_deer", 1.65, 1.1, 0.72, (0.55, 0.36, 0.20), belly=(0.82, 0.74, 0.62), head=0.24, neck=0.42, neck_up=55, ears=0.13, tail=0.12, antlers=0.45, seed=31))
    objs.append(quadruped("animal_hare", 0.48, 0.3, 0.12, (0.52, 0.45, 0.36), belly=(0.80, 0.76, 0.68), head=0.09, neck=0.06, neck_up=40, ears=0.12, tail=0.05, bushy=True, tail_tip=(0.95, 0.95, 0.92), snout=0.2, seed=32))
    objs.append(quadruped("animal_aurochs", 2.7, 1.65, 0.85, (0.20, 0.15, 0.12), belly=(0.28, 0.21, 0.16), hoof=(0.08, 0.07, 0.06), width=1.0, head=0.42, neck=0.4, neck_up=15, ears=0.12, tail=0.6, horns=0.55, horn_back=False, snout=0.6, hump=0.3, seed=33))
    objs.append(quadruped("animal_boar", 1.35, 0.85, 0.36, (0.28, 0.23, 0.19), belly=(0.33, 0.28, 0.23), hoof=(0.10, 0.08, 0.07), width=0.55, head=0.3, neck=0.15, neck_up=-10, ears=0.1, tail=0.18, tusks=True, snout=1.0, hump=0.25, seed=34))
    objs.append(quadruped("animal_ibex", 1.3, 0.9, 0.5, (0.50, 0.43, 0.34), belly=(0.80, 0.75, 0.66), head=0.2, neck=0.3, neck_up=50, ears=0.08, tail=0.1, horns=0.6, horn_back=True, seed=35))
    objs.append(quadruped("animal_wolf", 1.25, 0.78, 0.42, (0.46, 0.44, 0.42), belly=(0.72, 0.70, 0.66), hoof=(0.18, 0.17, 0.16), head=0.21, neck=0.22, neck_up=25, ears=0.11, tail=0.45, bushy=True, snout=0.9, seed=36))
    objs.append(quadruped("animal_fox", 0.75, 0.4, 0.2, (0.78, 0.38, 0.14), belly=(0.92, 0.88, 0.80), hoof=(0.12, 0.08, 0.06), head=0.13, neck=0.1, neck_up=30, ears=0.08, tail=0.38, bushy=True, tail_tip=(0.95, 0.94, 0.90), snout=1.0, seed=37))
    objs.append(quadruped("animal_bear", 2.0, 1.15, 0.5, (0.36, 0.25, 0.16), belly=(0.32, 0.22, 0.14), hoof=(0.12, 0.09, 0.07), width=0.95, head=0.35, neck=0.25, neck_up=10, ears=0.07, tail=0.1, snout=0.7, hump=0.35, seed=38))
    objs.append(seal("animal_seal"))
    # the households' animals (D2-048): a small dun cow with short horns (coats tinted in the game), a cream goat, a pink pig
    objs.append(quadruped("animal_cow", 2.0, 1.3, 0.66, (0.78, 0.68, 0.54), belly=(0.92, 0.88, 0.80), hoof=(0.10, 0.08, 0.07), width=0.85, head=0.34, neck=0.3, neck_up=12, ears=0.12, tail=0.55, horns=0.22, horn_back=False, snout=0.55, seed=51))
    objs.append(quadruped("animal_goat", 1.0, 0.72, 0.42, (0.88, 0.85, 0.78), belly=(0.93, 0.91, 0.86), hoof=(0.22, 0.19, 0.16), head=0.16, neck=0.22, neck_up=45, ears=0.09, tail=0.08, horns=0.26, horn_back=True, snout=0.45, seed=52))
    objs.append(quadruped("animal_pig", 1.15, 0.66, 0.24, (0.88, 0.68, 0.62), belly=(0.92, 0.74, 0.70), hoof=(0.42, 0.32, 0.30), width=0.62, head=0.27, neck=0.1, neck_up=-5, ears=0.12, tail=0.1, snout=1.0, seed=53))
    objs.append(fish("fish_shoal"))
    objs.append(granary("granary"))
    objs.append(conifer("tree_conifer", 8.5, 21))
    objs.append(conifer("tree_conifer_small", 5.5, 22))
    objs.append(broadleaf("tree_broadleaf", 8.0, 23))
    objs.append(broadleaf("tree_broadleaf_round", 6.5, 24, palette=((0.35, 0.50, 0.22), (0.40, 0.56, 0.25), (0.31, 0.46, 0.20))))
    objs.append(birch("tree_birch", 9.0, 25))
    objs.append(bush("bush", 26))
    objs.append(person("person"))
    objs.append(fence("fence"))
    objs.append(haystack("haystack"))
    objs.append(rick("rick"))                  # the sheaves in the stackyard (C3.7b)
    objs.append(grave("grave"))                # the burial ground (D2-151)
    objs.append(grave_old("grave_old"))
    objs.append(standard("standard"))         # the first household's standard (D2-152)
    objs.append(muck_heap("muck_heap"))       # the yards' muck (C3.7c)
    objs.append(well("well"))
    # the civilisation kit (D2-089, C1)
    objs.append(barn("barn"))
    objs.append(smithy("smithy"))
    objs.append(quarry_face("quarry_face"))
    objs.append(stone_pile("stone_pile"))
    objs.append(mine_pit("mine_pit"))
    objs.append(charcoal_clamp("charcoal_clamp"))
    objs.append(bog_pits("bog_pits"))
    objs.append(training_ground("training_ground"))
    objs.append(market_stall("market_stall"))
    objs.append(market_cross("market_cross"))
    objs.append(weavers_shed("weavers_shed"))   # (C3.1)
    objs.append(tenter("tenter"))
    objs.append(kiln("kiln"))                  # (C3.2)
    objs.append(clay_pit("clay_pit"))
    objs.append(storage_jars("storage_jars"))
    objs.append(ware_stack("ware_stack"))
    objs.append(handcart("handcart"))          # (C4.2)
    objs.append(ox_cart("ox_cart"))
    objs.append(cart_load("cart_load"))
    objs.append(grass_tuft("grass_tuft", 31))
    objs.append(flowers("flowers", 32))
    objs.append(reeds("reeds", 33))
    objs.append(boulder("boulder", 34))
    return objs


def export(objs, out):
    os.makedirs(out, exist_ok=True)
    for o in objs:
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        path = os.path.join(out, o.name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                                 axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE",
                                 colors_type="LINEAR", use_mesh_modifiers=True, add_leaf_bones=False, object_types={"MESH"})
        print("[kit] exported", path, len(o.data.polygons), "faces")


GRID = 0          # (--grid N: the preview lays models out N to a row)
GRID_STEP = 18.0


def preview(objs, out):
    """A contact sheet: every model in a row, workbench with vertex colours, soft studio light."""
    os.makedirs(out, exist_ok=True)
    if GRID:
        rows = [objs[i:i + GRID] for i in range(0, len(objs), GRID)]
        x = 0.0
        for ri, row in enumerate(rows):
            for ci, o in enumerate(row):
                o.location = (ci * GRID_STEP, -ri * GRID_STEP, 0)
        scene = bpy.context.scene
        scene.render.engine = "BLENDER_WORKBENCH"
        sh = scene.display.shading
        sh.light = "STUDIO"; sh.color_type = "VERTEX"; sh.show_shadows = True; sh.show_cavity = True; sh.cavity_type = "BOTH"
        scene.display.shadow_focus = 0.4
        scene.render.resolution_x = 2400; scene.render.resolution_y = 1500
        cam_data = bpy.data.cameras.new("cam"); cam_data.type = "ORTHO"
        wd, ht = (GRID - 1) * GRID_STEP, (len(rows) - 1) * GRID_STEP
        cam_data.ortho_scale = max(wd, ht * 1.6) + GRID_STEP * 1.1
        cam = bpy.data.objects.new("cam", cam_data); scene.collection.objects.link(cam)
        cx, cy = wd / 2.0, -ht / 2.0
        cam.location = (cx - 60.0 * 0.0, cy - 60.0, 50.0)
        cam.rotation_euler = (math.radians(50), 0, 0)
        scene.camera = cam
        scene.world = bpy.data.worlds.new("w")
        scene.render.filepath = os.path.join(os.path.abspath(out), "kit_preview.png")
        bpy.ops.render.render(write_still=True)
        print("[kit] preview", scene.render.filepath)
        return
    rows = [objs] if len(objs) <= 10 else [objs[: (len(objs) + 1) // 2], objs[(len(objs) + 1) // 2:]]
    width = 0.0
    for ri, row in enumerate(rows):
        x = 0.0
        for o in row:
            w = max(o.dimensions.x, o.dimensions.y)
            o.location = (x + w / 2.0, (ri if len(rows) > 1 else 1) * 16.0, 0)   # a single row stands where the camera looks
            x += w + 1.5
        width = max(width, x)
    x = width
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "VERTEX"
    shading.show_shadows = True
    shading.show_cavity = True
    shading.cavity_type = "BOTH"
    scene.display.shadow_focus = 0.4
    scene.render.resolution_x = 2400
    scene.render.resolution_y = 1100
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = x * (0.52 if len(rows) > 1 else 1.02)
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    if len(rows) > 1:
        cam.location = (x / 2.0, -40.0, 24.0)
    else:
        # a single row: aim at its middle (the camera looks down 22 degrees from the horizon)
        top = max(o.dimensions.z for o in rows[0])
        cam.location = (x / 2.0, 16.0 - 24.0 / math.tan(math.radians(22)), 24.0 + top * 0.5)
    cam.rotation_euler = (math.radians(68), 0, 0)
    scene.camera = cam
    world = bpy.data.worlds.new("w")
    scene.world = world
    scene.render.filepath = os.path.join(os.path.abspath(out), "kit_preview.png")   # absolute: no .blend to be relative to
    bpy.ops.render.render(write_still=True)
    print("[kit] preview", scene.render.filepath)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[argv.index("--out") + 1] if "--out" in argv else "kit_out"
    prev = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    only = argv[argv.index("--only") + 1] if "--only" in argv else None
    global GRID
    GRID = int(argv[argv.index("--grid") + 1]) if "--grid" in argv else 0
    objs = build_all()
    if only:
        # just these models (by name prefix); the others are dropped before export and preview
        for o in list(objs):
            if not any(o.name.startswith(p) for p in only.split(",")):
                bpy.data.objects.remove(o)
                objs.remove(o)
    export(objs, out)
    if "--tint" in argv:
        # (the preview only, after export: each model's roof and walls painted with sample materials, cycling)
        roofs = [(0.62, 0.50, 0.30), (0.42, 0.36, 0.30), (0.66, 0.30, 0.18), (0.40, 0.42, 0.46), (0.55, 0.47, 0.34)]
        walls = [(0.93, 0.91, 0.86), (0.80, 0.66, 0.46), (0.45, 0.33, 0.22), (0.62, 0.60, 0.56), (0.88, 0.80, 0.70)]
        for i, o in enumerate(objs):
            cl = o.data.color_attributes[0] if o.data.color_attributes else None
            if cl is None:
                continue
            r, wcol = roofs[i % len(roofs)], walls[(i // 2) % len(walls)]
            for d in cl.data:
                c = d.color
                if c[3] > 0.9:
                    d.color = (c[0] * r[0] ** 2.2, c[1] * r[1] ** 2.2, c[2] * r[2] ** 2.2, 1.0)
                elif 0.15 < c[3] < 0.35:
                    d.color = (c[0] * wcol[0] ** 2.2, c[1] * wcol[1] ** 2.2, c[2] * wcol[2] ** 2.2, 1.0)
    if prev:
        if only:
            for o in objs:
                o.rotation_euler = (0, 0, math.radians(55))   # a three-quarter view (after export: never in the files)
            bpy.context.view_layer.update()
        preview(objs, prev)


main()
