using System;

namespace RetroRally.Core
{
    /// <summary>What happened to a ball during one <see cref="BallPhysics.Step"/>.</summary>
    public struct StepResult
    {
        /// <summary>The ball bounced off the top or bottom wall.</summary>
        public bool Wall;

        /// <summary>The paddle that returned the ball, if any.</summary>
        public Side? Paddle;

        /// <summary>Where it met that paddle: -1 bottom tip, 0 centre, +1 top tip.</summary>
        public float HitOffset;

        /// <summary>The side whose goal the ball went into, if any (the other side scores).</summary>
        public Side? Conceded;
    }

    /// <summary>
    /// Ball flight, wall and paddle bounces and goals. Everything is in units per second and the
    /// step is split into sub-steps shorter than the ball, so a fast ball cannot tunnel through a
    /// paddle and the result does not depend on the frame rate.
    /// </summary>
    public static class BallPhysics
    {
        const int MaxSubSteps = 64;

        /// <summary>The ball speed after <paramref name="hits"/> paddle returns.</summary>
        public static float SpeedAfterHits(int hits) =>
            Math.Min(Tuning.ServeSpeed + Math.Max(hits, 0) * Tuning.SpeedPerHit, Tuning.MaxBallSpeed);

        /// <summary>
        /// The velocity of a ball leaving <paramref name="paddle"/> after meeting it at height
        /// <paramref name="ballY"/>: the further from the centre, the steeper the angle.
        /// </summary>
        public static Vec2 Rebound(Paddle paddle, float ballY, float ballRadius, int hitsAfter, out float offset) =>
            Rebound(paddle.Side, paddle.Y, paddle.HalfHeight, ballY, ballRadius, hitsAfter, out offset);

        /// <inheritdoc cref="Rebound(Paddle, float, float, int, out float)"/>
        public static Vec2 Rebound(Side side, float paddleY, float paddleHalfHeight, float ballY, float ballRadius,
            int hitsAfter, out float offset)
        {
            offset = Math.Clamp((ballY - paddleY) / (paddleHalfHeight + ballRadius), -1f, 1f);
            float angle = offset * Tuning.MaxBounceAngle;
            float speed = SpeedAfterHits(hitsAfter);
            return new Vec2(MathF.Cos(angle) * side.Facing(), MathF.Sin(angle)) * speed;
        }

        /// <summary>
        /// Puts the ball in front of <paramref name="server"/> and sends it towards the other goal.
        /// <paramref name="aim"/> (-1..1) tilts the serve down or up.
        /// </summary>
        public static void Serve(Ball ball, Paddle server, float aim)
        {
            float facing = server.Side.Facing();
            float angle = Math.Clamp(aim, -1f, 1f) * Tuning.MaxServeAngle;
            ball.Position = HeldPosition(ball, server);
            ball.Velocity = new Vec2(MathF.Cos(angle) * facing, MathF.Sin(angle)) * Tuning.ServeSpeed;
            ball.Hits = 0;
            ball.LastHitter = server.Side;
        }

        /// <summary>Where a ball waiting to be served sits: just in front of the server's paddle.</summary>
        public static Vec2 HeldPosition(Ball ball, Paddle server) =>
            new Vec2(server.Face + server.Side.Facing() * (ball.Radius + 0.06f), server.Y);

        /// <summary>Moves the ball by <paramref name="dt"/> seconds and resolves walls, paddles and goals.</summary>
        public static StepResult Step(Ball ball, Paddle left, Paddle right, float dt)
        {
            var result = new StepResult();
            if (dt <= 0f)
            {
                return result;
            }
            float travel = ball.Speed * dt;
            int steps = Math.Clamp((int)MathF.Ceiling(travel / (ball.Radius * 0.75f)), 1, MaxSubSteps);
            float h = dt / steps;
            float wall = Tuning.CourtHalfHeight - ball.Radius;
            for (int i = 0; i < steps; i++)
            {
                Vec2 from = ball.Position;
                Vec2 to = from + ball.Velocity * h;

                if (to.Y > wall)
                {
                    to = new Vec2(to.X, 2f * wall - to.Y);
                    ball.Velocity = new Vec2(ball.Velocity.X, -MathF.Abs(ball.Velocity.Y));
                    result.Wall = true;
                }
                else if (to.Y < -wall)
                {
                    to = new Vec2(to.X, -2f * wall - to.Y);
                    ball.Velocity = new Vec2(ball.Velocity.X, MathF.Abs(ball.Velocity.Y));
                    result.Wall = true;
                }

                if (TryPaddle(ball, left, from, to, ref result) || TryPaddle(ball, right, from, to, ref result))
                {
                    continue;
                }
                ball.Position = to;

                if (to.X < -Tuning.CourtHalfWidth - ball.Radius)
                {
                    result.Conceded = Side.Left;
                    return result;
                }
                if (to.X > Tuning.CourtHalfWidth + ball.Radius)
                {
                    result.Conceded = Side.Right;
                    return result;
                }
            }
            return result;
        }

        static bool TryPaddle(Ball ball, Paddle paddle, Vec2 from, Vec2 to, ref StepResult result)
        {
            float facing = paddle.Side.Facing();
            if (ball.Velocity.X * facing >= 0f)
            {
                return false; // moving away from this paddle
            }
            float contact = paddle.Face + facing * ball.Radius;
            // In front of the face before the sub-step, level with it or past it after.
            float before = (from.X - contact) * facing;
            float after = (to.X - contact) * facing;
            if (before < 0f || after > 0f)
            {
                return false;
            }
            float t = before - after > 1e-6f ? before / (before - after) : 0f;
            float y = from.Y + (to.Y - from.Y) * t;
            if (MathF.Abs(y - paddle.Y) > paddle.HalfHeight + ball.Radius)
            {
                return false;
            }
            ball.Hits++;
            ball.LastHitter = paddle.Side;
            ball.Position = new Vec2(contact, y);
            ball.Velocity = Rebound(paddle, y, ball.Radius, ball.Hits, out float offset);
            result.Paddle = paddle.Side;
            result.HitOffset = offset;
            return true;
        }

        /// <summary>
        /// Where the ball will cross <paramref name="x"/> if nothing touches it, folding its path
        /// off the walls, and how long that takes. False when it is not heading there.
        /// </summary>
        public static bool TryPredict(Ball ball, float x, out float y, out float time)
        {
            y = ball.Position.Y;
            time = 0f;
            if (MathF.Abs(ball.Velocity.X) < 1e-4f)
            {
                return false;
            }
            time = (x - ball.Position.X) / ball.Velocity.X;
            if (time < 0f)
            {
                return false;
            }
            y = Fold(ball.Position.Y + ball.Velocity.Y * time, Tuning.CourtHalfHeight - ball.Radius);
            return true;
        }

        /// <summary>Reflects an unbounded height back between -<paramref name="limit"/> and +<paramref name="limit"/>.</summary>
        public static float Fold(float y, float limit)
        {
            if (limit <= 0f)
            {
                return 0f;
            }
            float period = 4f * limit;
            float shifted = (y + limit) % period;
            if (shifted < 0f)
            {
                shifted += period;
            }
            return (shifted > 2f * limit ? period - shifted : shifted) - limit;
        }
    }
}
