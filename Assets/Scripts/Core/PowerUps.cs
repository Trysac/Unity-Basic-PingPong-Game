namespace RetroRally.Core
{
    public enum PowerUpKind
    {
        /// <summary>The collector's paddle grows for a while.</summary>
        Grow = 0,
        /// <summary>The opponent's paddle shrinks for a while.</summary>
        Shrink = 1,
        /// <summary>The ball splits into three.</summary>
        MultiBall = 2,
    }

    /// <summary>A power-up waiting on the court; a ball that passes through it collects it.</summary>
    public sealed class Pickup
    {
        public PowerUpKind Kind;
        public Vec2 Position;
        public float Age;
    }

    /// <summary>A timed paddle size change.</summary>
    public sealed class PaddleEffect
    {
        public PowerUpKind Kind;
        public Side Target;
        public float Remaining;

        /// <summary>The paddle size factor this effect applies.</summary>
        public float Scale => Kind == PowerUpKind.Grow ? PowerUpTuning.GrowScale : PowerUpTuning.ShrinkScale;
    }

    public static class PowerUpTuning
    {
        public const float PickupRadius = 0.45f;
        /// <summary>Pickups appear between these X and Y limits, away from the paddles.</summary>
        public const float SpawnHalfWidth = 3.6f;
        public const float SpawnHalfHeight = 3f;
        public const float FirstSpawnDelay = 4f;
        public const float MinSpawnInterval = 5f;
        public const float MaxSpawnInterval = 9f;
        /// <summary>An uncollected pickup fades away after this long.</summary>
        public const float PickupLifetime = 9f;
        public const float GrowScale = 1.5f;
        public const float ShrinkScale = 0.6f;
        public const float EffectDuration = 9f;
        /// <summary>Angle step between the original ball and each of the two copies (radians).</summary>
        public const float MultiBallSpread = 0.4f;
        public const int MaxBalls = 3;

        public static string Title(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Grow: return "BIG PADDLE";
                case PowerUpKind.Shrink: return "SHRINK RAY";
                default: return "MULTI-BALL";
            }
        }
    }
}
