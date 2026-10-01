# Tiny Consequences villager builder (docs/v3/20_CHARACTER_PIPELINE.md, D-078).
#
#   blender -b --factory-startup -P tools/blender/build_villager.py -- [--out assets/characters] [--preview out/s9/villager]
#
# Uses the CC0 Universal Animation Library (Quaternius) only as skeleton + animation source: its
# 13.7k-triangle mannequin is deleted and replaced by a bespoke blocky villager built here from
# the bone positions, every part rigidly weighted to one bone. One mesh carries every optional part
# (hair styles, hats, skirt, dress, apron, cloak, beard, tools, loads); each vertex's UV encodes
#   u = (palette slot + 0.5) / 16   (0 skin, 1 hair, 2 upper, 3 lower, 4 accent, 5 leather, 6 wood,
#                                     7 metal, 8 straw, 9 eyes, 10 white)
#   v = (part id + 0.5) / 64        (PARTS below)
# and the villager shader (game/render/shaders/villager.vp) shows the parts a person's instance
# mask selects and colours slots 0-4 from the instance palette: one mesh, one draw call per LOD,
# many different people. LODs: near, mid, far (fewer segments, fewer parts). Animations: chosen
# UAL clips renamed to the actions they show, plus carry clips made from Walk/Idle with the arms
# holding a load, baked (sampled) for Defold.
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix, Quaternion, Euler

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
def arg(name, default=None):
    if name in argv:
        return argv[argv.index(name) + 1]
    return default

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
UAL = os.path.join(ROOT, "assets", "3d-models", "ual", "Universal Animation Library[Standard]", "Unreal-Godot", "UAL1_Standard.glb")
OUT = os.path.join(ROOT, arg("--out", "assets/characters"))
PREVIEW = arg("--preview", None)
os.makedirs(OUT, exist_ok=True)

PARTS = [
    "body", "head", "eyes", "legs_trousers", "legs_bare", "feet",
    "hair_short", "hair_long", "hair_bun", "beard",
    "hat_straw", "hat_hood", "hat_cap", "headscarf",
    "tunic", "skirt", "dress", "apron", "cloak", "belt",
    "axe", "hoe", "sickle", "hammer", "basket", "sack", "bucket", "log", "rod", "spear", "shield", "flail",
    "babe",
]
PID = {n: i for i, n in enumerate(PARTS)}
SLOT = {"skin": 0, "hair": 1, "upper": 2, "lower": 3, "accent": 4, "leather": 5, "wood": 6, "metal": 7,
        "straw": 8, "eyes": 9, "white": 10}

# UAL clip -> our animation name
CLIPS = {
    "Idle_Loop": "idle", "Walk_Loop": "walk", "Jog_Fwd_Loop": "run", "Sword_Attack": "swing",
    "Fixing_Kneeling": "kneel_work", "Crouch_Fwd_Loop": "crouch_walk", "Crouch_Idle_Loop": "crouch",
    "Interact": "interact", "PickUp_Table": "pickup", "Idle_Talking_Loop": "talk",
    "Sitting_Idle_Loop": "sit", "Spell_Simple_Shoot": "throw", "Death01": "death", "Push_Loop": "push",
    "Dance_Loop": "dance", "Jump_Loop": "jump", "Walk_Formal_Loop": "walk_slow",
}

# ------------------------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=UAL)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
for o in list(bpy.data.objects):
    if o.type == "MESH":
        bpy.data.objects.remove(o, do_unlink=True)
bones = arm.data.bones

def bh(name):   # bone head (armature space)
    return Vector(bones[name].head_local)
def bt(name):
    return Vector(bones[name].tail_local)

# ------------------------------------------------------------------------------------------
# Mesh building: parts are lists of (verts, faces, bone, slot, part)
class Builder:
    def __init__(self):
        self.verts, self.faces, self.uvs, self.vbone, self.fpart = [], [], [], [], []
    def add(self, verts, faces, bone, slot, part):
        base = len(self.verts)
        u = (SLOT[slot] + 0.5) / 16.0
        v = (PID[part] + 0.5) / 64.0
        for p in verts:
            self.verts.append(Vector(p))
            self.vbone.append(bone)
        for f in faces:
            self.faces.append([base + i for i in f])
            self.uvs.append([(u, v)] * len(f))
            self.fpart.append(part)

def box(c, size, rot=None):
    """Axis-aligned box centred at c with size (sx, sy, sz); rot: optional Matrix3."""
    sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
    vs = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
          (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz)]
    out = []
    for v in vs:
        p = Vector(v)
        if rot is not None: p = rot @ p
        out.append(Vector(c) + p)
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    return out, faces

def taper_box(c0, c1, w0, d0, w1, d1, up=Vector((0, 0, 1))):
    """A box from point c0 to c1 (its axis), cross-section w x d tapering from (w0,d0) to (w1,d1)."""
    axis = (Vector(c1) - Vector(c0))
    L = axis.length
    a = axis.normalized()
    ref = Vector((0, 1, 0)) if abs(a.dot(Vector((0, 1, 0)))) < 0.9 else Vector((1, 0, 0))
    x = a.cross(ref).normalized()
    y = a.cross(x).normalized()
    vs = []
    for (cc, w, d) in ((Vector(c0), w0, d0), (Vector(c1), w1, d1)):
        for (sx, sy) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vs.append(cc + x * (sx * w / 2) + y * (sy * d / 2))
    faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    return vs, faces

def prism(c0, c1, r0, r1, sides):
    axis = Vector(c1) - Vector(c0)
    a = axis.normalized()
    ref = Vector((0, 0, 1)) if abs(a.z) < 0.9 else Vector((1, 0, 0))
    x = a.cross(ref).normalized()
    y = a.cross(x).normalized()
    vs = []
    for (cc, r) in ((Vector(c0), r0), (Vector(c1), r1)):
        for k in range(sides):
            ang = 2 * math.pi * (k + 0.5) / sides
            vs.append(cc + x * (math.cos(ang) * r) + y * (math.sin(ang) * r))
    faces = []
    for k in range(sides):
        k2 = (k + 1) % sides
        faces.append((k, k2, sides + k2, sides + k))
    faces.append(tuple(reversed(range(sides))))
    faces.append(tuple(range(sides, 2 * sides)))
    return vs, faces

def rbox(c, size, seg):
    """A rounded box (cube subdivided and pushed toward an ellipsoid): the head."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    if seg > 0:
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=seg, use_grid_fill=True)
    for v in bm.verts:
        p = v.co.copy()
        s = Vector((p.x * 2, p.y * 2, p.z * 2))
        n = s.normalized()
        # between a cube and a sphere: blocky but soft
        q = s.lerp(n * 1.18, 0.42)
        v.co = Vector((q.x * size[0] / 2, q.y * size[1] / 2, q.z * size[2] / 2)) + Vector(c)
    vs = [v.co.copy() for v in bm.verts]
    faces = [[v.index for v in f.verts] for f in bm.faces]
    bm.free()
    return vs, faces

def cone(base_c, r, h, sides, r_top=0.0):
    vs = []
    c = Vector(base_c)
    for k in range(sides):
        a = 2 * math.pi * k / sides
        vs.append(c + Vector((math.cos(a) * r, math.sin(a) * r, 0)))
    if r_top > 0:
        for k in range(sides):
            a = 2 * math.pi * k / sides
            vs.append(c + Vector((math.cos(a) * r_top, math.sin(a) * r_top, h)))
        faces = [(k, (k + 1) % sides, sides + (k + 1) % sides, sides + k) for k in range(sides)]
        faces.append(tuple(reversed(range(sides))))
        faces.append(tuple(range(sides, 2 * sides)))
    else:
        vs.append(c + Vector((0, 0, h)))
        faces = [(k, (k + 1) % sides, sides) for k in range(sides)]
        faces.append(tuple(reversed(range(sides))))
    return vs, faces

def build(lod):
    B = Builder()
    S = {0: 1.0, 1: 0.6, 2: 0.35}[lod]
    side = {0: 8, 1: 6, 2: 3}[lod]
    head_seg = {0: 2, 1: 1, 2: 0}[lod]
    # the far LOD (D-082): ~150-250 triangles drawn for a typical villager: triangular limbs, no
    # hands, feet, neck, belt or second hair lumps; one box for hats, aprons, logs and the babe;
    # tools keep their silhouette (a three-sided handle and a head)
    far = lod == 2
    pel = bh("pelvis"); s1 = bh("spine_01"); s3 = bh("spine_03"); nk = bh("neck_01"); hd = bh("Head")
    # --- body ---------------------------------------------------------------------------
    # hips and belly (the tunic colour covers the torso: slot upper), chest
    if far:
        B.add(*taper_box(pel + Vector((0, 0.01, -0.06)), s1 + Vector((0, 0.01, 0.03)), 0.33, 0.21, 0.31, 0.2), "pelvis", "upper", "body")
        B.add(*taper_box(s1 + Vector((0, 0.01, 0.03)), s3 + Vector((0, 0.01, 0.06)), 0.31, 0.2, 0.38, 0.22), "spine_03", "upper", "body")
    else:
        B.add(*taper_box(pel + Vector((0, 0.01, -0.06)), s1 + Vector((0, 0.01, 0.03)), 0.33, 0.21, 0.31, 0.2), "pelvis", "upper", "body")
        B.add(*taper_box(s1 + Vector((0, 0.01, 0.03)), bh("spine_02") + Vector((0, 0.0, 0.05)), 0.31, 0.2, 0.33, 0.21), "spine_01", "upper", "body")
        B.add(*taper_box(bh("spine_02") + Vector((0, 0.0, 0.05)), s3 + Vector((0, 0.01, 0.06)), 0.33, 0.21, 0.38, 0.22), "spine_03", "upper", "body")
        # neck
        B.add(*prism(nk + Vector((0, 0, -0.02)), hd + Vector((0, 0, 0.02)), 0.055, 0.05, side), "neck_01", "skin", "head")
    # head
    hc = hd + Vector((0, -0.01, 0.13))
    B.add(*rbox(hc, (0.27, 0.27, 0.29), head_seg), "Head", "skin", "head")
    if lod < 2:
        # eyes and nose
        for sx in (-1, 1):
            B.add(*box(hc + Vector((sx * 0.055, -0.137, 0.01)), (0.035, 0.012, 0.045)), "Head", "eyes", "eyes")
        B.add(*box(hc + Vector((0, -0.145, -0.035)), (0.04, 0.03, 0.05)), "Head", "skin", "head")
    # arms (upper in the tunic colour, forearms bare skin, hands)
    for sd in ("l", "r"):
        ua0, ua1 = bh("upperarm_" + sd), bt("upperarm_" + sd)
        la0, la1 = bh("lowerarm_" + sd), bt("lowerarm_" + sd)
        B.add(*prism(ua0, ua1, 0.058, 0.05, side), "upperarm_" + sd, "upper", "body")
        B.add(*prism(la0, la1, 0.046, 0.04, side), "lowerarm_" + sd, "skin", "body")
        if not far:
            hn = bh("hand_" + sd)
            sgn = 1 if sd == "l" else -1
            B.add(*box(hn + Vector((sgn * 0.06, 0, -0.01)), (0.12, 0.06, 0.09)), "hand_" + sd, "skin", "body")
    # legs: trousers (lower colour) or bare (skin, under a dress) + shoes
    for sd in ("l", "r"):
        th0, th1 = bh("thigh_" + sd), bt("thigh_" + sd)
        ca0, ca1 = bh("calf_" + sd), bt("calf_" + sd)
        for part, slot in (("legs_trousers", "lower"), ("legs_bare", "skin")):
            B.add(*prism(th0, th1, 0.085, 0.07, side), "thigh_" + sd, slot, part)
            # (far: the calf reaches the ground, no shoe)
            B.add(*prism(ca0, ca1 + Vector((0, 0, 0.03 if not far else -0.08)), 0.065, 0.055, side), "calf_" + sd, slot, part)
        if not far:
            ft = bh("foot_" + sd)
            B.add(*box(ft + Vector((0, -0.06, -0.06)), (0.11, 0.24, 0.09)), "foot_" + sd, "leather", "feet")
    # --- hair and hats ---------------------------------------------------------------------
    B.add(*rbox(hc + Vector((0, 0.012, 0.045)), (0.29, 0.29, 0.22), max(0, head_seg - 1)), "Head", "hair", "hair_short")
    B.add(*rbox(hc + Vector((0, 0.015, 0.035)), (0.295, 0.295, 0.24), max(0, head_seg - 1)), "Head", "hair", "hair_long")
    if not far:
        B.add(*box(hc + Vector((0, 0.11, -0.12)), (0.25, 0.07, 0.24)), "Head", "hair", "hair_long")
    B.add(*rbox(hc + Vector((0, 0.012, 0.04)), (0.29, 0.29, 0.22), max(0, head_seg - 1)), "Head", "hair", "hair_bun")
    if not far:
        B.add(*rbox(hc + Vector((0, 0.13, 0.08)), (0.11, 0.09, 0.11), 0), "Head", "hair", "hair_bun")
    if lod < 2:
        B.add(*box(hc + Vector((0, -0.11, -0.11)), (0.2, 0.08, 0.12)), "Head", "hair", "beard")
    # straw hat: a wide brim and a low crown
    if far:
        B.add(*cone(hc + Vector((0, 0, 0.1)), 0.27, 0.11, 4, 0.11), "Head", "straw", "hat_straw")
    else:
        B.add(*cone(hc + Vector((0, 0, 0.1)), 0.27, 0.025, side + 4, 0.2), "Head", "straw", "hat_straw")
        B.add(*cone(hc + Vector((0, 0, 0.12)), 0.15, 0.1, side, 0.11), "Head", "straw", "hat_straw")
    # hood (the accent colour), cap, headscarf
    B.add(*rbox(hc + Vector((0, 0.025, 0.02)), (0.33, 0.33, 0.33), max(0, head_seg - 1)), "Head", "accent", "hat_hood")
    B.add(*box(hc + Vector((0, 0.0, 0.135)), (0.3, 0.3, 0.07)), "Head", "accent", "hat_cap")
    if not far:
        B.add(*box(hc + Vector((0, -0.08, 0.11)), (0.26, 0.16, 0.03)), "Head", "accent", "hat_cap")
    B.add(*rbox(hc + Vector((0, 0.02, 0.05)), (0.3, 0.3, 0.25), max(0, head_seg - 1)), "Head", "accent", "headscarf")
    # --- clothes -----------------------------------------------------------------------------
    # tunic hem (a short flared skirt over the hips), a skirt to the knee, a dress to the ankle
    B.add(*taper_box(pel + Vector((0, 0.01, 0.02)), pel + Vector((0, 0.01, -0.2)), 0.35, 0.23, 0.42, 0.29), "pelvis", "upper", "tunic")
    B.add(*taper_box(pel + Vector((0, 0.01, 0.02)), pel + Vector((0, 0.01, -0.42)), 0.35, 0.23, 0.5, 0.36), "pelvis", "lower", "skirt")
    B.add(*taper_box(pel + Vector((0, 0.01, 0.02)), pel + Vector((0, 0.01, -0.8)), 0.35, 0.23, 0.6, 0.46), "pelvis", "lower", "dress")
    B.add(*box(pel + Vector((0, -0.165, -0.15)), (0.3, 0.03, 0.42)), "pelvis", "white", "apron")
    if not far:
        B.add(*box(s3 + Vector((0, -0.115, -0.05)), (0.26, 0.02, 0.22)), "spine_03", "white", "apron")
    B.add(*taper_box(s3 + Vector((0, 0.13, 0.08)), pel + Vector((0, 0.2, -0.45)), 0.42, 0.04, 0.55, 0.06), "spine_03", "accent", "cloak")
    if not far:
        B.add(*box(pel + Vector((0, 0.0, 0.03)), (0.36, 0.24, 0.05)), "pelvis", "leather", "belt")
    # --- tools and loads -------------------------------------------------------------------
    # A fist holds a handle across the palm: in the rest (T) pose the hands point along x with
    # the palms down, so handles run front-back (y). With the arm hanging the tool points forward;
    # in the swing (UAL Sword_Attack) it cuts like a blade. Hanging loads (bucket, basket) hang
    # past the fist along the arm, i.e. below it when the arm is down.
    hr = bh("hand_r")
    grip = hr + Vector((-0.07, 0.0, -0.005))
    F = Vector((0, -1, 0))        # forward (front of the character)
    def handle(g, back, fwd, r, part, slot="wood"):
        B.add(*prism(g - F * back, g + F * fwd, r, r, 3 if far else 5), "hand_r", slot, part)
    handle(grip, 0.12, 0.55, 0.018, "axe")
    B.add(*box(grip + F * 0.52 + Vector((0, 0, -0.05)), (0.03, 0.1, 0.13)), "hand_r", "metal", "axe")
    handle(grip, 0.3, 0.8, 0.016, "hoe")
    B.add(*box(grip + F * 0.8 + Vector((0, 0, -0.07)), (0.13, 0.02, 0.15)), "hand_r", "metal", "hoe")
    handle(grip, 0.05, 0.14, 0.018, "sickle")
    for k in range(1 if far else 4):
        a = k * 0.55
        B.add(*box(grip + F * (0.16 + 0.07 * math.sin(a + 0.3) * 1.2) + Vector((0, 0, -0.03 - 0.07 * (1 - math.cos(a)))),
              (0.02, 0.06, 0.025)), "hand_r", "metal", "sickle")
    handle(grip, 0.06, 0.3, 0.017, "hammer")
    B.add(*box(grip + F * 0.31, (0.07, 0.07, 0.15)), "hand_r", "metal", "hammer")
    handle(grip, 0.1, 0.55, 0.016, "flail")
    if not far:
        B.add(*prism(grip + F * 0.57, grip + F * 0.62 + Vector((0, 0, -0.25)), 0.02, 0.02, 5), "hand_r", "wood", "flail")
    handle(grip, 0.9, 0.6, 0.015, "spear")
    B.add(*prism(grip + F * 0.6, grip + F * 0.75, 0.03, 0.002, 4), "hand_r", "metal", "spear")
    la = bh("lowerarm_l")
    B.add(*prism(la + Vector((0.12, 0.05, 0)), la + Vector((0.12, 0.09, 0)), 0.2, 0.2, side + 2), "lowerarm_l", "accent", "shield")
    B.add(*prism(grip, grip + F * 0.9 + Vector((0, 0, 0.6)), 0.012, 0.006, 4), "hand_r", "wood", "rod")
    # bucket (right) and basket (left) hang past the fist
    B.add(*prism(grip + Vector((-0.1, 0, 0)), grip + Vector((-0.3, 0, 0)), 0.085, 0.1, side), "hand_r", "wood", "bucket")
    hl = bh("hand_l")
    gl = hl + Vector((0.07, 0.0, -0.005))
    B.add(*prism(gl + Vector((0.12, 0, 0)), gl + Vector((0.28, 0, 0)), 0.13, 0.11, side), "hand_l", "straw", "basket")
    if not far:
        B.add(*prism(gl + Vector((0.0, 0, 0)), gl + Vector((0.12, 0, 0)), 0.012, 0.012, 4), "hand_l", "straw", "basket")
    # loads held in front of the chest (the carry clips hold the arms round them)
    s3c = s3 + Vector((0, -0.24, -0.08))
    B.add(*rbox(s3c, (0.34, 0.2, 0.3), 0), "spine_03", "straw", "sack")
    if far:
        B.add(*box(s3c + Vector((0, 0, 0.1)), (0.64, 0.18, 0.15)), "spine_03", "wood", "log")
    else:
        for k in (-1, 0, 1):
            lift = 0.075 if k == 0 else 0.0
            p0 = s3c + Vector((-0.32, 0.05 * k, 0.07 + lift))
            p1 = s3c + Vector((0.32, 0.05 * k, 0.07 + lift))
            B.add(*prism(p0, p1, 0.045, 0.045, side), "spine_03", "wood", "log")
    # a babe in arms: a swaddled bundle and a little head against the chest
    B.add(*rbox(s3 + Vector((0, -0.2, -0.1)), (0.3, 0.16, 0.17), 0), "spine_03", "white", "babe")
    if not far:
        B.add(*rbox(s3 + Vector((0.16, -0.2, -0.07)), (0.12, 0.12, 0.12), 0), "spine_03", "skin", "babe")
    return B

def make_object(B, name):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in B.verts], [], B.faces)
    me.update()
    uvl = me.uv_layers.new(name="UVMap")
    li = 0
    for fi, poly in enumerate(me.polygons):
        for k, loop_index in enumerate(poly.loop_indices):
            uvl.data[loop_index].uv = B.uvs[fi][k]
    for poly in me.polygons:
        poly.use_smooth = False
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    # rigid weights: every vertex to its part's bone
    groups = {}
    for i, bname in enumerate(B.vbone):
        g = groups.get(bname)
        if g is None:
            g = ob.vertex_groups.new(name=bname)
            groups[bname] = g
        g.add([i], 1.0, "REPLACE")
    ob.parent = arm
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    # a flat material (the shader colours by UV); recalculate normals outward
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    mat = bpy.data.materials.get("villager") or bpy.data.materials.new("villager")
    me.materials.append(mat)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    return ob, tris

# ------------------------------------------------------------------------------------------
# Animations: keep the chosen clips (renamed), make carry clips (arms holding a load in front)
def carry_from(src_name, new_name):
    src = bpy.data.actions.get(src_name)
    if not src:
        return None
    arm.animation_data_create()
    arm.animation_data.action = src
    f0, f1 = int(src.frame_range[0]), int(src.frame_range[1])
    new = bpy.data.actions.new(new_name)
    pb = arm.pose.bones
    adds = {
        # upper arms a little forward, elbows bent so the forearms hold the load in front
        # (local x swings an arm forward: left +, right -)
        "upperarm_l": Euler((math.radians(30), 0.0, 0.0), "XYZ"),
        "upperarm_r": Euler((math.radians(-30), 0.0, 0.0), "XYZ"),
        "lowerarm_l": Euler((math.radians(80), 0.0, 0.0), "XYZ"),
        "lowerarm_r": Euler((math.radians(-80), 0.0, 0.0), "XYZ"),
    }
    frames = []
    for f in range(f0, f1 + 1):
        bpy.context.scene.frame_set(f)
        pose = {}
        for b in pb:
            q = b.rotation_quaternion.copy() if b.rotation_mode == "QUATERNION" else b.rotation_euler.to_quaternion()
            pose[b.name] = (b.location.copy(), q, b.scale.copy())
        frames.append((f, pose))
    arm.animation_data.action = new
    for f, pose in frames:
        for b in pb:
            loc, q, sc = pose[b.name]
            if b.name in adds:
                q = q @ adds[b.name].to_quaternion()
            b.rotation_mode = "QUATERNION"
            b.location, b.rotation_quaternion, b.scale = loc, q, sc
            b.keyframe_insert("location", frame=f)
            b.keyframe_insert("rotation_quaternion", frame=f)
            b.keyframe_insert("scale", frame=f)
    return new

keep = {}
for a in bpy.data.actions:
    if a.name in CLIPS:
        keep[a.name] = CLIPS[a.name]
made = []
for src, dst in (("Walk_Loop", "carry_walk"), ("Idle_Loop", "carry_idle")):
    if carry_from(src, dst):
        made.append(dst)
for a in list(bpy.data.actions):
    if a.name in keep:
        a.name = keep[a.name]
    elif a.name not in made:
        bpy.data.actions.remove(a)
for a in bpy.data.actions:
    a.use_fake_user = True
print("ACTIONS", sorted(a.name for a in bpy.data.actions))

# ------------------------------------------------------------------------------------------
def export(lod, path):
    B = build(lod)
    ob, tris = make_object(B, "villager_lod%d" % lod)
    for o in bpy.data.objects:
        o.select_set(o == ob or o == arm)
    bpy.context.view_layer.objects.active = arm
    arm.animation_data.action = None
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True,
        export_animations=(lod == 0), export_animation_mode="ACTIONS", export_force_sampling=True,
        export_optimize_animation_size=False, export_frame_step=1, export_skins=True, export_def_bones=False,
        export_yup=True, export_apply=False, export_texcoords=True, export_normals=True, export_materials="EXPORT",
        export_morph=False, export_reset_pose_bones=True, export_anim_single_armature=True)
    print("EXPORT", path, "tris", tris, "verts", len(B.verts))
    per = {}
    for fi, f in enumerate(B.faces):
        part = B.fpart[fi] if hasattr(B, "fpart") else "?"
        per[part] = per.get(part, 0) + (len(f) - 2)
    typical = ["body", "head", "eyes", "legs_trousers", "feet", "hair_short", "tunic", "belt", "hat_cap", "axe"]
    print("PARTS", lod, sorted(per.items()), "typical (man, cap, axe):", sum(per.get(k, 0) for k in typical))
    return ob, tris

stats = []
objs = []
for lod in (0, 1, 2):
    ob, tris = export(lod, os.path.join(OUT, "villager_lod%d.glb" % lod))
    stats.append((lod, tris))
    objs.append(ob)
    if lod < 2:
        ob.hide_render = True
        ob.hide_set(True)

# ------------------------------------------------------------------------------------------
# Preview renders (a line-up of looks at rest pose, and a few animation frames)
if PREVIEW:
    import random
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 900, 500
    ob0 = objs[0]
    ob0.hide_render = False
    ob0.hide_set(False)
    for o in objs[1:]:
        o.hide_render = True
    # vertex colours from UV slots, for the preview only
    PAL = {0: (0.95, 0.78, 0.62), 1: (0.35, 0.24, 0.15), 2: (0.70, 0.45, 0.30), 3: (0.35, 0.40, 0.55),
           4: (0.55, 0.25, 0.25), 5: (0.25, 0.18, 0.12), 6: (0.55, 0.38, 0.22), 7: (0.7, 0.72, 0.75),
           8: (0.88, 0.76, 0.42), 9: (0.1, 0.1, 0.1), 10: (0.92, 0.9, 0.85)}
    print("PREVIEW", PREVIEW, stats)
