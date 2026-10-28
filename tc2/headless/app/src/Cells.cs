using System;
using System.Globalization;
using System.IO;
using TC2.Core.Persistence;

namespace TC2.Headless
{
    public static partial class Commands
    {
        /// <summary>
        /// (session 29) The ground's cells along lines out of a place: what each cell holds that the view colours the open land and the farmland by - its
        /// biome, woods (standing and felled), grass, the climate's moisture, the soil's water, the mean temperature, height over the sea, whose field it is.
        /// To find which of them draws an edge seen in a shot. Usage: cells --load SAVE [--sid N] [--km 2.5] [--dirs 8] [--step 25]
        /// </summary>
        public static int Cells(Args a)
        {
            var w = SaveFile.Load(File.ReadAllBytes(a.Get("load")), out _);
            var P = w.Planet;
            if (a.Get("homes") != null)
            {
                // (--homes SID) where a place's buildings stand from its anchor (the square the camera and its name go to): lived-in homes, empty, fallen;
                // works - by distance band
                var iv = CultureInfo.InvariantCulture;
                int hs = int.Parse(a.Get("homes"), iv);
                ref var st = ref w.Settlements.Get(hs);
                var bands = new int[8, 4];   // [band of 50 m][lived, empty, fallen, work]
                double sx = 0, sy = 0; int nl = 0;
                var bl = w.BuildingsOf(hs);
                for (int i = 0; i < bl.Count; i++)
                {
                    var b = w.Buildings.Get(bl[i]);
                    if (TC2.Core.Places.Boats.IsBoat(b.Kind)) continue;
                    double d = Math.Sqrt(b.X * b.X + b.Y * b.Y);
                    int band = Math.Min(7, (int)(d / 50.0));
                    bool home = TC2.Core.Places.Structures.IsHome(b.Kind);
                    int kind = !home ? 3 : TC2.Core.Places.Homes.Fallen(in b, w.Now) ? 2 : b.Household != 0 ? 0 : 1;
                    bands[band, kind]++;
                    if (kind == 0) { sx += b.X; sy += b.Y; nl++; }
                }
                Console.WriteLine("place #" + hs + " " + w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(hs)) + ": people " + st.Population + "; lived-in homes' centre " + (nl > 0 ? (sx / nl).ToString("0", iv) + ", " + (sy / nl).ToString("0", iv) + " m from the anchor" : "-"));
                var an = st.Anchor; double d2r = 180.0 / Math.PI;
                var lc = nl > 0 ? st.LocalToSphere(sx / nl, sy / nl, P.Radius) : an;
                Console.WriteLine("  anchor lat " + (an.Latitude * d2r).ToString("0.00", iv) + " lon " + (an.Longitude * d2r).ToString("0.00", iv) + "; lived centre lat " + (lc.Latitude * d2r).ToString("0.00", iv) + " lon " + (lc.Longitude * d2r).ToString("0.00", iv));
                Console.WriteLine("  from anchor   lived  empty  fallen  works");
                for (int k = 0; k < 8; k++) Console.WriteLine("  " + (k * 50).ToString(iv).PadLeft(4) + (k == 7 ? "+  m" : "-" + ((k + 1) * 50).ToString(iv).PadRight(4) + "m") + "   " + bands[k, 0].ToString(iv).PadLeft(4) + "   " + bands[k, 1].ToString(iv).PadLeft(4) + "   " + bands[k, 2].ToString(iv).PadLeft(5) + "   " + bands[k, 3].ToString(iv).PadLeft(4));
                return 0;
            }
            if (a.Get("burned") != null)
            {
                // (--burned YEARS) homes burned by bands and buildings flattened by stones from the sky in the last YEARS, and the ruins standing now that fell
                // at such a moment - the view's rule for a charred ruin (WorldObjectsView.Charred: Left + its fall time + 1 ms = a fire record's time)
                var iv = CultureInfo.InvariantCulture;
                long since = w.Now - (long)(double.Parse(a.Get("burned"), iv) * TC2.Core.Foundation.SimTime.Year);
                var at = new System.Collections.Generic.HashSet<long>();
                var bySid = new System.Collections.Generic.SortedDictionary<int, int>();
                for (int i = 0; i < w.History.Count; i++)
                {
                    var e = w.History.At(i);
                    if (e.Kind != TC2.Core.History.HistoryKind.BuildingBurned && e.Kind != TC2.Core.History.HistoryKind.MeteorStruck) continue;
                    at.Add(e.Time);
                    if (e.Time >= since) bySid[e.Settlement] = bySid.TryGetValue(e.Settlement, out int n0) ? n0 + 1 : 1;
                }
                Console.WriteLine("fire records (burned homes, stones from the sky): " + at.Count + " in all; in the last " + a.Get("burned") + " years by place:");
                foreach (var kv in bySid) Console.WriteLine("  #" + kv.Key + " " + w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(kv.Key)) + ": " + kv.Value);
                int charred = 0, ruins = 0, found = 0;
                var charredBy = new System.Collections.Generic.SortedDictionary<int, int>();
                for (int id = w.Buildings.First(); id != 0; id = w.Buildings.Next(id))
                {
                    var b = w.Buildings.Get(id);
                    if (!TC2.Core.Places.Homes.Fallen(in b, w.Now) || TC2.Core.Places.Homes.RuinLeft(in b, w.Now) <= 0.03) continue;
                    ruins++;
                    long fell = b.Left + TC2.Core.Foundation.DMath.ToLong(TC2.Core.Places.Homes.FallYears(in b) * TC2.Core.Foundation.SimTime.Year) + 1;
                    if (!at.Contains(fell)) continue;
                    charred++; if (TC2.Core.History.Whys.FellTo(w, in b) != 0) found++;
                    if (fell >= since) charredBy[b.Settlement] = charredBy.TryGetValue(b.Settlement, out int n1) ? n1 + 1 : 1;
                }
                Console.WriteLine("ruins standing " + ruins + ", fallen to fire " + charred + " (their fire record found by Whys.FellTo: " + found + "); fallen to fire in the last " + a.Get("burned") + " years by place:");
                foreach (var kv in charredBy) Console.WriteLine("  #" + kv.Key + " " + w.History.NameOf(TC2.Core.Foundation.EntityRef.Settlement(kv.Key)) + ": " + kv.Value);
                return 0;
            }
            if (a.Get("meridian") != null)
            {
                // (--meridian LON [--days D]) the climate along a meridian, degree by degree of latitude, after D days lived (a season's soil): the cells'
                // temperature now and its yearly swing, the soil's water, the rain today - to find an edge the ground's seasons draw across the land
                var iv = CultureInfo.InvariantCulture;
                double lon = double.Parse(a.Get("meridian"), iv) * Math.PI / 180.0;
                int days = a.Int("days", 0);
                if (days > 0) w.AdvanceTo(w.Now + days * TC2.Core.Foundation.SimTime.Day);
                Console.WriteLine("at " + TC2.Core.Foundation.SimTime.FormatDate(w.Now) + " along longitude " + a.Get("meridian"));
                Console.WriteLine("  lat  cell land  tMean  tAmp   temp  moist soilW  rain");
                for (double lat = a.Double("from", -40); lat <= a.Double("to", 40); lat += a.Double("dlat", 1))
                {
                    double la = lat * Math.PI / 180.0;
                    var d = TC2.Core.Foundation.Vec3d.FromLatLon(la, lon);
                    int c = P.Grid.CellOf(d);
                    Console.WriteLine(lat.ToString("0.0", iv).PadLeft(5) + " " + c.ToString(iv).PadLeft(5) + " " + (P.IsLand(c) ? "  y " : "  - ") + P.TMean[c].ToString("0.0", iv).PadLeft(6) + " " + P.TAmp[c].ToString("0.0", iv).PadLeft(5) + " " +
                        P.Temp[c].ToString("0.0", iv).PadLeft(6) + " " + P.Moist[c].ToString("0.00", iv).PadLeft(5) + " " + P.SoilWater[c].ToString("0.00", iv).PadLeft(5) + " " + P.RainToday[c].ToString("0.00", iv).PadLeft(5));
                }
                return 0;
            }
            int sid = a.Int("sid", w.Settlements.First());
            if (sid == 0 || !w.Settlements.Exists(sid)) { Console.WriteLine("no place " + sid); return 1; }
            ref var s = ref w.Settlements.Get(sid);
            var inv = CultureInfo.InvariantCulture;
            double km = a.Double("km", 2.5), step = a.Double("step", 25);
            int dirs = a.Int("dirs", 8);
            Console.WriteLine("place #" + sid + " inhabited " + s.Inhabited + " cell " + s.Cell + "; cells " + P.Cells + ", the radius " + P.Radius.ToString("0", inv) + " m");
            Console.WriteLine("dist  cell  biome forest felled grass moist soilW  tMean  elev  owner");
            for (int k = 0; k < dirs; k++)
            {
                double ang = 2 * Math.PI * k / dirs;
                Console.WriteLine("-- bearing " + (ang * 180 / Math.PI).ToString("0", inv));
                int last = -1;
                for (double r = 0; r <= km * 1000; r += step)
                {
                    var d = s.LocalToSphere(r * Math.Cos(ang), r * Math.Sin(ang), P.Radius);
                    int c = P.Grid.CellOf(d);
                    if (c == last) continue;
                    last = c;
                    Console.WriteLine(r.ToString("0", inv).PadLeft(5) + " " + c.ToString(inv).PadLeft(6) + " " + P.Biome[c].ToString(inv).PadLeft(4) + " " +
                        P.Forest[c].ToString("0.00", inv).PadLeft(6) + " " + P.Felled[c].ToString("0.00", inv).PadLeft(6) + " " + P.Grass[c].ToString("0.00", inv).PadLeft(5) + " " +
                        P.Moist[c].ToString("0.00", inv).PadLeft(5) + " " + P.SoilWater[c].ToString("0.00", inv).PadLeft(5) + " " + P.TMean[c].ToString("0.0", inv).PadLeft(6) + " " +
                        (P.Elev[c] - P.SeaLevel).ToString("0", inv).PadLeft(5) + " " + P.FieldOwner[c].ToString(inv).PadLeft(6) + (P.IsLand(c) ? "" : " sea"));
                }
            }
            // the fields' owners met: what each one's fields are drawn by (TerrainLod.CropColour, the share of its arable sown)
            var owners = new System.Collections.Generic.SortedDictionary<int, int>();
            for (int c = 0; c < P.Cells; c++) { int o = P.FieldOwner[c]; if (o == 0 || !w.Settlements.Exists(o)) continue; double dd = TC2.Core.Foundation.Vec3d.ArcDistance(P.Grid.Center[c], s.Anchor, P.Radius); if (dd > km * 1000) continue; owners[o] = owners.TryGetValue(o, out int n) ? n + 1 : 1; }
            foreach (var kv in owners)
            {
                ref var o = ref w.Settlements.Get(kv.Key);
                Console.WriteLine("owner #" + kv.Key + " cells " + kv.Value + " inhabited " + o.Inhabited + " people " + o.Population + " crop " + o.Crop + " growth " + o.CropGrowth.ToString("0.00", inv) +
                    " fieldHa " + o.FieldHa.ToString("0", inv) + " arableHa " + o.ArableHa.ToString("0", inv) + " muck t/ha " + TC2.Core.Places.Muck.TonnesPerHa(in o).ToString("0.0", inv) +
                    " burnt " + o.CropBurnt.ToString("0.00", inv) + " from #1 " + TC2.Core.Foundation.Vec3d.ArcDistance(o.Anchor, s.Anchor, P.Radius).ToString("0", inv) + " m");
            }
            return 0;
        }
    }
}
