using System;

namespace Rollback
{
    /// <summary>
    /// Q16.16 signed fixed-point number. Range ±32767.99999. Precision 1/65536 ≈ 0.000015.
    /// All arithmetic is integer-only — bit-identical on every platform and CPU.
    /// Currently used as groundwork; the full Controller2D migration to Fix64 is a
    /// follow-up task. Once migrated, the simulation will be deterministic cross-platform.
    /// </summary>
    [Serializable]
    public struct Fix64 : IEquatable<Fix64>, IComparable<Fix64>
    {
        public const int FractionalBits = 16;
        public const int RawOne = 1 << FractionalBits; // 65536

        public int Raw;

        Fix64(int raw) => Raw = raw;

        // ── Construction ─────────────────────────────────────────────────
        public static Fix64 FromRaw(int raw)      => new Fix64(raw);
        public static Fix64 FromInt(int value)     => new Fix64(value << FractionalBits);
        public static Fix64 FromFloat(float value) => new Fix64((int)(value * RawOne));

        // ── Conversion ───────────────────────────────────────────────────
        public int   ToInt()   => Raw >> FractionalBits;
        public float ToFloat() => (float)Raw / RawOne;

        // ── Constants ────────────────────────────────────────────────────
        public static readonly Fix64 Zero   = new Fix64(0);
        public static readonly Fix64 One    = new Fix64(RawOne);
        public static readonly Fix64 NegOne = new Fix64(-RawOne);
        public static readonly Fix64 Half   = new Fix64(RawOne >> 1);
        public static readonly Fix64 Two    = FromInt(2);
        public static readonly Fix64 MaxVal = new Fix64(int.MaxValue);
        public static readonly Fix64 MinVal = new Fix64(int.MinValue);

        // Pre-baked small constants used by SmoothDamp
        public static readonly Fix64 F0_48  = FromFloat(0.48f);
        public static readonly Fix64 F0_235 = FromFloat(0.235f);

        // ── Arithmetic ───────────────────────────────────────────────────
        public static Fix64 operator +(Fix64 a, Fix64 b) => new Fix64(a.Raw + b.Raw);
        public static Fix64 operator -(Fix64 a, Fix64 b) => new Fix64(a.Raw - b.Raw);
        public static Fix64 operator -(Fix64 a)          => new Fix64(-a.Raw);

        public static Fix64 operator *(Fix64 a, Fix64 b)
        {
            long p = (long)a.Raw * b.Raw;
            return new Fix64((int)(p >> FractionalBits));
        }

        public static Fix64 operator /(Fix64 a, Fix64 b)
        {
            long n = (long)a.Raw << FractionalBits;
            return new Fix64((int)(n / b.Raw));
        }

        // ── Comparison ───────────────────────────────────────────────────
        public static bool operator ==(Fix64 a, Fix64 b) => a.Raw == b.Raw;
        public static bool operator !=(Fix64 a, Fix64 b) => a.Raw != b.Raw;
        public static bool operator  <(Fix64 a, Fix64 b) => a.Raw  < b.Raw;
        public static bool operator  >(Fix64 a, Fix64 b) => a.Raw  > b.Raw;
        public static bool operator <=(Fix64 a, Fix64 b) => a.Raw <= b.Raw;
        public static bool operator >=(Fix64 a, Fix64 b) => a.Raw >= b.Raw;

        // ── Casts ────────────────────────────────────────────────────────
        public static explicit operator float(Fix64 f) => f.ToFloat();
        public static explicit operator Fix64(float f) => FromFloat(f);
        public static explicit operator Fix64(int   i) => FromInt(i);

        // ── Math helpers ─────────────────────────────────────────────────
        public static Fix64 Abs(Fix64 v)  => new Fix64(v.Raw < 0 ? -v.Raw : v.Raw);
        public static Fix64 Min(Fix64 a, Fix64 b) => a.Raw < b.Raw ? a : b;
        public static Fix64 Max(Fix64 a, Fix64 b) => a.Raw > b.Raw ? a : b;
        public static Fix64 Sign(Fix64 v) => v.Raw > 0 ? One : v.Raw < 0 ? NegOne : Zero;

        public static Fix64 Clamp(Fix64 v, Fix64 min, Fix64 max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static Fix64 Sqrt(Fix64 v)
        {
            if (v.Raw < 0) throw new ArgumentException("Sqrt of negative Fix64");
            if (v.Raw == 0) return Zero;
            long n = (long)v.Raw << FractionalBits;
            return new Fix64((int)Math.Sqrt(n));
        }

        /// <summary>
        /// Critically-damped spring SmoothDamp for a fixed timestep.
        /// Deterministic equivalent of Mathf.SmoothDamp.
        /// </summary>
        public static Fix64 SmoothDamp(Fix64 current, Fix64 target,
                                        ref Fix64 velocity, Fix64 smoothTime,
                                        Fix64 maxSpeed, Fix64 dt)
        {
            smoothTime = Max(FromFloat(0.0001f), smoothTime);
            Fix64 omega = Two / smoothTime;
            Fix64 x     = omega * dt;
            Fix64 exp   = One / (One + x + F0_48 * x * x + F0_235 * x * x * x);
            Fix64 change = current - target;
            Fix64 maxChange = maxSpeed * smoothTime;
            change = Clamp(change, -maxChange, maxChange);
            Fix64 adjustedTarget = current - change;
            Fix64 temp   = (velocity + omega * change) * dt;
            velocity     = (velocity - omega * temp) * exp;
            Fix64 result = adjustedTarget + (change + temp) * exp;
            if ((target - current).Raw > 0 && result > target) { result = target; velocity = Zero; }
            if ((target - current).Raw < 0 && result < target) { result = target; velocity = Zero; }
            return result;
        }

        public bool Equals(Fix64 other)         => Raw == other.Raw;
        public override bool Equals(object obj) => obj is Fix64 f && Equals(f);
        public override int  GetHashCode()      => Raw;
        public int CompareTo(Fix64 other)        => Raw.CompareTo(other.Raw);
        public override string ToString()        => ToFloat().ToString("F4");
    }
}
