# Render a line-up of animals from assets/animals/*.glb (colours decoded from the UV the way
# game/render/shaders/animal.vp does), each posed by one of its clips at a frame.
#   blender -b --factory-startup -P tools/blender/preview_animals.py -- OUT.png ANIM_GLB entry entry ...
# entry = variant_lodN:clip:frame (e.g. sheep_shorn_lod0:graze:20); ANIM_GLB = the glb holding the
# clips (e.g. sheep_lod0). The camera looks from the animals' right side.
import bpy, bmesh, sys, os, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
OUTP = os.path.abspath(argv[0])
ANIM = argv[1]
ENTRIES = argv[2:]
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DIR = os.path.join(ROOT, "assets", "animals")

def unpack(u):
    p = int(math.floor(u * 2097152.0))
    r, g, b = p // 16384, (p // 128) % 128, p % 128
    return (r / 127.0, g / 127.0, b / 127.0)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
# the clips
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=os.path.join(DIR, ANIM + ".glb"))
for o in [o for o in bpy.data.objects if o not in before]:
    bpy.data.objects.remove(o, do_unlink=True)
acts = {a.name.split("_")[0] if False else a.name: a for a in bpy.data.actions}
print("ACTIONS", sorted(acts))
spacing = None
for i, e in enumerate(ENTRIES):
    name, clip, frame = e.split(":")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(DIR, name + ".glb"))
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == "ARMATURE")
    mesh = next(o for o in new if o.type == "MESH" and o.parent == arm)
    for o in new:
        if o.type == "MESH" and o != mesh:
            bpy.data.objects.remove(o, do_unlink=True)
    if spacing is None:
        spacing = max(mesh.dimensions) * 1.05
    arm.location.x = (i - (len(ENTRIES) - 1) / 2) * spacing
    arm.rotation_mode = "XYZ"
    arm.rotation_euler.z = math.radians(-90)   # side on: heads to the right
    me = mesh.data
    bm = bmesh.new()
    bm.from_mesh(me)
    uvl = bm.loops.layers.uv[0]
    col = bm.loops.layers.color.new("Col")
    for f in bm.faces:
        c = unpack(f.loops[0][uvl].uv[0])
        for l in f.loops:
            l[col] = (c[0], c[1], c[2], 1.0)
    bm.to_mesh(me)
    bm.free()
    me.color_attributes.active_color_name = "Col"
    me.color_attributes.render_color_index = me.color_attributes.active_color_index
    act = None
    for a in bpy.data.actions:
        if a.name == clip or a.name.startswith(clip + "_") or a.name.endswith("|" + clip):
            act = a
    if act:
        if arm.animation_data is None:
            arm.animation_data_create()
        # (a copy per animal so each keeps its own frame)
        a2 = act.copy()
        arm.animation_data.action = a2
        if hasattr(arm.animation_data, "action_slot") and len(a2.slots) > 0:
            arm.animation_data.action_slot = a2.slots[0]
        # shift the copy so `frame` lands on the scene frame 100
        try:
            for layer in a2.layers:
                for strip in layer.strips:
                    for cb in strip.channelbags:
                        for fc in cb.fcurves:
                            for kp in fc.keyframe_points:
                                kp.co.x += 100 - int(frame)
                                kp.handle_left.x += 100 - int(frame)
                                kp.handle_right.x += 100 - int(frame)
        except Exception as ex:
            print("SHIFT", ex)
    else:
        print("NO CLIP", clip, "in", sorted(a.name for a in bpy.data.actions))
scene.frame_set(100)
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, 0))
plane = bpy.context.active_object
pm = bpy.data.materials.new("ground")
pm.diffuse_color = (0.45, 0.55, 0.32, 1)
plane.data.materials.append(pm)
cam = bpy.data.cameras.new("cam")
camo = bpy.data.objects.new("cam", cam)
scene.collection.objects.link(camo)
width = spacing * len(ENTRIES)
camo.location = (0, -width * 1.1, width * 0.22)
camo.rotation_euler = (math.radians(80), 0, 0)
cam.lens = 40
scene.camera = camo
scene.render.engine = "BLENDER_WORKBENCH"
sh = scene.display.shading
sh.light = "STUDIO"
sh.color_type = "VERTEX"
sh.show_shadows = True
scene.render.resolution_x, scene.render.resolution_y = 1500, 480
scene.render.filepath = OUTP
bpy.ops.render.render(write_still=True)
print("RENDERED", OUTP)
