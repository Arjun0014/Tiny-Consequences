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
        k.blob(c, height * r.uniform(0.16, 0.22), srgb(p[0] + r.uniform(-0.03, 0.03), p[1] + r.uniform(-0.03, 0.03), p[2]), subdiv=2, jitter=0.10, squash=0.85, smooth=True)
    k.blob((0, 0, top + height * 0.15), height * 0.23, srgb(*palette[1]), subdiv=2, jitter=0.08, squash=0.8, smooth=True)
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
        k.blob((r.uniform(-0.3, 0.3), r.uniform(-0.3, 0.3), z), height * (0.16 - 0.02 * i), srgb(0.48, 0.62, 0.26), subdiv=2, jitter=0.12, squash=1.3, smooth=True)
    return k.finish()


def bush(name, seed):
    k = Kit(name, seed)
    r = k.rng
    for i in range(4):
        a = i * 1.7
        k.blob((math.cos(a) * 0.4, math.sin(a) * 0.4, 0.45 + r.uniform(0, 0.2)), r.uniform(0.45, 0.65), srgb(0.28 + r.uniform(0, 0.05), 0.44 + r.uniform(0, 0.06), 0.19), subdiv=2, jitter=0.12, squash=0.8, smooth=True)
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


def build_all():
    objs = []
    objs.append(house("house_cottage", 7.0, 5.0, 2.6, 42, THATCH, seed=11))
    objs.append(house("house_timber", 8.0, 5.4, 3.2, 45, TILE, wall=srgb(0.90, 0.86, 0.76), frame=True, seed=12))
    objs.append(house("house_long", 11.5, 5.2, 2.2, 40, THATCH, wall=WALL2, chimney=True, seed=13))
    objs.append(house("house_small", 4.6, 3.8, 2.3, 44, THATCH, wall=srgb(0.80, 0.74, 0.62), chimney=False, seed=14))
    objs.append(round_hut("hut_round", 2.3, 2.1, 2.6))
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
    rows = [objs[: (len(objs) + 1) // 2], objs[(len(objs) + 1) // 2:]]
    width = 0.0
    for ri, row in enumerate(rows):
        x = 0.0
        for o in row:
            w = max(o.dimensions.x, o.dimensions.y)
            o.location = (x + w / 2.0, ri * 16.0, 0)
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
    cam_data.ortho_scale = x * 0.52
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (x / 2.0, -40.0, 24.0)
    cam.rotation_euler = (math.radians(68), 0, 0)
    scene.camera = cam
    world = bpy.data.worlds.new("w")
    scene.world = world
    scene.render.filepath = os.path.join(out, "kit_preview.png")
    bpy.ops.render.render(write_still=True)
    print("[kit] preview", scene.render.filepath)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[argv.index("--out") + 1] if "--out" in argv else "kit_out"
    prev = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    objs = build_all()
    export(objs, out)
    if prev:
        preview(objs, prev)


main()
