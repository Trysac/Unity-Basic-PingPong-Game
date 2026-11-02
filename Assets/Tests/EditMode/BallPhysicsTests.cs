using System;
using NUnit.Framework;

namespace RetroRally.Core.Tests
{
    public class BallPhysicsTests
    {
        const float Epsilon = 1e-3f;

        static Ball BallAt(float x, float y, float vx, float vy) =>
            new Ball { Position = new Vec2(x, y), Velocity = new Vec2(vx, vy) };

        /// <summary>Runs <paramref name="seconds"/> of flight in equal frames, stopping at a goal.</summary>
        static StepResult Fly(Ball ball, Paddle left, Paddle right, float seconds, int frames)
        {
            var last = new StepResult();
            for (int i = 0; i < frames; i++)
            {
                last = BallPhysics.Step(ball, left, right, seconds / frames);
                if (last.Conceded.HasValue)
                {
                    break;
                }
            }
            return last;
        }

        [Test]
        public void EveryReturnSpeedsTheBallUpUntilTheCap()
        {
            Assert.That(BallPhysics.SpeedAfterHits(0), Is.EqualTo(Tuning.ServeSpeed));
            Assert.That(BallPhysics.SpeedAfterHits(2), Is.EqualTo(Tuning.ServeSpeed + 2 * Tuning.SpeedPerHit).Within(Epsilon));
            Assert.That(BallPhysics.SpeedAfterHits(1000), Is.EqualTo(Tuning.MaxBallSpeed));
        }

        [Test]
        public void TheCentreOfThePaddleReturnsTheBallStraight()
        {
            var left = new Paddle(Side.Left);
            Vec2 velocity = BallPhysics.Rebound(left, left.Y, Tuning.BallRadius, 1, out float offset);
            Assert.That(offset, Is.EqualTo(0f));
            Assert.That(velocity.X, Is.EqualTo(BallPhysics.SpeedAfterHits(1)).Within(Epsilon));
            Assert.That(velocity.Y, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void TheTipsOfThePaddleSendTheBallAtTheSteepestAngleAwayFromIt()
        {
            var right = new Paddle(Side.Right);
            right.Place(1f);
            Vec2 up = BallPhysics.Rebound(right, 1f + right.HalfHeight + Tuning.BallRadius, Tuning.BallRadius, 3, out float top);
            Vec2 down = BallPhysics.Rebound(right, -5f, Tuning.BallRadius, 3, out float bottom);

            Assert.That(top, Is.EqualTo(1f));
            Assert.That(bottom, Is.EqualTo(-1f), "offsets are clamped to the paddle");
            Assert.That(up.X, Is.LessThan(0f), "the right paddle hits towards -X");
            Assert.That(MathF.Atan2(up.Y, -up.X), Is.EqualTo(Tuning.MaxBounceAngle).Within(Epsilon));
            Assert.That(MathF.Atan2(down.Y, -down.X), Is.EqualTo(-Tuning.MaxBounceAngle).Within(Epsilon));
            Assert.That(up.Length, Is.EqualTo(BallPhysics.SpeedAfterHits(3)).Within(Epsilon));
        }

        [Test]
        public void FlightDoesNotDependOnTheFrameRate()
        {
            var left = new Paddle(Side.Left);
            var right = new Paddle(Side.Right);
            right.Place(-3f); // out of the ball's way
            Ball slow = BallAt(-2f, 0f, 6f, 5f);
            Ball fast = BallAt(-2f, 0f, 6f, 5f);

            Fly(slow, left, right, 1f, 30);
            Fly(fast, left, right, 1f, 144);

            Assert.That(fast.Position.X, Is.EqualTo(slow.Position.X).Within(Epsilon));
            Assert.That(fast.Position.Y, Is.EqualTo(slow.Position.Y).Within(Epsilon));
            Assert.That(fast.Speed, Is.EqualTo(slow.Speed).Within(Epsilon));
        }

        [Test]
        public void WallsReflectTheBallAndReportTheBounce()
        {
            var left = new Paddle(Side.Left);
            var right = new Paddle(Side.Right);
            float wall = Tuning.CourtHalfHeight - Tuning.BallRadius;
            Ball ball = BallAt(0f, wall - 0.1f, 1f, 2f);

            StepResult result = BallPhysics.Step(ball, left, right, 0.1f);

            Assert.That(result.Wall, Is.True);
            Assert.That(ball.Velocity.Y, Is.EqualTo(-2f));
            Assert.That(ball.Position.Y, Is.EqualTo(wall - 0.1f).Within(Epsilon), "0.1 up to the wall and 0.1 back down");
            Assert.That(result.Paddle, Is.Null);
            Assert.That(result.Conceded, Is.Null);
        }

        [Test]
        public void APaddleHitReturnsTheBallCountsItAndReportsWhereItLanded()
        {
            var left = new Paddle(Side.Left);
            var right = new Paddle(Side.Right);
            left.Place(0.5f);
            Ball ball = BallAt(-5f, 0.9f, -8f, 0f);

            StepResult result = Fly(ball, left, right, 0.5f, 5);

            Assert.That(ball.Hits, Is.EqualTo(1));
            Assert.That(ball.LastHitter, Is.EqualTo(Side.Left));
            Assert.That(ball.Velocity.X, Is.GreaterThan(0f));
            Assert.That(ball.Velocity.Y, Is.GreaterThan(0f), "hit above the centre, so it leaves upwards");
            Assert.That(ball.Speed, Is.EqualTo(BallPhysics.SpeedAfterHits(1)).Within(Epsilon));
            Assert.That(ball.Position.X, Is.GreaterThan(left.Face));
        }

        [Test]
        public void TheHitIsReportedInTheStepItHappens()
        {
            var left = new Paddle(Side.Left);
            var right = new Paddle(Side.Right);
            Ball ball = BallAt(right.Face - 0.3f, 0f, 8f, 0f);

            StepResult result = BallPhysics.Step(ball, left, right, 0.05f);

            Assert.That(result.Paddle, Is.EqualTo(Side.Right));
            Assert.That(result.HitOffset, Is.EqualTo(0f).Within(Epsilon));
        }

        [Test]
        public void AFullSpeedBallInALongFrameCannotTunnelThroughAPaddle()
        {
            var left = new Paddle(Side.Left);
            var right = new Paddle(Side.Right);
            Ball ball = BallAt(5f, 0f, Tuning.MaxBallSpeed, 0f);

            StepResult result = BallPhysics.Step(ball, left, right, 0.25f);

            Assert.That(result.Paddle, Is.EqualTo(Side.Right));
            Assert.That(result.Conceded, Is.Null);
            Assert.That(ball.Velocity.X, Is.LessThan(0f));
        }

        [Test]
        public void ABallThatMissesThePaddleIsAGoalForTheOtherSide()
        {
            var left = new Paddle(Side.Left);
            var right = new Paddle(Side.Right);
            left.Place(3f);
            Ball ball = BallAt(-5f, -2f, -10f, 0f);

            StepResult result = Fly(ball, left, right, 2f, 60);

            Assert.That(result.Conceded, Is.EqualTo(Side.Left));
            Assert.That(ball.Hits, Is.EqualTo(0));
        }

        [Test]
        public void AServeLeavesFromTheServersPaddleTowardsTheOtherGoal()
        {
            var right = new Paddle(Side.Right);
            right.Place(1.5f);
            var ball = new Ball { Hits = 7, LastHitter = Side.Left };

            BallPhysics.Serve(ball, right, 5f);

            Assert.That(ball.Position.Y, Is.EqualTo(1.5f));
            Assert.That(ball.Position.X, Is.LessThan(right.Face));
            Assert.That(ball.Velocity.X, Is.LessThan(0f));
            Assert.That(ball.Speed, Is.EqualTo(Tuning.ServeSpeed).Within(Epsilon));
            Assert.That(MathF.Atan2(ball.Velocity.Y, -ball.Velocity.X), Is.EqualTo(Tuning.MaxServeAngle).Within(Epsilon), "aim is clamped");
            Assert.That(ball.Hits, Is.EqualTo(0));
            Assert.That(ball.LastHitter, Is.EqualTo(Side.Right));
        }

        [TestCase(0f, 0f)]
        [TestCase(1f, 1f)]
        [TestCase(2.5f, 1.5f)]
        [TestCase(-2.5f, -1.5f)]
        [TestCase(4.5f, -0.5f)]
        [TestCase(8f, 0f)]
        public void FoldBouncesAHeightBackBetweenTheWalls(float y, float expected)
        {
            Assert.That(BallPhysics.Fold(y, 2f), Is.EqualTo(expected).Within(Epsilon));
        }

        [Test]
        public void ThePredictionMatchesTheFlightOffTheWalls()
        {
            var left = new Paddle(Side.Left);
            var right = new Paddle(Side.Right);
            Ball ball = BallAt(-6f, 1f, 9f, 11f);
            Assert.That(BallPhysics.TryPredict(ball, 6f, out float predicted, out float time), Is.True);

            float flown = 0f;
            while (ball.Position.X < 6f && flown < 5f)
            {
                BallPhysics.Step(ball, left, right, 1f / 240f);
                flown += 1f / 240f;
            }

            Assert.That(time, Is.EqualTo(12f / 9f).Within(Epsilon));
            Assert.That(ball.Position.Y, Is.EqualTo(predicted).Within(0.06f));
        }

        [Test]
        public void ThereIsNoPredictionForABallGoingTheOtherWay()
        {
            Ball ball = BallAt(0f, 0f, -5f, 1f);
            Assert.That(BallPhysics.TryPredict(ball, 6f, out _, out _), Is.False);
        }
    }
}
