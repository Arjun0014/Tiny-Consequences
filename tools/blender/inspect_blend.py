# Print what a .blend holds (objects, meshes with triangle counts and materials, armature bones,
# actions with frame ranges): blender -b FILE.blend --factory-startup -P tools/blender/inspect_blend.py
import bpy

for o in bpy.data.objects:
    line = "OBJ %s type=%s parent=%s loc=%s scale=%s" % (o.name, o.type, o.parent.name if o.parent else "-",
        tuple(round(v, 3) for v in o.location), tuple(round(v, 3) for v in o.scale))
    if o.type == "MESH":
        me = o.data
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        mats = [m.name if m else "-" for m in me.materials]
        cols = []
        for m in me.materials:
            if m and m.use_nodes:
                bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
                if bsdf:
                    c = bsdf.inputs["Base Color"].default_value
                    cols.append((m.name, tuple(round(x, 3) for x in c[:3])))
            elif m:
                cols.append((m.name, tuple(round(x, 3) for x in m.diffuse_color[:3])))
        per = {}
        for p in me.polygons:
            per[p.material_index] = per.get(p.material_index, 0) + len(p.vertices) - 2
        line += " verts=%d tris=%d mats=%s per=%s groups=%d" % (len(me.vertices), tris, mats, per, len(o.vertex_groups))
        line += "\n   colours " + str(cols)
        mods = [(m.type, getattr(m, "object", None) and m.object.name) for m in o.modifiers]
        line += "\n   modifiers " + str(mods)
        dims = o.dimensions
        line += "\n   dims " + str(tuple(round(v, 3) for v in dims))
    if o.type == "ARMATURE":
        line += " bones=%d" % len(o.data.bones)
        for b in o.data.bones:
            line += "\n   bone %s parent=%s head=%s tail=%s" % (b.name, b.parent.name if b.parent else "-",
                tuple(round(v, 3) for v in b.head_local), tuple(round(v, 3) for v in b.tail_local))
    print(line)
for a in bpy.data.actions:
    print("ACTION %s frames=%s fcurves=%d" % (a.name, tuple(a.frame_range), len(a.fcurves) if hasattr(a, "fcurves") else -1))
print("SCENE fps", bpy.context.scene.render.fps)
