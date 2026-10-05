using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Headless
{
    /// <summary>
    /// The planet over time (F1): statistics every N days and equirectangular maps (biomes, rivers, lakes, fields,
    /// settlements, fire). Usage: planet [--seed N] [--years Y] [--every D] [--map DIR] [--width W] [--nopeople]
    /// </summary>
    public static partial class Commands
    {
        public static int Planet(Args a)
        {
            var cfg = ConfigFrom(a);
            if (a.Has("nopeople")) cfg.Settlements = 0;
            double years = a.Double("years", 1);
            int every = (int)a.Double("every", 48);
            string mapDir = a.Get("map");
            int width = (int)a.Double("width", 1024);
            string zoom = a.Get("zoom"); // "LAT,LON,DEG": a region instead of the whole planet; "first" = the first village
            _zoom = zoom;
            var sw = Stopwatch.StartNew();
            var w = WorldGenerator.Create(cfg);
            Console.WriteLine("worldgen " + sw.ElapsedMilliseconds + " ms; seed " + cfg.Seed + " cells " + w.Planet.Cells);
            if (a.Has("coarse")) w.RequestResolution(Resolution.Coarse, SimTime.StartOfDay(w.Now) + SimTime.Day);
            if (mapDir != null) { Directory.CreateDirectory(mapDir); WriteMap(w, Path.Combine(mapDir, "map_0000.png"), width); }
            Console.WriteLine(PlanetHeader());
            Console.WriteLine(PlanetLine(w));
            long end = w.Now + (long)(years * SimTime.Year);
            int k = 0;
            var run = Stopwatch.StartNew();
            while (w.Now < end)
            {
                long next = Math.Min(end, w.Now + every * SimTime.Day);
                var b = Budget.Unlimited;
                w.AdvanceTo(next, ref b);
                k++;
                Console.WriteLine(PlanetLine(w));
                if (a.Has("tect")) Console.WriteLine("      " + _tect);
                if (a.Has("volc")) Console.WriteLine("      " + VolcLine(w));
                if (a.Has("quake")) Console.WriteLine("      " + QuakeLine(w));
                if (a.Has("erosion")) Console.WriteLine("      " + ErosionLine(w));
                if (a.Has("storms")) Console.WriteLine("      " + StormLine(w));
                if (a.Has("eco")) Console.WriteLine("      " + EcoLine(w));
                if (mapDir != null) WriteMap(w, Path.Combine(mapDir, "map_" + k.ToString("0000") + ".png"), width);
            }
            FlowHistogram(w);
            double secs = run.Elapsed.TotalSeconds;
            Console.WriteLine("ran " + years.ToString("0.##", CultureInfo.InvariantCulture) + " years in " + secs.ToString("0.00", CultureInfo.InvariantCulture) +
                              " s (" + (years / Math.Max(1e-9, secs)).ToString("0.0", CultureInfo.InvariantCulture) + " years/s)");
            return 0;
        }

        static void FlowHistogram(World w)
        {
            var P = w.Planet;
            double[] edges = { 0.001, 0.003, 0.01, 0.03, 0.1, 0.3, 1, 3 };
            var sb = new System.Text.StringBuilder("land cells with smoothed flow >= (m3/s):");
            foreach (double e in edges)
            {
                int n = 0;
                for (int c = 0; c < P.Cells; c++) if (P.IsLand(c) && P.RiverFlow[c] / 86400.0 >= e) n++;
                sb.Append(' ').Append(e.ToString(CultureInfo.InvariantCulture)).Append(':').Append(n);
            }
            Console.WriteLine(sb.ToString());
        }

        static string _tect = "";

        static string EcoLine(World w)
        {
            var P = w.Planet;
            var sb = new System.Text.StringBuilder("animals:");
            for (int si = 0; si < P.Species.Count; si++)
            {
                double tot = 0, cond = 0, cells = 0;
                for (int c = 0; c < P.Cells; c++) { double v = P.Abund[si * P.Cells + c]; tot += v; if (v > 0.05) { cond += P.Cond[si * P.Cells + c]; cells++; } }
                sb.AppendFormat(CultureInfo.InvariantCulture, " {0} {1:0} (in {2:0} cells, cond {3:0.00})", TC2.Core.Planet.Ecology.KindName(P.Species[si].Kind), tot, cells, cells > 0 ? cond / cells : 0);
            }
            double g = 0, land = 0; for (int c = 0; c < P.Cells; c++) if (P.IsLand(c)) { g += P.Grass[c]; land++; }
            sb.AppendFormat(CultureInfo.InvariantCulture, " | grass {0:0.00}", g / Math.Max(1, land));
            return sb.ToString();
        }

        static string StormLine(World w)
        {
            var P = w.Planet; var m = w.Metrics;
            var sb = new System.Text.StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "storms born {0} great {1} landfalls {2} surges {3} deaths {4} | peaks by 10 m/s", m.Storms, m.GreatStorms, m.StormLandfalls, m.Surges, m.StormDeaths);
            for (int i = 0; i < m.StormPeaks.Length; i++) sb.Append(' ').Append(m.StormPeaks[i]);
            sb.Append(" | now");
            foreach (var st in P.Storms)
                sb.AppendFormat(CultureInfo.InvariantCulture, " [{0} lat {1:0} wind {2:0} peak {3:0} r {4:0}]", st.Kind == 0 ? "cyclone" : "winter", st.At.Latitude * 180 / Math.PI, st.Wind, st.Peak, st.Radius);
            return sb.ToString();
        }

        static string ErosionLine(World w)
        {
            var P = w.Planet;
            int land = 0, rivers = 0; double relief = 0, valley = 0;
            var g = P.Grid;
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c)) continue;
                land++;
                double hi = P.Elev[c], lo = P.Elev[c];
                for (int k = 0; k < 8; k++) { double e = P.Elev[g.Nbr8[c * 8 + k]]; if (e > hi) hi = e; if (e < lo) lo = e; }
                relief += hi - lo;
                if (P.RiverFlow[c] >= 5000f) { rivers++; double m = 0; for (int k = 0; k < 8; k++) m += P.Elev[g.Nbr8[c * 8 + k]]; valley += m / 8 - P.Elev[c]; }
            }
            return string.Format(CultureInfo.InvariantCulture, "erosion last step: cut {0:0} m3 settled {1:0} deep {2:0} | land {3} local relief {4:0} m | river cells {5} mean valley depth {6:0.0} m",
                TC2.Core.Planet.Erosion.Eroded, TC2.Core.Planet.Erosion.Settled, TC2.Core.Planet.Erosion.ToDeep, land, relief / Math.Max(1, land), rivers, valley / Math.Max(1, rivers));
        }

        static string QuakeLine(World w)
        {
            var P = w.Planet; var m = w.Metrics;
            int faults = 0, near = 0; double maxS = 0;
            for (int c = 0; c < P.Cells; c++) { if (P.Stress[c] > 0f) faults++; if (P.Stress[c] > 0.8 * TC2.Core.Planet.Quakes.Threshold) near++; if (P.Stress[c] > maxS) maxS = P.Stress[c]; }
            var sb = new System.Text.StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "faults {0} near-critical {1} maxS {2:0} | quakes by M", faults, near, maxS);
            for (int i = 4; i < m.Quakes.Length; i++) sb.Append(' ').Append(i).Append(':').Append(m.Quakes[i]);
            sb.AppendFormat(CultureInfo.InvariantCulture, " | deaths {0} slides {1} waves {2} drowned {3} | village-fault km", m.QuakeDeaths, m.Landslides, m.Tsunamis, m.TsunamiDeaths);
            for (int sid = w.Settlements.First(); sid != 0; sid = w.Settlements.Next(sid))
            {
                var st = w.Settlements.Get(sid);
                double best = 1e9;
                for (int c = 0; c < P.Cells; c++) if (w.Tect.Slip[c] > 0.2f) best = Math.Min(best, TC2.Core.Foundation.Vec3d.ArcDistance(P.Grid.Center[c], st.Anchor, P.Radius));
                sb.Append(' ').Append((best / 1000).ToString("0.0", CultureInfo.InvariantCulture)).Append(st.Inhabited ? "" : "x");
            }
            return sb.ToString();
        }

        static string VolcLine(World w)
        {
            var P = w.Planet; var m = w.Metrics;
            int arc = 0, hot = 0; double maxP = 0, sumS = 0;
            foreach (var v in P.Vents) { if (v.Kind == 1) hot++; else arc++; if (v.Pressure > maxP) maxP = v.Pressure; sumS += v.Supply; }
            int feeding = 0; double maxA = 0, maxM = 0;
            for (int c = 0; c < P.Cells; c++) { if (w.Tect.ArcSupply[c] > 0) feeding++; if (w.Tect.ArcSupply[c] > maxA) maxA = w.Tect.ArcSupply[c]; if (P.Magma[c] > maxM) maxM = P.Magma[c]; }
            var sb = new System.Text.StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "vents {0} (arc {1} hot {2}) maxP {3:0} supply {4:0.00}/yr | arc cells {5} maxArc {6:0.000}/yr maxMagma {7:0} | VEI",
                P.Vents.Count, arc, hot, maxP, sumS, feeding, maxA, maxM);
            for (int i = 1; i < m.Eruptions.Length; i++) sb.Append(' ').Append(m.Eruptions[i]);
            sb.AppendFormat(CultureInfo.InvariantCulture, " | born {0} died {1} islands {2} killed {3} | CO2 {4:0.0} aer {5:0.000}", m.VentsBorn, m.VentsDied, m.Islands, m.EruptionDeaths, P.CO2, P.Aerosol);
            return sb.ToString();
        }

        static string PlanetHeader() =>
            "date                      landT  soilW  rainy  forest grass  rivers lakes  maxQ(m3/s) seaQ(m3/s) pop  | obliq  ecc   Tglob  ice(km3) seaNow  coast  ice%  glac";

        static string PlanetLine(World w)
        {
            var P = w.Planet; var d = w.Drainage;
            double t = 0, sw = 0, forest = 0, grass = 0, area = 0, maxQ = 0, seaQ = 0;
            int land = 0, rivers = 0, lakes = 0, rainy = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                if (!P.IsLand(c)) continue;
                land++;
                double ar = P.AreaHa(c);
                area += ar; t += P.Temp[c] * ar; sw += P.SoilWater[c] * ar; forest += P.Forest[c] * ar; grass += P.Grass[c] * ar;
                if (P.RainToday[c] > 0.05f) rainy++;
                if (P.IsStream(c)) rivers++;
                if (P.IsLake(c)) lakes++;
                if (P.RiverFlow[c] > maxQ) maxQ = P.RiverFlow[c];
                if (!P.IsLand(d.Receiver[c])) seaQ += P.RiverFlow[c];
            }
            int pop = 0;
            for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s)) pop += w.Settlements.Get(s).Population;
            int iced = 0, young = 0; double maxE = -1e9, contE = 0, landE = 0; int contN = 0, landN = 0;
            for (int c = 0; c < P.Cells; c++)
            {
                if (P.IsLand(c) && P.Ice[c] > 5f) iced++;
                if (P.Crust[c] == 0 && P.CrustAge[c] < 100) young++;
                if (P.Elev[c] > maxE) maxE = P.Elev[c];
                if (P.Crust[c] == 1) { contE += P.Elev[c]; contN++; }
                if (P.IsLand(c)) { landE += P.Elev[c] - P.SeaLevel; landN++; }
            }
            var T = w.Tect;
            _tect = string.Format(CultureInfo.InvariantCulture, "land {0:0.000} landE {18:0} ridge {1} maxE {2:0} contE {3:0} cont {4:0.000} | coll {5} subd {6} ridges {7} fills {8} lostFill {9} gainFill {14} dropC {15} dropO {16} ridgeOnCont {17} accr {10} upC {11:0} upO {12:0} sea {13:0.0}",
                land / (double)P.Cells, young, maxE, contE / Math.Max(1, contN), contN / (double)P.Cells, T.Collided, T.Subducted, T.Ridges, T.Fills, T.ContLostToFill, T.Accreted, T.UpliftCont / Math.Max(1, contN), T.UpliftOcean / Math.Max(1, P.Cells - contN), P.SeaLevel - P.SeaLevel0, T.ContGainedByFill, T.DropC, T.DropO, T.RidgeOnCont, landE / Math.Max(1, landN));
            return string.Format(CultureInfo.InvariantCulture, "{0,-25} {1,5:0.0} {2,6:0.00} {3,6:0.00} {4,6:0.00} {5,5:0.00} {6,6} {7,5} {8,10:0.0} {9,10:0.0} {10,4}  | {11,5:0.0} {12,5:0.000} {13,6:0.00} {14,8:0.00} {15,6:0.0} {16,6:0.0} {17,5:0.0} {18,3}",
                SimTime.Format(w.Now), t / area, sw / area, rainy / (double)land, forest / area, grass / area, rivers, lakes,
                maxQ / 86400.0, seaQ / 86400.0, pop,
                P.Tilt * 180 / DMath.PI, P.Ecc, P.TGlobal, P.IceVolume / 1e9, P.SeaLevelNow - P.SeaLevel0, P.SeaLevel - P.SeaLevel0, 100.0 * iced / land, P.Glacial);
        }

        // ------------------------------------------------------------------ maps

        static string _zoom;

        public static void WriteMap(World w, string file, int width)
        {
            var P = w.Planet; var d = w.Drainage;
            int W = width, H = width / 2;
            double lat0 = DMath.HalfPi, lon0 = -DMath.PI, dLat = DMath.PI, dLon = 2 * DMath.PI;
            if (_zoom != null)
            {
                double zl, zo, deg;
                if (_zoom.StartsWith("first", StringComparison.Ordinal))
                {
                    int s0 = w.Settlements.First();
                    var an = w.Settlements.Get(s0).Anchor;
                    zl = an.Latitude * 180 / DMath.PI; zo = an.Longitude * 180 / DMath.PI;
                    deg = _zoom.Length > 6 ? double.Parse(_zoom.Substring(6), CultureInfo.InvariantCulture) : 20;
                }
                else
                {
                    var parts = _zoom.Split(',');
                    zl = double.Parse(parts[0], CultureInfo.InvariantCulture); zo = double.Parse(parts[1], CultureInfo.InvariantCulture); deg = double.Parse(parts[2], CultureInfo.InvariantCulture);
                }
                double rad = DMath.PI / 180;
                dLat = deg * rad; dLon = 2 * deg * rad / Math.Max(0.2, Math.Cos(zl * rad));
                lat0 = zl * rad + dLat / 2; lon0 = zo * rad - dLon / 2;
            }
            var rgb = new byte[W * H * 3];
            for (int y = 0; y < H; y++)
            {
                double lat = lat0 - (y + 0.5) / H * dLat;
                for (int x = 0; x < W; x++)
                {
                    double lon = lon0 + (x + 0.5) / W * dLon;
                    int c = P.Grid.CellOf(Vec3d.FromLatLon(lat, lon));
                    Col(w, c, out double r, out double g, out double b);
                    int i = (y * W + x) * 3;
                    rgb[i] = (byte)Math.Max(0, Math.Min(255, r * 255)); rgb[i + 1] = (byte)Math.Max(0, Math.Min(255, g * 255)); rgb[i + 2] = (byte)Math.Max(0, Math.Min(255, b * 255));
                }
            }
            // settlements: a red square at each anchor
            for (int s = w.Settlements.First(); s != 0; s = w.Settlements.Next(s))
            {
                ref var st = ref w.Settlements.Get(s);
                double lat = st.Anchor.Latitude, lon = st.Anchor.Longitude;
                double ux = (lon - lon0) / dLon;
                if (_zoom == null) ux -= Math.Floor(ux);
                else { ux -= Math.Floor(ux * dLon / (2 * DMath.PI)) * (2 * DMath.PI) / dLon; if (ux < 0 || ux >= 1) continue; }
                int cx = (int)(ux * W), cy = (int)((lat0 - lat) / dLat * H);
                if (cy < 0 || cy >= H) continue;
                bool alive = st.Population > 0;
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int px = ((cx + dx) % W + W) % W, py = Math.Max(0, Math.Min(H - 1, cy + dy));
                        int i = (py * W + px) * 3;
                        rgb[i] = (byte)(alive ? 230 : 120); rgb[i + 1] = (byte)(alive ? 30 : 110); rgb[i + 2] = (byte)(alive ? 30 : 110);
                    }
            }
            WritePng(file, W, H, rgb);
        }

        static void Col(World w, int c, out double r, out double g, out double b)
        {
            var P = w.Planet; var d = w.Drainage;
            if (!P.IsLand(c))
            {
                double depth = Math.Min(1.0, (P.SeaLevel - P.Elev[c]) / 3000.0);
                r = 0.10 - 0.06 * depth; g = 0.28 - 0.14 * depth; b = 0.48 - 0.18 * depth;
                return;
            }
            if (w.Fire.IsBurning(c)) { r = 1.0; g = 0.45; b = 0.05; return; }
            if (P.IsLake(c)) { r = 0.25; g = 0.50; b = 0.78; return; }
            if (P.IsStream(c)) { double k = P.IsRiver(c) ? 1.0 : 0.6; r = 0.16 * k + (1 - k) * 0.4; g = 0.40 * k + (1 - k) * 0.6; b = 0.80; return; }
            var col = Palette((Biome)P.Biome[c]);
            r = col.r; g = col.g; b = col.b;
            if (P.FieldOwner[c] != 0) { r = 0.80; g = 0.70; b = 0.35; }
            // light from the north-west: the slope toward the next cell north-west brightens
            double e = P.Elev[c], en = P.Elev[P.Grid.Nbr8[c * 8 + 0]];
            double shade = 1.0 + Math.Max(-0.25, Math.Min(0.25, (e - en) / 300.0));
            r *= shade; g *= shade; b *= shade;
        }

        static (double r, double g, double b) Palette(Biome bm)
        {
            switch (bm)
            {
                case Biome.Ice: return (0.92, 0.95, 0.98);
                case Biome.Tundra: return (0.58, 0.60, 0.52);
                case Biome.Taiga: return (0.17, 0.33, 0.24);
                case Biome.Grassland: return (0.50, 0.66, 0.30);
                case Biome.Woodland: return (0.32, 0.52, 0.22);
                case Biome.Forest: return (0.18, 0.40, 0.16);
                case Biome.Jungle: return (0.10, 0.36, 0.14);
                case Biome.Savanna: return (0.70, 0.64, 0.34);
                case Biome.Desert: return (0.86, 0.76, 0.52);
                case Biome.Mountain: return (0.48, 0.44, 0.40);
                case Biome.Snowcap: return (0.94, 0.95, 0.97);
                case Biome.Burnt: return (0.20, 0.17, 0.14);
                case Biome.Farmland: return (0.74, 0.66, 0.36);
                default: return (0.18, 0.40, 0.48);
            }
        }

        static void WritePng(string file, int w, int h, byte[] rgb)
        {
            using var fs = File.Create(file);
            fs.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            var ihdr = new byte[13];
            BE(ihdr, 0, (uint)w); BE(ihdr, 4, (uint)h); ihdr[8] = 8; ihdr[9] = 2; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            Chunk(fs, "IHDR", ihdr);
            var raw = new byte[(w * 3 + 1) * h];
            for (int y = 0; y < h; y++) { raw[y * (w * 3 + 1)] = 0; Buffer.BlockCopy(rgb, y * w * 3, raw, y * (w * 3 + 1) + 1, w * 3); }
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Fastest, true)) z.Write(raw, 0, raw.Length);
                Chunk(fs, "IDAT", ms.ToArray());
            }
            Chunk(fs, "IEND", Array.Empty<byte>());
        }

        static void BE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

        static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; BE(len, 0, (uint)data.Length); s.Write(len);
            var t = System.Text.Encoding.ASCII.GetBytes(type); s.Write(t); s.Write(data);
            uint crc = Crc(t, 0xFFFFFFFFu); crc = Crc(data, crc) ^ 0xFFFFFFFFu;
            var cb = new byte[4]; BE(cb, 0, crc); s.Write(cb);
        }

        static uint[] _crcTable;
        static uint Crc(byte[] data, uint crc)
        {
            if (_crcTable == null)
            {
                _crcTable = new uint[256];
                for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; _crcTable[n] = c; }
            }
            for (int i = 0; i < data.Length; i++) crc = _crcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return crc;
        }
    }
}
