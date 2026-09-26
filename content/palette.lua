-- Palette (06_PRESENTATION.md §1). Pure data shared by the view and the headless map tools.
local P = {
  deep_ocean = "#1F5FA8", shallow_ocean = "#3FB7C9", foam = "#E8F7F5",
  sea_ice = "#DDF2FF", ice_sheet = "#F4FBFF",
  grassland = "#8CCB5E", forest = "#2E8B4A", jungle = "#1F7A3A",
  farmland = "#E3C35A", desert = "#E8C98A", beach = "#F2DFA7",
  tundra = "#B9C7A3", marsh = "#6E9C6A", barren = "#A89C86",
  ash = "#5A5552", ruins = "#8A7F74", village = "#C9A27A",
  lava = "#FF5A1F", fire = "#FF9F2E",
  oil = "#1B1424", oil_sheen = "#3C2A5A",
  sky_top = "#2B6FC4", sky_mid = "#5FD0F0", sky_horizon = "#FFF1B8",
  ui_panel = "#FFF8EC", ui_text = "#2A2335",
  god = "#F2B233", nature = "#3AA6A0", danger = "#F26B5B",
}

-- Biome id (sim/systems/biome.lua) -> palette key
P.BIOME_KEY = { "deep_ocean", "sea_ice", "ice_sheet", "village", "farmland", "ruins", "ash",
  "marsh", "desert", "tundra", "jungle", "forest", "grassland", "barren" }

function P.rgb(hex)
  return tonumber(hex:sub(2, 3), 16), tonumber(hex:sub(4, 5), 16), tonumber(hex:sub(6, 7), 16)
end

return P
