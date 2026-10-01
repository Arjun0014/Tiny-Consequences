# V3-20 — Character and animal asset pipeline (D-078)

## 1. Sources (CC0, licences kept in `assets/3d-models/`)

| Set | What it is | Complexity | Use |
|---|---|---|---|
| Universal Animation Library [Standard] (Quaternius) | `UAL1_Standard.glb`: one 65-bone humanoid skeleton (`root`, `pelvis`, `spine_01-03`, `neck_01`, `Head`, arms, 30 finger bones, legs), 43 clips, a mannequin | mannequin 13,743 triangles | **skeleton + animations only** — the mannequin is deleted |
| Farm Animals Pack (Quaternius) | cow, horse, llama, pig, pug, sheep, zebra (`.blend`, `.fbx`, `.obj`), each rigged (24-28 bones) with Idle, Walk, WalkSlow, Run, Jump, Death | 562-796 triangles | already low-poly: near LOD as is; mid/far LODs by decimation (R3) |

## 2. Villagers: `tools/blender/build_villager.py` (Blender 5.2, headless)

```
blender -b --factory-startup -P tools/blender/build_villager.py -- --out assets/characters
blender -b --factory-startup -P tools/blender/preview_villagers.py -- out/s9/villagers_lod0.png 0 6.5
```

1. Import the UAL GLB; delete its meshes (the mannequin and the importer's bone-shape icosphere).
2. Build a **bespoke blocky villager** from the rest-pose bone positions: tapered boxes for hips,
   belly and chest, prisms for limbs, a rounded box head (big, toy-like), eyes and nose, hands and
   shoes. Every part is weighted **rigidly to one bone** (a figurine with jointed limbs).
3. **One mesh carries every optional part**: hair (short, long, bun), beard, hats (straw, hood,
   cap, headscarf), clothes (tunic hem, skirt, dress, apron, cloak, belt), tools (axe, hoe, sickle,
   hammer, flail, spear, shield, rod), loads (basket, bucket, sack, log bundle). Each vertex's UV
   encodes its **palette slot** (`u × 16`: skin, hair, upper, lower, accent, leather, wood, iron,
   straw, eyes, linen) and **part id** (`v × 64`).
4. Tools are held across the fist (front-back in the rest pose), so they point forward with the
   arm hanging and cut like a blade in the swing; buckets and baskets hang past the fist; loads are
   held against the chest.
5. **LODs** from the same builder with fewer segments: near 1,468 triangles (all parts; a
   typical villager shows ~600-800), mid 1,060, far 808. (Far LOD still carries every part; R2 will
   cut it to ~150-250 by dropping minor parts and merging limbs.)
6. **Animations**: 17 UAL clips renamed to what they show (`idle`, `walk`, `walk_slow`, `run`,
   `swing` (hoe/axe/flail), `kneel_work` (building, grubbing), `crouch_walk` (reaping, weeding,
   gathering), `crouch`, `interact` (drawing water), `pickup`, `talk`, `sit`, `throw`, `push`,
   `dance`, `jump`, `death`) + two made here, `carry_walk` and `carry_idle` (Walk/Idle with the
   upper arms forward and the elbows bent round a load: additive local-x rotations, baked).
   Exported **sampled** (Defold needs a matrix per bone per key) in `villager_lod0.glb`; the mid and
   far LOD models use lod0's skeleton and animation set — one skeleton, one animation library.

## 3. In Defold

- `assets/characters/villager_lod{0,1,2}.model` (`create_go_bones: false` — otherwise 65 game
  objects per model exhaust the collection), `game/view/villager.go` (three model components),
  `/main#villager_factory`.
- Material `game/render/villager.material` + `shaders/villager.vp`: GPU skinning
  (`/builtins/materials/skinning.glsl`, local vertex space, instancing attributes `mtx_world`,
  `animation_data`), the part mask and palette from material constants `u_pal`, `u_parts`, and the
  planet props' fragment shader (`props.fp`: the same sun, shade, fog, shadows). Shadow pass:
  `shadow_villager.material` (the same skinning and mask).
- **Engine finding**: custom per-instance vertex attributes (step function Instance) on a skinned
  instanced model abort Defold 1.13.1 (`size <= Capacity()` / out-of-bounds) — the palette lives in
  per-component constants instead: one draw call per visible villager (≤ 64), measured fine.
- `game/view/agents_view.lua`: a pool of villager objects; each frame a person's look (from sex,
  stage, role and key: dress or trousers, hair, beard, hat, apron, cloak, palette) plus what they
  hold **for the action the sim says they are doing** (hoe while hoeing, axe while felling, sickle
  while reaping, hammer while building, flail while threshing, basket while gathering or sowing,
  bucket while drawing water, logs or a sack while carrying them). The clip follows the action
  (walk / carry_walk / walk_slow for elders / run for children at play / swing / kneel_work /
  crouch_walk / talk / sit / jump), its rate follows walking speed × game speed (capped at 3).
  Scale: adults 0.04 (~0.073 world units tall), apprentices × 0.86, children × 0.62.
- LOD by camera zoom (≥ 1.42 near, ≥ 1.08 mid, else far; hidden below 0.75 and while skipping).

## 4. Animals (R3)

The farm animals are already within budget (≤ 800 triangles, 6 clips each). Plan: export each to
GLB with sampled clips, a mid LOD by Blender's decimate (~300) and a far LOD (~120); one shared
material with the villagers' lighting; wild species (deer, wolves) from the horse/llama skeletons
or the existing procedural movers until a CC0 wild set is found.
