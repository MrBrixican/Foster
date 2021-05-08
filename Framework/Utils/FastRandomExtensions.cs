using System;
using System.Collections.Generic;
using System.Numerics;

namespace Foster.Framework
{
    /// <summary>
    /// Random Utilities
    /// </summary>
    public static class FastRandomExtensions
    {
        /// <summary>
        /// Returns random <see cref="Vector2"/> with range [<paramref name="min"/>, <paramref name="max"/>)
        /// </summary>
        public static Vector2 NextVector2(this FastRandom random, Vector2 min, Vector2 max)
            => new Vector2(random.NextFloat(min.X, max.X), random.NextFloat(min.Y, max.Y));

        #region Choose

        public static T Choose<T>(this FastRandom random, T a, T b)
            => Calc.GiveMe<T>(random.NextInt(2), a, b);

        public static T Choose<T>(this FastRandom random, T a, T b, T c)
            => Calc.GiveMe<T>(random.NextInt(3), a, b, c);

        public static T Choose<T>(this FastRandom random, T a, T b, T c, T d)
            => Calc.GiveMe<T>(random.NextInt(4), a, b, c, d);

        public static T Choose<T>(this FastRandom random, T a, T b, T c, T d, T e)
            => Calc.GiveMe<T>(random.NextInt(5), a, b, c, d, e);

        public static T Choose<T>(this FastRandom random, T a, T b, T c, T d, T e, T f)
            => Calc.GiveMe<T>(random.NextInt(6), a, b, c, d, e, f);

        public static T Choose<T>(this FastRandom random, params T[] choices)
            => choices[random.NextInt(choices.Length)];

        public static T Choose<T>(this FastRandom random, IList<T> choices)
            => choices[random.NextInt(choices.Count)];

        public static T Choose<T>(this FastRandom random, ReadOnlySpan<T> choices)
            => choices[random.NextInt(choices.Length)];

        #endregion
    }
}
