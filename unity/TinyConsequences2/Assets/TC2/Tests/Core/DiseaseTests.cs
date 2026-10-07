using System;
using System.Collections.Generic;
using NUnit.Framework;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.Health;
using TC2.Core.History;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// Sickness among persistent people (D2-066; F5's done-when): an infection runs its course and leaves immunity; an epidemic
    /// jumps along travel - a visitor catches it and carries it home - and is traceable back to where it broke out; the COARSE
    /// day couples villages at the lived rate of visits. Mechanisms, not outcomes.
    /// </summary>
    [TestFixture]
    public class DiseaseTests
    {
        static void Coarse(World w) => w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);

        /// <summary>Walks an event's causes back (Cause1) and says whether it reaches the target.</summary>
        static bool Leads(World w, long from, long target)
        {
            long e = from;
            for (int i = 0; i < 64 && e != 0; i++)
            {
                if (e == target) return true;
                if (!w.History.TryGet(e, out var ev)) return false;
                e = ev.Cause1;
            }
            return false;
        }

        [Test]
        public void ASicknessRunsItsCourseAndLeavesTheRecoveredImmune()
        {
            var w = TestWorlds.Make(491);
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Day);
            int sid = w.Settlements.First();
            long mark = w.History.LastId;
            int path = w.Disease.Outbreak(sid, beta: 1.2, latent: 3, infectious: 6, virulence: 0.0, immunity: Disease.ForLife);
            Assert.AreNotEqual(0, path);
            int antigen = w.Pathogens.Get(path).Antigen;
            w.AdvanceTo(w.Now + SimTime.Days(60));
            var g = w.Pathogens.Get(path);
            int immune = 0, sick = 0, people = 0;
            var res = w.Residents(sid);
            for (int i = 0; i < res.Count; i++)
            {
                var p = w.People.Get(res[i]);
                if (!p.Alive) continue;
                people++;
                if (Disease.ImmuneTo(in p, antigen, w.Now)) immune++;
                if (p.Sick != 0) sick++;
            }
            bool ended = false;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++) { var e = w.History.At(i); if (e.Kind == HistoryKind.EpidemicEnded && e.Settlement == sid) { ended = true; Console.WriteLine(HistoryText.Line(w, e)); } }
            Console.WriteLine("after 60 days: " + g.Cases + " caught it in all, " + immune + " of " + people + " immune, " + sick + " sick");
            Assert.Greater(g.Cases, 3, "it spread among the village's people");
            Assert.Greater(immune, 0, "the recovered are immune");
            Assert.AreEqual(0, sick, "it has run its course");
            Assert.IsTrue(ended, "and its end is history");
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void AnEpidemicJumpsAlongTravelAndIsTraceable()
        {
            var w = TestWorlds.Make(493);
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            int a = w.Settlements.First(), b = 0;
            for (int s = w.Settlements.Next(a); s != 0; s = w.Settlements.Next(s)) if (w.Routes.Get(w, a, s) != null) { b = s; break; }
            Assume.That(b != 0, "a village with a road to the stricken one");
            // premise: a contagious sickness breaks out in A and most of its people are down with it
            int path = w.Disease.Outbreak(a, beta: 4.0, latent: 4, infectious: 10, virulence: 0.0, immunity: Disease.ForLife);
            var resA = w.Residents(a);
            for (int i = 0; i < resA.Count; i++) { ref var q = ref w.People.Get(resA[i]); if (q.Alive && q.Sick == 0) { w.Disease.Infect(ref q, path, w.Settlements.Get(a).Epidemic); } }
            w.AdvanceTo(w.Now + SimTime.Days(5));   // (they are infectious now: the village's day counts them)
            w.AdvanceTo(SimTime.StartOfDay(w.Now) + SimTime.Day + SimTime.Hours(1));
            // (A counts them at its own midnight, which may come later than the world clock's: wait for it - D2-085, a village elsewhere)
            for (int guard = 0; guard < 30 && w.Settlements.Get(a).InfNow <= 0; guard++) w.AdvanceTo(w.Now + SimTime.Hours(1));
            long epiA = w.Settlements.Get(a).Epidemic;
            Assert.AreNotEqual(0L, epiA);
            // and people of B go visiting A (as they do: the premise only chooses the days - again the next day while none has caught it:
            // a few hours' stay gives each a chance in three or four, and in one world eleven came home well, D2-085)
            var resB = w.Residents(b);
            int sent = 0;
            for (int round = 0; round < 4; round++)
            {
                for (int i = 0; i < resB.Count; i++)
                {
                    var p = w.People.Get(resB[i]);
                    if (!p.Alive || p.At != b || p.AgeYears(w.Now) < 16) continue;
                    if (w.Agents.StartTrip(resB[i], a)) sent++;
                }
                w.AdvanceTo(w.Now + SimTime.Hours(12));
                bool caught = false;
                for (int i = 0; i < resB.Count; i++) if (w.People.Get(resB[i]).Sick != 0) caught = true;
                if (caught || sent == 0) break;
                w.AdvanceTo(w.Now + SimTime.Hours(12));
            }
            Assume.That(sent > 0, "visitors set out");
            long mark = w.History.LastId;
            w.AdvanceTo(w.Now + SimTime.Days(14));
            long arrived = 0; int carrier = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind == HistoryKind.EpidemicArrived && e.Settlement == b) { arrived = e.Id; carrier = e.Other.Id; Console.WriteLine(HistoryText.Line(w, e)); break; }
            }
            Assert.AreNotEqual(0L, arrived, "a visitor carried it home");
            Assert.AreEqual(b, w.People.Get(carrier).Home, "one of B's own, back from A");
            Assert.IsTrue(Leads(w, arrived, epiA), "its cause is A's epidemic");
            Assert.IsTrue(Leads(w, arrived, w.Pathogens.Get(path).Event), "and back to where the sickness appeared");
            Console.WriteLine(HistoryText.Why(w, arrived));
            Assert.IsNull(TestWorlds.Audit(w));
        }

        [Test]
        public void ACoarseDayCarriesItBetweenVillagesAtTheLivedRateOfVisits()
        {
            // (a full world whose villages lie within a walk of each other: the small test worlds' are farther apart than visits go)
            // (villages of sixty: with two dozen a village's 10-18 grown people make 0.3-0.5 visits a day among five neighbours, and the
            // whole outbreak's expected catches were one or two - none in the 6 km world, D2-085)
            var w = TC2.Core.Worldgen.WorldGenerator.Create(new WorldConfig { Seed = 42, FoundersPerSettlement = 60 });
            Coarse(w);
            w.AdvanceTo(w.Now + SimTime.Day);
            int a = w.Settlements.First();
            // (a COARSE catch that falls on a child is dropped, which halves them: a known bug for its own calibrated slice, PROGRESS)
            int path = w.Disease.Outbreak(a, beta: 2.0, latent: 4, infectious: 12, virulence: 0.0, immunity: Disease.ForLife);
            long mark = w.History.LastId;
            w.AdvanceTo(w.Now + SimTime.Days(120));
            int villages = 0;
            for (int i = w.History.FirstIndexAfter(mark); i < w.History.Count; i++)
            {
                var e = w.History.At(i);
                if (e.Kind != HistoryKind.EpidemicArrived || e.Settlement == a) continue;
                villages++;
                Assert.IsTrue(Leads(w, e.Id, w.Pathogens.Get(path).Event), "every arrival leads back to the outbreak");
                Console.WriteLine(HistoryText.Line(w, e));
            }
            Assert.Greater(villages, 0, "visits carried it to a neighbour");
        }

        [Test]
        public void ABodyRemembersSixSicknessesAndForgetsTheOneEndingSoonest()
        {
            // (D2-070: two were forgotten as new antigens came, and a dense world caught the same sickness again and again)
            var p = new Person { Alive = true };
            long now = SimTime.Years(10);
            for (int a = 1; a <= Disease.Slots; a++) Disease.Remember(ref p, a, now + SimTime.Years(a), now);
            for (int a = 1; a <= Disease.Slots; a++) Assert.IsTrue(Disease.ImmuneTo(in p, a, now), "remembers " + a);
            Disease.Remember(ref p, 99, now + SimTime.Years(50), now);
            Assert.IsTrue(Disease.ImmuneTo(in p, 99, now), "a seventh is remembered");
            Assert.IsFalse(Disease.ImmuneTo(in p, 1, now), "in place of the one ending soonest");
            for (int a = 2; a <= Disease.Slots; a++) Assert.IsTrue(Disease.ImmuneTo(in p, a, now));
            Disease.Remember(ref p, 3, now + SimTime.Years(80), now);
            Assert.IsTrue(Disease.ImmuneTo(in p, 3, now + SimTime.Years(60)), "caught again: its own slot renewed");
            Assert.IsTrue(Disease.ImmuneTo(in p, 2, now), "and nothing else forgotten");
            Assert.IsFalse(Disease.ImmuneTo(in p, 2, now + SimTime.Years(3)), "an immunity ends in its years");
        }

        [Test]
        public void AStrainWithAShiftedAntigenMeetsPartOfTheOldImmunity()
        {
            var w = TestWorlds.Make(499);
            w.AdvanceTo(w.Now + SimTime.Hours(30));
            int sid = w.Settlements.First();
            int path = w.Disease.Outbreak(sid, beta: 0.5, latent: 3, infectious: 6, virulence: 0.0, immunity: Disease.ForLife);
            int shifted = w.Disease.Strain(path, sid, true), drifted = w.Disease.Strain(path, sid, false);
            Assert.AreNotEqual(w.Pathogens.Get(path).Antigen, w.Pathogens.Get(shifted).Antigen, "a shifted antigen");
            Assert.AreEqual(w.Pathogens.Get(path).Antigen, w.Pathogens.Get(drifted).Antigen, "a drift keeps it");
            Assert.AreEqual(w.Pathogens.Get(path).Family, w.Pathogens.Get(shifted).Family, "the same sickness");
            // premise: everyone who had the old strain recovered from it
            int people = 0, caught = 0, caughtDrift = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id))
            {
                ref var p = ref w.People.Get(id);
                if (!p.Alive) continue;
                p.Sick = 0; p.SickPhase = 0;
                Disease.Remember(ref p, w.Pathogens.Get(path).Antigen, long.MaxValue / 4, w.Now);
                people++;
                w.Disease.Infect(ref p, drifted, 0);
                if (p.Sick != 0) caughtDrift++;
                w.Disease.Infect(ref p, shifted, 0);
                if (p.Sick != 0) caught++;
            }
            double share = (double)caught / people;
            Console.WriteLine(people + " immune to the old strain: the drifted one took " + caughtDrift + ", the shifted one " + caught + " (" + share.ToString("0.00") + "; " + (1 - Disease.CrossProtection) + " expected)");
            Assert.AreEqual(0, caughtDrift, "a drifted strain meets the whole immunity");
            Assert.That(share, Is.InRange(1 - Disease.CrossProtection - 0.15, 1 - Disease.CrossProtection + 0.15), "a shifted one meets part of it");
        }

        [Test]
        public void SicknessesComeFromHerdsAndCrowdsBoundedly()
        {
            Assert.Greater(Disease.SpillChance(40, 100), Disease.SpillChance(0, 100), "more with herds");
            Assert.Greater(Disease.SpillChance(0, 200), Disease.SpillChance(0, 100), "more with crowding");
            Assert.AreEqual(6 * Disease.SpillPerDay, Disease.SpillChance(5000, 50000), 1e-15, "but bounded: a dense world of herds is not a sickness a year");
        }

        [Test]
        public void TheCoarseVisitRateIsTheLivedOne()
        {
            // lived days: arrivals of grown visitors per healthy grown person a day, against the constant a COARSE day uses
            // (trading trips are visits too, but a COARSE day makes those in its trade step at their own rate: D2-067)
            var w = TestWorlds.Make(497);
            w.AdvanceTo(w.Now + SimTime.Day);
            long visits0 = 0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) visits0 += w.People.Get(id).Visits;
            double adultDays = 0;
            var trading = new HashSet<(int, long)>();
            for (int d = 0; d < 20; d++)
            {
                for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { var p = w.People.Get(id); double age = p.AgeYears(w.Now); if (p.Alive && age >= 16 && age < 62 && p.Health > 0.7) adultDays++; }
                long end = w.Now + SimTime.Day;
                while (w.Now < end)
                {
                    w.AdvanceTo(w.Now + 600);
                    for (int id = w.People.First(); id != 0; id = w.People.Next(id)) { ref var p = ref w.People.Get(id); if (p.Alive && p.Act == ActivityKind.TripOut && p.Why == WhyCode.Trade) trading.Add((id, p.T0)); }
                }
            }
            long visits = -visits0;
            for (int id = w.People.First(); id != 0; id = w.People.Next(id)) visits += w.People.Get(id).Visits;
            double rate = (visits - trading.Count) / adultDays;
            Console.WriteLine("lived visits per healthy grown person a day " + rate.ToString("0.000") + " and trading trips " + (trading.Count / adultDays).ToString("0.000") +
                              " (COARSE uses " + Disease.VisitsPerAdultDay + ")");
            Assert.That(rate / Disease.VisitsPerAdultDay, Is.InRange(0.5, 2.0), "refit Disease.VisitsPerAdultDay if trips change");
        }
    }
}
