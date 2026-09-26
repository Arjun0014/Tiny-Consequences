-- content/names.lua — every name list and name generator (06_PRESENTATION.md §4–5).
--
-- Pure Lua 5.1 data + functions, loaded by the deterministic sim:
--   * ALL randomness comes from a caller-supplied u() -> float in [0,1).
--     Every generator takes u as its first argument and keeps no state between calls,
--     so the same sequence of u() values always yields the same names.
--   * No random/os/io library calls, no Defold APIs; arrays + numeric loops only.
--
-- Tone: whimsical, gentle, a little ominous. No real people, brands, places or politics.

local names = {}

local floor = math.floor
local sub, upper, lower, find, gsub, gmatch =
  string.sub, string.upper, string.lower, string.find, string.gsub, string.gmatch

local function pick(u, t)
  local n = #t
  local i = floor(u() * n) + 1
  if i > n then i = n elseif i < 1 then i = 1 end
  return t[i]
end

local function cap(s)
  return upper(sub(s, 1, 1)) .. sub(s, 2)
end

---------------------------------------------------------------------------
-- Blocklist: generated words are checked word-by-word (lowercased).
-- BLOCK_SUB entries are Lua patterns searched inside each word;
-- BLOCK_WORD entries must equal a whole word. Over-blocking is harmless
-- (the generator just retries with the next u()).
---------------------------------------------------------------------------
local BLOCK_SUB = {
  -- profanity / sexual / bodily
  "fuck", "shit", "cunt", "twat", "wank", "slut", "whor", "bitch", "bastard", "cock", "dick",
  "piss", "titt", "tits", "titi", "boob", "penis", "vagin", "dildo", "porn", "sex", "semen",
  "sperm", "spunk", "jizz", "cumm", "rape", "rapist", "horny", "boner", "minge", "todger",
  "bellend", "tosser", "bollock", "bugger", "skank", "pedo", "paedo", "nonce", "turd",
  "poop", "fart", "puke", "barf", "kaka", "caca", "pipi", "weewee", "peepee", "diddl",
  "widdl", "suck", "damn", "lesbo", "dyke", "fuk", "fuc", "fck", "kunt", "twot", "pussy",
  "dikk", "ildo",
  -- slurs / hate
  "nigg", "niga", "nigr", "nig$", "negro", "fag", "kkk", "klan", "nazi", "hitler", "retard",
  "spastic", "spaz", "tranny", "^paki", "^coon", "gyp", "jew", "negr",
}
local BLOCK_WORD = {
  "ass", "arse", "arses", "butt", "butts", "tit", "anal", "anus", "bum", "crap", "hell",
  "poo", "poos", "pee", "wee", "cum", "cums", "hump", "bonk", "knob", "nob", "dong", "wang",
  "willy", "muff", "prick", "pecker", "fanny", "hoe", "thot", "slag", "sod", "tard",
  "coon", "spic", "spick", "chink", "gook", "kike", "wop", "jap", "wog", "darkie", "darky",
  "mong", "mongo", "midget", "homo", "lez", "dik", "dyk", "kok", "cok", "kum", "poon", "bint",
  "hore", "hash", "kack", "cack", "pepe",
  -- religion / real people / politics
  "shia", "isis", "allah", "jesus", "christ", "satan", "lucifer",
  "putin", "biden", "trump", "obama", "stalin", "lenin", "modi", "elon", "musk", "mao",
  "bezos", "xi",
}

function names.is_blocked(s)
  local low = lower(s)
  for w in gmatch(low, "%a+") do
    for i = 1, #BLOCK_SUB do
      if find(w, BLOCK_SUB[i]) then return true end
    end
    for i = 1, #BLOCK_WORD do
      if w == BLOCK_WORD[i] then return true end
    end
  end
  return false
end

-- Pronounceability guard for a single lowercase word.
local function pronounceable(w)
  if find(w, "[aeiou][aeiou][aeiou]") then return false end                     -- 3 vowels
  if find(w, "[^aeiouy][^aeiouy][^aeiouy][^aeiouy]") then return false end      -- 4 consonants
  if find(w, "[aeiou][aeiou][^aeiouy][^aeiouy]") then return false end           -- "aurk"
  if find(w, "(%a)%1%1") then return false end                                  -- "sss"
  if find(w, "ck[ck]") or find(w, "yy") then return false end                   -- "ckk", "yy"
  if find(w, "^(%a)%1") then return false end                                   -- "Llo"
  return true
end

---------------------------------------------------------------------------
-- Cultures (phoneme sets). One per continent (index wraps if there are more).
-- Pattern tokens: O initial onset, M medial onset, V nucleus, C coda,
--                 R repeat current syllable, E ending (gendered / place / surname).
---------------------------------------------------------------------------
local CULTURES = {
  { id = "aelun", label = "Aelun", flavour = "soft, vowel-rich, drifting",
    on  = { "", "", "l", "l", "m", "n", "s", "v", "f", "h", "y", "r", "th" },
    mid = { "l", "l", "m", "n", "n", "r", "s", "v", "y", "th", "ll" },
    nu  = { "a", "a", "e", "e", "i", "i", "o", "u", "ae", "ia", "ei", "io" },
    co  = { "n", "l", "s", "r", "n", "l" },
    person = { "OVE", "OVE", "OVMVE", "OVMVE", "OVMV", "OVMVC", "OVMVC" },
    fem  = { "a", "ia", "ine", "elle", "ya", "wen", "esse", "ima", "ly", "ea", "isse" },
    masc = { "o", "en", "an", "ion", "el", "as", "ir", "eo", "orin", "and", "iel" },
    place = { "OVE", "OVMVE", "OVCE", "OVMV" },
    place_end  = { "mere", "lune", "dell", "wyn", "ora", "lis", "vale", "wen", "mist", "illow" },
    place_pre  = { "Little ", "Upper ", "Lower ", "Nether " },
    place_post = { " Glade", " Dell", " Rest", " Hollow", " Shore" },
    sur_end    = { "wen", "dew", "song", "mere", "lark", "ley", "wyn", "dove" },
    fallback_f = { "Mela", "Ione", "Saela" }, fallback_m = { "Vaelo", "Lorien", "Sael" },
    fallback_place = { "Melamere", "Little Lune" }, fallback_sur = { "Dewsong", "Larkwen" },
  },
  { id = "grundmark", label = "Grundmark", flavour = "crunchy, northern, stubborn",
    on  = { "b", "br", "d", "dr", "g", "gr", "h", "j", "k", "kn", "kr", "sk", "st", "t", "th",
            "v", "fr", "r", "s", "b", "t" },
    mid = { "d", "g", "k", "l", "m", "n", "r", "t", "v", "s", "ld", "rn", "st", "nd" },
    nu  = { "a", "a", "e", "i", "o", "o", "u", "y", "ei", "au" },
    co  = { "k", "g", "rk", "rn", "nd", "lf", "st", "ld", "rg", "m", "r", "n", "sk", "l" },
    person = { "OVE", "OVCE", "OVCE", "OVMVE", "OVMVE", "OVC", "OVMVC" },
    fem  = { "a", "hild", "dis", "ey", "ja", "run", "frid", "lin", "vi", "dra" },
    masc = { "ar", "ulf", "ik", "geir", "mund", "olf", "ur", "ald", "vin", "grim" },
    place = { "OVCE", "OVCE", "OVE", "OVMVE", "OVC" },
    place_end  = { "holm", "skeld", "brekk", "gard", "vorn", "knap", "by", "thorp", "heim", "fell" },
    place_pre  = { "Old ", "Nether ", "Upper ", "Great " },
    place_post = { " Crag", " Knoll", " Hearth", " Fjord", " Rise" },
    sur_end    = { "son", "kin", "hald", "brand", "strand", "holt", "mark", "berg" },
    fallback_f = { "Brakka", "Hildra", "Gudra" }, fallback_m = { "Tovik", "Skarn", "Vorik" },
    fallback_place = { "Skarnholm", "Old Brekk" }, fallback_sur = { "Brandkin", "Holtmark" },
  },
  { id = "pippadoo", label = "Pippadoo", flavour = "sing-song, bouncy, doubled",
    on  = { "p", "b", "t", "l", "m", "n", "k", "w", "d", "j", "f", "h", "t", "pl" },
    mid = { "p", "b", "t", "l", "m", "n", "k", "w", "d", "pp", "mm", "ll", "bb", "tt", "nn" },
    nu  = { "a", "i", "o", "u", "e", "a", "i", "o", "oo", "ee" },
    co  = { "m", "n", "p", "b", "nk", "mp" },
    person = { "OVR", "OVR", "OVRE", "OVMVE", "OVMV", "OVE", "OVMVC" },
    fem  = { "i", "a", "ella", "ina", "ippa", "ly", "bell", "ie", "etta" },
    masc = { "o", "u", "oppo", "in", "ob", "umbo", "ock", "ib", "ey", "ton" },
    place = { "OVRE", "OVMVE", "OVCE", "OVR", "OVMV" },
    place_end  = { "pom", "dingle", "bumbo", "tootle", "pip", "bobble", "nook", "la", "wimble", "tump" },
    place_pre  = { "Little ", "Tiny ", "Bitty ", "Upper " },
    place_post = { " Nook", " Patch", " Hollow", " Hop", " Knoll" },
    sur_end    = { "kins", "wiggle", "pop", "wimble", "bun", "tuck", "wick", "bloom" },
    fallback_f = { "Pippa", "Lulu", "Nella" }, fallback_m = { "Momo", "Tobbo", "Wibbo" },
    fallback_place = { "Lulupom", "Tiny Dingle" }, fallback_sur = { "Popkins", "Wigglebun" },
  },
  { id = "tak", label = "Tak", flavour = "clipped, crisp, busy",
    on  = { "k", "t", "p", "d", "z", "j", "r", "v", "b", "g", "ch", "sk", "kr", "tr" },
    mid = { "k", "t", "z", "r", "d", "v", "n", "m", "sk" },
    nu  = { "a", "a", "e", "i", "o", "o", "u" },
    co  = { "k", "t", "x", "z", "ck", "tt", "sk", "rk", "n", "m", "p", "zz", "g", "ll" },
    person = { "OVC", "OVC", "OVMVC", "OVMVC", "OVE", "OVMVE", "OVCE" },
    fem  = { "a", "i", "e", "ka", "ix", "yn", "ta", "ez", "ia" },
    masc = { "o", "ek", "ax", "or", "ok", "ut", "ez", "an", "ik" },
    place = { "OVCE", "OVC", "OVMVC", "OVE" },
    place_end  = { "tok", "dak", "pek", "zat", "kip", "rak", "vex", "tik", "kett", "dun" },
    place_pre  = { "Low ", "Old ", "Upper ", "Far " },
    place_post = { " Point", " Notch", " Rock", " Cut", " Stop" },
    sur_end    = { "ek", "ax", "om", "ack", "ik", "uz", "ox", "et" },
    fallback_f = { "Kora", "Vetta", "Zika" }, fallback_m = { "Tekko", "Dax", "Pim" },
    fallback_place = { "Zattok", "Low Dak" }, fallback_sur = { "Kordek", "Vatox" },
  },
  { id = "bumbleshire", label = "Bumbleshire", flavour = "earthy, rumbly, burrowed-in",
    on  = { "b", "m", "r", "d", "g", "h", "w", "br", "gr", "th", "f", "wh", "bl", "gl" },
    mid = { "b", "m", "r", "d", "g", "l", "n", "mb", "nd", "rr", "ll", "dd", "bb", "gg" },
    nu  = { "o", "u", "a", "e", "i", "o", "u", "a" },
    co  = { "m", "n", "r", "b", "d", "g", "mble", "ddle", "ggle", "bble", "ck", "rt", "lk", "mp", "nk" },
    person = { "OVMVE", "OVMVE", "OVCE", "OVCE", "OVMV", "OVMVC" },
    fem  = { "ie", "etta", "ina", "ly", "ora", "ice", "abel", "wyn", "sy" },
    masc = { "o", "er", "ert", "ald", "ock", "ard", "us", "wick", "kin" },
    place = { "OVCE", "OVCE", "OVE", "OVMVE", "OVC" },
    place_end  = { "bottom", "burrow", "wick", "mouth", "ford", "combe", "stead", "by", "ton", "dale" },
    place_pre  = { "Little ", "Much ", "Nether ", "Under ", "Lower " },
    place_post = { " Bottom", " Green", " End", " Burrow", " Puddle" },
    sur_end    = { "bottom", "foot", "wick", "bury", "thorn", "bun", "kettle", "root" },
    fallback_f = { "Hoddie", "Merrin", "Midge" }, fallback_m = { "Bumble", "Gubbo", "Rolly" },
    fallback_place = { "Grumblebottom", "Much Wobbling" }, fallback_sur = { "Mudfoot", "Bramblewick" },
  },
  { id = "sishae", label = "Sishae", flavour = "airy, whispery, windswept",
    on  = { "s", "s", "sh", "z", "th", "f", "h", "wh", "y", "l", "n", "" },
    mid = { "s", "sh", "l", "n", "th", "r", "y", "f", "z", "ss", "ll" },
    nu  = { "i", "i", "e", "e", "a", "a", "ee", "ia", "ae", "y" },
    co  = { "s", "sh", "th", "l", "n", "ss", "r" },
    person = { "OVE", "OVMVE", "OVMVE", "OVMV", "OVMVC", "OVMVE" },
    fem  = { "ia", "ee", "ise", "eth", "ira", "yl", "ssa", "ae", "iss" },
    masc = { "is", "en", "ir", "ith", "an", "el", "ael", "or", "ys" },
    place = { "OVE", "OVMVE", "OVCE", "OVMV" },
    place_end  = { "whist", "shee", "sill", "fen", "wisp", "sigh", "mist", "lea", "wind", "hush" },
    place_pre  = { "High ", "Far ", "Upper ", "Little " },
    place_post = { " Reach", " Drift", " Breeze", " Sands", " Spire" },
    sur_end    = { "wind", "wisp", "whistle", "feather", "silk", "shine", "reed", "sigh" },
    fallback_f = { "Sylla", "Thessi", "Fenna" }, fallback_m = { "Shael", "Ziris", "Whisan" },
    fallback_place = { "Sillwhist", "High Shee" }, fallback_sur = { "Reedwhistle", "Silkwind" },
  },
}
names.CULTURES = CULTURES

local function culture(ci)
  local n = #CULTURES
  local i = floor(tonumber(ci) or 1)
  return CULTURES[((i - 1) % n) + 1]
end

-- Join stem + ending, eliding the stem's trailing vowels before a vowel-initial ending.
local function join(stem, ending)
  if ending == "" then return stem end
  if find(sub(ending, 1, 1), "[aeiouy]") then
    stem = gsub(stem, "[aeiouy]+$", "")
  end
  return stem .. ending
end

local function build(u, c, pat, ends)
  local out, syl = "", ""
  for k = 1, #pat do
    local t = sub(pat, k, k)
    if t == "O" then syl = pick(u, c.on); out = out .. syl
    elseif t == "M" then syl = pick(u, c.mid); out = out .. syl
    elseif t == "V" then local v = pick(u, c.nu); syl = syl .. v; out = out .. v
    elseif t == "C" then local x = pick(u, c.co); syl = syl .. x; out = out .. x
    elseif t == "R" then out = out .. syl
    elseif t == "E" then out = join(out, pick(u, ends))
    end
  end
  return out
end

local TRIES = 16

local function word_ok(w, lo, hi)
  local n = #w
  return n >= lo and n <= hi and pronounceable(w) and not names.is_blocked(w)
end

---------------------------------------------------------------------------
-- People
---------------------------------------------------------------------------
function names.person(u, culture_index, sex)
  local c = culture(culture_index)
  if sex ~= "f" and sex ~= "m" then sex = (u() < 0.5) and "f" or "m" end
  local ends = (sex == "f") and c.fem or c.masc
  for _ = 1, TRIES do
    local w = build(u, c, pick(u, c.person), ends)
    if word_ok(w, 3, 8) then return cap(w) end
  end
  return pick(u, (sex == "f") and c.fallback_f or c.fallback_m)
end

local SURNAME_PATTERNS = { "OVE", "OVCE", "OVMVE", "OVE" }

function names.surname(u, culture_index)
  local c = culture(culture_index)
  for _ = 1, TRIES do
    local w = build(u, c, pick(u, SURNAME_PATTERNS), c.sur_end)
    if word_ok(w, 4, 12) then return cap(w) end
  end
  return pick(u, c.fallback_sur)
end

---------------------------------------------------------------------------
-- Villages
---------------------------------------------------------------------------
function names.village(u, culture_index)
  local c = culture(culture_index)
  for _ = 1, TRIES do
    local w = build(u, c, pick(u, c.place), c.place_end)
    if word_ok(w, 5, 14) then
      w = cap(w)
      local r = u()
      if r < 0.14 then return pick(u, c.place_pre) .. w end
      if r < 0.26 then return w .. pick(u, c.place_post) end
      return w
    end
  end
  return pick(u, c.fallback_place)
end

-- Hopeful names settlers give a fresh start. Short, earnest, slightly doomed.
local HOPEFUL = {
  "New Hope", "Second Try", "Probably Safe", "Fresh Start", "Try Again", "Higher Ground",
  "Far Enough", "Better Luck", "Clean Slate", "Somewhere Else", "Plan B", "Safe Enough",
  "Hope Again", "Dry Feet", "Solid Ground", "Mostly Dry", "Fine Now", "Nearly Home",
  "Last Chance", "Third Time", "Good Omen", "Quiet Hill", "Fingers Crossed", "Upwind",
  "No Wolves", "Still Hopeful", "Brave Start", "Lucky Spot", "Calm Corner", "Firm Footing",
}
local MIGRANT_TEMPLATES = {
  { "Second ", "" }, { "Other ", "" }, { "", " Again" }, { "", " Too" }, { "Far ", "" },
  { "Not ", "" }, { "Little ", "" }, { "Better ", "" },
}

function names.new_village(u, culture_index, from_village_name)
  local from = from_village_name
  local r = u()
  if type(from) == "string" and from ~= "" then
    if r < 0.30 then
      local s
      if sub(from, 1, 6) == "Newer " then s = "Newest " .. sub(from, 7)
      elseif sub(from, 1, 4) == "New " then s = "Newer " .. sub(from, 5)
      elseif sub(from, 1, 7) ~= "Newest " then s = "New " .. from end
      if s and #s <= 28 then return s end
      return pick(u, HOPEFUL)
    elseif r < 0.50 then
      local t = pick(u, MIGRANT_TEMPLATES)
      local s = t[1] .. from .. t[2]
      if #s <= 28 then return s end
      return pick(u, HOPEFUL)
    end
  end
  if r < 0.75 then return pick(u, HOPEFUL) end
  return names.village(u, culture_index)
end

---------------------------------------------------------------------------
-- Regions: adjective + noun (continents also get grand or compound names)
---------------------------------------------------------------------------
local REGION_ADJ = {
  "Sapphire", "Ashen", "Grumbling", "Whistling", "Sleepy", "Mossy", "Rusty", "Hollow",
  "Wandering", "Humming", "Crooked", "Gloomy", "Drowsy", "Sulking", "Sighing", "Mumbling",
  "Tumbling", "Silver", "Amber", "Copper", "Velvet", "Dappled", "Hushed", "Restless",
  "Fickle", "Brooding", "Dozing", "Yawning", "Shivering", "Blushing", "Murmuring", "Bristling",
  "Muttering", "Lonesome", "Forgotten", "Tattered", "Patient", "Nervous", "Sneezing", "Gentle",
  "Wistful", "Grizzled", "Tangled", "Weeping", "Giggling", "Stubborn", "Crumbling", "Rattling",
  "Hungry", "Twinkling", "Rumbling", "Squabbling", "Pensive", "Moody", "Dreaming", "Grumpy",
}

local REGION = {
  continent = { the = 0, adj = { "Great", "Grand", "Old", "Far", "Endless", "Greater", "Lesser",
      "Middle", "Elder", "Vast" },
    nouns = { "Muddle", "Sprawl", "Lump", "Expanse", "Heap", "Hold", "Crust", "Tumble", "Reach",
      "Wilds", "Clump", "Jumble", "Hodgepodge", "Landmass" } },
  island = { adj = { "Lonely", "Stray", "Castaway", "Tiny", "Outer" },
    nouns = { "Isle", "Islet", "Rock", "Key", "Atoll", "Lump", "Speck", "Nub", "Crumb", "Pebble", "Skerry" } },
  sea = { adj = { "Briny", "Salty", "Sloshing", "Choppy", "Glassy" },
    nouns = { "Sea", "Deep", "Shallows", "Brine", "Waters", "Gulf", "Sound", "Bay", "Swell", "Slosh" } },
  ocean = { the = 0.5, adj = { "Endless", "Bottomless", "Wide", "Great", "Boundless" },
    nouns = { "Ocean", "Expanse", "Blue", "Depths", "Main", "Vastness", "Drink", "Wet", "Deep", "Brine" } },
  mountains = { adj = { "Craggy", "Jagged", "Lofty", "Frowning", "Toothy" },
    nouns = { "Peaks", "Crags", "Teeth", "Heights", "Spires", "Knuckles", "Tors", "Ridge", "Range",
      "Elbows", "Shoulders" } },
  forest = { adj = { "Whispering", "Shady", "Leafy", "Knotted", "Twiggy" },
    nouns = { "Wood", "Woods", "Forest", "Thicket", "Tangle", "Grove", "Weald", "Wilds", "Copse", "Timbers" } },
  plains = { adj = { "Windy", "Rolling", "Golden", "Flat", "Grassy" },
    nouns = { "Plains", "Flats", "Meadows", "Fields", "Downs", "Lowlands", "Grasslands", "Prairie",
      "Levels", "Sweep" } },
  desert = { adj = { "Thirsty", "Parched", "Scorching", "Dusty", "Baked" },
    nouns = { "Sands", "Wastes", "Dunes", "Barrens", "Dustbowl", "Scorch", "Drylands", "Hotplate",
      "Griddle", "Expanse" } },
  ice = { adj = { "Frozen", "Brittle", "Glittering", "Numb", "Creaking" },
    nouns = { "Ice", "Glacier", "Shelf", "Frost", "Floe", "Cap", "Icefield", "Rime", "Snowfield", "Freeze" } },
  marsh = { adj = { "Soggy", "Squelchy", "Murky", "Damp", "Sodden" },
    nouns = { "Marsh", "Bog", "Fen", "Mire", "Swamp", "Squelch", "Mudflats", "Wetlands", "Sludge", "Slough" } },
  jungle = { adj = { "Steaming", "Humid", "Buzzing", "Verdant", "Sticky" },
    nouns = { "Jungle", "Tangle", "Canopy", "Green", "Snarl", "Vines", "Wilds", "Greenery",
      "Overgrowth", "Thicket" } },
  tundra = { adj = { "Bleak", "Chilly", "Windswept", "Barren", "Hushed" },
    nouns = { "Tundra", "Barrens", "Waste", "Chill", "Moor", "Heath", "Frostlands", "Steppe",
      "Emptiness", "Nothing" } },
  river = { adj = { "Babbling", "Chattering", "Winding", "Lazy", "Hurrying" },
    nouns = { "River", "Run", "Brook", "Beck", "Stream", "Rush", "Wend", "Trickle", "Meander", "Flow" } },
}
names.REGION_KINDS = { "continent", "island", "sea", "ocean", "mountains", "forest", "plains",
  "desert", "ice", "marsh", "jungle", "tundra", "river" }

-- Invented compound continents: "Oldhollow", "Mossmoor", "Emberfall".
local CONT_A = { "Old", "Far", "Grey", "Deep", "Mud", "Moss", "Thorn", "Stone", "Dusk", "Ember",
  "Bramble", "Hum", "Mist", "Frost", "Sun", "Dim", "Bog", "Wren" }
local CONT_B = { "hollow", "reach", "mark", "land", "moor", "fall", "wold", "heap", "hold", "mere",
  "hearth", "lands" }

function names.region(u, kind)
  local k = REGION[kind] or REGION.plains
  if kind == "continent" then
    local r = u()
    if r < 0.33 then
      for _ = 1, 8 do
        local a, b = pick(u, CONT_A), pick(u, CONT_B)
        if lower(sub(a, -1)) ~= sub(b, 1, 1) and not names.is_blocked(a .. b) then return a .. b end
      end
      return "Oldhollow"
    elseif r < 0.66 then
      return "The " .. pick(u, k.adj) .. " " .. pick(u, k.nouns)
    end
  end
  local s
  for _ = 1, 8 do
    local adj
    if kind ~= "continent" and u() < 0.3 then adj = pick(u, k.adj) else adj = pick(u, REGION_ADJ) end
    local noun = pick(u, k.nouns)
    s = adj .. " " .. noun
    if lower(sub(adj, 1, 4)) ~= lower(sub(noun, 1, 4)) then break end  -- no "Frosty Frost"
  end
  if k.the and k.the > 0 and u() < k.the then s = "The " .. s end
  return s
end

---------------------------------------------------------------------------
-- Volcanoes: affectionate
---------------------------------------------------------------------------
local VOLCANO_TITLES = { "Mount", "Mount", "Old", "Big", "Little", "Great", "Uncle", "Auntie",
  "Granny", "Grandpa", "Sleepy", "Poor Old", "Dear Old" }
local VOLCANO_NAMES = {
  "Grumpus", "Hiccup", "Sulky", "Grumbles", "Sniffles", "Tantrum", "Rumbletum", "Snort",
  "Cinders", "Toasty", "Wheezy", "Crumpet", "Bellyache", "Hotfoot", "Sizzle", "Grumblebelly",
  "Snoozer", "Blister", "Kettle", "Cough", "Mutter", "Huff", "Pout", "Fusspot", "Grouch",
  "Scowl", "Fizzle", "Belch", "Soot", "Glower", "Harrumph", "Sneeze", "Porridge", "Bonfire",
  "Crackle", "Simmer", "Stewpot", "Grizzle", "Mumbles", "Sputter", "Hothead", "Fidget",
  "Snuffle", "Bluster",
}
local VOLCANO_SUFFIX = { " Peak", " Top", " Hill", " Summit", "'s Chimney", " Mountain" }
local VOLCANO_SPECIAL = {
  "Mount Probably Dormant", "Mount Not Again", "Mount Oh Dear", "The Angry Hill",
  "Mount Please Don't", "Mount Mostly Quiet", "Mount Sorry", "Big Hot Lump", "The Warm Bit",
  "Mount Any Minute Now", "Old Unreliable", "The Sleeping Grump", "Mount Hush Now",
  "Mount Fine Last Year",
}

function names.volcano(u)
  local r = u()
  if r < 0.12 then return pick(u, VOLCANO_SPECIAL) end
  local n = pick(u, VOLCANO_NAMES)
  if r < 0.40 then return n .. pick(u, VOLCANO_SUFFIX) end
  return pick(u, VOLCANO_TITLES) .. " " .. n
end

---------------------------------------------------------------------------
-- Tankers (the fictional Tanker Company)
---------------------------------------------------------------------------
local TANKER_FIXED = {
  "Good Intentions", "Probably Fine", "Minor Setback", "Oops Again", "Best Efforts",
  "No Comment", "Acceptable Losses", "Lessons Learned", "Under Review", "Deeply Concerned",
  "Plausible Deniability", "Fine Print", "Due Diligence", "Business As Usual", "Silver Lining",
  "Honest Mistake", "Not Our Fault", "Bright Future", "Clean Conscience", "Quarterly Target",
  "Calm Waters", "Nothing To See", "Safety Third", "Shareholder Joy", "Full Steam Ahead",
  "Paperwork Pending", "Whoops Again", "Somebody Else", "Mild Regret", "Close Enough",
  "It Floats", "Still Floating", "Almost Seaworthy", "Moving Forward", "Tuesday's Problem",
  "Lessons Pending", "Measured Response", "Out of Scope",
}
local TANKER_ADV = { "Probably", "Mostly", "Largely", "Nearly", "Totally", "Perfectly",
  "Basically", "Technically", "Apparently", "Allegedly", "Reasonably", "Relatively",
  "Generally", "Hopefully", "Legally" }
local TANKER_ADJ = { "Fine", "Safe", "Seaworthy", "Watertight", "Stable", "Sound", "Compliant",
  "Okay", "Buoyant", "Upright", "Afloat", "Insured", "Reliable", "Sensible", "Hardy", "Tidy" }
local TANKER_ADJ2 = { "Minor", "Slight", "Brief", "Tiny", "Small", "Temporary", "Gentle",
  "Regrettable", "Unforeseen", "Isolated", "Acceptable", "Modest", "Routine", "Standard",
  "Harmless", "Friendly", "Little", "Honest" }
local TANKER_NOUN = { "Setback", "Oversight", "Mishap", "Hiccup", "Wobble", "Leak", "Incident",
  "Blip", "Spillage", "Mistake", "Surprise", "Adjustment", "Detour", "Drip", "Misstep",
  "Puddle", "Kerfuffle" }
local TANKER_SPIRIT = { "Spirit of ", "Pride of " }
local TANKER_VIRTUE = { "Compliance", "Progress", "Efficiency", "Synergy", "Optimism",
  "Innovation", "Confidence", "Enterprise", "Growth", "Tomorrow", "Excellence", "Diligence" }

function names.tanker(u)
  local r = u()
  if r < 0.20 then return pick(u, TANKER_FIXED) end
  if r < 0.55 then return pick(u, TANKER_ADV) .. " " .. pick(u, TANKER_ADJ) end
  if r < 0.90 then return pick(u, TANKER_ADJ2) .. " " .. pick(u, TANKER_NOUN) end
  return pick(u, TANKER_SPIRIT) .. pick(u, TANKER_VIRTUE)
end

---------------------------------------------------------------------------
-- God names (title screen, editable): adjective + noun
---------------------------------------------------------------------------
local GOD_ADJ = {
  "Humble", "Sleepy", "Petty", "Grumpy", "Gentle", "Fickle", "Bored", "Curious", "Tiny",
  "Distracted", "Moody", "Tired", "Clumsy", "Polite", "Nervous", "Restless", "Sulky",
  "Cheerful", "Lazy", "Jealous", "Wobbly", "Anxious", "Hungry", "Ticklish", "Forgetful",
  "Earnest", "Cranky", "Mild", "Dramatic", "Peckish", "Lonely", "Overworked", "Sincere",
  "Impatient", "Whimsical", "Busy", "Shy", "Fussy", "Sheepish", "Grouchy", "Giddy", "Bashful",
  "Sniffly", "Squeaky", "Theatrical",
}
local GOD_NOUN = {
  "Thunder", "Comet", "Monsoon", "Drizzle", "Tempest", "Eclipse", "Avalanche", "Earthquake",
  "Hurricane", "Blizzard", "Meteor", "Tide", "Breeze", "Squall", "Hailstorm", "Lightning",
  "Volcano", "Cyclone", "Gale", "Aurora", "Nebula", "Sunbeam", "Fog", "Frost", "Rainbow",
  "Tremor", "Whirlwind", "Downpour", "Moon", "Cloud", "Typhoon", "Sleet", "Heatwave", "Deluge",
  "Sandstorm", "Drought", "Supernova", "Puddle", "Rumble", "Mist",
}

function names.god(u)
  return pick(u, GOD_ADJ) .. " " .. pick(u, GOD_NOUN)
end

---------------------------------------------------------------------------
-- Display names for species
---------------------------------------------------------------------------
names.species_display = {
  grazers   = { one = "deer", many = "deer" },
  predators = { one = "wolf", many = "wolves" },
  fish      = { one = "fish", many = "fish" },
}

---------------------------------------------------------------------------
-- Uniqueness helper
---------------------------------------------------------------------------
local ROMAN_V = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 }
local ROMAN_S = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" }

local function roman(n)
  local out = ""
  for i = 1, #ROMAN_V do
    while n >= ROMAN_V[i] do
      out = out .. ROMAN_S[i]
      n = n - ROMAN_V[i]
    end
  end
  return out
end

-- Calls gen_fn(u) until it yields a name not in used_set (name -> true), records and
-- returns it. After max_tries, appends a roman numeral: "Good Intentions II", "... III".
function names.unique(u, gen_fn, used_set, max_tries)
  local used = used_set or {}
  local tries = max_tries or 12
  if tries < 1 then tries = 1 end
  local name
  for _ = 1, tries do
    name = gen_fn(u)
    if not used[name] then
      used[name] = true
      return name
    end
  end
  local n = 2
  while used[name .. " " .. roman(n)] do n = n + 1 end
  name = name .. " " .. roman(n)
  used[name] = true
  return name
end

-- Raw word lists, exposed for tests and tooling (read-only by convention).
names.LISTS = {
  region_adj = REGION_ADJ, cont_a = CONT_A, cont_b = CONT_B, hopeful = HOPEFUL,
  volcano_titles = VOLCANO_TITLES, volcano_names = VOLCANO_NAMES, volcano_suffix = VOLCANO_SUFFIX,
  volcano_special = VOLCANO_SPECIAL, tanker_fixed = TANKER_FIXED, tanker_adv = TANKER_ADV,
  tanker_adj = TANKER_ADJ, tanker_adj2 = TANKER_ADJ2, tanker_noun = TANKER_NOUN,
  tanker_spirit = TANKER_SPIRIT, tanker_virtue = TANKER_VIRTUE, god_adj = GOD_ADJ,
  god_noun = GOD_NOUN, region = REGION,
}

return names
