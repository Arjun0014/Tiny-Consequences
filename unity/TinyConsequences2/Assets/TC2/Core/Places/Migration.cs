using System;
using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>A party of households on its way to a new home (D2-045). State: saved.</summary>
    public struct Party
    {
        public int Id;
        public int Origin;      // the village they leave
        public int Site;        // the place they go to found a village (-1 when joining one)
        public int Dest;        // the village they go to join (0 when founding)
        public long Depart, Arrive;
        public long Event;      // the history event of their setting out
        public double Food;     // FU of the village's store they carry (their larders go with them too)
        public byte State;      // 0 gathering (leaving at Depart), 1 on the way

        public void Write(BinWriter w) { w.Int(Id); w.Int(Origin); w.Int(Site); w.Int(Dest); w.Long(Depart); w.Long(Arrive); w.Long(Event); w.Double(Food); w.Byte(State); }
        public static Party Read(BinReader r) => new Party { Id = r.Int(), Origin = r.Int(), Site = r.Int(), Dest = r.Int(), Depart = r.Long(), Arrive = r.Long(), Event = r.Long(), Food = r.Double(), State = r.Byte() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Origin); h.Add(Site); h.Add(Dest); h.Add(Depart); h.Add(Arrive); h.Add(Event); h.Add(Food); h.Add(State); }
    }

    /// <summary>
    /// Households leave, move and found (D2-045). Once a year, after the harvest, each household of a pressed village (land too
    /// short for its mouths, rations cut, famine remembered) weighs leaving; those who go together to a place their village knows
    /// - seen by its own scouts - set out as a party the next morning with their larders and a share of the store, walk there,
    /// and make camp: a village with no houses, no well, no granary and no fields until it raises and sows them. Nobody knows a
    /// place nobody went to see. Both resolutions decide with the same functions; ACTIVITY walks the way, COARSE arrives in the
    /// same time.
    /// </summary>
    public sealed class Migration
    {
        public const int DecideDay = 41;          // local day of the year households weigh leaving (after the harvest)
        public const int MinHouseholds = 2, MinPeople = 5;
        public const double CarryDays = 20;       // days of the party's food taken from the village's store, at most
        public const double PartySpeed = 0.9;     // m/s: a party walks at its children's and its loads' pace
        public const double ScoutMin = 2000, ScoutMax = 4200;   // m: how far a scout goes to look

        World _w;
        readonly List<int> _scratch = new List<int>(), _cells = new List<int>(), _leaving = new List<int>();

        public void Bind(World w) { _w = w; }

        /// <summary>
        /// What presses a village's households to leave: the share of its food its land cannot grow, rations cut now, famine
        /// remembered. 0 for a village that lives well within its land.
        /// </summary>
        public double Pressure(in Settlement s)
        {
            var cfg = _w.Config;
            double need = DMath.Max(1.0, s.Population * cfg.FieldHaPerPerson);
            double crowding = DMath.Clamp01(1.0 - s.ArableHa / need);
            double shortage = DMath.Max(0.0, 1.0 - s.Ration);
            return crowding + 0.6 * shortage + 0.4 * s.Memory;
        }

        /// <summary>Whether a household can take to the road now: grown heads young enough to start over, no birth near.</summary>
        bool Mobile(int hid)
        {
            var w = _w;
            var mem = w.Members(hid);
            if (mem.Count == 0) return false;
            double head = 0;
            for (int i = 0; i < mem.Count; i++)
            {
                ref var p = ref w.People.Get(mem[i]);
                if (!p.Alive) continue;
                if (p.PregDue != 0 && p.PregDue - w.Now < 12 * SimTime.Day) return false;
                double age = p.AgeYears(w.Now);
                if (age > head) head = age;
            }
            return head >= 17 && head <= 50;
        }

        /// <summary>
        /// The yearly weighing (SettlementSystem.Daily, both resolutions): households of a pressed village decide to leave by a
        /// keyed chance growing with the pressure (young households more); together, to the best place the village knows.
        /// </summary>
        public void Weigh(int sid)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            // a new village has not sown its land yet: it does not measure its land by a year without fields
            if (w.Now - s.Founded < SimTime.Years(3)) return;
            double pressure = Pressure(in s);
            if (pressure < 0.15) return;
            // the place: the best the village knows that is still free and still good (or none)
            int site = BestSite(sid, out double q);
            _leaving.Clear();
            int people = 0;
            var hhs = w.HouseholdsOf(sid);
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                ref var hh = ref w.Households.Get(hid);
                if (hh.Party != 0 || !Mobile(hid)) continue;
                double chance = DMath.Clamp(1.1 * pressure - 0.12, 0.0, 0.45) * (w.Now - hh.Formed < SimTime.Years(6) ? 1.4 : 1.0);
                if (KeyedRng.Unit(w.Seed, RngStream.Demography, (ulong)hid, (ulong)SimTime.FloorDiv(w.Now, SimTime.Day), 41) >= chance) continue;
                _leaving.Add(hid);
                people += w.Members(hid).Count;
            }
            if (_leaving.Count == 0) return;
            if (site >= 0 && _leaving.Count >= MinHouseholds && people >= MinPeople)
            {
                // together, to found a village at the place their people saw
                int pid = NewParty(sid, site, 0);
                for (int i = 0; i < _leaving.Count; i++) w.Households.Get(_leaving[i]).Party = pid;
                s.Lore.Forget(site);   // (taken: not offered to the next party)
                return;
            }
            // too few to make a village, or no place known: each goes to a village it knows with land to spare and less want -
            // where kin live, sooner - or stays this year
            for (int i = 0; i < _leaving.Count; i++)
            {
                int hid = _leaving[i];
                int dest = VillageToJoin(sid, hid, pressure);
                if (dest == 0) continue;
                int pid = NewParty(sid, -1, dest);
                w.Households.Get(hid).Party = pid;
            }
        }

        /// <summary>A party that sets out the next morning at the village's seven o'clock.</summary>
        int NewParty(int sid, int site, int dest)
        {
            var w = _w;
            int pid = w.Ids.Next(EntityKind.Process);
            long off = w.LocalOffsetOf(sid);
            long depart = SimTime.StartOfDay(w.Now + off) + SimTime.Day + 7 * SimTime.Hour - off;
            w.Parties.Add(pid, new Party { Id = pid, Origin = sid, Site = site, Dest = dest, Depart = depart, State = 0 });
            w.Queue.Push(depart, Phase.Process, EventKind.PartyDepart, pid);
            return pid;
        }

        /// <summary>
        /// A village a household could join (D2-045): one its people know (within a day's walk, as marriages and visits reach),
        /// with arable land to spare for them and less want than home; kin there make it likelier. 0 when none will do.
        /// </summary>
        int VillageToJoin(int sid, int hid, double homePressure)
        {
            var w = _w; var P = w.Planet; var cfg = w.Config;
            ref var s = ref w.Settlements.Get(sid);
            double need = w.Members(hid).Count * cfg.FieldHaPerPerson;
            int best = 0; double bs = 0;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref w.Settlements.Get(o);
                if (!t.Inhabited || w.Now - t.Founded < SimTime.Years(1)) continue;
                double d = Vec3d.ArcDistance(s.Anchor, t.Anchor, P.Radius);
                if (d > cfg.MarriageReach) continue;
                double p = Pressure(in t);
                if (p > homePressure - 0.1) continue;
                double spare = SpareLand(o) - need;
                if (spare <= 0) continue;
                if (w.Routes.Get(w, sid, o) == null) continue;
                double score = DMath.Min(spare, 10.0) / (1.0 + d / 2000.0) * (1.0 - DMath.Min(0.9, p)) * (KinIn(hid, o) ? 2.0 : 1.0);
                if (score > bs) { bs = score; best = o; }
            }
            return best;
        }

        /// <summary>Arable hectares within a village's fields' reach that no other village holds, beyond what its people need.</summary>
        double SpareLand(int sid)
        {
            var w = _w; var P = w.Planet;
            ref var s = ref w.Settlements.Get(sid);
            w.Hazards.CellsWithin(w, s.Cell, w.Config.FieldReach, _scratch);
            double ha = 0;
            for (int i = 0; i < _scratch.Count; i++)
            {
                int c = _scratch[i];
                if (!P.IsLand(c) || (P.FieldOwner[c] != 0 && P.FieldOwner[c] != sid) || w.SettlementAtCell(c) != 0 && w.SettlementAtCell(c) != sid) continue;
                ha += P.AreaHa(c) * P.Arability(c, w.PlanetSys.MeanTemp(c));
            }
            return ha - s.Population * w.Config.FieldHaPerPerson;
        }

        /// <summary>Whether a household has kin living in a village (a parent, a child or a sibling of one of its members).</summary>
        bool KinIn(int hid, int sid)
        {
            var w = _w;
            var mem = w.Members(hid);
            var res = w.Residents(sid);
            for (int i = 0; i < mem.Count; i++)
            {
                ref var a = ref w.People.Get(mem[i]);
                for (int j = 0; j < res.Count; j++)
                {
                    ref var b = ref w.People.Get(res[j]);
                    if (!b.Alive) continue;
                    if (b.Id == a.Mother || b.Id == a.Father || b.Mother == a.Id || b.Father == a.Id) return true;
                    if (a.Mother != 0 && b.Mother == a.Mother) return true;
                }
            }
            return false;
        }

        int BestSite(int sid, out double q)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            q = 0;
            int best = -1;
            // what the village knows, looked at again: a village may have been founded there since
            for (int k = 0; k < 3; k++)
            {
                int c = k == 0 ? s.Lore.C0 : k == 1 ? s.Lore.C1 : s.Lore.C2;
                if (c < 0) continue;
                double v = Sites.Quality(w, c, _scratch);
                if (v < Sites.MinQuality) { s.Lore.Forget(c); continue; }
                if (v > q) { q = v; best = c; }
            }
            return best;
        }

        /// <summary>The party sets out (PartyDepart): the route, the store's share, the history; in ACTIVITY everyone walks.</summary>
        public void Depart(int pid)
        {
            var w = _w;
            if (!w.Parties.Exists(pid)) return;
            ref var party = ref w.Parties.Get(pid);
            int sid = party.Origin;
            ref var s = ref w.Settlements.Get(sid);
            var route = party.Site >= 0 ? w.Routes.GetToCell(w, sid, party.Site) : w.Routes.Get(w, sid, party.Dest);
            // who is going: the households still bound to it (a death or a marriage may have changed them)
            _leaving.Clear();
            int people = 0;
            for (int hid = w.Households.First(); hid != 0; hid = w.Households.Next(hid))
            {
                ref var hh = ref w.Households.Get(hid);
                if (hh.Party != pid || hh.Dissolved != 0) continue;
                if (route == null || hh.Settlement != sid) { hh.Party = 0; continue; }
                _leaving.Add(hid); people += w.Members(hid).Count;
            }
            if (route == null || _leaving.Count == 0 || people == 0)
            {
                for (int i = 0; i < _leaving.Count; i++) w.Households.Get(_leaving[i]).Party = 0;
                w.Parties.Remove(pid);
                return;
            }
            // food for the way and the first weeks from the village's store, as their share of it allows
            double share = s.Population > 0 ? (double)people / s.Population : 0;
            double food = DMath.Min(s.Granary * share, CarryDays * people * w.Config.FoodPerAdultDay);
            s.Granary -= food;
            party.Food = food;
            // why they go: the famine they remember, the hunger or the rationing now, else their own choice for more land
            long cause = s.FamineEvent != 0 && s.Memory > 0.3 ? s.FamineEvent : (s.HungerEvent != 0 ? s.HungerEvent : s.RationEvent);
            if (party.Site >= 0)
                party.Event = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.PartySetOut, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                    Object = new EntityRef(EntityKind.Process, pid), Cause1 = cause, Rel1 = CauseRel.InResponseTo,
                    RootKind = cause == 0 ? RootKind.People : RootKind.Unknown, ArgA = _leaving.Count, ArgB = people,
                });
            else
                party.Event = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.HouseholdMoved, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Household(_leaving[0]),
                    Object = EntityRef.Settlement(party.Dest), Cause1 = cause, Rel1 = CauseRel.InResponseTo,
                    RootKind = cause == 0 ? RootKind.People : RootKind.Unknown, ArgA = _leaving.Count, ArgB = people,
                });
            w.History.Register(new EntityRef(EntityKind.Process, pid), "the party from " + w.History.NameOf(EntityRef.Settlement(sid)), w.Now);
            long dur = Math.Max(60L, DMath.ToLong(DMath.RoundHalfUp(route.Length / PartySpeed)));
            party.Arrive = w.Now + dur;
            party.State = 1;
            if (w.Resolution == Resolution.Activity)
                for (int i = 0; i < _leaving.Count; i++)
                {
                    var mem = w.Members(_leaving[i]);
                    for (int j = 0; j < mem.Count; j++) w.Agents.Walk(mem[j], route, route.A == sid, party.Arrive, party.Site >= 0 ? Routes0(party) : party.Dest);
                }
            w.Queue.Push(party.Arrive, Phase.Process, EventKind.PartyArrive, pid);
            w.Metrics.Parties++;
        }

        static int Routes0(in Party party) => RouteCache.PlaceEnd(party.Site);

        /// <summary>
        /// The party arrives (PartyArrive): at a place, they found a village - a camp, with what they carried; joining a village,
        /// they become its households. Either way their homes are yet to be built.
        /// </summary>
        public void Arrive(int pid)
        {
            var w = _w;
            if (!w.Parties.Exists(pid)) return;
            var party = w.Parties.Get(pid);
            int dest = party.Dest;
            if (party.Site >= 0)
            {
                // the place may have been taken meanwhile (another party, a village grown near): they found it all the same - a
                // party on the land does not turn back for a report it never had
                dest = FoundAt(party.Site, party.Event, party.Origin);
            }
            if (dest == 0 || !w.Settlements.Exists(dest) || !w.Settlements.Get(dest).Inhabited)
            {
                // (the village they went to is gone: they make camp where it stood - a village again)
                if (dest != 0 && w.Settlements.Exists(dest)) dest = FoundAt(w.Settlements.Get(dest).Cell, party.Event, party.Origin);
                else { for (int hid = w.Households.First(); hid != 0; hid = w.Households.Next(hid)) if (w.Households.Get(hid).Party == pid) w.Households.Get(hid).Party = 0; w.Parties.Remove(pid); return; }
            }
            ref var d = ref w.Settlements.Get(dest);
            d.Granary += party.Food;
            for (int hid = w.Households.First(); hid != 0; hid = w.Households.Next(hid))
            {
                ref var hh = ref w.Households.Get(hid);
                if (hh.Party != pid) continue;
                hh.Party = 0;
                if (hh.Dissolved != 0) continue;
                Rehome(hid, dest);
            }
            w.Parties.Remove(pid);
        }

        /// <summary>A household moves its home to another village: a new lot, a camp to begin with, its people with it.</summary>
        void Rehome(int hid, int dest)
        {
            var w = _w;
            ref var hh = ref w.Households.Get(hid);
            int from = hh.Settlement;
            if (from == dest) return;
            w.HouseholdsOf(from).Remove(hid);
            ref var d = ref w.Settlements.Get(dest);
            d.LotPosition(d.Lots++, out float hx, out float hy);
            hh.Settlement = dest; hh.HomeX = hx; hh.HomeY = hy; hh.Shelter = Construction.Camp; hh.Build = 0f;
            w.HouseholdsOf(dest).InsertSorted(hid);
            var mem = w.Members(hid);
            for (int j = 0; j < mem.Count; j++)
            {
                int id = mem[j];
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                if (p.Home == dest) continue;
                w.Residents(p.Home).Remove(id);
                w.Settlements.Get(p.Home).Population--;
                if (p.At != 0 && p.At != p.Home) w.Visitors(p.At).Remove(id);
                p.Home = dest;
                w.Residents(dest).InsertSorted(id);
                w.Settlements.Get(dest).Population++;
                // in COARSE everyone is where they live; in ACTIVITY a walker arrives with their step, a visitor goes home later
                if (w.Resolution == Resolution.Coarse || p.At == from) p.At = p.Step == StepKind.Travel ? 0 : dest;
                if (p.At != 0 && p.At != dest) w.Visitors(p.At).InsertSorted(id);
            }
        }

        /// <summary>A village is founded at a place (at a party's arrival): its frame, its ground, no houses or works yet.</summary>
        public int FoundAt(int cell, long cause, int origin)
        {
            var w = _w; var P = w.Planet;
            int sid = w.Ids.Next(EntityKind.Settlement);
            Vec3d anchor = P.Grid.Center[cell];
            Settlement.Frame(anchor, out Vec3d east, out Vec3d north);
            // the fields will lie toward the most fertile land beside the camp; wood toward the woods
            int fieldCell = cell; double fbest = -1;
            int woodCell = cell; double wbest = -1;
            for (int j = 0; j < 8; j++)
            {
                int nb = P.Grid.Nbr8[cell * 8 + j];
                if (!P.IsLand(nb)) continue;
                double f = P.Moist[nb] + 0.3 * P.Grass[nb] - 0.2 * P.Forest[nb];
                if (f > fbest) { fbest = f; fieldCell = nb; }
                if (P.Forest[nb] > wbest) { wbest = P.Forest[nb]; woodCell = nb; }
            }
            var s = new Settlement
            {
                Id = sid, Cell = cell, Anchor = anchor, East = east, North = north, Founded = w.Now, FieldCell = fieldCell, Ration = 1.0,
                Hunt = Grounds.Empty, Fish = Grounds.Empty, Gath = Grounds.Empty, Lore = KnownSites.Empty, Works = 0, Crop = CropStage.Fallow,
            };
            s.SphereToLocal(P.Grid.Center[fieldCell], P.Radius, out double fx, out double fy);
            double fl = DMath.Sqrt(fx * fx + fy * fy);
            if (fl < 1) { fx = 150; fy = 0; fl = 150; }
            s.FieldX = (float)(fx / fl * 170); s.FieldY = (float)(fy / fl * 170);
            s.SphereToLocal(P.Grid.Center[woodCell], P.Radius, out double wx, out double wy);
            double wl = DMath.Sqrt(wx * wx + wy * wy);
            if (wl < 1) { wx = 0; wy = 200; wl = 200; }
            s.WoodX = (float)(wx / wl * 210); s.WoodY = (float)(wy / wl * 210);
            s.WellX = 8f; s.WellY = -6f;
            w.Settlements.Add(sid, s);
            w.IndexSettlementCells(sid);
            w.History.Register(EntityRef.Settlement(sid), Worldgen.Names.Place(w.Seed, sid), w.Now);
            ref var sr = ref w.Settlements.Get(sid);
            sr.FoundedEvent = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.SettlementFounded, Time = w.Now, Cell = cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                Object = EntityRef.Settlement(origin), Cause1 = cause, Rel1 = CauseRel.CausedBy,
            });
            w.Routes.Invalidate();
            w.Wild.Survey(sid);
            w.SettlementSys.ScheduleFirstDaily(sid);
            w.Metrics.Foundings++;
            return sid;
        }

        // ------------------------------------------------------------------ knowing places
        /// <summary>Whether a village wants someone to go and look at the land beyond: pressed, and knowing no good place.</summary>
        public bool WantsScout(in Settlement s)
        {
            if (!s.Inhabited || _w.Now - s.Founded < SimTime.Years(3) || Pressure(in s) < 0.12) return false;
            if (_w.Now - s.ScoutAt < 6 * SimTime.Day) return false;   // someone went lately
            s.Lore.Best(out float q);
            return q < 0.6f;
        }

        /// <summary>A land cell 2-4 km out in a keyed direction, reachable on foot, or -1 (a scout's goal).</summary>
        public int ScoutTarget(int sid, ref RandomSequence rs)
        {
            var w = _w; var P = w.Planet;
            ref var s = ref w.Settlements.Get(sid);
            for (int tries = 0; tries < 8; tries++)
            {
                double ang = rs.NextUnit() * DMath.TwoPi, d = rs.Range(ScoutMin, ScoutMax);
                int c = P.Grid.CellOf(s.LocalToSphere(DMath.Cos(ang) * d, DMath.Sin(ang) * d, P.Radius));
                if (!P.IsLand(c) || P.Ice[c] > 5f || P.LakeDepth[c] > 0.3f) continue;
                if (w.Routes.GetToCell(w, sid, c) == null) continue;
                return c;
            }
            return -1;
        }

        /// <summary>
        /// Someone of a village stands at a place and looks round (a scout, a hunter far afield): the best site within sight
        /// enters the village's knowledge; a good one is told (history).
        /// </summary>
        public void Look(int sid, int at, int person)
        {
            var w = _w;
            if (!w.Settlements.Exists(sid)) return;
            w.Hazards.CellsWithin(w, at, Sites.LookRadius, _cells);
            int best = -1; double bq = 0;
            for (int i = 0; i < _cells.Count; i++)
            {
                double q = Sites.Quality(w, _cells[i], _scratch);
                if (q > bq) { bq = q; best = _cells[i]; }
            }
            ref var s = ref w.Settlements.Get(sid);
            s.ScoutAt = w.Now;
            if (best < 0 || bq < Sites.MinQuality) return;
            s.Lore.Best(out float known);
            s.Lore.Learn(best, bq, w.Now);
            if (bq > known + 0.05)
                w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.Scouted, Time = w.Now, Cell = best, Settlement = sid, Subject = EntityRef.Settlement(sid),
                    Object = person != 0 ? EntityRef.Person(person) : EntityRef.None, Magnitude = (float)bq, RootKind = RootKind.People,
                });
        }
    }
}
