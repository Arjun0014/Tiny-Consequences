# V3-80 — World audio: the sound of what is there (session 21, D-173)

## 1. Principle
The sound represents the simulation. A burning house is heard only where a house burns; an axe only where
someone is felling; a dog only when a dog barked; thunder when lightning struck; a river in spate where the
water is over its banks. Nothing plays for drama. Presentation randomness (which take, a slight pitch) never
touches the world.

## 2. The listener
The camera's focus (a point on the planet; on a village's map when it is close to one) and the zoom:
- **planet** (zoom < ~0.6): broad and quiet - high wind; storms, a wildfire, a stirring volcano under the
  focus; thunder from strikes anywhere, faint.
- **mid**: the region - woods, the shore, rain, the village faintly.
- **close** (zoom > ~1.1): the village - its people out of doors, the herds, the river, anything burning, and
  one-shots from real actions within ~1 world unit of the focus, louder and panned by where they are.
Night and day, rain or downpour, snow or cold, woods and shore come from the state at the focus (the
village's own weather and snow; the planet's fields of the cell under the focus).

## 3. Beds (seamless loops, gains eased toward their targets; started only when wanted)
wind (high), winter wind (snow or frost), woodland by day / by night (the cell's forest), shore (sea near),
river (river tiles near), river in flood (flood water or the swollen river near, while `s.fl`), rain,
downpour, village by day (people out of doors near, scaled by how many), village at night (hearths), fire
(things burning near, by heat), wildfire (the planet's fire on the cell), volcano (a mountain near the focus
whose pressure is high or that erupted lately - read from the volcanoes the world has made, never making
them), herd (beasts near).

## 4. One-shots (close in; never during a skip; fewer at the fast watch speed)
| Sound | From | Gap |
|---|---|---|
| axe | someone chopping or lopping | ~1.6 s a person |
| tree falling | a standing tree near gone while someone near is felling | 4 s |
| hammer | building, repairing, mending a boat, walling a fold, kin helping at a site | ~1.3 s |
| pick on stone | quarrying, mining, digging out the trapped, the quern | ~1.5 s |
| anvil | smithing | ~1.8 s |
| hoe / sickle | tilling, grubbing, weeding, spreading dung / reaping, mowing | ~2 s |
| water | filling a bucket; dousing a fire (hiss) | 2.5-3 s |
| footsteps (grass / snow) | someone walking very close (zoom in) | occasional |
| sheep / goat / cow / pig | a beast of a herd near, now and then (by how many) | 1.6 s |
| dog | a dog near that just barked (`d.barked`) | 0.6 s |
| wolf | wolves on the map at night | 25 s |
| oars | a boat being rowed near | 2.5 s |
| shout | someone near with a fresh alarm (fire, flood, thief, the sea, a quake) | 6 s a person |
| thunder / quake / house falling | deeds of the map: a strike, a quake, a building falling in | - |
Distance attenuation `(1 - d/R)^1.6`, pan from the screen position, a 4-per-second density cap.

## 5. News cues (game/ui/newsfeed.lua -> audio.news_cue)
A story new among the cards: a soft wooden chime (STORY, SLOW), a low knock (BREAKING), a quiet shimmer when
its chain ends at YOU. Once per story, at most one cue in two seconds, never on the first look at the feed.

## 6. Mixer
Groups `ambient` 0.7, `sfx` 0.85, `ui` 0.6 under `master` 0.9 (`sound.set_group_gain`). Bridge
`audio{on}` reports the beds playing and their gains, the shots and cues so far.

## 7. The library (scripts/gen_audio.mjs, assets/audio/manifest.json)
Each sound is a prompt, a length, whether it loops, how many takes, a group. The script reads the
ElevenLabs key from `.env` (never printed or written), asks the sound effects model
(`eleven_text_to_sound_v2`) for raw PCM (no MP3 padding to break a loop's seam), keeps it in
`out/audio_cache`, levels it with ffmpeg (static gain to the group's loudness, peaks under -1 dB; one-shots
trimmed of silence, loops untouched), encodes Ogg Vorbis (beds 24 kHz at the lowest quality, actions 44.1
kHz), and writes the Defold side: a `.sound` per file (`game/audio/snd/`), `game/audio/sounds.go` and
`game/audio/catalog.lua`. Idempotent: rerun after adding to the manifest; `--only id`, `--force`.
First library: 16 beds, 23 actions in 2-3 takes, 5 cues - 71 files, 3.4 MB.

## 8. Tuned from play (session 22, D-178)
- The deeds that sound (thunder, a quake, a house falling) are read at every zoom - the cursor no longer waits while
  the camera is far out (a burst of old thunder played on coming close); from afar a quake is heard faint by how near
  its village is, thunder rolls in.
- Fire is much louder the nearer it is; a big fire near adds the roaring bed.
- A herd calls a few times a minute by its size (not every 1.6 s); while it cries at a fire (`H.fire_cry`) it calls
  often and louder.

## 9. The village mix (session 23, D-181)
Arjun: close in, the animals took over - like standing inside a livestock recording. The mix, not the world,
was wrong: the herd bed played as loud as the village's own bed, and a call came every few seconds from any
beast within a wide radius. Now (`game/audio/audio.lua` `M.MIX`):
- **Hierarchy** close in: a danger near (fire, shouts, thunder, a quake, a house falling) 1.0 > work near (axe,
  hammer, hoe, pick, oars) 0.8 and water and weather > the village's own hum 0.65 > dogs 0.6 (a dog barks only
  at wolves or a stranger: always a real alarm) > wild 0.55 > **the herds 0.3**.
- **The herds are mostly their bed**: soft, scaled by the log of the beasts near (texture, not a count), under
  the village bed; ducked by half while a danger is near.
- **Calls are rare and near**: one beast at a time over all the herds (`M.BEAST_GAP` 2.8 s), a few a minute
  from a big flock, heard only within `M.BEAST_R` 0.75 with a steep fall-off; one right under the camera at the
  closest zoom may be heard for itself. While the herd cries out at a fire (the sim's own cry) the calls are
  loud and often - the world justifies it.
- **Measured** (`scripts/play_audio.mjs`: what the mixer plays, headless, at watch speed): at the flock close
  in the herd bed 0.44 -> 0.14 (under the village bed 0.41-0.54); among the flock the beasts' share of one-shot
  energy 88% -> rare; at the square the work carries it.
