using System;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// A people's ways carried and copied (C11.1b): each household builds as it was raised to - the way of the house the groom grew up in, its
    /// founders' where it founded a village, the one it brought where it joined one - now and then taking one way of its village's first household
    /// or of the bride's own; the first household of a tributary comes to build as its lord's seat. `--oldways` builds every house in its village's
    /// way, as D2-114 did. Mechanisms.
    /// </summary>
    [TestFixture]
    public class BuildingWaysTests
    {
        /// <summary>Two villages whose ways differ in both the roof's form and the shade, and a household of the second with a house.</summary>
        static World TwoWays(out int a, out int b, out int hid, out int bid)
        {
            for (ulong seed = 7; seed < 40; seed++)
            {
                var w = TestWorlds.Make(seed);
                for (a = w.Settlements.First(); a != 0; a = w.Settlements.Next(a))
                    for (b = w.Settlements.Next(a); b != 0; b = w.Settlements.Next(b))
                    {
                        int ta = w.Settlements.Get(a).Tradition, tb = w.Settlements.Get(b).Tradition;
                        if (Architecture.TraditionRoof(ta) == Architecture.TraditionRoof(tb) || Architecture.TraditionPalette(ta) == Architecture.TraditionPalette(tb)) continue;
                        // (the cold's half-hips stand in for hipped roofs there: a village warm enough that its roof is its way's)
                        if (w.Planet.TMean[w.Settlements.Get(b).Cell] < 2.0) continue;
                        var hl = w.HouseholdsOf(b);
                        for (int i = 0; i < hl.Count; i++)
                        {
                            ref var hh = ref w.Households.Get(hl[i]);
                            if (hh.Building != 0 && hh.Shelter >= Construction.House) { hid = hl[i]; bid = hh.Building; return w; }
                        }
                    }
            }
            a = b = hid = bid = 0;
            return null;
        }

        [Test]
        public void AHouseholdBuildsInTheWayItCarries()
        {
            var w = TwoWays(out int a, out int b, out int hid, out int bid);
            Assume.That(w != null, "a world with two villages building apart");
            int ta = w.Settlements.Get(a).Tradition, tb = w.Settlements.Get(b).Tradition;
            Assert.AreEqual(tb, w.Households.Get(hid).Way, "the first households carry their village's way");
            Assert.AreEqual(Architecture.TraditionRoof(tb), Architecture.RoofOf(Architecture.HomeForm(w, b, hid, bid, Construction.House)), "built as its people build");
            // the household came from the first village (it carries that way): it builds as it was raised to among its new neighbours
            w.Households.Get(hid).Way = ta;
            int form = Architecture.HomeForm(w, b, hid, bid, Construction.House);
            Console.WriteLine("the village's way " + Architecture.RoofName(Architecture.TraditionRoof(tb)) + ", the household's " + Architecture.RoofName(Architecture.TraditionRoof(ta)) + ": " + Architecture.Describe(form, true));
            Assert.AreEqual(Architecture.TraditionRoof(ta), Architecture.RoofOf(form), "roofed in the way it carries");
            int pal = Architecture.PaletteOf(form), tp = Architecture.TraditionPalette(ta);
            Assert.IsTrue(pal == tp || pal == (tp + 1) % 8 || pal == (tp + 2) % 8, "its walls in its way's shades");
            // --oldways: in its village's way
            Architecture.WaysOff = true;
            try { Assert.AreEqual(Architecture.TraditionRoof(tb), Architecture.RoofOf(Architecture.HomeForm(w, b, hid, bid, Construction.House)), "--oldways: its village's"); }
            finally { Architecture.WaysOff = false; }
        }

        [Test]
        public void ACoupleBuildsAsTheGroomsHouseNowAndThenAsTheFirstHouseholdOrTheBrides()
        {
            var w = TwoWays(out int a, out int b, out int hid, out int bid);
            Assume.That(w != null, "a world with two villages building apart");
            int ta = w.Settlements.Get(a).Tradition, tb = w.Settlements.Get(b).Tradition;
            // the premise: the groom's house builds in the first village's way, the village's first household in its own
            ref var sb = ref w.Settlements.Get(b);
            int head = 0;
            var hl = w.HouseholdsOf(b);
            for (int i = 0; i < hl.Count; i++) if (hl[i] != hid) { head = hl[i]; break; }
            Assume.That(head != 0, "a second household to lead the village");
            sb.HeadHh = head;
            w.Households.Get(hid).Way = ta;
            int n = 2000, same = 0, headTook = 0;
            for (int k = 0; k < n; k++)
            {
                int way = Architecture.WayAtMarriage(w, b, 900000 + k, hid, hid);
                Assert.AreEqual(Architecture.Line(ta), Architecture.Line(way), "the line it descends from is kept");
                bool roof = Architecture.TraditionRoof(way) != Architecture.TraditionRoof(ta), shade = Architecture.TraditionPalette(way) != Architecture.TraditionPalette(ta);
                Assert.IsFalse(roof && shade, "one way at a time");
                if (!roof && !shade) same++;
                else { headTook++; Assert.IsTrue(roof ? Architecture.TraditionRoof(way) == Architecture.TraditionRoof(tb) : Architecture.TraditionPalette(way) == Architecture.TraditionPalette(tb), "the first household's way"); }
            }
            Console.WriteLine("as the groom's house " + same + " of " + n + ", one of the first household's ways " + headTook);
            Assert.That(headTook / (double)n, Is.InRange(Architecture.HeadCopy - 0.04, Architecture.HeadCopy + 0.04));
            // a bride from the first village, the groom's house building as its own village does and leading it: now and then one of hers
            w.Households.Get(hid).Way = tb; sb.HeadHh = hid;
            int brideHh = w.HouseholdsOf(a)[0];
            Assert.AreEqual(ta, w.Households.Get(brideHh).Way);
            int hers = 0;
            for (int k = 0; k < n; k++) if (!Architecture.Alike(Architecture.WayAtMarriage(w, b, 910000 + k, hid, brideHh), tb)) hers++;
            Console.WriteLine("one of the bride's ways " + hers + " of " + n);
            Assert.That(hers / (double)n, Is.InRange(Architecture.BrideCopy - 0.04, Architecture.BrideCopy + 0.04));
            Architecture.WaysOff = true;
            try { Assert.AreEqual(0, Architecture.WayAtMarriage(w, b, 920000, hid, brideHh), "--oldways: no way of its own"); }
            finally { Architecture.WaysOff = false; }
        }

        [Test]
        public void ATributarysFirstHouseholdComesToBuildAsItsLordsSeat()
        {
            var w = TwoWays(out int a, out int b, out int hid, out int bid);
            Assume.That(w != null, "a world with two villages building apart");
            int ta = w.Settlements.Get(a).Tradition, tb = w.Settlements.Get(b).Tradition;
            // the premise: the second village pays the first; each has its first household
            ref var sa = ref w.Settlements.Get(a);
            ref var sb = ref w.Settlements.Get(b);
            sa.HeadHh = w.HouseholdsOf(a)[0];
            sb.HeadHh = hid;
            sb.Lord = a;
            long t0 = w.Now;
            int firstTook = -1, both = -1;
            for (int y = 0; y < 200 && both < 0; y++)
            {
                w.Now = t0 + y * SimTime.Year;
                Architecture.Year(w, b);
                int way = w.Households.Get(hid).Way;
                Assert.AreEqual(Architecture.Line(tb), Architecture.Line(way), "the line it descends from is kept");
                if (firstTook < 0 && way != tb) firstTook = y;
                if (Architecture.Alike(way, ta)) both = y;
            }
            w.Now = t0;
            Console.WriteLine("the first of the lord's ways taken in year " + firstTook + ", both by year " + both);
            Assert.GreaterOrEqual(both, 0, "in time the first household builds as the lord's seat");
            Assert.Greater(both, firstTook, "a way at a time");
            // a free village's first household copies no one
            int before = w.Households.Get(hid).Way = tb;
            sb.Lord = 0;
            for (int y = 0; y < 50; y++) { w.Now = t0 + y * SimTime.Year; Architecture.Year(w, b); }
            w.Now = t0;
            Assert.AreEqual(before, w.Households.Get(hid).Way, "no lord, no lord's way");
        }

        [Test]
        public void FoundersBringTheirFirstHouseholdsWayAChangedNowAndThen()
        {
            var w = TestWorlds.Make(7);
            int origin = w.Settlements.First();
            int t0 = w.Settlements.Get(origin).Tradition;
            // the party's first household builds otherwise than its village (it came from elsewhere): the new village descends from its way
            int carried = (t0 & ~3) | ((Architecture.TraditionRoof(t0) + 1) % 3);
            int same = 0, n = 400;
            for (int sid = 3000; sid < 3000 + n; sid++)
            {
                int t = Architecture.TraditionAt(w, sid, origin, carried);
                bool roof = Architecture.TraditionRoof(t) != Architecture.TraditionRoof(carried), shade = Architecture.TraditionPalette(t) != Architecture.TraditionPalette(carried);
                Assert.IsFalse(roof && shade, "one trait changes at a founding");
                if (!roof && !shade) same++;
            }
            Console.WriteLine(same + " of " + n + " foundings build as their first household did");
            Assert.That(same / (double)n, Is.InRange(1.0 - Architecture.Drift - 0.08, 1.0 - Architecture.Drift + 0.08));
        }

        [Test]
        public void EveryHouseholdCarriesAWayAfterNinetyYears()
        {
            var w = TestWorlds.Make(631);
            w.AdvanceTo(w.Now + SimTime.Years(90));
            int households = 0, carrying = 0, villagesMixed = 0;
            for (int hid = w.Households.First(); hid != 0; hid = w.Households.Next(hid))
            {
                ref var hh = ref w.Households.Get(hid);
                if (hh.Dissolved != 0) continue;
                households++;
                if (hh.Way != 0) carrying++;
            }
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                var hl = w.HouseholdsOf(sid);
                for (int i = 1; i < hl.Count; i++) if (!Architecture.Alike(w.Households.Get(hl[i]).Way, w.Households.Get(hl[0]).Way)) { villagesMixed++; break; }
            }
            Console.WriteLine(households + " households, " + carrying + " carrying a way; villages whose households build more than one way " + villagesMixed);
            Assert.Greater(households, 0);
            Assert.AreEqual(households, carrying, "every household builds as it was raised to");
        }
    }
}
