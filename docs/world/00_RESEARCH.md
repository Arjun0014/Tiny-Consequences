# W00 — Research notes: real mechanisms behind the tiny world

Research done 27 Sep 2026 for the world-simulation reset (D-050). The goal is not to reproduce
scientific models, but to give every model in `docs/world/` a real conceptual basis and
realistic *ratios*. Game units (food units, wood units, cells) are calibrated to these ratios,
not to literal hectares: the planet is deliberately tiny (≈ 60 cells around).

Each section: what the literature says → what the game takes from it.

---

## 1. Subsistence households and village agent models

**Artificial Anasazi** (Dean, Gumerman, Epstein, Axtell et al.; Long House Valley ABM; replication
in Janssen 2009, *JASSS* 12(4)13). Agents are households of 5; each needs 800 kg maize a year
(160 kg/person); surplus is stored at most 2 years with losses; a household fissions when a
daughter marries (probability 0.125/yr); households relocate fields when next year's projected
food is short, choosing the nearest unfarmed plot that can produce enough within 1 mile of water;
settlements sit near their fields. Key sensitivity result: the *environment → yield → carrying
capacity* chain drove the fit; the behavioural rules mostly smoothed it.
- **Game takes**: households as the unit of residence and fission; food stored with losses for
  up to ~2 years; relocation driven by projected shortfall; fields chosen by expected yield and
  distance. Lesson: agent rules only matter if they are coupled to the environment and to each
  other — so every agent decision in our design reads world state and writes world state.

**Chayanov's labour–consumer balance** (Chayanov 1925; Harrison 1975; Smith 1979). A peasant
household works until its consumption needs (scaled by its consumer/worker ratio) are met; beyond
that, the drudgery of more work outweighs its value. Households with many dependants work harder.
- **Game takes**: the village labour allocator stops assigning work once stocks reach comfortable
  targets (leisure is the result), and raises hours when stocks are short. Villages with many
  children per worker are more stressed. Visible: well-fed villages socialise; hungry ones toil.

## 2. Food, farming labour, yields, soil

Sources: Pinhasi/Shukurov et al., *Productivity of pre-modern agriculture in the
Cucuteni-Trypillia area* (arXiv:1505.05121) — a synthesis of experimental archaeology (Semyonov
1974, Steensberg 1943), Columella/White, Halstead & Jones; medieval manor data (Sussex yields).
- Diet ≈ 2,200–3,000 kcal/person/day; grain ≈ 3,150–3,500 kcal/kg; cereals alone → 250–350
  kg/person/year; with animal food 100–140 kg.
- Pre-modern cereal yields 500–1,200 kg/ha; seed ≈ 10–15% of the harvest; ~25% further losses
  (pests, storage) → ≈ 440 kg/ha available. Medieval seed:yield ratios ≈ 1:4 (1:2 bad years,
  1:7 good years).
- Labour: hand tillage 10–25 m²/person-hour (1 ha ≈ 400–1,000 h); ard/oxen 150–500 m²/h;
  reaping 20–40 m²/person-hour (≈ 30 person-days/ha); threshing ≈ as much again. Roman wheat:
  ≈ 44 person-days/ha excluding harvest. **Seasonal bottlenecks**: soil preparation and sowing must
  fit ≈ 30 days; harvest is the peak of time stress; overproduction and >1 year of storage are
  normal responses to crop-failure risk. Working year ≈ 250 days. A labour return (food energy
  produced / energy spent) ≥ ~10 is the minimum acceptable in subsistence economies.
- **Game takes**: food unit FU = one adult-day of food; children eat 0.6, elders 0.8. A farm cell
  yields grain in proportion to crop biomass; sowing consumes seed ≈ 15% of an expected harvest;
  sowing, tending and harvest each cost person-hours, with sowing and harvest windows that saturate
  a village's labour (everyone helps; fishing and building pause). Stored grain decays slowly
  (~15%/yr), fresh food fast.

**Shifting cultivation / fallow** (FAO & literature syntheses; Mertz 2002 on fallow length):
fertility can fall ~50% within 2–3 years of continuous cropping; fallows of 10–20 years restore
it; fallows under 5–7 years degrade land. Boserup: population pressure shortens fallow and
intensifies labour. Long-term unmanured wheat plots fall to a stable low yield in ~20 years.
- **Game takes**: cropping depletes cell fertility (existing), fallow cells regrow vegetation that
  rebuilds fertility (existing vegetation/soil coupling); a crowded village shortens fallow and
  crops exhausted land (lower yields) — pressure is visible in the fields.

## 3. Fuel and timber

- Pre-industrial household firewood: most estimates 1–3 t of dry wood per person per year in
  temperate Europe (Warde, *Firewood consumption and energy transition*; Kander 19th-c. Sweden:
  ~4 m³ loose per capita); cooking a year-round base, heating dominant in cold months; ranges
  0.5–8 m³ depending on climate and forest access.
- Construction: Rackham's survey of a 15th-century Suffolk farmhouse (Grundle House) found it used
  ~330 trees, most of them small and young (coppice and hedgerow wood) — timber houses are
  *many small trees*, not a few giants. Experimental archaeology: a Bronze Age longhouse ≈ 30
  people × 2 months including material preparation; small houses are a few hundred person-days.
- **Game takes**: wood unit WU. Households burn fuel every day: a cooking base plus heating ∝
  degrees below comfort (so cold villages need far more wood and cut forests faster). A house
  needs ~60 WU of timber and ~250 person-hours; repairs need wood too; boats and stores likewise.
  Woodcutters fell in the nearest forest cells, which thin (the `forest` field falls by the wood
  taken) and regrow slowly (existing vegetation dynamics) — settlements carve clearings.

## 4. Demography

Sources: Coale–Demeny model life tables (Model West); Devereaux, *A Collection of Unmitigated
Pedantry*, "Life, Work, Death and the Peasant" IIIb (2025), summarising Frier/Bagnall/Scheidel;
Menken & Kuhn 1996 (breastfeeding); natural-fertility literature; Ó Gráda, *Famine: A Short
History* (2009); Dyson 1993 (demographic responses to famine).
- Female first marriage 17–25 (late in NW Europe, see §5); conception probability 85%/yr under
  30, 75% at 30, 66% at 35, 44% at 40; ~25% of pregnancies miscarry or are stillborn; 18–24
  months of nursing with ~1 year of lactational amenorrhoea; total fertility 5–6 for slow growth.
  Model West level 3: ~33% die before age 1, ~55% before adulthood (harsh case).
- Famine: conceptions and births fall promptly and almost universally; mortality rises later and
  depends on disease, migration and relief. Coping sequence: rationing and diversification
  (reversible) → selling breeding stock / eating seed / mortgaging land (irreversible, lowers
  future production) → migration → malnutrition and disease deaths.
- **Game takes**: births come from real couples living in their own home: daily conception chance
  for fecund women, lower when poorly fed, zero while nursing (~12 months after a birth unless
  the infant dies), pregnancy of 9 months (252 days), a child entity at birth. Mortality by age
  (high infant hazard, Gompertz old age) × nutrition, cold and crowding. Famine response follows
  the coping sequence: rationing → eating seed grain (fewer fields sown next spring) → migration →
  deaths. Game rates are gentler than Model West so small villages are not wiped out by noise.

## 5. Household formation

**Hajnal's European marriage pattern**: late marriage, many never married, and *neolocal*
residence — a couple married only once they could set up their own household; bad economic
conditions delayed marriage (the Malthusian "preventive check"). East of the Hajnal line, joint
households allowed earlier marriage.
- **Game takes**: a couple forms a household when a home is available: a vacant house, or a new
  one the village builds for them. Until then they are engaged and live with their parents (no
  births). Housing therefore gates births, and housing needs wood and labour — the chain
  *population grows → housing short → wood needed → forest cut → house built → family formed*
  emerges instead of being scripted.

## 6. Fisheries

Schaefer (1954) surplus production: dB/dt = rB(1 − B/K) − C, catch C = q·E·B (catchability ×
effort × biomass), maximum sustainable yield at B = K/2; catch per unit effort (CPUE) ∝ biomass.
Small-scale fisheries: day trips from a landing site; effort in boat-hours.
- **Game takes**: fish stay a biomass field (the right level for fish); each boat trip catches
  q × fish density × crew × hours on the ground, capped by the hold, and removes exactly that from
  the ground. Distant grounds cost sailing time (fewer fishing hours). Overfishing near a village
  forces boats farther and lowers catches — real CPUE dynamics.

## 7. Predators, prey and vegetation

Minnesota wolf/white-tailed deer studies (International Wolf Center; Mech; Gable et al. 2024):
wolves need ~15–20 adult-sized deer per wolf per year; kill rates rise ~2× with snow cover;
predation peaks in late winter (deer in poor condition, deep snow) and at fawning (neonates);
pairs kill more per wolf than big packs; wolves alone rarely *limit* deer — severe winters and
habitat do most of the damage, wolves amplify it; up to 85% of northern deer migrate seasonally
3–37 km. Deer: does breed from ~1.5 years, 1–2 fawns a year, fawn survival tied to the previous
winter and doe condition.
**Old-field succession** (Rutgers/NY Natural Heritage; passive rewilding study, PMC8208563): years
2–5 perennial grasses and forbs, 3–10 shrubs and pioneer seedlings, 10–20 pioneer thickets, 20–70
taller longer-lived trees, 70–100+ mature forest; one abandoned field had 91% tree canopy after 53
years.
- **Game takes**: herds and packs as agents (W4): birth pulse in spring scaled by body condition;
  winter starvation when snow hides forage; kill rates per wolf rising with snow and fawns; herds
  move seasonally. Abandoned fields follow the existing grass → forest succession, which is
  already tuned to ~30–60 years to canopy.

## 8. Days, seasons, weather

- **Day length** (Forsythe et al. 1995, *Ecological Modelling* 80: CBM model): declination from day
  of year; day length from latitude and declination with a sunrise angle of 0.833°; within a
  minute of almanac values below 40° latitude. Opposite hemispheres get opposite seasons by
  construction.
- **Sleep in pre-industrial societies** (Yetish et al. 2015, *Current Biology*; Hadza, San,
  Tsimane): sleep onset ≈ 3.3 h after sunset, waking just before sunrise, 5.7–7.1 h of sleep,
  ~1 h longer in winter; naps rare.
- **Daily weather** (Richardson 1981 WGEN; Wilks & Wilby 1999): wet/dry days as a first-order
  Markov chain whose probabilities follow the month's precipitation, amounts from a gamma
  distribution. Used to *downscale* a coarse climate model to daily weather.
- **Game takes**: the weekly planet climate stays; each day is realised per place by a
  WGEN-style downscaling keyed by (cell, day) so the week's rain total is conserved. People wake
  before local sunrise and sleep ~3 h after sunset (bounded), work in daylight; storms and heavy
  rain stop outdoor work and fishing. Sun position follows the real day of year, so the terminator
  and polar days/nights appear naturally.

## 9. Transport and trade

- ORBIS (Scheidel & Meeks, Stanford) and Diocletian's Price Edict: sea transport is roughly an
  order of magnitude cheaper than river, and river far cheaper than land (often quoted ≈ 1 : 5 :
  28–56 sea:river:land per tonne-km). Pre-modern bulk trade was waterborne.
- **Norse Greenland** (Cambridge walrus-ivory DNA studies; Smith College climate-history notes):
  the colonies depended on shipping from Norway for iron, timber and some food; falling ivory
  prices, over-hunted walrus, more sea ice and storms (Little Ice Age), and Norway's Black Death
  losses thinned the ships until the settlements were abandoned (~1450) — a real example of a
  settlement failing because a supply chain stopped.
- **Game takes**: bulk goods move by boat; overland carrying is slow and small. Merchant ships
  redistribute surplus along a circuit of ports; destroying one means its cargo never arrives, and
  villages that relied on those calls run short — a chain that propagates through the economy, not
  a script.

## 10. Spatial economics

von Thünen's rings: activities arrange by the cost of travel from the settlement — intensive
use near, extensive far. In our design this falls out of travel time eating into daily work hours:
villages farm the nearest good land, cut the nearest wood, fish the nearest good grounds; as near
resources deplete, work shifts outward and productivity falls.

---

## Sources

- Janssen, M. A. (2009). *Understanding Artificial Anasazi*. JASSS 12(4)13. https://jasss.soc.surrey.ac.uk/12/4/13.html
- Axtell, Epstein, Dean et al. (2002). *Population growth and collapse in a multiagent model of the Kayenta Anasazi*. PNAS. https://www.pnas.org/doi/full/10.1073/pnas.092080799
- Shukurov, Sarson, Videiko et al. (2015). *Productivity of pre-modern agriculture in the Cucuteni-Trypillia area*. https://arxiv.org/pdf/1505.05121
- Medieval yields and practice: https://spartacus-educational.com/MEDfarming.htm ; https://en.wikipedia.org/wiki/Crop_yield
- Chayanov and the labour–consumer balance: https://era.anthropology.ac.uk/Era_Resources/Era/Peasants/theory07.html ; https://warwick.ac.uk/fac/soc/economics/staff/mharrison/public/1975_jps_postprint.pdf ; https://www.journals.uchicago.edu/doi/abs/10.1086/jar.35.4.3629542
- Firewood: Warde, *Firewood consumption and energy transition*, https://www.historiaagraria.com/FILE/articulos/RHA77_warde.pdf ; https://www.researchgate.net/publication/277822676_Household_firewood_consumption_in_Sweden_during_the_nineteenth_century
- Timber in houses (Rackham's Grundle House method): https://www.tandfonline.com/doi/full/10.1080/03055477.2015.1123416 ; https://www.buildingconservation.com/articles/architectural-timber/architectural-timber.htm
- Longhouse construction labour: https://exarc.net/issue-2018-2/ea/construction-bronze-age-longhouse-model
- Peasant demography: https://acoup.blog/2025/08/08/collections-life-work-death-and-the-peasant-part-iiib-children-and-childrearing/ ; https://en.wikipedia.org/wiki/Natural_fertility ; https://doi.org/10.1177/156482659601700414
- Famine: Ó Gráda, https://assets.press.princeton.edu/chapters/s8857.pdf ; Dyson 1993, https://onlinelibrary.wiley.com/doi/10.1111/j.1759-5436.1993.mp24004002.x ; https://ourworldindata.org/do-famines-curb-population-growth ; Swift, https://www.ids.ac.uk/files/dmfile/swift24.4.pdf
- Hajnal line: https://en.wikipedia.org/wiki/Western_European_marriage_pattern ; https://www.encyclopedia.com/international/encyclopedias-almanacs-transcripts-and-maps/european-marriage-pattern
- Surplus production models: https://haddonm.github.io/URMQMF/surplus-production-models.html ; https://www.fao.org/4/w6914e/w6914e02.htm
- Wolves and deer: https://wolf.org/the-ecological-relationship-of-gray-wolves-and-white-tailed-deer-in-minnesota/ ; https://pmc.ncbi.nlm.nih.gov/articles/PMC11570190/
- Old-field succession: http://bio.rutgers.edu/~gb102/lab_13/13i3l.html ; https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8208563/ ; https://guides.nynhp.org/successional-old-field/
- Shifting cultivation: https://en.wikipedia.org/wiki/Shifting_cultivation ; https://link.springer.com/article/10.1023/A:1020507631848
- Day length: Forsythe et al. (1995), https://ui.adsabs.harvard.edu/abs/1995EcMod..80...87F
- Sleep: Yetish et al. (2015), https://www.cell.com/fulltext/S0960-9822(15)01157-4
- Weather generators: https://support.goldsim.com/hc/en-us/article_attachments/115026531468/WGEN.pdf ; https://perso.univ-rennes1.fr/valerie.monbet/doc/Wilks&Wilby.pdf
- Transport costs: ORBIS, https://orbis.stanford.edu/assets/Scheidel_60.pdf ; https://www.repository.cam.ac.uk/items/356ffa4e-e45f-4376-af1d-537b2d6657b1
- Norse Greenland: https://www.science.smith.edu/climatelit/abandonment-of-norse-settlements/ ; https://www.cam.ac.uk/stories/norsewalrus
