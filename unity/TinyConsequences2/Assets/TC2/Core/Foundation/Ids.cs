using System;

namespace TC2.Core.Foundation
{
    /// <summary>Kinds of historical entities (D2-008). Append only; values are saved.</summary>
    public enum EntityKind : byte
    {
        None = 0,
        Person = 1,
        Household = 2,
        Settlement = 3,
        Building = 4,
        Polity = 5,
        Culture = 6,
        Species = 7,
        Population = 8,
        Pathogen = 9,
        Technique = 10,
        Place = 11,
        Process = 12,
        Route = 13,
        Cell = 14,
        Command = 15,
        World = 16,
    }

    /// <summary>A reference to any entity: (kind, id) packed into a long. 0 means none.</summary>
    public readonly struct EntityRef : IEquatable<EntityRef>
    {
        public readonly long Packed;
        public EntityRef(EntityKind kind, int id) { Packed = id == 0 && kind == EntityKind.None ? 0 : ((long)kind << 32) | (uint)id; }
        EntityRef(long packed) { Packed = packed; }
        public static EntityRef FromPacked(long p) => new EntityRef(p);
        public static readonly EntityRef None = default;

        public EntityKind Kind => (EntityKind)(Packed >> 32);
        public int Id => (int)(Packed & 0xFFFFFFFFL);
        public bool IsNone => Packed == 0;

        public static EntityRef Person(int id) => new EntityRef(EntityKind.Person, id);
        public static EntityRef Household(int id) => new EntityRef(EntityKind.Household, id);
        public static EntityRef Settlement(int id) => new EntityRef(EntityKind.Settlement, id);
        public static EntityRef Process(int id) => new EntityRef(EntityKind.Process, id);
        public static EntityRef Cell(int id) => new EntityRef(EntityKind.Cell, id);

        public bool Equals(EntityRef o) => Packed == o.Packed;
        public override bool Equals(object obj) => obj is EntityRef o && o.Packed == Packed;
        public override int GetHashCode() => (int)(Packed ^ (Packed >> 32));
        public static bool operator ==(EntityRef a, EntityRef b) => a.Packed == b.Packed;
        public static bool operator !=(EntityRef a, EntityRef b) => a.Packed != b.Packed;
        public override string ToString() => IsNone ? "none" : Kind + "#" + Id;
    }

    /// <summary>
    /// Monotonic id counters per entity kind; ids are never reused (0 = none). Event ids are 64-bit.
    /// Serialized with the world.
    /// </summary>
    public sealed class IdAllocator
    {
        readonly int[] _next = new int[32];
        long _nextEvent = 1;

        public IdAllocator() { for (int i = 0; i < _next.Length; i++) _next[i] = 1; }

        public int Next(EntityKind kind) => _next[(int)kind]++;
        public int Peek(EntityKind kind) => _next[(int)kind];
        public long NextEvent() => _nextEvent++;
        public long PeekEvent => _nextEvent;

        public void Write(BinWriter w)
        {
            w.Int(_next.Length);
            for (int i = 0; i < _next.Length; i++) w.Int(_next[i]);
            w.Long(_nextEvent);
        }

        public void Read(BinReader r)
        {
            int n = r.Int();
            for (int i = 0; i < n; i++) { int v = r.Int(); if (i < _next.Length) _next[i] = v; }
            _nextEvent = r.Long();
        }

        public void Hash(ref StateHasher h)
        {
            for (int i = 0; i < _next.Length; i++) h.Add(_next[i]);
            h.Add(_nextEvent);
        }
    }
}
