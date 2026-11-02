using System;

namespace RetroRally.Core
{
    /// <summary>Every number the rules use, in world units and seconds, in one place.</summary>
    public static class Tuning
    {
        // Court: walls at +-CourtHalfHeight, goals behind +-CourtHalfWidth.
        public const float CourtHalfWidth = 8.4f;
        public const float CourtHalfHeight = 4.3f;

        // Paddles.
        public const float PaddleX = 7.4f;
        public const float PaddleHalfWidth = 0.14f;
        public const float PaddleHalfHeight = 0.8f;
        public const float PaddleSpeed = 9.5f;

        // Ball: speeds are units per second, never per frame.
        public const float BallRadius = 0.15f;
        public const float ServeSpeed = 8.5f;
        public const float SpeedPerHit = 0.45f;
        public const float MaxBallSpeed = 18f;
        /// <summary>A hit on the very tip of the paddle leaves at this angle from the horizontal.</summary>
        public const float MaxBounceAngle = 55f * MathF.PI / 180f;
        /// <summary>The steepest serve, so a serve can never go straight up and down.</summary>
        public const float MaxServeAngle = 30f * MathF.PI / 180f;
    }
}
