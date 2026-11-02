namespace RetroRally.Core
{
    /// <summary>
    /// A tiny seeded xorshift generator: the same seed gives the same sequence on every platform,
    /// which keeps the CPU and the tests deterministic.
    /// </summary>
    public sealed class Rng
    {
        uint state;

        public Rng(int seed) => Reseed(seed);

        public void Reseed(int seed) => state = (uint)seed * 2654435761u ^ 0x9E3779B9u | 1u;

        public uint NextUInt()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        /// <summary>A float in [0, 1).</summary>
        public float Value() => (NextUInt() >> 8) / 16777216f;

        /// <summary>A float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * Value();

        /// <summary>An int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive) => maxExclusive <= min ? min : min + (int)(NextUInt() % (uint)(maxExclusive - min));
    }
}
