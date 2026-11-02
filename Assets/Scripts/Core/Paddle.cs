using System;

namespace RetroRally.Core
{
    /// <summary>A paddle that slides along its goal line inside the court.</summary>
    public sealed class Paddle
    {
        public Paddle(Side side, float halfHeight = Tuning.PaddleHalfHeight)
        {
            Side = side;
            X = side == Side.Left ? -Tuning.PaddleX : Tuning.PaddleX;
            HalfHeight = halfHeight;
        }

        public Side Side { get; }
        public float X { get; }
        public float Y { get; private set; }
        public float HalfHeight { get; set; }
        public float HalfWidth => Tuning.PaddleHalfWidth;

        /// <summary>Vertical speed of the last move, in units per second (used to put spin on serves).</summary>
        public float Velocity { get; private set; }

        /// <summary>The X of the face that meets the ball.</summary>
        public float Face => X + Side.Facing() * HalfWidth;

        /// <summary>
        /// Moves by <paramref name="input"/> (-1 down .. +1 up) at <paramref name="speed"/> units per
        /// second, staying inside the walls.
        /// </summary>
        public void Move(float input, float dt, float speed = Tuning.PaddleSpeed)
        {
            input = Math.Clamp(input, -1f, 1f);
            float before = Y;
            Place(Y + input * speed * dt);
            Velocity = dt > 0f ? (Y - before) / dt : 0f;
        }

        /// <summary>Puts the paddle at <paramref name="y"/>, clamped so it never leaves the court.</summary>
        public void Place(float y)
        {
            float limit = Math.Max(Tuning.CourtHalfHeight - HalfHeight, 0f);
            Y = Math.Clamp(y, -limit, limit);
        }
    }
}
