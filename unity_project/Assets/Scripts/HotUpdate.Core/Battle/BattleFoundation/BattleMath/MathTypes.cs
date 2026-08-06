using System;

namespace BattleFoundation
{
    /// <summary>
    /// 纯逻辑浮点三维向量，替代 UnityEngine.Vector3，使 L0/L1/L2 可设 noEngineReferences。
    /// 表现层（L3）通过扩展方法与 UnityEngine.Vector3 互转。
    /// </summary>
    public readonly struct Float3 : IEquatable<Float3>
    {
        public readonly float x;
        public readonly float y;
        public readonly float z;

        public Float3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Float3 zero => default;
        public static Float3 one => new Float3(1f, 1f, 1f);
        public static Float3 forward => new Float3(0f, 0f, 1f);
        public static Float3 back => new Float3(0f, 0f, -1f);
        public static Float3 up => new Float3(0f, 1f, 0f);
        public static Float3 down => new Float3(0f, -1f, 0f);
        public static Float3 left => new Float3(-1f, 0f, 0f);
        public static Float3 right => new Float3(1f, 0f, 0f);

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Float3 normalized => Normalize(this);

        public static Float3 operator +(Float3 a, Float3 b) => new Float3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Float3 operator -(Float3 a, Float3 b) => new Float3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Float3 operator -(Float3 a) => new Float3(-a.x, -a.y, -a.z);
        public static Float3 operator *(Float3 a, float d) => new Float3(a.x * d, a.y * d, a.z * d);
        public static Float3 operator *(float d, Float3 a) => new Float3(a.x * d, a.y * d, a.z * d);
        public static Float3 operator /(Float3 a, float d) => new Float3(a.x / d, a.y / d, a.z / d);

        public static float Distance(Float3 a, Float3 b) => (a - b).magnitude;
        public static float SqrDistance(Float3 a, Float3 b) => (a - b).sqrMagnitude;

        public static Float3 Lerp(Float3 a, Float3 b, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return new Float3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        public static Float3 Normalize(Float3 v)
        {
            float mag = v.magnitude;
            return mag > 1e-6f ? new Float3(v.x / mag, v.y / mag, v.z / mag) : zero;
        }

        public static float Dot(Float3 a, Float3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public static Float3 MoveTowards(Float3 current, Float3 target, float maxDelta)
        {
            Float3 diff = target - current;
            float dist = diff.magnitude;
            if (dist <= maxDelta || dist < 1e-6f)
                return target;
            return current + diff / dist * maxDelta;
        }

        public static Float3 Cross(Float3 a, Float3 b) =>
            new Float3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

        public bool Equals(Float3 other) => x == other.x && y == other.y && z == other.z;
        public override bool Equals(object obj) => obj is Float3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
        public static bool operator ==(Float3 a, Float3 b) => a.Equals(b);
        public static bool operator !=(Float3 a, Float3 b) => !a.Equals(b);

        public override string ToString() => $"({x}, {y}, {z})";
    }

    /// <summary>
    /// 纯逻辑四元数，替代 UnityEngine.Quaternion。
    /// 仅作为状态存储与快照使用，不提供旋转运算（旋转运算留在 L3 表现层）。
    /// </summary>
    public readonly struct Float4 : IEquatable<Float4>
    {
        public readonly float x;
        public readonly float y;
        public readonly float z;
        public readonly float w;

        public Float4(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static Float4 identity => new Float4(0f, 0f, 0f, 1f);

        public bool Equals(Float4 other) => x == other.x && y == other.y && z == other.z && w == other.w;
        public override bool Equals(object obj) => obj is Float4 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y, z, w);
        public static bool operator ==(Float4 a, Float4 b) => a.Equals(b);
        public static bool operator !=(Float4 a, Float4 b) => !a.Equals(b);

        public override string ToString() => $"({x}, {y}, {z}, {w})";
    }

    /// <summary>
    /// 纯逻辑二维向量，替代 UnityEngine.Vector2。
    /// </summary>
    public readonly struct Float2 : IEquatable<Float2>
    {
        public readonly float x;
        public readonly float y;

        public Float2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Float2 zero => default;
        public static Float2 one => new Float2(1f, 1f);

        public float sqrMagnitude => x * x + y * y;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);

        public static Float2 operator +(Float2 a, Float2 b) => new Float2(a.x + b.x, a.y + b.y);
        public static Float2 operator -(Float2 a, Float2 b) => new Float2(a.x - b.x, a.y - b.y);
        public static Float2 operator *(Float2 a, float d) => new Float2(a.x * d, a.y * d);
        public static Float2 operator *(float d, Float2 a) => new Float2(a.x * d, a.y * d);

        public bool Equals(Float2 other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is Float2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y);
        public static bool operator ==(Float2 a, Float2 b) => a.Equals(b);
        public static bool operator !=(Float2 a, Float2 b) => !a.Equals(b);

        public override string ToString() => $"({x}, {y})";
    }

    /// <summary>
    /// 纯逻辑数学工具，替代 UnityEngine.Mathf。
    /// 仅包含战斗逻辑实际用到的方法。
    /// 类名用 BattleMathF 而非 MathF，避免与 System.MathF（.NET Core 2.1+）冲突。
    /// </summary>
    public static class BattleMathF
    {
        public const float PI = (float)Math.PI;

        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Pow(float x, float y) => (float)Math.Pow(x, y);

        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;

        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static float Clamp01(float v) => Clamp(v, 0f, 1f);

        public static float Lerp(float a, float b, float t)
        {
            t = Clamp01(t);
            return a + (b - a) * t;
        }

        /// <summary>
        /// 近似相等比较，替代 Mathf.Approximately。
        /// 使用相对 epsilon，兼容大数值与零值。
        /// </summary>
        public static bool Approximately(float a, float b)
        {
            float diff = a - b;
            if (diff < 0f) diff = -diff;
            if (diff < 1e-6f) return true;
            float mag = a > b ? a : b;
            if (mag < 0f) mag = -mag;
            return diff < mag * 1e-6f;
        }
    }
}
