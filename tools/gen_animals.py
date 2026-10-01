# Defold resources for the v3 farm animals (docs/v3/20_CHARACTER_PIPELINE.md §4): for every variant
# built by tools/blender/build_animals.py, three LOD models (assets/animals/VARIANT_lodN.model, the
# species' first variant carrying the skeleton and clips), a game object with the three
# (game/view/animals/VARIANT.go) and its factory; the materials; the factories in main.collection.
#   python tools/gen_animals.py
import os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
VARIANTS = {
    "sheep": ["sheep", "sheep_shorn", "sheep_dark", "sheep_dark_shorn"],
    "goat": ["goat", "goat_pied"],
    "cow": ["cow", "cow_pied"],
    "pig": ["pig", "pig_dark"],
}

def write(rel, text):
    p = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)

for species, vs in VARIANTS.items():
    anim = "/assets/animals/%s_lod0.glb" % vs[0]
    for v in vs:
        for lod in range(3):
            write("assets/animals/%s_lod%d.model" % (v, lod),
                'mesh: "/assets/animals/%s_lod%d.glb"\n'
                'materials {\n  name: "%s"\n  material: "/game/render/animal.material"\n}\n'
                'skeleton: "%s"\nanimations: "%s"\ndefault_animation: "idle"\nname: "%s_lod%d"\ncreate_go_bones: false\n'
                % (v, lod, "animal", anim, anim, v, lod))
        write("game/view/animals/%s.go" % v, "".join(
            'components {\n  id: "lod%d"\n  component: "/assets/animals/%s_lod%d.model"\n}\n' % (lod, v, lod) for lod in range(3)))
        write("game/view/animals/%s.factory" % v, 'prototype: "/game/view/animals/%s.go"\nload_dynamically: false\n' % v)

# main.collection: one factory per variant (id VARIANT_factory)
p = os.path.join(ROOT, "game", "main.collection")
s = open(p, encoding="utf-8").read()
s = re.sub(r'  "components \{\\n"\n  "  id: \\"animal_[a-z_]+_factory\\"\\n"\n  "  component: \\"[^"]+\\"\\n"\n  "\}\\n"\n', "", s)
anchor = '  "components {\\n"\n  "  id: \\"villager_factory\\"\\n"\n'
assert anchor in s, "villager_factory not found in main.collection"
add = ""
for vs in VARIANTS.values():
    for v in vs:
        add += ('  "components {\\n"\n  "  id: \\"animal_%s_factory\\"\\n"\n'
                '  "  component: \\"/game/view/animals/%s.factory\\"\\n"\n  "}\\n"\n') % (v, v)
s = s.replace(anchor, add + anchor)
open(p, "w", encoding="utf-8", newline="\n").write(s)
print("variants", sum(len(v) for v in VARIANTS.values()))
