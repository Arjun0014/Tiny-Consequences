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
    }
}
