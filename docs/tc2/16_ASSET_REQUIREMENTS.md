# TC2-16 — Asset requirements

Early work never waits for assets: procedural geometry and generated placeholders first. Only CC0, OFL or
MIT assets (or assets Arjun buys and licenses explicitly); every third-party asset recorded in
`unity/TinyConsequences2/Assets/TC2/CREDITS.md`. Never Tiny Skies' assets (no licence).

Already in the repository (CC0, from TC1, reusable): Quaternius Universal Animation Library (65-bone
skeleton, 43 clips; `assets/3d-models/ual/`), Quaternius Farm Animals (`assets/3d-models/animals_quaternius/`),
TC1's Blender build scripts (`tools/blender/`), fonts Fredoka and Nunito (OFL, `assets/fonts/`).
Unity imports FBX natively; GLB needs glTFast (registry download) or a Blender conversion to FBX.

| # | Asset | Why | Technical requirements | Recommended sources | Free/paid | Licence | Priority | Needed by |
|---|---|---|---|---|---|---|---|---|
| A1 | Stylised human base meshes (male/female/child/elder) | believable villagers close in | <= 1.5K tris LOD0, humanoid rig compatible with UAL clips, separate parts for clothing variants | build on UAL skeleton (CC0) in Blender (TC1 pipeline); Quaternius "Ultimate Modular Men/Women" (CC0) | free | CC0 | high | F3 |
| A2 | Animation clips (walk, run, carry, hoe, chop, sit, sleep, talk, fight, swim, row) | visible work | baked to vertex animation textures for instancing | UAL (CC0) + clips derived in Blender | free | CC0 | high | F3 |
| A3 | Animals (sheep, goat, cattle, pig, dog, horse, deer, wolf, boar, birds, fish) | ecology and herds visible | <= 1K tris, 4-6 clips each | Quaternius Animals packs (CC0); Kenney (CC0) | free | CC0 | high | F2/F3 |
| A4 | Tree and plant kit (broadleaf, conifer, shrub, grass clumps, crops by stage) | forests that feel like forests | 3 LODs + impostor, wind sway in vertex shader | procedural generation first; Quaternius "Stylized Nature" (CC0) as reference/fallback | free | CC0 | medium | F1/F10 |
| A5 | Modular building kit (timber, wattle, stone; walls, roofs, posts, doors, towers, gates, keep modules, bridges) | settlements and castles that grow | snapping grid, kit pieces <= 500 tris, trim-sheet texture | procedural kit first (Editor generator); Kenney "Castle Kit"/"Survival Kit" (CC0) as reference | free | CC0 | high | F3/F6 |
| A6 | Terrain material textures (rock, soil, sand, grass, snow) | lovely terrain close in | tileable 512-1K, albedo+normal(+mask) | ambientCG (CC0), Poly Haven (CC0) | free | CC0 | medium | F1 |
| A7 | Water normal maps / foam | convincing water | tileable 512 | procedural first; ambientCG (CC0) | free | CC0 | low | F1 |
| A8 | Fonts (titles, body, handwriting for the History Book) | readable, characterful UI | TTF/OTF with Latin Extended | Google Fonts (OFL): Fredoka, Nunito (have), a book serif (e.g. Fraunces, EB Garamond) | free | OFL | medium | F7 |
| A9 | Ambient and action audio | the world's sound | Ogg, normalised | TC1's ElevenLabs library (check account terms before publishing); freesound CC0 | free/paid | CC0 / account terms | low | F10 |
| A10 | Music (optional) | mood | loops | Arjun's choice | either | must be explicit | low | F10 |

When a high-quality external asset would materially improve the game but needs a download, a licence
acceptance, a purchase or a login, it is listed here with the exact need and work continues with
placeholders. Nothing in this list blocks the foundation.
