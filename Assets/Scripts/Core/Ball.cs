namespace RetroRally.Core
{
    /// <summary>A ball in flight. Velocity is in units per second.</summary>
    public sealed class Ball
    {
        public Vec2 Position;
        public Vec2 Velocity;
        public float Radius = Tuning.BallRadius;

        /// <summary>Paddle hits since the serve; each one speeds the ball up.</summary>
        public int Hits;

        /// <summary>Who touched it last (the server until the first return).</summary>
        public Side LastHitter;

        public float Speed => Velocity.Length;
    }
}
