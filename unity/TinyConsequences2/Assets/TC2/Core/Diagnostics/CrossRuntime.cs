using System.Text;
using TC2.Core.Foundation;
using TC2.Core.Simulation;
using TC2.Core.Worldgen;

namespace TC2.Core.Diagnostics
{
    /// <summary>
    /// The cross-runtime determinism probe (D2-003): one string of hashes that every host computes with the same
    /// code - dotnet (RyuJIT), the Unity editor (Mono) and the Web build (IL2CPP to wasm). Equal strings mean the
    /// runtimes live the same world bit for bit.
    /// </summary>
    public static class CrossRuntime
    {
        public static WorldConfig ReferenceConfig() => new WorldConfig { Seed = 7, SurfaceN = 48, Settlements = 4, FoundersPerSettlement = 18 };

        public static string Probe(int days = 3)
        {
            var sb = new StringBuilder();
            var h = StateHasher.Create();
            for (int k = 1; k <= 1000; k++)
            {
                double x = k * 0.0173;
                h.Add(DMath.Sin(x)); h.Add(DMath.Cos(x)); h.Add(DMath.Exp(x * 0.1)); h.Add(DMath.Log(x)); h.Add(DMath.Atan2(x, 1.3 - x));
                h.Add(DMath.Pow(x, 1.7)); h.Add(DMath.Sqrt(x)); h.Add(DMath.Tanh(x - 3));
            }
            var rh = StateHasher.Create();
            for (ulong k = 0; k < 1000; k++) { rh.Add(KeyedRng.Hash(1234, RngStream.Test, k, k * 7)); rh.Add(KeyedRng.Normal(KeyedRng.Hash(1, RngStream.Test, k), KeyedRng.Hash(2, RngStream.Test, k))); }
            sb.Append("dmath ").Append(h.ToString()).Append(" rng ").Append(rh.ToString());
            var w = WorldGenerator.Create(ReferenceConfig());
            sb.Append(" gen ").Append(w.StateHash().ToString("x16"));
            for (int d = 1; d <= days; d++)
            {
                w.AdvanceTo(SimTime.Days(d));
                sb.Append(" d").Append(d).Append(' ').Append(w.StateHash().ToString("x16"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// The same newly generated world hashed part by part - the planet field by field, then each store - so that two runtimes
        /// whose probes differ show where they part.
        /// </summary>
        public static string Parts()
        {
            var w = WorldGenerator.Create(ReferenceConfig());
            var P = w.Planet;
            int n = P.Cells;
            var sb = new StringBuilder("parts");
            void Put(string name, StateHasher h) => sb.Append(' ').Append(name).Append(' ').Append(h.ToString());
            { var h = StateHasher.Create(); w.Config.Hash(ref h); Put("config", h); }
            { var h = StateHasher.Create(); w.Ids.Hash(ref h); Put("ids", h); }
            { var h = StateHasher.Create(); w.Queue.Hash(ref h); Put("queue", h); }
            { var h = StateHasher.Create(); h.Add(P.SeaLevel); h.Add(P.CO2); h.Add(P.GlobalTempOffset); h.Add(P.IceVolume); h.Add(P.SeaLevelRef); h.Add(P.TGlobalRef); h.Add(P.OceanAreaRef); h.Add(P.SupplyRef); h.Add(P.FreeLandRef); Put("globals", h); }
            foreach (var (name, v) in new[] { ("sea", P.SeaLevel), ("co2", P.CO2), ("toff", P.GlobalTempOffset), ("icevol", P.IceVolume), ("seaRef", P.SeaLevelRef), ("tRef", P.TGlobalRef), ("oceanRef", P.OceanAreaRef), ("supplyRef", P.SupplyRef), ("landRef", P.FreeLandRef) })
                sb.Append(' ').Append(name).Append(' ').Append(System.BitConverter.DoubleToInt64Bits(v).ToString("x16"));
            { var h = StateHasher.Create(); for (int i = 0; i < n; i++) { h.Add(P.TMean[i]); } Put("tmean", h); }
            { var h = StateHasher.Create(); for (int i = 0; i < n; i++) { h.Add(P.TAmp[i]); } Put("tamp", h); }
            { var h = StateHasher.Create(); for (int j = 0; j < P.ZonalT.Length; j++) h.Add(P.ZonalT[j]); Put("zonal", h); }
            { var h = StateHasher.Create(); for (int i = 0; i < n; i++) h.Add(P.Elev[i]); Put("elev", h); }
            { var h = StateHasher.Create(); for (int i = 0; i < n; i++) { h.Add(P.Crust[i]); h.Add(P.CrustAge[i]); h.Add(P.Plate[i]); } Put("crust", h); }
            { var h = StateHasher.Create(); for (int i = 0; i < n; i++) h.Add(P.Temp[i]); Put("temp", h); }
            { var h = StateHasher.Create(); for (int i = 0; i < n; i++) h.Add(P.Moist[i]); Put("moist", h); }
            { var h = StateHasher.Create(); for (int i = 0; i < n; i++) { h.Add(P.SoilWater[i]); h.Add(P.RiverFlow[i]); h.Add(P.LakeWater[i]); } Put("water", h); }
            { var h = StateHasher.Create(); for (int i = 0; i < n; i++) { h.Add(P.Grass[i]); h.Add(P.Forest[i]); h.Add(P.Biome[i]); } Put("veg", h); }
            { var h = StateHasher.Create(); for (int i = 0; i < n; i++) h.Add(P.Ice[i]); Put("ice", h); }
            { var h = StateHasher.Create(); for (int i = 0; i < P.Species.Count; i++) P.Species[i].Hash(ref h); Put("species", h); }
            { var h = StateHasher.Create(); for (int i = 0; i < P.Abund.Length; i++) h.Add(P.Abund[i]); Put("abund", h); }
            { var h = StateHasher.Create(); for (int i = 0; i < P.Abund.Length; i++) { h.Add(P.Cond[i]); h.Add(P.Coat[i]); h.Add(P.Drift[i]); } Put("coat", h); }
            { var h = StateHasher.Create(); for (int i = 0; i < n; i++) h.Add(P.FieldOwner[i]); Put("fields", h); }
            { var h = StateHasher.Create(); P.Hash(ref h); Put("planet", h); }
            { var h = StateHasher.Create(); w.Fire.Hash(ref h); Put("fire", h); }
            { var h = StateHasher.Create(); for (int id = w.Settlements.First(); id != 0; id = w.Settlements.Next(id)) w.Settlements.Get(id).Hash(ref h); Put("settlements", h); }
            { var h = StateHasher.Create(); for (int id = w.Households.First(); id != 0; id = w.Households.Next(id)) w.Households.Get(id).Hash(ref h); Put("households", h); }
            { var h = StateHasher.Create(); for (int id = w.People.First(); id != 0; id = w.People.Next(id)) w.People.Get(id).Hash(ref h); Put("people", h); }
            { var h = StateHasher.Create(); for (int id = w.Herds.First(); id != 0; id = w.Herds.Next(id)) w.Herds.Get(id).Hash(ref h); Put("herds", h); }
            { var h = StateHasher.Create(); for (int id = w.Beasts.First(); id != 0; id = w.Beasts.Next(id)) w.Beasts.Get(id).Hash(ref h); Put("beasts", h); }
            { var h = StateHasher.Create(); for (int id = w.Buildings.First(); id != 0; id = w.Buildings.Next(id)) w.Buildings.Get(id).Hash(ref h); Put("buildings", h); }
            { var h = StateHasher.Create(); for (int id = w.Pathogens.First(); id != 0; id = w.Pathogens.Next(id)) w.Pathogens.Get(id).Hash(ref h); Put("pathogens", h); }
            { var h = StateHasher.Create(); for (int id = w.Deeds.First(); id != 0; id = w.Deeds.Next(id)) w.Deeds.Get(id).Hash(ref h); w.DeedBook.Hash(ref h); Put("deeds", h); }
            { var h = StateHasher.Create(); w.History.Hash(ref h); Put("history", h); }
            return sb.ToString();
        }
    }
}
