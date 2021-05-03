using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Foster.Framework
{
    /// <summary>
    /// A fast random number generator
    /// </summary>
    public class FastRandom
    {
        public static FastRandom Instance { get; } = new FastRandom();

        private const double DoubleUnit = 1.0 / (int.MaxValue + 1.0);
        private const float FloatUnit = 1.0f / (int.MaxValue + 256.0f);

        private ulong _a, _b;
        private ulong _buffer;
        private ulong _bufferMask = 0;

        public FastRandom() : this((ulong)Guid.NewGuid().GetHashCode() << 32 | ((uint)Guid.NewGuid().GetHashCode()))
        {
        }

        public FastRandom(ulong seed)
        {
            _a = seed << 3;
            _b = seed >> 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong Next()
        {
            ulong t = _a;
            ulong s = _b;
            t ^= t << 23;
            t ^= t >> 17;
            t ^= s ^ (s >> 26);
            _a = s;
            _b = t;
            return t + s;
        }

        public bool NextBool()
        {
            if (_bufferMask > 0)
            {
                var b = (_buffer & _bufferMask) == 0;
                _bufferMask >>= 1;
                return b;
            }

            _buffer = Next();
            _bufferMask = 0x4000000000000000;
            return (_buffer & 0x8000000000000000) == 0;
        }

        public byte NextByte()
        {
            if (_bufferMask >= 0x80)
            {
                var b = (byte)_buffer;
                _buffer >>= 8;
                _bufferMask >>= 8;
                return b;
            }

            _buffer = Next();
            _bufferMask = 0x80000000000000;
            var b2 = (byte)_buffer;
            _buffer >>= 8;
            return b2;
        }

        public short NextShort() => (short)Next();

        public ushort NextUShort() => (ushort)Next();

        public int NextInt() => (int)Next();

        public uint NextUInt() => (uint)Next();

        public long NextLong() => (long)Next();

        public ulong NextULong() => Next();

        public float NextFloat() => (Next() & int.MaxValue) * FloatUnit;

        public double NextDouble() => (Next() & int.MaxValue) * DoubleUnit;
    }
}
