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
