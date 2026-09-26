# Art direction — target look (from Arjun, 27 Sep)

Arjun shared a Tiny Skies title-screen screenshot as **the look we are going for**: "a properly realistic miniature cute world". Tiny Skies is a **visual reference only** (no licence: never copy code or assets, D-002). This file records what makes that image work, so Stage 1.8 (rendering) and later polish stages build toward it. Check every screenshot pass against it, together with 06_PRESENTATION.md §6.

## What the reference does (and what we must match)

1. **The planet dominates the frame.** It fills ~70% of the screen height at the default/title view. The camera sits slightly above the equator, and the planet sits a little low in the frame with sky above.
2. **Sky gradient**: deep blue at the top (#2B6FC4) → bright cyan (#5FD0F0) in the middle → **warm pale-yellow glow at the bottom horizon** (#FFF1B8). Soft white **speed streaks / wisps** drift diagonally across the sky. A few soft, blurred cloud puffs float in front of and around the planet.
3. **Atmosphere**: a thin, bright, soft **white-cyan rim glow** hugs the planet's silhouette. The limb is bright, not dark.
4. **Ocean**: saturated medium blue with a **fine wave/ripple texture** (noise normal or animated pattern), light specular glints, **bright cyan shallow halos around every coast** (continental shelf), and slightly darker deep water. It is never flat colour.
5. **Land**: vivid lime/grass green with yellow-green variation and sandy beaches. It is **densely covered in tiny 3D trees**, thousands of them, clustered into forests. The trees make **bumpy silhouettes on the horizon/limb**, which is the single strongest "miniature diorama" cue. Small houses, farms and props read clearly at mid zoom.
6. **Relief**: gentle hills and exaggerated but soft mountains; no harsh, faceted low-poly look on the terrain itself (smooth shading, soft colour blending).
7. **Lighting**: a warm sun from the upper left plus strong soft ambient (nothing muddy or dark). The sunlit side faces the camera, with soft toon banding at most.
8. **Tone**: bright, clean, toy-like, slightly glossy. It should feel "realistic miniature" (a tilt-shift photo of a model), not flat cartoon.

## Engineering consequences (decided)

- **Props as batched chunk meshes**, not one game object per prop. The planet is split into ~40–80 surface chunks. Each chunk is one mesh component with a runtime buffer containing all its trees, houses, crops and so on, baked in planet space and rebuilt only when that chunk's sim values change (amortised, ≤ ~4 chunks per frame). That allows **5k–15k tiny trees** at a few dozen draw calls, which the "dense forest" look requires. Moving things (people, animals, boats, tankers, clouds) are pooled game objects or a per-frame dynamic mesh.
- **Tree density follows `forest`** (and `grass` for bushes/tufts), with multiple tree species by biome (round deciduous trees, pines at cold latitudes/altitudes, palms in the tropics, bare/snowy variants).
- **Ocean shader**: depth tint from the baked seabed depth, a **cyan shelf halo** where depth is shallow, a scrolling ripple normal pattern, fresnel rim, specular, and oil/sea-ice overlays from per-vertex data.
- **Atmosphere**: a back-face shell with a fresnel glow, plus a separate soft screen-space halo if needed.
- **Sky**: a full-screen gradient quad with drifting streak sprites. Fog-like depth blending toward the horizon colour at the limb helps the miniature feel.
- **Terrain**: render level 5–6 icosphere, smooth normals, vertex colours blended from biome and field values with per-vertex noise, beaches as a sand band at the coast, and snow from the `ice` field.
