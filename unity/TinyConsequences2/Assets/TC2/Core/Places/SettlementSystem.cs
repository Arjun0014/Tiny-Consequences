using System;
using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Scheduling;
using TC2.Core.Simulation;

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
            Food(ref s);
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
                s.FieldHa = Math.Max(1.0, s.Population * cfg.FieldHaPerPerson);
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
                int fc = s.FieldCell;
                double t = P.Temp[fc];
                double tf = DMath.SmoothStep(3, 13, t) * (1.0 - DMath.SmoothStep(31, 39, t));
                double wf = DMath.SmoothStep(0.06, 0.32, P.SoilWater[fc]);
                double tend = 0.75 + 0.25 * DMath.Clamp01(s.CropWork / Math.Max(1.0, s.CropWorkNeed));
                s.CropGrowth = DMath.Clamp01(s.CropGrowth + tf * wf * tend / 16.0);
            }
        }

        void Food(ref Settlement s)
        {
            var w = _w;
            // the granary refills each household's larder to two days of its members' needs
            double total = 0;
            var hhs = w.HouseholdsOf(s.Id);
            for (int i = 0; i < hhs.Count; i++) total += HouseholdNeed(hhs[i]) * 2.0;
            double share = total > 0 ? Math.Min(1.0, s.Granary / total) : 1.0;
            for (int i = 0; i < hhs.Count; i++)
            {
                int hid = hhs[i];
                ref var hh = ref w.Households.Get(hid);
                double want = HouseholdNeed(hid) * 2.0 - hh.Larder;
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
                double annual = age < 1 ? 0.10 : age < 5 ? 0.02 : age < 15 ? 0.004 : age < 45 ? 0.005 : age < 60 ? 0.014 : age < 70 ? 0.04 : age < 80 ? 0.10 : 0.25;
                double u = KeyedRng.Unit(seed, RngStream.Death, (ulong)pid, (ulong)day);
                bool starving = now - p.LastMeal > SimTime.Day;
                if (starving)
                {
                    p.Health = (float)Math.Max(0.0, p.Health - 0.12);
                    if (p.HungryFrom == 0) p.HungryFrom = now;
                }
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
                if (age < 17 || age > 36) continue;
                if (KeyedRng.Unit(seed, RngStream.Marriage, (ulong)pid, (ulong)day) >= cfg.MarriageRatePerDay) continue;
                _men.Clear();
                for (int j = 0; j < _scratch.Count; j++)
                {
                    int mid = _scratch[j];
                    ref var man = ref w.People.Get(mid);
                    if (!man.Alive || man.IsFemale || man.Partner != 0) continue;
                    double ma = man.AgeYears(now);
                    if (ma < 18 || ma > 48) continue;
                    if (CloseKin(in woman, in man)) continue;
                    _men.Add(mid);
                }
                if (_men.Count == 0) continue;
                int pick = _men[KeyedRng.Below(KeyedRng.Hash(seed, RngStream.Marriage, (ulong)pid, (ulong)day, 1), _men.Count)];
                Marry(sid, pid, pick);
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
                double fert = age < 30 ? 1.0 : (age < 38 ? 0.75 : 0.4);
                if (KeyedRng.Unit(seed, RngStream.Birth, (ulong)pid, (ulong)day) < cfg.ConceptionPerDay * fert * p.Health)
                {
                    p.PregDue = now + 36 * SimTime.Day;
                    p.PregFather = p.Partner;
                    w.Queue.Push(p.PregDue, Phase.Agent, EventKind.Birth, pid, 0, p.PregDue);
                }
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
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.PersonBorn, Time = w.Now, Cell = w.Settlements.Get(mm.Home).Cell, Settlement = mm.Home,
                Subject = EntityRef.Person(child), Object = EntityRef.Person(motherId), Other = EntityRef.Person(father), RootKind = RootKind.People,
            });
            w.Metrics.Births++;
        }

        /// <summary>Fire burning on a settlement's field or home cell damages the standing crop.</summary>
        public void FireOnCell(int cell, int proc)
        {
            var w = _w;
            int sid = w.SettlementAtCell(cell);
            if (sid == 0) return;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) return;
            if (s.Crop != CropStage.Sown && s.Crop != CropStage.Growing && s.Crop != CropStage.Ripe) return;
            double before = s.CropBurnt;
            s.CropBurnt = DMath.Min(1.0, s.CropBurnt + 0.18 * w.Fire.Intensity[cell]);
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
