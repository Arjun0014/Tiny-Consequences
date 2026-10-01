# Inspect a glTF file: objects, meshes (tris), armature bones, actions. Usage:
#   blender -b --factory-startup -P tools/blender/inspect_glb.py -- file.glb
import bpy, sys
args = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
for path in args:
    if path.lower().endswith((".glb", ".gltf")):
        bpy.ops.import_scene.gltf(filepath=path)
    elif path.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=path)
    elif path.lower().endswith(".obj"):
        bpy.ops.wm.obj_import(filepath=path)
    elif path.lower().endswith(".blend"):
        bpy.ops.wm.open_mainfile(filepath=path)
    print("FILE", path)
    for o in bpy.data.objects:
        if o.type == "MESH":
            me = o.data
            me.calc_loop_triangles()
            print("  MESH", o.name, "verts", len(me.vertices), "tris", len(me.loop_triangles), "dims", tuple(round(d, 3) for d in o.dimensions), "parent", o.parent.name if o.parent else None, "vgroups", len(o.vertex_groups), "mats", [m.name for m in me.materials if m])
        elif o.type == "ARMATURE":
            print("  ARMATURE", o.name, "bones", len(o.data.bones))
            for b in o.data.bones:
                print("    BONE", b.name, "parent", b.parent.name if b.parent else None, "head", tuple(round(x, 3) for x in b.head_local), "tail", tuple(round(x, 3) for x in b.tail_local))
        else:
            print("  OBJ", o.type, o.name)
    print("  ACTIONS", len(bpy.data.actions))
    for a in bpy.data.actions:
        print("    ACTION", a.name, "frames", tuple(round(f, 1) for f in a.frame_range))
