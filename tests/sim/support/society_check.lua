-- Structural invariants of the settlement layer (sim/society/): every list and back-reference
-- agrees. Returns nil when the society is consistent, else the first violation as text.
local M = {}

function M.check(W)
  local E = W.ent
  local P, H, B, Bo = E.people, E.households, E.buildings, E.boats
  local owner = {}   -- person id -> village id (members lists)
  for vi, v in ipairs(E.villages) do
    if v.id ~= vi then return "village id ~= index at " .. vi end
    if v.alive then
      if v.pop ~= #v.members then return v.name .. ": pop " .. v.pop .. " ~= #members " .. #v.members end
      if W.F.land_use[v.cell] ~= -v.id then return v.name .. ": centre land_use" end
      for _, id in ipairs(v.members) do
        local p = P[id]
        if not p then return v.name .. ": member " .. id .. " missing" end
        if p.dead then return v.name .. ": dead member " .. id end
        if p.village ~= v.id then return v.name .. ": member " .. id .. " lives in " .. p.village end
        if p.away ~= 0 then
          -- only porters on an errand stay members while away (sim/society/relations.lua)
          local pa = W.ent.parties and W.ent.parties[p.away]
          if not pa or (pa.kind ~= "porters" and pa.kind ~= "returning") then return v.name .. ": member " .. id .. " is away" end
        end
        if owner[id] then return "person " .. id .. " in two villages" end
        owner[id] = v.id
        local h = H[p.household]
        if not h then return v.name .. ": person " .. id .. " without a household" end
        if h.village ~= v.id then return "person " .. id .. " in a household of another village" end
        local found = false
        for _, m in ipairs(h.members) do if m == id then found = true end end
        if not found then return "person " .. id .. " not listed in household " .. h.id end
        if p.partner ~= 0 then
          local q = P[p.partner]
          if not q or q.partner ~= p.id then return "partners not mutual: " .. id end
        end
        if p.betrothed ~= 0 then
          local q = P[p.betrothed]
          if not q or q.betrothed ~= p.id then return "betrothal not mutual: " .. id end
        end
      end
      local seen_h = {}
      for _, hid in ipairs(v.households) do
        local h = H[hid]
        if not h then return v.name .. ": household " .. hid .. " missing" end
        if seen_h[hid] then return v.name .. ": household listed twice" end
        seen_h[hid] = true
        for _, m in ipairs(h.members) do
          if not P[m] or P[m].household ~= hid then return "household " .. hid .. " lists " .. m .. " who lives elsewhere" end
        end
        if h.home ~= 0 then
          local b = B[h.home]
          if not b then return "household " .. hid .. " home missing" end
          if b.household ~= hid then return "household " .. hid .. " home " .. b.id .. " points to " .. b.household end
          if b.village ~= v.id then return "household " .. hid .. " lives in another village's house" end
        end
      end
      local lots = {}
      for _, bid in ipairs(v.buildings) do
        local b = B[bid]
        if not b then return v.name .. ": building " .. bid .. " missing" end
        if b.village ~= v.id then return "building " .. bid .. " village" end
        if b.household ~= 0 and (not H[b.household] or H[b.household].home ~= b.id) then
          return "building " .. bid .. " occupant does not live there"
        end
        if b.state ~= "gone" and b.kind ~= "boat" then
          local key = b.cell * 64 + b.lot
          if lots[key] then return v.name .. ": two buildings on lot " .. b.lot .. " of cell " .. b.cell end
          lots[key] = true
        end
      end
      for _, id in ipairs(v.boats) do
        local bo = Bo[id]
        if not bo then return v.name .. ": boat " .. id .. " missing" end
        if bo.village ~= v.id then return "boat " .. id .. " village" end
        for _, cid in ipairs(bo.crew) do
          local p = P[cid]
          if p and not p.dead and p.village == v.id and p.task ~= "fish" then return "boat crew " .. cid .. " is not fishing" end
        end
      end
      for _, c in ipairs(v.farms) do
        if W.F.land_use[c] ~= v.id then return v.name .. ": farm " .. c .. " land_use " .. W.F.land_use[c] end
        local f = W.S.farm[c]
        if not f or f.v ~= v.id then return v.name .. ": farm record " .. c end
        if f.sown < -1e-9 or f.sown > 1 + 1e-9 or f.harvested < -1e-9 or f.harvested > 1 + 1e-9 then return "farm fractions " .. c end
      end
      for k, x in pairs(v.stock) do
        if x ~= x or x < -1e-6 then return v.name .. ": stock " .. k .. " = " .. tostring(x) end
      end
    end
  end
  for id, party in pairs(E.parties) do
    for _, m in ipairs(party.members) do
      local p = P[m]
      if p and not p.dead and p.away ~= id then return "party " .. id .. " member " .. m .. " not away with it" end
    end
  end
  for id, p in pairs(P) do
    if not p.dead and p.away == 0 and not owner[id] then
      local v = E.villages[p.village]
      if v and v.alive then return "living person " .. id .. " missing from " .. v.name end
    end
  end
  return nil
end

return M
