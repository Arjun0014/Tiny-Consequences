using TC2.Core.Foundation;

namespace TC2.Core.Simulation
{
    public enum FxKind : byte { None = 0, Quake = 1, Landslide = 2, Wave = 3, Lava = 4 }

    /// <summary>One thing that happened somewhere, for the view to show (a cell, a time, a size).</summary>
    public struct Fx
    {
        public long Time;
        public int Cell;
        public FxKind Kind;
        public float Size;    // quake: magnitude; wave: run-up m; lava: VEI
    }

    /// <summary>
    /// A ring of the latest physical happenings (quakes, slides, waves, lava) for the presentation: written by the
    /// simulation, never read by it, never saved or hashed - a loaded world starts with an empty ring, which only means
    /// the view has nothing old to replay. <see cref="Version"/> counts writes so views can catch up.
    /// </summary>
    public sealed class FxRing
    {
        readonly Fx[] _a = new Fx[1024];
        public long Version;
        public void Add(long time, int cell, FxKind kind, float size) { _a[Version % _a.Length] = new Fx { Time = time, Cell = cell, Kind = kind, Size = size }; Version++; }
        public int Capacity => _a.Length;
        /// <summary>The entry written as number <paramref name="n"/> (valid while n is within the last Capacity writes).</summary>
        public Fx At(long n) => _a[n % _a.Length];
    }
}
