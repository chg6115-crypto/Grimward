using System;
using System.Collections.Generic;
namespace Grimward.Domain.Map
{
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public int Q { get; }
        public int R { get; }
        public HexCoord(int q, int r) { Q = q; R = r; }
        public int DistanceFromOrigin => DistanceTo(new HexCoord(0, 0));
        public int DistanceTo(HexCoord other) =>
            (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) +
             Math.Abs(Q + R - other.Q - other.R)) / 2;
        public IEnumerable<HexCoord> Neighbors()
        {
            yield return new HexCoord(Q + 1, R);
            yield return new HexCoord(Q + 1, R - 1);
            yield return new HexCoord(Q, R - 1);
            yield return new HexCoord(Q - 1, R);
            yield return new HexCoord(Q - 1, R + 1);
            yield return new HexCoord(Q, R + 1);
        }
        public static IEnumerable<HexCoord> WithinRadius(int radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            for (int q = -radius; q <= radius; q++)
                for (int r = Math.Max(-radius, -q - radius); r <= Math.Min(radius, -q + radius); r++)
                    yield return new HexCoord(q, r);
        }
        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => unchecked(Q * 397 ^ R);
        public override string ToString() => $"({Q}, {R})";
    }
}
