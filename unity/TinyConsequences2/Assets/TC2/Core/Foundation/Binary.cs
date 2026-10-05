using System;
using System.Text;

namespace TC2.Core.Foundation
{
    /// <summary>
    /// Little-endian binary writer over a growable byte buffer. Explicit, allocation-light,
    /// identical output on every runtime (no reflection, no BinaryFormatter, no endianness surprises).
    /// </summary>
    public sealed class BinWriter
    {
        byte[] _buf;
        int _len;

        public BinWriter(int capacity = 1 << 16) { _buf = new byte[Math.Max(16, capacity)]; }

        public int Length => _len;
        public byte[] Buffer => _buf;

        public byte[] ToArray()
        {
            var r = new byte[_len];
            System.Buffer.BlockCopy(_buf, 0, r, 0, _len);
            return r;
        }

        public void Clear() { _len = 0; }

        void Ensure(int extra)
        {
            int need = _len + extra;
            if (need <= _buf.Length) return;
            int cap = _buf.Length;
            while (cap < need) cap *= 2;
            Array.Resize(ref _buf, cap);
        }

        public void Byte(byte v) { Ensure(1); _buf[_len++] = v; }
        public void Bool(bool v) { Byte(v ? (byte)1 : (byte)0); }
        public void UShort(ushort v) { Ensure(2); _buf[_len++] = (byte)v; _buf[_len++] = (byte)(v >> 8); }
        public void Short(short v) { UShort((ushort)v); }
        public void Int(int v)
        {
            Ensure(4);
            _buf[_len++] = (byte)v; _buf[_len++] = (byte)(v >> 8); _buf[_len++] = (byte)(v >> 16); _buf[_len++] = (byte)(v >> 24);
        }
        public void UInt(uint v) { Int((int)v); }
        public void Long(long v) { Int((int)v); Int((int)(v >> 32)); }
        public void ULong(ulong v) { Long((long)v); }
        public void Float(float v) { Int(BitConverter.SingleToInt32Bits(v)); }
        public void Double(double v) { Long(BitConverter.DoubleToInt64Bits(v)); }

        public void String(string s)
        {
            if (s == null) { Int(-1); return; }
            int n = Encoding.UTF8.GetByteCount(s);
            Int(n);
            Ensure(n);
            Encoding.UTF8.GetBytes(s, 0, s.Length, _buf, _len);
            _len += n;
        }

        public void Bytes(byte[] b, int offset, int count) { Ensure(count); System.Buffer.BlockCopy(b, offset, _buf, _len, count); _len += count; }

        public void FloatArray(float[] a, int count)
        {
            Int(count);
            Ensure(count * 4);
            for (int i = 0; i < count; i++)
            {
                int v = BitConverter.SingleToInt32Bits(a[i]);
                _buf[_len++] = (byte)v; _buf[_len++] = (byte)(v >> 8); _buf[_len++] = (byte)(v >> 16); _buf[_len++] = (byte)(v >> 24);
            }
        }

        public void DoubleArray(double[] a, int count) { Int(count); for (int i = 0; i < count; i++) Double(a[i]); }
        public void IntArray(int[] a, int count) { Int(count); for (int i = 0; i < count; i++) Int(a[i]); }
        public void ByteArray(byte[] a, int count) { Int(count); Bytes(a, 0, count); }

        /// <summary>Reserves 4 bytes for a length to be patched later; returns the position.</summary>
        public int BeginLength() { int p = _len; Int(0); return p; }
        public void EndLength(int pos)
        {
            int n = _len - pos - 4;
            _buf[pos] = (byte)n; _buf[pos + 1] = (byte)(n >> 8); _buf[pos + 2] = (byte)(n >> 16); _buf[pos + 3] = (byte)(n >> 24);
        }
    }

    public sealed class BinReader
    {
        readonly byte[] _buf;
        int _pos;
        readonly int _end;

        public BinReader(byte[] buf, int offset = 0, int count = -1)
        {
            _buf = buf; _pos = offset; _end = count < 0 ? buf.Length : offset + count;
        }

        public int Position { get => _pos; set => _pos = value; }
        public int End => _end;
        public bool AtEnd => _pos >= _end;

        void Need(int n) { if (_pos + n > _end) throw new FormatException("TC2 save: unexpected end of data"); }

        public byte Byte() { Need(1); return _buf[_pos++]; }
        public bool Bool() => Byte() != 0;
        public ushort UShort() { Need(2); ushort v = (ushort)(_buf[_pos] | (_buf[_pos + 1] << 8)); _pos += 2; return v; }
        public short Short() => (short)UShort();
        public int Int()
        {
            Need(4);
            int v = _buf[_pos] | (_buf[_pos + 1] << 8) | (_buf[_pos + 2] << 16) | (_buf[_pos + 3] << 24);
            _pos += 4; return v;
        }
        public uint UInt() => (uint)Int();
        public long Long() { uint lo = (uint)Int(); int hi = Int(); return ((long)hi << 32) | lo; }
        public ulong ULong() => (ulong)Long();
        public float Float() => BitConverter.Int32BitsToSingle(Int());
        public double Double() => BitConverter.Int64BitsToDouble(Long());

        public string String()
        {
            int n = Int();
            if (n < 0) return null;
            Need(n);
            string s = Encoding.UTF8.GetString(_buf, _pos, n);
            _pos += n;
            return s;
        }

        public void Skip(int n) { Need(n); _pos += n; }

        public float[] FloatArray(float[] into = null)
        {
            int n = Int();
            var a = into != null && into.Length >= n ? into : new float[n];
            for (int i = 0; i < n; i++) a[i] = Float();
            return a;
        }
        public double[] DoubleArray(double[] into = null)
        {
            int n = Int();
            var a = into != null && into.Length >= n ? into : new double[n];
            for (int i = 0; i < n; i++) a[i] = Double();
            return a;
        }
        public int[] IntArray(int[] into = null)
        {
            int n = Int();
            var a = into != null && into.Length >= n ? into : new int[n];
            for (int i = 0; i < n; i++) a[i] = Int();
            return a;
        }
        public byte[] ByteArray(byte[] into = null)
        {
            int n = Int();
            var a = into != null && into.Length >= n ? into : new byte[n];
            Need(n);
            System.Buffer.BlockCopy(_buf, _pos, a, 0, n);
            _pos += n;
            return a;
        }
    }

    /// <summary>
    /// 64-bit state hash (FNV-1a over 64-bit words with a final mix). Used for determinism, slicing,
    /// save/load and cross-runtime tests. Doubles are hashed by their exact bit patterns.
    /// </summary>
    public struct StateHasher
    {
        ulong _h;
        public static StateHasher Create() { var s = new StateHasher(); s._h = 14695981039346656037UL; return s; }

        public void Add(ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                _h ^= (v & 0xFF);
                _h *= 1099511628211UL;
                v >>= 8;
            }
        }
        public void Add(long v) => Add((ulong)v);
        public void Add(int v) => Add((ulong)(uint)v);
        public void Add(uint v) => Add((ulong)v);
        public void Add(byte v) { _h ^= v; _h *= 1099511628211UL; }
        public void Add(bool v) => Add(v ? (byte)1 : (byte)0);
        public void Add(double v) => Add((ulong)BitConverter.DoubleToInt64Bits(v));
        public void Add(float v) => Add((uint)BitConverter.SingleToInt32Bits(v));
        public void Add(string s)
        {
            if (s == null) { Add(-1); return; }
            Add(s.Length);
            for (int i = 0; i < s.Length; i++) { _h ^= s[i]; _h *= 1099511628211UL; }
        }

        public ulong Value => KeyedRng.Mix(_h);
        public override string ToString() => Value.ToString("x16");
    }
}
