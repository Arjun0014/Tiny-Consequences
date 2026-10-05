using System;
using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;
using Resolution = TC2.Core.Simulation.Resolution;

namespace TC2.Core.Places
{
    /// <summary>
    /// A settlement's day (07 §7, 08 §2), once at local midnight: the crop calendar and harvest, food from
    /// the granary to the larders, hunger, deaths, marriages and new households, conceptions; births are
    /// scheduled events of their own. Iteration is always in ascending id order.
    /// </summary>
    public sealed class SettlementSystem
    {
        World _w;
        readonly List<int> _scratch = new List<int>();
        readonly List<int> _men = new List<int>();

        public void Bind(World w) { _w = w; }

        public void ScheduleFirstDaily(int sid)
        {
            ref var s = ref _w.Settlements.Get(sid);
            long off = Planet.PlanetState.LocalOffset(s.Anchor);
            // next local midnight
            long t = _w.Now;
            long localNow = t + off;
            long nextLocalMidnight = SimTime.StartOfDay(localNow) + SimTime.Day;
            _w.Queue.Push(nextLocalMidnight - off, Phase.Settlement, EventKind.SettlementDaily, sid);
        }

        public void Daily(int sid)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            long now = w.Now;
            int doy = SimTime.DayOfYear(now + Planet.PlanetState.LocalOffset(s.Anchor));
            if (s.Anchor.Y < 0) doy = (doy + 24) % SimTime.DaysPerYear; // southern seasons
            Crops(ref s, doy);
            Food(ref s, doy);
            if (w.Resolution == Resolution.Coarse) w.Agents.CoarseDay(sid);
            Demography(sid);
            ref var s2 = ref w.Settlements.Get(sid);
            if (s2.Population <= 0 && s2.Abandoned == 0)
            {
                s2.Abandoned = now;
                w.Record(new HistoryEvent { Kind = HistoryKind.SettlementAbandoned, Time = now, Cell = s2.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid), Cause1 = s2.HungerEvent });
                w.History.EndEntity(EntityRef.Settlement(sid), now);
                return;
            }
            w.Queue.Push(now + SimTime.Day, Phase.Settlement, EventKind.SettlementDaily, sid);
        }

        void Crops(ref Settlement s, int doy)
        {
            var w = _w;
            var cfg = w.Config;
            var P = w.Planet;
            if (doy == 0)
            {
                s.Crop = CropStage.Sown; s.CropWork = 0; s.Sown = 0; s.Harvested = 0; s.CropGrowth = 0; s.CropBurnt = 0; s.CropCause = 0;
                ClaimFields(ref s, s.Population * cfg.FieldHaPerPerson);
                s.FieldHa = Math.Max(0.5, Math.Min(s.Population * cfg.FieldHaPerPerson, s.ArableHa));
                s.CropWorkNeed = s.FieldHa * 40.0;
                s.ExpectedHarvest = s.FieldHa * cfg.FieldYieldPerHa * 0.8;
            }
            else if (s.Crop == CropStage.Sown && doy >= 12)
            {
                s.Sown = DMath.Clamp01(s.CropWork / Math.Max(1.0, s.CropWorkNeed));
                s.Crop = CropStage.Growing; s.CropWork = 0; s.CropWorkNeed = s.FieldHa * 8.0;
            }
            else if (s.Crop == CropStage.Growing && doy >= 30)
            {
                s.Crop = CropStage.Ripe; s.CropWork = 0; s.CropWorkNeed = s.FieldHa * 45.0;
            }
            else if (s.Crop == CropStage.Ripe && doy >= 39)
            {
                s.Harvested = DMath.Clamp01(s.CropWork / Math.Max(1.0, s.CropWorkNeed));
                double yield = s.FieldHa * cfg.FieldYieldPerHa * DMath.Clamp01(s.CropGrowth) * s.Sown * s.Harvested * (1.0 - s.CropBurnt);
                s.Granary += yield;
                s.LastHarvest = yield;
                s.Crop = CropStage.Harvested; s.CropWorkNeed = 0; s.CropWork = 0;
                long hev = w.Record(new HistoryEvent { Kind = HistoryKind.Harvest, Time = w.Now, Cell = s.FieldCell, Settlement = s.Id, Subject = EntityRef.Settlement(s.Id), Magnitude = (float)yield });
                if (s.ExpectedHarvest > 0 && yield < 0.5 * s.ExpectedHarvest)
                {
                    w.Record(new HistoryEvent
                    {
                        Kind = HistoryKind.HarvestFailed, Time = w.Now, Cell = s.FieldCell, Settlement = s.Id, Subject = EntityRef.Settlement(s.Id),
                        Cause1 = s.CropCause, Rel1 = CauseRel.CausedBy, Cause2 = hev, Rel2 = CauseRel.PartOf,
                        Magnitude = (float)(1.0 - yield / s.ExpectedHarvest), RootKind = RootKind.Nature,
                    });
                }
            }
            if (s.Crop == CropStage.Growing)
            {
                // growth on the claimed cells, weighted by their arable area (the weather of each field)
                double tf = 0, wf = 0, wsum = 0;
                ForEachField(s.Id, (c, ha) =>
                {
                    double t = P.Temp[c];
                    tf += ha * DMath.SmoothStep(3, 13, t) * (1.0 - DMath.SmoothStep(31, 39, t));
                    wf += ha * DMath.SmoothStep(0.06, 0.32, P.SoilWater[c]);
                    wsum += ha;
                });
                if (wsum > 0) { tf /= wsum; wf /= wsum; }
                else
                {
                    int fc = s.FieldCell;
                    tf = DMath.SmoothStep(3, 13, P.Temp[fc]) * (1.0 - DMath.SmoothStep(31, 39, P.Temp[fc]));
                    wf = DMath.SmoothStep(0.06, 0.32, P.SoilWater[fc]);
                }
                double tend = 0.75 + 0.25 * DMath.Clamp01(s.CropWork / Math.Max(1.0, s.CropWorkNeed));
                s.CropGrowth = DMath.Clamp01(s.CropGrowth + tf * wf * tend / 16.0);
            }
        }

        readonly List<int> _claims = new List<int>();
        readonly List<(double score, int cell)> _cand = new List<(double, int)>();

        /// <summary>Calls back (cell, arable ha) for every cell the settlement's fields claim.</summary>
        void ForEachField(int sid, Action<int, double> f)
        {
            var w = _w; var P = w.Planet;
            var list = w.FieldCellsOf(sid);
            for (int i = 0; i < list.Count; i++)
            {
                int c = list[i];
                f(c, P.AreaHa(c) * P.Arability(c, w.PlanetSys.MeanTemp(c)));
            }
        }

        /// <summary>
        /// Each spring a settlement holds on to the fields it needs and claims more within reach when it has grown
        /// (best land first, never another village's), or lets the worst go when it has shrunk (08 §1).
        /// </summary>
        void ClaimFields(ref Settlement s, double needHa)
        {
            var w = _w; var P = w.Planet; var cfg = w.Config;
            var mine = w.FieldCellsOf(s.Id);
            double have = 0;
            for (int i = 0; i < mine.Count; i++) have += P.AreaHa(mine[i]) * P.Arability(mine[i], w.PlanetSys.MeanTemp(mine[i]));
            if (have > needHa * 1.3 && mine.Count > 1)
            {
                // release the poorest claims beyond the need
                _cand.Clear();
                for (int i = 0; i < mine.Count; i++) { int c = mine[i]; _cand.Add((P.Arability(c, w.PlanetSys.MeanTemp(c)), c)); }
                _cand.Sort((x, y) => x.score != y.score ? x.score.CompareTo(y.score) : x.cell.CompareTo(y.cell));
                for (int i = 0; i < _cand.Count && have > needHa * 1.15; i++)
                {
                    int c = _cand[i].cell;
                    double ha = P.AreaHa(c) * _cand[i].score;
                    if (have - ha < needHa) break;
                    have -= ha;
                    P.FieldOwner[c] = 0;
                    mine.Remove(c);
                }
            }
            else if (have < needHa)
            {
                w.Hazards.CellsWithin(w, s.Cell, cfg.FieldReach, _claims);
                _cand.Clear();
                for (int i = 0; i < _claims.Count; i++)
                {
                    int c = _claims[i];
                    if (P.FieldOwner[c] != 0 || !P.IsLand(c)) continue;
                    if (w.SettlementAtCell(c) != 0) continue; // a village's own ground: houses, yards, gardens
                    double a = P.Arability(c, w.PlanetSys.MeanTemp(c));
                    if (a < 0.08) continue;
                    double d = Vec3d.ArcDistance(s.Anchor, P.Grid.Center[c], P.Radius);
                    _cand.Add((a / (1.0 + d / 600.0), c));
                }
                _cand.Sort((x, y) => x.score != y.score ? y.score.CompareTo(x.score) : x.cell.CompareTo(y.cell));
                for (int i = 0; i < _cand.Count && have < needHa; i++)
                {
                    int c = _cand[i].cell;
                    P.FieldOwner[c] = s.Id;
                    mine.InsertSorted(c);
                    have += P.AreaHa(c) * P.Arability(c, w.PlanetSys.MeanTemp(c));
                }
            }
            s.ArableHa = have;
            s.FieldCells = mine.Count;
        }

        public void ClaimInitialFields(int sid, double needHa)
        {
            ref var s = ref _w.Settlements.Get(sid);
            ClaimFields(ref s, needHa);
        }

        void Food(ref Settlement s, int doy)
        {
            var w = _w;
            // the granary refills each household's larder to two days of its members' needs - rationed so
            // that what is stored lasts until the next harvest (the reversible first step of coping)
            double daily = 0;
            var hhs = w.HouseholdsOf(s.Id);
            for (int i = 0; i < hhs.Count; i++) daily += HouseholdNeed(hhs[i]);
            int toHarvest = 39 - doy; if (toHarvest <= 0) toHarvest += SimTime.DaysPerYear;
            double ration = daily > 0 ? DMath.Clamp(s.Granary / (daily * (toHarvest + 6)), 0.45, 1.0) : 1.0;
            s.Ration = ration;
            double total = daily * 2.0 * ration;
            double share = total > 0 ? Math.Min(1.0, s.Granary / total) : 1.0;
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                ref var hh = ref w.Households.Get(hid);
                double want = HouseholdNeed(hid) * 2.0 * ration - hh.Larder;
                if (want <= 0) continue;
                double give = Math.Min(want, want * share);
                give = Math.Min(give, s.Granary);
                hh.Larder += give; s.Granary -= give;
            }
            // spoilage of stored grain (~12% a year)
            s.Granary *= 1.0 - 0.12 / SimTime.DaysPerYear;
            bool empty = s.Granary < 1.0;
            if (empty && s.HungerEvent == 0 && s.Population > 0)
            {
                s.HungerEvent = w.Record(new HistoryEvent { Kind = HistoryKind.Hunger, Time = w.Now, Cell = s.Cell, Settlement = s.Id, Subject = EntityRef.Settlement(s.Id), Cause1 = LastFailedHarvest(s.Id), Rel1 = CauseRel.CausedBy });
            }
            else if (!empty && s.Granary > 2.0 * total && s.HungerEvent != 0) s.HungerEvent = 0;
        }

        long LastFailedHarvest(int sid)
        {
            var list = _w.History.EventsOf(EntityRef.Settlement(sid));
            if (list == null) return 0;
            for (int i = list.Count - 1; i >= 0; i--)
                if (_w.History.TryGet(list[i], out var e) && e.Kind == HistoryKind.HarvestFailed) return e.Id;
            return 0;
        }

        double HouseholdNeed(int hid)
        {
            var w = _w;
            var mem = w.Members(hid);
            double need = 0;
            for (int i = 0; i < mem.Count; i++)
            {
                ref var p = ref w.People.Get(mem[i]);
                double age = p.AgeYears(w.Now);
                need += w.Config.FoodPerAdultDay * (age < 2 ? 0.35 : (age < 12 ? 0.6 : (age > 60 ? 0.8 : 1.0)));
            }
            return need;
        }

        void Demography(int sid)
        {
            var w = _w;
            var cfg = w.Config;
            long now = w.Now;
            ulong seed = w.Seed;
            long day = SimTime.FloorDiv(now, SimTime.Day);
            var res = w.Residents(sid);
            _scratch.Clear();
            for (int i = 0; i < res.Count; i++) _scratch.Add(res[i]);
            // deaths and health
            for (int i = 0; i < _scratch.Count; i++)
            {
                int pid = _scratch[i];
                ref var p = ref w.People.Get(pid);
                if (!p.Alive) continue;
                double age = p.AgeYears(now);
                double annual = age < 1 ? 0.22 : age < 5 ? 0.045 : age < 15 ? 0.008 : age < 45 ? 0.010 : age < 60 ? 0.020 : age < 70 ? 0.045 : age < 80 ? 0.10 : 0.22;
                double u = KeyedRng.Unit(seed, RngStream.Death, (ulong)pid, (ulong)day);
                bool starving = now - p.LastMeal > SimTime.Day;
                // each body bears hunger differently: the young and old weaken first, reserves differ
                double frail = (age < 5 ? 1.5 : age < 12 ? 1.2 : age > 60 ? 1.4 : 1.0) * (0.6 + 0.8 * KeyedRng.Unit(seed, RngStream.Death, (ulong)pid, (ulong)day, 3));
                if (starving)
                {
                    p.Health = (float)Math.Max(0.0, p.Health - 0.10 * frail);
                    if (p.HungryFrom == 0) p.HungryFrom = now;
                }
                else if (p.Nutrition < 0.65) p.Health = (float)Math.Max(0.0, p.Health - (0.65 - p.Nutrition) * 0.12 * frail);
                else p.Health = (float)Math.Min(1.0, p.Health + 0.05);
                if (p.Health <= 0.0f)
                {
                    w.Agents.Kill(ref p, w.Settlements.Get(sid).HungerEvent, 2);
                    continue;
                }
                double hazard = annual / SimTime.DaysPerYear * (1.0 + 3.0 * (1.0 - p.Health));
                if (u < hazard) { w.Agents.Kill(ref p, 0, 1); continue; }
            }
            // marriages (women in id order choose among unmarried men of the place)
            for (int i = 0; i < _scratch.Count; i++)
            {
                int pid = _scratch[i];
                ref var woman = ref w.People.Get(pid);
                if (!woman.Alive || !woman.IsFemale || woman.Partner != 0) continue;
                double age = woman.AgeYears(now);
                if (age < 18 || age > 36) continue;
                double peak = age < 21 ? 0.6 : (age < 27 ? 1.0 : (age < 31 ? 0.6 : 0.3));
                if (KeyedRng.Unit(seed, RngStream.Marriage, (ulong)pid, (ulong)day) >= cfg.MarriageRatePerDay * peak) continue;
                _men.Clear();
                AddSuitors(in woman, sid, now);
                // neighbouring villages within a day's easy walk (exogamy keeps small places alive)
                ref var here = ref w.Settlements.Get(sid);
                for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
                {
                    if (o == sid) continue;
                    ref var other = ref w.Settlements.Get(o);
                    if (!other.Inhabited) continue;
                    if (Vec3d.ArcDistance(here.Anchor, other.Anchor, w.Planet.Radius) > cfg.MarriageReach) continue;
                    if (w.Routes.Get(w, sid, o) == null) continue;
                    AddSuitors(in woman, o, now);
                }
                if (_men.Count == 0) continue;
                int pick = _men[KeyedRng.Below(KeyedRng.Hash(seed, RngStream.Marriage, (ulong)pid, (ulong)day, 1), _men.Count)];
                Marry(w.People.Get(pick).Home, pid, pick);
            }
            // conceptions
            for (int i = 0; i < _scratch.Count; i++)
            {
                int pid = _scratch[i];
                ref var p = ref w.People.Get(pid);
                if (!p.Alive || !p.IsFemale || p.Partner == 0 || p.PregDue != 0) continue;
                if (p.NursingUntil > now) continue;
                double age = p.AgeYears(now);
                if (age < 16 || age > 44) continue;
                ref var partner = ref w.People.Get(p.Partner);
                if (!partner.Alive) continue;
                double fert = age < 25 ? 1.0 : (age < 30 ? 0.93 : (age < 35 ? 0.82 : (age < 40 ? 0.62 : 0.3)));
                if (KeyedRng.Unit(seed, RngStream.Birth, (ulong)pid, (ulong)day) < cfg.ConceptionPerDay * fert * p.Health)
                {
                    p.PregDue = now + 36 * SimTime.Day;
                    p.PregFather = p.Partner;
                    w.Queue.Push(p.PregDue, Phase.Agent, EventKind.Birth, pid, 0, p.PregDue);
                }
            }
        }

        void AddSuitors(in Person woman, int sid, long now)
        {
            var res = _w.Residents(sid);
            for (int j = 0; j < res.Count; j++)
            {
                int mid = res[j];
                ref var man = ref _w.People.Get(mid);
                if (!man.Alive || man.IsFemale || man.Partner != 0) continue;
                double ma = man.AgeYears(now);
                if (ma < 18 || ma > 48) continue;
                if (CloseKin(in woman, in man)) continue;
                _men.Add(mid);
            }
        }

        static bool CloseKin(in Person a, in Person b)
        {
            if (a.Mother != 0 && a.Mother == b.Mother) return true;
            if (a.Father != 0 && a.Father == b.Father) return true;
            if (a.Mother == b.Id || a.Father == b.Id || b.Mother == a.Id || b.Father == a.Id) return true;
            return false;
        }

        void Marry(int sid, int womanId, int manId)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            int hid = w.Ids.Next(EntityKind.Household);
            s.LotPosition(s.Lots++, out float hx, out float hy);
            int manHh = w.People.Get(manId).Household;
            int fam = w.Households.Exists(manHh) ? w.Households.Get(manHh).FamilyName : manHh;
            var hh = new Household
            {
                Id = hid, Settlement = sid, Formed = w.Now, FamilyName = fam,
                HomeX = hx, HomeY = hy,
            };
            w.Households.Add(hid, hh);
            w.HouseholdsOf(sid).InsertSorted(hid);
            w.History.Register(EntityRef.Household(hid), w.FamilyName(hh.FamilyName), w.Now);
            MoveTo(womanId, hid);
            MoveTo(manId, hid);
            ref var woman = ref w.People.Get(womanId);
            ref var man = ref w.People.Get(manId);
            woman.Partner = manId; man.Partner = womanId;
            if (woman.Home != sid)
            {
                // she will walk to her new home when she next decides (her old village is no longer home)
                w.Residents(woman.Home).Remove(womanId);
                w.Settlements.Get(woman.Home).Population--;
                woman.Home = sid;
                w.Residents(sid).InsertSorted(womanId);
                w.Settlements.Get(sid).Population++;
                if (w.Resolution == Resolution.Coarse) { if (woman.At != sid && woman.At != 0) w.Visitors(woman.At).Remove(womanId); woman.At = sid; }
                else if (woman.At != 0 && woman.At != sid) { w.Visitors(woman.At).InsertSorted(womanId); woman.Until = w.Now; }
                else if (woman.At == sid) w.Visitors(sid).Remove(womanId);
            }
            long mev = w.Record(new HistoryEvent { Kind = HistoryKind.Marriage, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Person(womanId), Object = EntityRef.Person(manId), RootKind = RootKind.People });
            w.Record(new HistoryEvent { Kind = HistoryKind.HouseholdFormed, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Household(hid), Cause1 = mev, Rel1 = CauseRel.CausedBy });
            w.Metrics.Marriages++;
        }

        void MoveTo(int pid, int hid)
        {
            var w = _w;
            ref var p = ref w.People.Get(pid);
            int old = p.Household;
            if (old != 0 && w.Households.Exists(old))
            {
                w.Members(old).Remove(pid);
                ref var oh = ref w.Households.Get(old);
                // a share of the old larder comes along
                int left = w.Members(old).Count;
                double take = left == 0 ? oh.Larder : oh.Larder / (left + 1);
                oh.Larder -= take;
                w.Households.Get(hid).Larder += take;
                if (left == 0) { oh.Dissolved = w.Now; w.History.EndEntity(EntityRef.Household(old), w.Now); w.HouseholdsOf(oh.Settlement).Remove(old); }
            }
            p.Household = hid;
            w.Members(hid).InsertSorted(pid);
        }

        public void OnBirth(int motherId, long due)
        {
            var w = _w;
            if (!w.People.Exists(motherId)) return;
            ref var m = ref w.People.Get(motherId);
            if (!m.Alive || m.PregDue != due) return;
            m.PregDue = 0;
            m.NursingUntil = w.Now + SimTime.Years(1.5);
            int father = m.PregFather;
            m.PregFather = 0;
            byte sex = (byte)(KeyedRng.Unit(w.Seed, RngStream.Birth, (ulong)motherId, (ulong)due, 2) < 0.5 ? Sex.Female : Sex.Male);
            int child = w.Agents.CreatePerson(sex, w.Now, motherId, father, m.Household, m.Home, w.Now + SimTime.Hour);
            ref var mm = ref w.People.Get(motherId);
            long born = 0;
            born = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.PersonBorn, Time = w.Now, Cell = w.Settlements.Get(mm.Home).Cell, Settlement = mm.Home,
                Subject = EntityRef.Person(child), Object = EntityRef.Person(motherId), Other = EntityRef.Person(father), RootKind = RootKind.People,
            });
            w.Metrics.Births++;
            // childbirth is dangerous: about one mother in a hundred dies of it (more when weak)
            double risk = 0.010 * (1.0 + 2.0 * (1.0 - mm.Health));
            if (KeyedRng.Unit(w.Seed, RngStream.Death, (ulong)motherId, (ulong)due, 9) < risk) w.Agents.Kill(ref mm, born, 3);
        }

        /// <summary>Ash (or lava) on a village's fields: the standing crop is lost by this share, the eruption its cause.</summary>
        public void AshOnFields(int sid, double share, long cause) => CropLoss(sid, share, cause, HistoryKind.AshFall);

        /// <summary>A share of a village's standing crop lost (ash, the sea), with its cause; recorded as the given kind.</summary>
        public void CropLoss(int sid, double share, long cause, HistoryKind kind)
        {
            var w = _w;
            if (!w.Settlements.Exists(sid)) return;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            if (s.Crop != CropStage.Sown && s.Crop != CropStage.Growing && s.Crop != CropStage.Ripe) return;
            double before = s.CropBurnt;
            s.CropBurnt = DMath.Min(1.0, s.CropBurnt + share);
            if (s.CropBurnt - before < 0.02) return;
            long ash = w.Record(new HistoryEvent
            {
                Kind = kind, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                Cause1 = cause, Rel1 = CauseRel.CausedBy, Magnitude = (float)(s.CropBurnt - before), RootKind = RootKind.Nature,
            });
            if (s.CropCause == 0) s.CropCause = ash;
        }

        /// <summary>Fire burning on a settlement's field or home cell damages the standing crop.</summary>
        public void FireOnCell(int cell, int proc)
        {
            var w = _w;
            int sid = w.Planet.FieldOwner[cell];
            if (sid == 0) sid = w.SettlementAtCell(cell);
            if (sid == 0) return;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            if (s.Crop != CropStage.Sown && s.Crop != CropStage.Growing && s.Crop != CropStage.Ripe) return;
            double before = s.CropBurnt;
            double share = s.FieldCells > 0 ? 1.0 / s.FieldCells : 1.0;
            s.CropBurnt = DMath.Min(1.0, s.CropBurnt + 0.5 * share * w.Fire.Intensity[cell]);
            if (s.CropCause == 0 && s.CropBurnt > 0.05 && before <= 0.05)
            {
                long start = w.Fire.Procs.Exists(proc) ? w.Fire.Procs.Get(proc).StartEvent : 0;
                s.CropCause = w.Record(new HistoryEvent
                {
                    Kind = HistoryKind.FieldsBurned, Time = w.Now, Cell = cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                    Object = EntityRef.Process(proc), Cause1 = start, Rel1 = CauseRel.CausedBy, Magnitude = (float)s.CropBurnt,
                });
            }
        }
    }
}
