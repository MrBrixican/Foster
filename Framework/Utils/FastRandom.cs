using System;
using System.Runtime.CompilerServices;

namespace Foster.Framework
{
    /// <summary>
    /// A fast random number generator based on xorshift128+
    /// </summary>
    public class FastRandom
    {
        public static FastRandom Instance { get; } = new FastRandom();

        private const double DoubleMult = 1.0 / (1ul << 53);
        private const float FloatMult = 1.0f / (1u << 24);

        private ulong _a, _b;
        private ulong _buffer;
        private ulong _bufferMask = 0;

        public FastRandom() : this((ulong)Guid.NewGuid().GetHashCode() << 32 | ((uint)Guid.NewGuid().GetHashCode()))
        {
        }

        public FastRandom(ulong seed)
        {
            seed = seed == 0 ? 1337 : seed;
            _a = seed << 3;
            _b = seed >> 3;
        }

        /// <summary>
        /// Returns random <see cref="ulong"/> with range [0, <see cref="ulong.MaxValue"/>]
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong NextSample()
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

        /// <summary>
        /// Returns random <see cref="bool"/>
        /// </summary>
        public bool NextBool()
        {
            if (_bufferMask > 0)
            {
                var b = (_buffer & _bufferMask) == 0;
                _bufferMask >>= 1;
                return b;
            }

            _buffer = NextSample();
            _bufferMask = 0x4000000000000000;
            return (_buffer & 0x8000000000000000) == 0;
        }

        /// <summary>
        /// Returns random <see cref="bool"/> that is true with a probability of <paramref name="chance"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextBool(float chance)
            => NextFloat() <= chance;

        /// <summary>
        /// Returns random <see cref="byte"/> with range [0, <see cref="byte.MaxValue"/>]
        /// </summary>
        public byte NextByte()
        {
            if (_bufferMask >= 0x80)
            {
                var b = (byte)_buffer;
                _buffer >>= 8;
                _bufferMask >>= 8;
                return b;
            }

            _buffer = NextSample();
            _bufferMask = 0x80000000000000;
            var b2 = (byte)_buffer;
            _buffer >>= 8;
            return b2;
        }

        /// <summary>
        /// Returns random <see cref="int"/> with range [0, <see cref="int.MaxValue"/>]
        /// </summary>
        public int NextInt() 
            => ((int)NextSample()) & int.MaxValue;

        /// <summary>
        /// Returns random <see cref="int"/> with range [0, <paramref name="max"/>)
        /// </summary>
        public int NextInt(int max) 
            => max > 1 ? (int)(NextSample() % (ulong)max) : 0;

        /// <summary>
        /// Returns random <see cref="int"/> with range [<paramref name="min"/>, <paramref name="max"/>)
        /// </summary>
        public int NextInt(int min, int max) 
            => max > min ? (int)(NextSample() % (ulong)((long)max - min)) + min : min;

        /// <summary>
        /// Return random <see cref="double"/> with range [0, 1)
        /// </summary>
        public double NextDouble()
            // As described in http://prng.di.unimi.it/:
            // "A standard double (64-bit) floating-point number in IEEE floating point format has 52 bits of significand,
            //  plus an implicit bit at the left of the significand. Thus, the representation can actually store numbers with
            //  53 significant binary digits. Because of this fact, in C99 a 64-bit unsigned integer x should be converted to
            //  a 64-bit double using the expression
            //  (x >> 11) * 0x1.0p-53"
            => (NextSample() >> 11) * DoubleMult;

        /// <summary>
        /// Returns random <see cref="double"/> with range [0, <paramref name="max"/>)
        /// </summary>
        public double NextDouble(double max)
            => (NextSample() >> 11) * DoubleMult * max;

        /// <summary>
        /// Returns random <see cref="double"/> with range [<paramref name="min"/>, <paramref name="max"/>)
        /// </summary>
        public double NextDouble(double min, double max)
            => (NextSample() >> 11) * DoubleMult * (max - min) + min;

        /// <summary>
        /// Returns random <see cref="float"/> with range [0, 1)
        /// </summary>
        public float NextFloat()
            // Same as NextDouble(), but with 24 bits instead of 53.
            => (NextSample() >> 40) * FloatMult;

        /// <summary>
        /// Returns random <see cref="float"/> with range [0, <paramref name="max"/>)
        /// </summary>
        public float NextFloat(float max)
            => (NextSample() >> 40) * FloatMult * max;

        /// <summary>
        /// Returns random <see cref="float"/> with range [<paramref name="min"/>, <paramref name="max"/>)
        /// </summary>
        public float NextFloat(float min, float max)
            => (NextSample() >> 40) * FloatMult * (max - min) + min;
    }
}
