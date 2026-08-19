using System;
using UnityEngine;

namespace Rollback
{
    [Serializable]
    public struct FixVec2 : IEquatable<FixVec2>
    {
        public Fix64 x;
        public Fix64 y;

        public FixVec2(Fix64 x, Fix64 y)  { this.x = x; this.y = y; }
        public FixVec2(float x, float y)   { this.x = Fix64.FromFloat(x); this.y = Fix64.FromFloat(y); }
        public FixVec2(int   x, int   y)   { this.x = Fix64.FromInt(x);   this.y = Fix64.FromInt(y); }

        public static readonly FixVec2 Zero  = new FixVec2(Fix64.Zero,   Fix64.Zero);
        public static readonly FixVec2 Right = new FixVec2(Fix64.One,    Fix64.Zero);
        public static readonly FixVec2 Left  = new FixVec2(Fix64.NegOne, Fix64.Zero);
        public static readonly FixVec2 Up    = new FixVec2(Fix64.Zero,   Fix64.One);
        public static readonly FixVec2 Down  = new FixVec2(Fix64.Zero,   Fix64.NegOne);

        public static FixVec2 operator +(FixVec2 a, FixVec2 b)  => new FixVec2(a.x + b.x, a.y + b.y);
        public static FixVec2 operator -(FixVec2 a, FixVec2 b)  => new FixVec2(a.x - b.x, a.y - b.y);
        public static FixVec2 operator -(FixVec2 a)              => new FixVec2(-a.x, -a.y);
        public static FixVec2 operator *(FixVec2 v, Fix64 s)     => new FixVec2(v.x * s, v.y * s);
        public static FixVec2 operator *(Fix64 s,   FixVec2 v)   => new FixVec2(v.x * s, v.y * s);
        public static bool    operator ==(FixVec2 a, FixVec2 b)  => a.x == b.x && a.y == b.y;
        public static bool    operator !=(FixVec2 a, FixVec2 b)  => !(a == b);

        public Fix64 SqrMagnitude => x * x + y * y;
        public Fix64 Magnitude    => Fix64.Sqrt(SqrMagnitude);

        public FixVec2 Normalized
        {
            get { Fix64 m = Magnitude; return m.Raw == 0 ? Zero : new FixVec2(x / m, y / m); }
        }

        public static Fix64  Dot(FixVec2 a, FixVec2 b)        => a.x * b.x + a.y * b.y;
        public static FixVec2 Reflect(FixVec2 dir, FixVec2 n)  => dir - Fix64.Two * Dot(dir, n) * n;

        public Vector2 ToVector2()                       => new Vector2(x.ToFloat(), y.ToFloat());
        public static FixVec2 FromVector2(Vector2 v)     => new FixVec2(v.x, v.y);

        public bool Equals(FixVec2 other)        => x == other.x && y == other.y;
        public override bool Equals(object obj)  => obj is FixVec2 v && Equals(v);
        public override int  GetHashCode()       => x.GetHashCode() ^ (y.GetHashCode() << 16);
        public override string ToString()        => $"({x}, {y})";
    }
}
