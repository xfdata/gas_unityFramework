
namespace BattleFoundation
{
    public class BattleRandom : IRandom
    {
        private uint _seed;
        private uint _state0;
        private uint _state1;

        private const uint Multiplier = 1664525u;
        private const uint Increment = 1013904223u;

        public BattleRandom(int seed)
        {
            SetSeed(seed);
        }

        public void SetSeed(int seed)
        {
            _seed = (uint)seed;
            _state0 = HashSeed((uint)seed);
            _state1 = HashSeed(~(uint)seed);
        }

        public int GetSeed() => (int)_seed;

        private static uint HashSeed(uint x)
        {
            x = ((x >> 16) ^ x) * 0x45d9f3b;
            x = ((x >> 16) ^ x) * 0x45d9f3b;
            x = (x >> 16) ^ x;
            return x != 0 ? x : 1u;
        }

        private uint NextUInt()
        {
            uint t = _state0;
            uint s = _state1;
            _state0 = s;
            t ^= t << 23;
            t ^= t >> 17;
            t ^= s ^ (s >> 26);
            _state1 = t;
            return t + s;
        }

        private float NextFloat01()
        {
            return (NextUInt() & 0x007FFFFF) * (1f / 8388608f);
        }

        public float Range(float min, float max)
        {
            return min + NextFloat01() * (max - min);
        }

        public int Range(int min, int max)
        {
            if (min >= max) return min;
            uint range = (uint)(max - min);
            return min + (int)(NextUInt() % range);
        }

        public int Range(int max)
        {
            return Range(0, max);
        }

        public float Value => NextFloat01();

        public Float2 InsideUnitCircle()
        {
            float angle = Range(0f, BattleMathF.PI * 2f);
            float radius = BattleMathF.Sqrt(Value);
            return new Float2(BattleMathF.Cos(angle), BattleMathF.Sin(angle)) * radius;
        }

        public Float3 InsideUnitSphere()
        {
            float z = Range(-1f, 1f);
            float angle = Range(0f, BattleMathF.PI * 2f);
            float radius = BattleMathF.Sqrt(1f - z * z);
            return new Float3(radius * BattleMathF.Cos(angle), z, radius * BattleMathF.Sin(angle)) * BattleMathF.Pow(Value, 1f / 3f);
        }

        // GAS 侧 API：转发到现有 BF 侧实现，保证同一随机流
        public int Next(int maxValue) => Range(maxValue);
        public int Next(int minValue, int maxValue) => Range(minValue, maxValue);
        public double NextDouble() => Value;
    }
}
