# Render a line-up of villagers from assets/characters/villager_lod*.glb (palette and part
# variants applied the way the game's shader does), posed from their animations.
#   blender -b --factory-startup -P tools/blender/preview_villagers.py -- out/s9/villagers.png [lod]
import bpy, bmesh, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
OUTP = os.path.abspath(argv[0])
LOD = int(argv[1]) if len(argv) > 1 else 0
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PARTS = [
    "body", "head", "eyes", "legs_trousers", "legs_bare", "feet",
    "hair_short", "hair_long", "hair_bun", "beard",
    "hat_straw", "hat_hood", "hat_cap", "headscarf",
    "tunic", "skirt", "dress", "apron", "cloak", "belt",
    "axe", "hoe", "sickle", "hammer", "basket", "sack", "bucket", "log", "rod", "spear", "shield", "flail",
    "babe",
]
# (parts shown, palette {slot: rgb}, animation, frame, scale)
LOOKS = [
    ({"body", "head", "eyes", "legs_trousers", "feet", "hair_short", "hat_straw", "tunic", "belt", "hoe"},
     {0: (0.95, 0.78, 0.62), 1: (0.45, 0.30, 0.16), 2: (0.80, 0.68, 0.36), 3: (0.42, 0.36, 0.28), 4: (0.6, 0.3, 0.2)}, "swing", 12, 1.0),
    ({"body", "head", "eyes", "legs_bare", "feet", "hair_bun", "headscarf", "dress", "apron", "basket"},
     {0: (0.87, 0.67, 0.50), 1: (0.25, 0.17, 0.12), 2: (0.55, 0.30, 0.32), 3: (0.62, 0.36, 0.38), 4: (0.85, 0.78, 0.62)}, "walk", 8, 0.97),
    ({"body", "head", "eyes", "legs_trousers", "feet", "hair_short", "beard", "hat_cap", "tunic", "belt", "axe"},
     {0: (0.64, 0.46, 0.33), 1: (0.22, 0.16, 0.12), 2: (0.30, 0.50, 0.30), 3: (0.35, 0.30, 0.25), 4: (0.30, 0.35, 0.25)}, "idle", 0, 1.03),
    ({"body", "head", "eyes", "legs_trousers", "feet", "hair_short", "tunic", "belt", "log"},
     {0: (0.95, 0.78, 0.62), 1: (0.62, 0.42, 0.22), 2: (0.72, 0.40, 0.32), 3: (0.40, 0.40, 0.48), 4: (0.5, 0.4, 0.3)}, "carry_walk", 10, 1.0),
    ({"body", "head", "eyes", "legs_bare", "feet", "hair_long", "skirt", "tunic", "bucket"},
     {0: (0.98, 0.84, 0.70), 1: (0.85, 0.72, 0.42), 2: (0.86, 0.56, 0.62), 3: (0.55, 0.45, 0.62), 4: (0.6, 0.5, 0.3)}, "walk", 20, 0.62),
    ({"body", "head", "eyes", "legs_trousers", "feet", "hair_short", "hat_hood", "cloak", "tunic", "belt"},
     {0: (0.87, 0.67, 0.50), 1: (0.86, 0.86, 0.84), 2: (0.45, 0.42, 0.55), 3: (0.35, 0.33, 0.32), 4: (0.40, 0.28, 0.22)}, "walk_slow", 6, 0.95),
    ({"body", "head", "eyes", "legs_trousers", "feet", "hair_short", "tunic", "belt", "sack"},
     {0: (0.64, 0.46, 0.33), 1: (0.20, 0.15, 0.12), 2: (0.62, 0.58, 0.70), 3: (0.40, 0.33, 0.25), 4: (0.6, 0.4, 0.3)}, "carry_walk", 2, 0.85),
    ({"body", "head", "eyes", "legs_trousers", "feet", "hair_short", "hat_straw", "tunic", "belt", "sickle"},
     {0: (0.95, 0.78, 0.62), 1: (0.33, 0.24, 0.17), 2: (0.80, 0.70, 0.40), 3: (0.42, 0.36, 0.28), 4: (0.6, 0.3, 0.2)}, "crouch_walk", 10, 1.0),
]
FIXED = {5: (0.28, 0.20, 0.13), 6: (0.55, 0.38, 0.22), 7: (0.72, 0.74, 0.78), 8: (0.88, 0.76, 0.42),
         9: (0.08, 0.07, 0.07), 10: (0.93, 0.91, 0.86)}

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
spacing = 1.0
for i, (parts, pal, anim, frame, sc) in enumerate(LOOKS):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(ROOT, "assets", "characters", "villager_lod%d.glb" % LOD))
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == "ARMATURE")
    mesh = next(o for o in new if o.type == "MESH" and o.name.startswith("villager"))
    for o in new:
        if o.type == "MESH" and not o.name.startswith("villager"):
            bpy.data.objects.remove(o, do_unlink=True)
    arm.location.x = (i - (len(LOOKS) - 1) / 2) * spacing
    arm.scale = (sc, sc, sc)
    # drop hidden parts, colour the rest
    me = mesh.data
    uv = me.uv_layers[0].data
    bm = bmesh.new()
    bm.from_mesh(me)
    uvl = bm.loops.layers.uv[0]
    col = bm.loops.layers.color.new("Col")
    kill = []
    for f in bm.faces:
        u, v = f.loops[0][uvl].uv
        slot = int(u * 16)
        part = PARTS[min(len(PARTS) - 1, int(v * 64))]
        if part not in parts:
            kill.append(f)
            continue
        c = pal.get(slot) or FIXED.get(slot) or (1, 0, 1)
        for l in f.loops:
            l[col] = (c[0], c[1], c[2], 1.0)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bm.to_mesh(me)
    bm.free()
    me.color_attributes.active_color_name = "Col"
    me.color_attributes.render_color_index = me.color_attributes.active_color_index
    act = bpy.data.actions.get(anim)
    if not act:
        for a in bpy.data.actions:
            if a.name.startswith(anim):
                act = a
    if act and arm.animation_data is None:
        arm.animation_data_create()
    if act:
        arm.animation_data.action = act
        if hasattr(arm.animation_data, "action_slot") and act.slots:
            arm.animation_data.action_slot = act.slots[0]
    arm["frame"] = frame
scene.frame_set(10)
# a ground plane, light and camera
bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, 0))
plane = bpy.context.active_object
pm = bpy.data.materials.new("ground")
pm.diffuse_color = (0.45, 0.55, 0.32, 1)
plane.data.materials.append(pm)
cam = bpy.data.cameras.new("cam")
camo = bpy.data.objects.new("cam", cam)
scene.collection.objects.link(camo)
camo.location = (0, -float(argv[2]) if len(argv) > 2 else -7.5, 2.4)
camo.rotation_euler = (math.radians(80), 0, 0)
cam.lens = 38
scene.camera = camo
scene.render.engine = "BLENDER_WORKBENCH"
sh = scene.display.shading
sh.light = "STUDIO"
sh.color_type = "VERTEX"
sh.show_shadows = True
sh.show_cavity = True
scene.render.resolution_x, scene.render.resolution_y = 1400, 520
scene.render.filepath = OUTP
bpy.ops.render.render(write_still=True)
print("RENDERED", OUTP)
