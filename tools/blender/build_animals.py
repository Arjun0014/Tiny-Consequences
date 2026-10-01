# Tiny Consequences farm animals (docs/v3/20_CHARACTER_PIPELINE.md §4, roadmap v3 R3).
#
#   blender -b --factory-startup -P tools/blender/build_animals.py -- [--out assets/animals] [--only sheep,cow]
#
# From the CC0 Farm Animals Pack (Quaternius, assets/3d-models/animals_quaternius/Blends): each rig
# (24-28 bones, IK legs) and its clips (Idle, Walk, WalkSlow, Run, Death) plus two made here from
# Idle: `graze` (neck and head down to the grass) and `lie` (the body lowered, the legs folding
# under it through their IK). Every vertex's colour is baked into its UV:
#   u = (r7 * 16384 + g7 * 128 + b7 + 0.5) / 2^21   (7 bits a channel, from the face's material)
#   v = 0.5 / 64
# so the animal shader (game/render/shaders/animal.vp) needs no per-instance constants and every
# animal of one variant and LOD is drawn in one instanced draw call. Variants are separate meshes
# on the same skeleton: a sheep in its fleece and shorn (the wool drawn in close to the body), dark
# sheep, goats (the shorn sheep's frame with horns and a beard), red and pied cattle, pink and dark
# pigs. LODs by decimation, colours assigned after it (faces keep their materials): near (as made),
# mid (50%), far (22%). The first variant's near LOD of a species carries the animations; the .model
# files of the other variants and LODs use its skeleton and animation set.
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix, Quaternion

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
def arg(name, default=None):
    if name in argv:
        return argv[argv.index(name) + 1]
    return default

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "assets", "3d-models", "animals_quaternius", "Blends")
OUT = os.path.join(ROOT, arg("--out", "assets/animals"))
ONLY = set(arg("--only", "").split(",")) - {""}
os.makedirs(OUT, exist_ok=True)

LODS = [(0, 1.0), (1, 0.5), (2, 0.22)]
CLIPS = {"Idle": "idle", "Walk": "walk", "WalkSlow": "walk_slow", "Run": "run", "Death": "death"}
HORN = (0.66, 0.60, 0.50)

SPECIES = [
    dict(name="sheep", blend="Sheep.blend", variants=[
        dict(id="sheep", colours={"White": (0.92, 0.90, 0.84), "Black": (0.14, 0.13, 0.12)}),
        dict(id="sheep_shorn", shorn=0.66, colours={"White": (0.82, 0.74, 0.66), "Black": (0.14, 0.13, 0.12)}),
        dict(id="sheep_dark", colours={"White": (0.33, 0.27, 0.23), "Black": (0.10, 0.09, 0.08)}),
        dict(id="sheep_dark_shorn", shorn=0.66, colours={"White": (0.38, 0.32, 0.27), "Black": (0.10, 0.09, 0.08)}),
    ]),
    dict(name="goat", blend="Sheep.blend", variants=[
        dict(id="goat", shorn=0.66, horns=True, beard=True, colours={"White": (0.50, 0.36, 0.23), "Black": (0.18, 0.13, 0.10)}),
        dict(id="goat_pied", shorn=0.66, horns=True, beard=True, colours={"White": (0.86, 0.84, 0.78), "Black": (0.22, 0.17, 0.13)}),
    ]),
    dict(name="cow", blend="Cow.blend", variants=[
        dict(id="cow", colours={"White": (0.60, 0.34, 0.19), "Black": (0.32, 0.19, 0.12), "Pink": (0.80, 0.52, 0.48)}),
        dict(id="cow_pied", colours={"White": (0.88, 0.86, 0.80), "Black": (0.13, 0.12, 0.11), "Pink": (0.82, 0.54, 0.52)}),
    ]),
    dict(name="pig", blend="Pig.blend", variants=[
        dict(id="pig", colours={"Material.003": (0.86, 0.60, 0.56), "Material": (0.42, 0.25, 0.21)}),
        dict(id="pig_dark", colours={"Material.003": (0.36, 0.27, 0.23), "Material": (0.20, 0.14, 0.12)}),
    ]),
]

def pack(c):
    r, g, b = [max(0, min(127, int(round(x * 127)))) for x in c]
    return (r * 16384 + g * 128 + b + 0.5) / 2097152.0

def set_action(arm, act):
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = act
    if act is not None and hasattr(arm.animation_data, "action_slot") and len(act.slots) > 0:
        arm.animation_data.action_slot = act.slots[0]

def derive(arm, src_name, new_name, rot_deltas, loc_deltas):
    """A new clip from src: each frame's pose with extra rotations (armature-space axis-angle per
    bone: (axis, degrees)) and translations (armature space) added to the named bones."""
    src = bpy.data.actions.get(src_name)
    if not src:
        return None
    set_action(arm, src)
    f0, f1 = int(src.frame_range[0]), int(src.frame_range[1])
    pb = arm.pose.bones
    frames = []
    for f in range(f0, f1 + 1):
        bpy.context.scene.frame_set(f)
        pose = {}
        for b in pb:
            q = b.rotation_quaternion.copy() if b.rotation_mode == "QUATERNION" else b.rotation_euler.to_quaternion()
            pose[b.name] = (b.location.copy(), q, b.scale.copy())
        frames.append((f, pose))
    new = bpy.data.actions.new(new_name)
    set_action(arm, new)
    for f, pose in frames:
        for b in pb:
            loc, q, sc = pose[b.name]
            R = b.bone.matrix_local.to_quaternion()
            if b.name in rot_deltas:
                axis, deg = rot_deltas[b.name]
                qa = Quaternion(Vector(axis), math.radians(deg))
                q = q @ (R.inverted() @ qa @ R)
            if b.name in loc_deltas:
                d = Vector(loc_deltas[b.name])
                loc = loc + R.inverted() @ d
            b.rotation_mode = "QUATERNION"
            b.location, b.rotation_quaternion, b.scale = loc, q, sc
            b.keyframe_insert("location", frame=f)
            b.keyframe_insert("rotation_quaternion", frame=f)
            b.keyframe_insert("scale", frame=f)
    return new

def dominant_group(ob, v):
    best, bw = None, 0.0
    for g in v.groups:
        if g.weight > bw:
            best, bw = ob.vertex_groups[g.group].name, g.weight
    return best

def head_box(ob):
    me = ob.data
    pts = [v.co.copy() for v in me.vertices if dominant_group(ob, v) == "Head"]
    xs, ys, zs = [p.x for p in pts], [p.y for p in pts], [p.z for p in pts]
    return min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)

def cone(bm, base, tip, r, sides, group_idx, deform, mat_index):
    a = (tip - base).normalized()
    ref = Vector((0, 0, 1)) if abs(a.z) < 0.9 else Vector((1, 0, 0))
    x = a.cross(ref).normalized()
    y = a.cross(x).normalized()
    ring = []
    for k in range(sides):
        ang = 2 * math.pi * k / sides
        v = bm.verts.new(base + x * (math.cos(ang) * r) + y * (math.sin(ang) * r))
        ring.append(v)
    t = bm.verts.new(tip)
    for v in ring + [t]:
        v[deform][group_idx] = 1.0
    for k in range(sides):
        f = bm.faces.new((ring[k], ring[(k + 1) % sides], t))
        f.material_index = mat_index
    f = bm.faces.new(list(reversed(ring)))
    f.material_index = mat_index

def shape_variant(ob, var):
    """Shorn wool, horns and beard on a copy of the species mesh (armature space, rest pose)."""
    me = ob.data
    if var.get("shorn"):
        s = var["shorn"]
        body = {"Body", "Hips", "Torso", "Back", "Shoulders", "root"}
        white = set()
        mi = {m.name: i for i, m in enumerate(me.materials)}
        for p in me.polygons:
            if p.material_index == mi.get("White"):
                white.update(p.vertices)
        pts = [me.vertices[i].co for i in white if dominant_group(ob, me.vertices[i]) in body]
        cx = sum(p.x for p in pts) / len(pts)
        cy = sum(p.y for p in pts) / len(pts)
        cz = sum(p.z for p in pts) / len(pts)
        for i in white:
            v = me.vertices[i]
            if dominant_group(ob, v) in body:
                v.co.x = cx + (v.co.x - cx) * s
                v.co.z = cz + (v.co.z - cz) * (0.5 + 0.5 * s)
                v.co.y = cy + (v.co.y - cy) * (0.82 + 0.18 * s)
    if var.get("horns") or var.get("beard"):
        if "Horn" not in [m.name for m in me.materials]:
            me.materials.append(bpy.data.materials.get("Horn") or bpy.data.materials.new("Horn"))
        hmi = [m.name for m in me.materials].index("Horn")
        dmi = [m.name for m in me.materials].index("Black") if "Black" in [m.name for m in me.materials] else 0
        x0, x1, y0, y1, z0, z1 = head_box(ob)
        w, L, H = x1 - x0, y1 - y0, z1 - z0
        cx = (x0 + x1) / 2
        bm = bmesh.new()
        bm.from_mesh(me)
        deform = bm.verts.layers.deform.verify()
        gi = ob.vertex_groups["Head"].index
        if var.get("horns"):
            for sx in (-1, 1):
                base = Vector((cx + sx * w * 0.2, y0 + L * 0.62, z1 - H * 0.06))
                tip = base + Vector((sx * w * 0.16, L * 0.38, H * 0.42))
                cone(bm, base, tip, w * 0.09, 5, gi, deform, hmi)
        if var.get("beard"):
            base = Vector((cx, y0 + L * 0.2, z0 + H * 0.12))
            tip = base + Vector((0, L * 0.05, -H * 0.42))
            cone(bm, base, tip, w * 0.08, 4, gi, deform, dmi)
        bm.to_mesh(me)
        bm.free()
        me.update()

def bake_uv(ob, colours):
    me = ob.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers[0].data
    names = [m.name if m else "-" for m in me.materials]
    for p in me.polygons:
        name = names[p.material_index] if p.material_index < len(names) else "-"
        c = HORN if name == "Horn" else colours.get(name, (1.0, 0.0, 1.0))
        u = pack(c)
        for li in p.loop_indices:
            uv[li].uv = (u, 0.5 / 64.0)
    # keep only the first UV layer; one material ("animal": the colours are in the UVs)
    while len(me.uv_layers) > 1:
        me.uv_layers.remove(me.uv_layers[-1])
    for p in me.polygons:
        p.material_index = 0
    me.materials.clear()
    me.materials.append(bpy.data.materials.get("animal") or bpy.data.materials.new("animal"))

def export(arm, ob, path, anims):
    for o in bpy.data.objects:
        o.select_set(o == ob or o == arm)
    bpy.context.view_layer.objects.active = arm
    set_action(arm, None)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True,
        export_animations=anims, export_animation_mode="ACTIONS", export_force_sampling=True,
        export_optimize_animation_size=False, export_frame_step=1, export_skins=True, export_def_bones=False,
        export_yup=True, export_apply=False, export_texcoords=True, export_normals=True, export_materials="EXPORT",
        export_morph=False, export_reset_pose_bones=True, export_anim_single_armature=True)

report = []
for sp in SPECIES:
    if ONLY and sp["name"] not in ONLY:
        continue
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SRC, sp["blend"]))
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    base = next(o for o in bpy.data.objects if o.type == "MESH" and o.parent == arm)
    assert arm.matrix_world == Matrix.Identity(4), "armature must sit at the origin unscaled"
    # the mesh in armature space at rest (its object transform baked into the vertices)
    base.data.transform(base.matrix_world)
    base.matrix_parent_inverse = Matrix.Identity(4)
    base.location, base.rotation_euler, base.scale = (0, 0, 0), (0, 0, 0), (1, 1, 1)
    # clips: keep and rename, derive graze and lie from Idle
    gz = derive(arm, "Idle", "graze", {"Shoulders": ((1, 0, 0), 14), "Neck": ((1, 0, 0), 86), "Head": ((1, 0, 0), -58)}, {})
    body = arm.data.bones["Body"]
    leg = (body.head_local.z - arm.data.bones["FrontFoot.R"].head_local.z)
    lie = derive(arm, "Idle", "lie", {"Neck": ((1, 0, 0), -6), "Head": ((1, 0, 0), 12)}, {"Body": (0, 0, -leg * 0.52)})
    for a in list(bpy.data.actions):
        if a.name in CLIPS:
            a.name = CLIPS[a.name]
        elif a.name not in ("graze", "lie"):
            bpy.data.actions.remove(a)
    for a in bpy.data.actions:
        a.use_fake_user = True
    # rest height and length (armature units) for the view's scale
    zs = [v.co.z for v in base.data.vertices]
    ys = [v.co.y for v in base.data.vertices]
    report.append("%s: height %.3f length %.3f actions %s" % (sp["name"], max(zs) - min(zs), max(ys) - min(ys),
        sorted(a.name for a in bpy.data.actions)))
    for vi, var in enumerate(sp["variants"]):
        for lod, ratio in LODS:
            ob = base.copy()
            ob.data = base.data.copy()
            ob.name = "%s_lod%d" % (var["id"], lod)
            bpy.context.scene.collection.objects.link(ob)
            shape_variant(ob, var)
            if ratio < 1.0:
                dec = ob.modifiers.new("Decimate", "DECIMATE")
                dec.ratio = ratio
                while ob.modifiers[0].name != "Decimate":
                    bpy.context.view_layer.objects.active = ob
                    bpy.ops.object.modifier_move_up(modifier="Decimate")
                bpy.context.view_layer.objects.active = ob
                bpy.ops.object.modifier_apply(modifier="Decimate")
            bake_uv(ob, var["colours"])
            tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
            path = os.path.join(OUT, "%s_lod%d.glb" % (var["id"], lod))
            export(arm, ob, path, anims=(vi == 0 and lod == 0))
            report.append("  %s lod%d: %d triangles%s" % (var["id"], lod, tris, " (+animations)" if vi == 0 and lod == 0 else ""))
            bpy.data.objects.remove(ob, do_unlink=True)
for r in report:
    print("ANIMALS", r)
