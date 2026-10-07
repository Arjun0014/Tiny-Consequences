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


def granary(name, seed=4):
    k = Kit(name, seed)
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.cyl((sx * 1.15, sy * 1.15, 0), 0.14, 0.12, 0.9, TIMBER, seg=6)
            k.cyl((sx * 1.15, sy * 1.15, 0.9), 0.28, 0.28, 0.12, STONE, seg=8)  # rat stones
    k.box((0, 0, 1.02 + 0.06), (3.0, 3.0, 0.12), TIMBER)
    k.box((0, 0, 1.14 + 1.0), (2.7, 2.7, 2.0), WALL2, bevel=0.04)
    k.cyl((0, 0, 3.1), 2.35, 0.1, 1.8, THATCH, seg=4)
    k.box((0, -1.37, 1.9), (0.7, 0.08, 1.1), DOOR)
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


def barn(name, seed=61):
    """A great timber barn (a large store, C1.1): long, tall, steep thatch, wide double doors on the long side, no chimney, no windows."""
    k = Kit(name, seed)
    L, W, H = 12.0, 7.0, 3.4
    k.box((0, 0, 0.15), (L + 0.3, W + 0.3, 0.3), STONE, bevel=0.03)          # a stone sill against the damp
    k.box((0, 0, 0.3 + (H - 0.3) / 2.0), (L, W, H - 0.3), WALL2, bevel=0.04)
    for x in (-L / 2.0, -L / 6.0, L / 6.0, L / 2.0):                        # the frame's posts and a rail
        for sy in (-1, 1):
            k.box((x, sy * (W / 2.0 + 0.03), H / 2.0 + 0.1), (0.24, 0.08, H - 0.1), TIMBER)
    for sy in (-1, 1):
        k.box((0, sy * (W / 2.0 + 0.04), H * 0.5), (L, 0.07, 0.16), TIMBER)
    gable_walls(k, L, W, H, 52, WALL2)
    for sx in (-1, 1):                                                        # the double doors, a wagon's width
        k.box((sx * 0.85, -(W / 2.0 + 0.05), 1.35), (1.6, 0.1, 2.7), DOOR)
    k.box((0, -(W / 2.0 + 0.08), 2.78), (3.6, 0.14, 0.18), TIMBER)
    gable_roof(k, L, W, H, 52, THATCH, overhang=0.45, thick=0.28, ridge_color=THATCH_DARK)
    return k.finish()


def smithy(name, seed=62):
    """A smithy (C1.4): an open-fronted timber shop over a stone forge with its chimney, an anvil on a stump, a quench trough, charcoal."""
    k = Kit(name, seed)
    L, W, H = 6.0, 4.6, 2.5
    k.box((0, 0, 0.08), (L + 0.6, W + 0.6, 0.16), STONE_DARK, bevel=0.02)   # a beaten floor of stones
    k.box((0, W / 2.0 - 0.1, H / 2.0), (L, 0.2, H), WALL2, bevel=0.03)     # the back wall
    for sx in (-1, 1):
        k.box((sx * L / 2.0, 0.2, H / 2.0), (0.2, W - 0.4, H), WALL2, bevel=0.03)   # side walls
    for sx in (-1, 1):
        k.box((sx * (L / 2.0 - 0.1), -W / 2.0 + 0.1, H / 2.0), (0.2, 0.2, H), TIMBER)   # the open front's posts
    gable_walls(k, L, W, H, 30, WALL2)
    gable_roof(k, L, W, H, 30, TILE, overhang=0.4, thick=0.18, ridge_color=srgb(0.5, 0.22, 0.14, 1.0))
    k.box((-L * 0.22, W / 2.0 - 0.75, 0.55), (1.6, 1.1, 1.1), STONE, bevel=0.05)   # the forge hearth
    k.box((-L * 0.22, W / 2.0 - 0.75, 1.12), (1.2, 0.8, 0.05), CHARCOAL)
    k.box((-L * 0.22, W / 2.0 - 0.35, 2.4), (0.8, 0.7, 3.0), CHIMNEY, bevel=0.03)   # its chimney through the roof
    k.cyl((-L * 0.22 + 1.1, W / 2.0 - 0.6, 0.6), 0.32, 0.2, 0.5, srgb(0.45, 0.30, 0.18), seg=6)   # the bellows
    k.cyl((L * 0.12, -0.2, 0.0), 0.32, 0.30, 0.6, TIMBER, seg=8)            # the anvil's stump
    k.box((L * 0.12, -0.2, 0.72), (0.62, 0.24, 0.24), IRON, bevel=0.02)       # the anvil
    k.box((L * 0.34, 0.5, 0.3), (1.0, 0.5, 0.6), TIMBER)                     # the quench trough
    k.box((L * 0.34, 0.5, 0.58), (0.86, 0.36, 0.04), WELL_WATER)
    for i in range(5):                                                        # charcoal heaped by the forge
        k.blob((-L * 0.5 - 0.8 + k.rng.uniform(-0.3, 0.3), W * 0.1 + k.rng.uniform(-0.6, 0.6), 0.25), 0.45, CHARCOAL, jitter=0.3, squash=0.6)
    return k.finish()


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


def market_stalls(name, seed=66):
    """A market (C1.6): three stalls with cloth awnings, tables of goods, barrels and crates."""
    k = Kit(name, seed)
    for i, x in enumerate((-3.6, 0.0, 3.6)):
        for sx in (-1, 1):
            for sy in (-1, 1):
                k.box((x + sx * 1.3, sy * 0.9, 1.1), (0.1, 0.1, 2.2), TIMBER)
        k.box((x, 0, 2.25), (2.9, 2.1, 0.06), CLOTH, rot_z=0.0)                # the awning
        k.box((x, -0.6, 0.8), (2.4, 0.7, 0.08), TIMBER)                         # the table
        for j in range(3):
            k.box((x - 0.7 + j * 0.7, -0.6, 0.95), (0.4, 0.35, 0.22), (STRAW, WALL2, srgb(0.62, 0.36, 0.22))[(i + j) % 3])
    for j in range(4):                                                          # barrels and crates
        k.cyl((-5.6 + (j % 2) * 0.7, 1.2 - (j // 2) * 0.8, 0), 0.32, 0.32, 0.8, srgb(0.45, 0.30, 0.17), seg=8)
        k.box((5.4, 1.0 - j * 0.6, 0.25), (0.55, 0.5, 0.5), TIMBER)
    return k.finish()


def build_all():
    objs = []
    objs.append(house("house_cottage", 7.0, 5.0, 2.6, 42, THATCH, seed=11))
    objs.append(house("house_timber", 8.0, 5.4, 3.2, 45, TILE, wall=srgb(0.90, 0.86, 0.76), frame=True, seed=12))
    objs.append(house("house_long", 11.5, 5.2, 2.2, 40, THATCH, wall=WALL2, chimney=True, seed=13))
    objs.append(house("house_small", 4.6, 3.8, 2.3, 44, THATCH, wall=srgb(0.80, 0.74, 0.62), chimney=False, seed=14))
    # the same homes on earth-fast posts (no stone footings: C1.3 - only villages that quarry stone raise houses on a stone sill)
    objs.append(house("house_cottage_posts", 7.0, 5.0, 2.6, 42, THATCH, seed=11, plinth=False))
    objs.append(house("house_timber_posts", 8.0, 5.4, 3.2, 45, TILE, wall=srgb(0.90, 0.86, 0.76), frame=True, seed=12, plinth=False))
    objs.append(house("house_long_posts", 11.5, 5.2, 2.2, 40, THATCH, wall=WALL2, chimney=True, seed=13, plinth=False))
    objs.append(house("house_small_posts", 4.6, 3.8, 2.3, 44, THATCH, wall=srgb(0.80, 0.74, 0.62), chimney=False, seed=14, plinth=False))
    objs.append(round_hut("hut_round", 2.3, 2.1, 2.6))
    objs.append(tent("tent"))
    objs.append(ruin("ruin"))
    objs.append(boat("boat"))
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
    objs.append(well("well"))
    # the civilisation kit (D2-089, C1)
    objs.append(barn("barn"))
    objs.append(smithy("smithy"))
    objs.append(quarry_face("quarry_face"))
    objs.append(stone_pile("stone_pile"))
    objs.append(training_ground("training_ground"))
    objs.append(market_stalls("market_stalls"))
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


def preview(objs, out):
    """A contact sheet: every model in a row, workbench with vertex colours, soft studio light."""
    os.makedirs(out, exist_ok=True)
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
    objs = build_all()
    if only:
        # just these models (by name prefix); the others are dropped before export and preview
        for o in list(objs):
            if not any(o.name.startswith(p) for p in only.split(",")):
                bpy.data.objects.remove(o)
                objs.remove(o)
    export(objs, out)
    if prev:
        if only:
            for o in objs:
                o.rotation_euler = (0, 0, math.radians(55))   # a three-quarter view (after export: never in the files)
            bpy.context.view_layer.update()
        preview(objs, prev)


main()
