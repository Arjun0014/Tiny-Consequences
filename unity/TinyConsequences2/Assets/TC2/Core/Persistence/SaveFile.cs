using System;
using System.IO;
using System.IO.Compression;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Persistence
{
    public enum OfflineMode : byte { Paused = 0, Gentle = 1, Living = 2, Ancient = 3, DeepTime = 4 }

    /// <summary>Metadata kept in the uncompressed header (12 §2): what the load screen shows, and offline state.</summary>
    public struct SaveHeader
    {
        public const uint Magic = 0x57324354; // "TC2W" little-endian
        public const ushort Format = 1;

        public ushort FormatVersion;
        public int SimVersion;
        public ulong Seed;
        public long WorldTime;
        public long SavedRealUtcMs;
        public OfflineMode Offline;
        public long CatchUpTarget;        // world time a catch-up in progress is heading to (0 none)
        public long CatchUpStartedUtcMs;
        public int Population;
        public int Settlements;
        public string Headline;
        public byte Compression;          // 0 none, 1 deflate
        public int PayloadLength;
        public int UncompressedLength;
        public ulong PayloadHash;
        public ulong StateHash;
    }

    /// <summary>
    /// Exact-state saves (D2-009): header + the world's full serialized state, compressed and hash-checked.
    /// Loading rebuilds derived indexes and verifies the state hash equals the one recorded at save time.
    /// </summary>
    public static class SaveFile
    {
        /// <summary>
        /// The buffers a host keeps from one save to the next (the game's autosave, a minute apart): a save into them allocates nothing large once
        /// they have grown to the world's size. Without them a great world's save made ~460 MB of short-lived buffers (its 79 MB state grown by
        /// doubling, copied, deflated through a growing stream, copied twice more), and the browser's heap - which only grows - reached its 2 GB
        /// ceiling after three autosaves of a year-900 world (session 24, `realmsperf`). One save at a time; the save is `Out.Buffer[0..length)`.
        /// </summary>
        public sealed class Scratch
        {
            public readonly BinWriter Body;
            public readonly MemoryStream Deflated;
            public readonly BinWriter Out;

            public Scratch() : this(1 << 20, 1 << 16) { }

            /// <summary>Buffers sized for a world already known (a loaded save's lengths, an eighth more): its first save grows nothing by doubling -
            /// which alone made ~255 MB of short-lived buffers for a 79 MB state. (The memory pass, 14 §7: the state goes through `Body` a chunk at a
            /// time, hashed and deflated as it comes - `Body` no longer holds the whole state, ~124 MB of a year-1,000 world kept for the session.)</summary>
            public Scratch(int stateBytes, int saveBytes)
            {
                Body = new BinWriter(Chunk);
                Deflated = new MemoryStream(Math.Max(1 << 16, saveBytes + saveBytes / 8));
                Out = new BinWriter(Math.Max(1 << 16, saveBytes + saveBytes / 8 + 4096));
            }

            /// <summary>For the world a save holds (its header's lengths).</summary>
            public static Scratch For(in SaveHeader h) => new Scratch(h.UncompressedLength, h.PayloadLength);
        }

        public static byte[] Save(World w, long realUtcMs, OfflineMode mode, long catchUpTarget = 0, long catchUpStartedUtcMs = 0, string headline = null, bool compress = true)
        {
            var s = new Scratch();
            Save(w, s, realUtcMs, mode, catchUpTarget, catchUpStartedUtcMs, headline, compress);
            return s.Out.ToArray();
        }

        /// <summary>A save into the host's buffers (the same bytes as `Save`); returns its length - the save is `scratch.Out.Buffer[0..length)`.</summary>
        public static int Save(World w, Scratch scratch, long realUtcMs, OfflineMode mode, long catchUpTarget = 0, long catchUpStartedUtcMs = 0, string headline = null, bool compress = true)
        {
            var body = scratch.Body;
            body.Clear();
            byte[] payload;
            int payloadLen, rawLen;
            ulong payloadHash;
            if (compress)
            {
                // (the memory pass, 14 §7) the state hashed and deflated a chunk at a time as it is written: the save never holds all of it
                var into = scratch.Deflated;
                into.SetLength(0); into.Position = 0;
                var hash = StateHasher.Create();
                using (var ds = new DeflateStream(into, CompressionLevel.Fastest, true))
                {
                    body.Sink = (b, n) => { HashInto(ref hash, b, 0, n); ds.Write(b, 0, n); };
                    try { w.WriteState(body); body.Flush(); }
                    finally { body.Sink = null; }
                }
                rawLen = (int)body.Total;
                body.Clear();
                payload = into.GetBuffer();
                payloadLen = (int)into.Length;
                payloadHash = hash.Value;
            }
            else
            {
                w.WriteState(body);
                payload = body.Buffer;
                payloadLen = rawLen = body.Length;
                payloadHash = HashBytes(payload, rawLen);
            }
            var h = new SaveHeader
            {
                FormatVersion = SaveHeader.Format, SimVersion = World.FormatSimVersion, Seed = w.Seed, WorldTime = w.Now,
                SavedRealUtcMs = realUtcMs, Offline = mode, CatchUpTarget = catchUpTarget, CatchUpStartedUtcMs = catchUpStartedUtcMs,
                Population = w.LivingPeople(), Settlements = w.InhabitedSettlements(), Headline = headline ?? "",
                Compression = (byte)(compress ? 1 : 0), PayloadLength = payloadLen, UncompressedLength = rawLen,
                PayloadHash = payloadHash, StateHash = w.StateHash(),
            };
            var outw = scratch.Out;
            outw.Clear();
            WriteHeader(outw, h);
            outw.Bytes(payload, 0, payloadLen);
            return outw.Length;
        }

        public static void WriteHeader(BinWriter w, in SaveHeader h)
        {
            w.UInt(SaveHeader.Magic); w.UShort(h.FormatVersion); w.Int(h.SimVersion); w.ULong(h.Seed); w.Long(h.WorldTime); w.Long(h.SavedRealUtcMs);
            w.Byte((byte)h.Offline); w.Long(h.CatchUpTarget); w.Long(h.CatchUpStartedUtcMs); w.Int(h.Population); w.Int(h.Settlements); w.String(h.Headline);
            w.Byte(h.Compression); w.Int(h.PayloadLength); w.Int(h.UncompressedLength); w.ULong(h.PayloadHash); w.ULong(h.StateHash);
        }

        public static SaveHeader ReadHeader(BinReader r)
        {
            if (r.UInt() != SaveHeader.Magic) throw new FormatException("not a TC2 world save");
            var h = new SaveHeader
            {
                FormatVersion = r.UShort(), SimVersion = r.Int(), Seed = r.ULong(), WorldTime = r.Long(), SavedRealUtcMs = r.Long(),
                Offline = (OfflineMode)r.Byte(), CatchUpTarget = r.Long(), CatchUpStartedUtcMs = r.Long(), Population = r.Int(), Settlements = r.Int(),
                Headline = r.String(), Compression = r.Byte(), PayloadLength = r.Int(), UncompressedLength = r.Int(), PayloadHash = r.ULong(), StateHash = r.ULong(),
            };
            if (h.FormatVersion > SaveHeader.Format) throw new FormatException("save format " + h.FormatVersion + " is newer than this game (" + SaveHeader.Format + ")");
            return h;
        }

        public static SaveHeader PeekHeader(byte[] data) => ReadHeader(new BinReader(data));

        public static World Load(byte[] data, out SaveHeader header, bool verify = true)
        {
            var r = new BinReader(data);
            header = ReadHeader(r);
            if (header.SimVersion != World.FormatSimVersion) throw new FormatException("the save is from another model of the world (" + header.SimVersion + ", this game " + World.FormatSimVersion + ")");
            World w;
            if (header.Compression == 1)
            {
                // (the memory pass, 14 §7) inflated a chunk at a time as the state is read, from the save in place - never all of the state at once
                // (a year-1,000 world's ~110 MB, in a browser's heap that only grows); its hash and length checked when it has all been read
                var hash = StateHasher.Create();
                long seen = 0;
                using (var ms = new MemoryStream(data, r.Position, header.PayloadLength))
                using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                {
                    var sr = new BinReader(ds, Chunk, (b, o, n) => { HashInto(ref hash, b, o, n); seen += n; });
                    w = World.ReadState(sr);
                    sr.Drain();
                }
                if (seen != header.UncompressedLength) throw new InvalidDataException("save payload is truncated");
                if (verify && hash.Value != header.PayloadHash) throw new InvalidDataException("save payload is corrupt (hash mismatch)");
            }
            else
            {
                var raw = new byte[header.PayloadLength]; Buffer.BlockCopy(data, r.Position, raw, 0, header.PayloadLength);
                if (verify && HashBytes(raw) != header.PayloadHash) throw new InvalidDataException("save payload is corrupt (hash mismatch)");
                w = World.ReadState(new BinReader(raw));
            }
            if (verify)
            {
                ulong h = w.StateHash();
                if (h != header.StateHash) throw new InvalidDataException("loaded world differs from the saved one (state hash " + h.ToString("x16") + " != " + header.StateHash.ToString("x16") + ")");
            }
            return w;
        }

        public static ulong HashBytes(byte[] b) => HashBytes(b, b.Length);

        /// <summary>The bytes a save's state passes through at a time, written or read (the memory pass, 14 §7).</summary>
        public const int Chunk = 1 << 20;

        /// <summary>`count` bytes of `b` from `offset` into a running payload hash: byte by byte, as `HashBytes` hashes them (its words are their
        /// eight bytes in order), so the chunks a save passes through do not change it.</summary>
        static void HashInto(ref StateHasher h, byte[] b, int offset, int count)
        {
            int end = offset + count;
            for (int i = offset; i < end; i++) h.Add(b[i]);
        }

        /// <summary>The hash of the first `length` bytes of `b`.</summary>
        public static ulong HashBytes(byte[] b, int length)
        {
            var h = StateHasher.Create();
            int i = 0;
            for (; i + 8 <= length; i += 8) h.Add(BitConverter.ToUInt64(b, i));
            for (; i < length; i++) h.Add(b[i]);
            return h.Value;
        }

        public static byte[] Deflate(byte[] raw)
        {
            using (var ms = new MemoryStream())
            {
                Deflate(raw, raw.Length, ms);
                return ms.ToArray();
            }
        }

        /// <summary>The first `length` bytes of `raw` deflated into `into` (emptied first; it keeps the capacity it has grown to).</summary>
        public static void Deflate(byte[] raw, int length, MemoryStream into)
        {
            into.SetLength(0);
            into.Position = 0;
            using (var ds = new DeflateStream(into, CompressionLevel.Fastest, true)) ds.Write(raw, 0, length);
        }

        public static byte[] Inflate(byte[] comp, int expected) => Inflate(comp, 0, comp.Length, expected);

        /// <summary>`count` deflated bytes of `comp` from `offset` inflated to `expected` bytes.</summary>
        public static byte[] Inflate(byte[] comp, int offset, int count, int expected)
        {
            var outBuf = new byte[expected];
            using (var ms = new MemoryStream(comp, offset, count))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            {
                int off = 0;
                while (off < expected)
                {
                    int n = ds.Read(outBuf, off, expected - off);
                    if (n <= 0) break;
                    off += n;
                }
                if (off != expected) throw new InvalidDataException("save payload is truncated");
            }
            return outBuf;
        }
    }

    /// <summary>
    /// Offline progression (12 §5, D2-010): converts a real absence into world time by mode, with caps.
    /// </summary>
    public static class OfflineProgress
    {
        public static double YearsPerRealHour(OfflineMode m)
        {
            switch (m)
            {
                case OfflineMode.Gentle: return 1.0;
                case OfflineMode.Living: return 4.0;
                case OfflineMode.Ancient: return 25.0;
                case OfflineMode.DeepTime: return 200.0;
                default: return 0.0;
            }
        }

        public static double CapYears(OfflineMode m)
        {
            switch (m)
            {
                case OfflineMode.Gentle: return 100;
                case OfflineMode.Living: return 400;
                case OfflineMode.Ancient: return 3000;
                case OfflineMode.DeepTime: return 20000;
                default: return 0;
            }
        }

        /// <summary>World seconds to simulate for an absence (negative absences count as none).</summary>
        public static long WorldSecondsFor(long realElapsedMs, OfflineMode mode)
        {
            if (realElapsedMs <= 0) return 0;
            double hours = realElapsedMs / 3600000.0;
            double years = Math.Min(CapYears(mode), hours * YearsPerRealHour(mode));
            return SimTime.Years(years);
        }

        /// <summary>Lives (person-years) above which a catch-up is lived at COARSE resolution (D2-023).</summary>
        public const double CoarsePersonYears = 40000;

        /// <summary>The last stretch before the player returns is lived in full, so everyone is mid-activity.</summary>
        public const long ActivityTail = 2 * SimTime.Day;

        /// <summary>
        /// Plans how a new catch-up to <paramref name="target"/> is lived: a span holding many lives is lived COARSE
        /// from the next midnight and in ACTIVITY again for the last two days. The switches are logged commands, so
        /// the plan is part of the world's history (a resumed catch-up keeps the plan it has). Returns true if COARSE.
        /// </summary>
        public static bool PlanCatchUp(World w, long target, double coarsePersonYears = CoarsePersonYears) =>
            PlanCatchUp(w, target, OfflineMode.Living, coarsePersonYears);

        /// <summary>
        /// As above, by the offline mode: Ancient and Deep time live their COARSE span in DEEP (F8) - calm villages
        /// <see cref="DeepStepDays"/> days at a step - and back to day by day <see cref="DeepTail"/> before the world returns to full
        /// activity, so every village's step has ended by then.
        /// </summary>
        public static bool PlanCatchUp(World w, long target, OfflineMode mode, double coarsePersonYears = CoarsePersonYears)
        {
            if (w.Resolution != Resolution.Activity) return false;
            double personYears = (target - w.Now) / (double)SimTime.Year * w.LivingPeople();
            if (personYears < coarsePersonYears) return false;
            long start = SimTime.StartOfDay(w.Now) + SimTime.Day;
            long back = target - ActivityTail;
            if (back <= start + SimTime.Day) return false;
            w.RequestResolution(Resolution.Coarse, start);
            if ((mode == OfflineMode.Ancient || mode == OfflineMode.DeepTime) && back - DeepTail > start + SimTime.Day)
            {
                w.RequestStepDays(DeepStepDays, start);
                w.RequestStepDays(1, back - DeepTail);
            }
            w.RequestResolution(Resolution.Activity, back);
            return true;
        }

        /// <summary>The days a calm village lives at a step in DEEP (F8).</summary>
        public const int DeepStepDays = 12;

        /// <summary>How long before the return to full activity DEEP ends (longer than any step).</summary>
        public const long DeepTail = (DeepStepDays + 1) * SimTime.Day;
    }
}
