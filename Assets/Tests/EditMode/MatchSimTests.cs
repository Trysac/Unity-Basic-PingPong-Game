using System.Linq;
using NUnit.Framework;

namespace RetroRally.Core.Tests
{
    public class MatchSimTests
    {
        const float Frame = 1f / 60f;

        static MatchSim Versus(int points = 5, GameMode mode = GameMode.Classic) =>
            new MatchSim(new MatchSettings { Opponent = Opponent.Human, PointsToWin = points, Mode = mode }, 1);

        static void Run(MatchSim sim, float seconds, float left = 0f, float right = 0f)
        {
            for (float t = 0f; t < seconds; t += Frame)
            {
                sim.Step(Frame, left, right);
            }
        }

        /// <summary>Serves, then sends the ball straight into <paramref name="conceding"/>'s goal, far from its paddle.</summary>
        static void ConcedeOnce(MatchSim sim, Side conceding)
        {
            sim.ServeNow();
            Ball ball = sim.Balls[0];
            Paddle keeper = sim.PaddleOf(conceding);
            keeper.Place(3f);
            ball.Position = new Vec2(conceding == Side.Left ? -5f : 5f, -3f);
            ball.Velocity = new Vec2(conceding == Side.Left ? -12f : 12f, 0f);
            for (int i = 0; i < 120 && sim.Phase == RallyPhase.Playing; i++)
            {
                sim.Step(Frame, 0f, 0f);
                if (sim.Phase == RallyPhase.Playing)
                {
                    keeper.Place(3f);
                }
            }
        }

        [Test]
        public void ANewMatchWaitsForPlayerOneToServeWithTheBallOnTheirPaddle()
        {
            MatchSim sim = Versus();
            Assert.That(sim.Phase, Is.EqualTo(RallyPhase.Serving));
            Assert.That(sim.Server, Is.EqualTo(Side.Left));
            Assert.That(sim.Score.Of(Side.Left) + sim.Score.Of(Side.Right), Is.EqualTo(0));

            sim.Step(0.2f, 1f, 0f);
            Assert.That(sim.Phase, Is.EqualTo(RallyPhase.Serving));
            Assert.That(sim.Balls[0].Position.Y, Is.EqualTo(sim.Left.Y), "the held ball follows the paddle");
            Assert.That(sim.Balls[0].Position.X, Is.GreaterThan(sim.Left.Face));
        }

        [Test]
        public void TheServeButtonLaunchesTheBallTowardsTheOpponent()
        {
            MatchSim sim = Versus();
            sim.Step(Frame, 0f, 0f, leftServe: true);

            Assert.That(sim.Phase, Is.EqualTo(RallyPhase.Playing));
            Assert.That(sim.Events.Single().Kind, Is.EqualTo(MatchEventKind.Serve));
            Assert.That(sim.Events.Single().Side, Is.EqualTo(Side.Left));
            Assert.That(sim.Balls[0].Velocity.X, Is.GreaterThan(0f));
            Assert.That(sim.Balls[0].Speed, Is.EqualTo(Tuning.ServeSpeed).Within(1e-3f));
        }

        [Test]
        public void TheOtherPlayersServeButtonDoesNothing()
        {
            MatchSim sim = Versus();
            sim.Step(Frame, 0f, 0f, rightServe: true);
            Assert.That(sim.Phase, Is.EqualTo(RallyPhase.Serving));
        }

        [Test]
        public void MovingWhileServingAnglesTheServe()
        {
            MatchSim sim = Versus();
            sim.Step(Frame, 1f, 0f, leftServe: true);
            Assert.That(sim.Balls[0].Velocity.Y, Is.GreaterThan(0f));
        }

        [Test]
        public void AServerWhoWaitsTooLongIsServedFor()
        {
            MatchSim sim = Versus();
            Run(sim, MatchSim.AutoServeTime - 0.1f);
            Assert.That(sim.Phase, Is.EqualTo(RallyPhase.Serving));
            Run(sim, 0.2f);
            Assert.That(sim.Phase, Is.EqualTo(RallyPhase.Playing));
        }

        [Test]
        public void AGoalScoresForTheOtherSideAndTheSideThatConcededServesNext()
        {
            MatchSim sim = Versus();
            ConcedeOnce(sim, Side.Right);

            Assert.That(sim.Score.Of(Side.Left), Is.EqualTo(1));
            Assert.That(sim.Score.Of(Side.Right), Is.EqualTo(0));
            Assert.That(sim.LastScorer, Is.EqualTo(Side.Left));
            Assert.That(sim.Phase, Is.EqualTo(RallyPhase.PointScored));
            Assert.That(sim.Events.Any(e => e.Kind == MatchEventKind.Goal && e.Side == Side.Left), Is.True);

            Run(sim, MatchSim.PointPause + Frame);
            Assert.That(sim.Phase, Is.EqualTo(RallyPhase.Serving));
            Assert.That(sim.Server, Is.EqualTo(Side.Right));
            Assert.That(sim.Balls[0].Position.X, Is.LessThan(sim.Right.Face), "held on the right paddle");
        }

        [Test]
        public void TheWinningPointEndsTheMatchAndNothingMovesAfterIt()
        {
            MatchSim sim = Versus(points: 3);
            for (int i = 0; i < 3; i++)
            {
                ConcedeOnce(sim, Side.Left);
                Run(sim, MatchSim.PointPause + Frame);
            }

            Assert.That(sim.Phase, Is.EqualTo(RallyPhase.MatchOver));
            Assert.That(sim.Score.Winner, Is.EqualTo(Side.Right));
            Assert.That(sim.Score.Of(Side.Right), Is.EqualTo(3));

            float y = sim.Left.Y;
            sim.Step(Frame, 1f, 1f, true, true);
            Assert.That(sim.Events, Is.Empty);
            Assert.That(sim.Left.Y, Is.EqualTo(y));
        }

        [Test]
        public void ReturnsAreCountedPerRallyAndTheLongestRallyIsKept()
        {
            MatchSim sim = Versus();
            sim.ServeNow();
            Ball ball = sim.Balls[0];
            ball.Position = new Vec2(5f, 0f);
            ball.Velocity = new Vec2(10f, 0f);
            Run(sim, 0.3f);
            Assert.That(sim.RallyHits, Is.EqualTo(1));

            ConcedeOnce(sim, Side.Left);
            Assert.That(sim.RallyHits, Is.EqualTo(0));
            Assert.That(sim.LongestRally, Is.EqualTo(1));
        }

        [Test]
        public void AgainstTheCpuTheRightInputIsIgnoredAndTheCpuServesByItself()
        {
            var sim = new MatchSim(new MatchSettings { Opponent = Opponent.Cpu, Difficulty = Difficulty.Easy }, 3);
            Assert.That(sim.Cpu, Is.Not.Null);
            Assert.That(sim.Cpu.Difficulty, Is.EqualTo(Difficulty.Easy));
            sim.Step(0.3f, 0f, 1f);
            Assert.That(sim.Right.Y, Is.EqualTo(0f).Within(1e-4f), "a human cannot steer the CPU paddle");

            ConcedeOnce(sim, Side.Right);
            Run(sim, MatchSim.PointPause + Frame);
            Assert.That(sim.Server, Is.EqualTo(Side.Right));
            Run(sim, MatchSim.CpuServeDelay + 0.05f);
            Assert.That(sim.Phase, Is.EqualTo(RallyPhase.Playing));
            Assert.That(sim.Balls[0].Velocity.X, Is.LessThan(0f));
        }

        [Test]
        public void InTwoPlayerModeBothPaddlesFollowTheirInputs()
        {
            MatchSim sim = Versus();
            sim.Step(0.1f, -1f, 1f);
            Assert.That(sim.Cpu, Is.Null);
            Assert.That(sim.Left.Y, Is.LessThan(0f));
            Assert.That(sim.Right.Y, Is.GreaterThan(0f));
        }

        [Test]
        public void ClassicMatchesNeverSpawnPowerUps()
        {
            MatchSim sim = Versus();
            sim.ServeNow();
            for (int i = 0; i < 1200; i++)
            {
                sim.Balls[0].Position = new Vec2(0f, 0f);
                sim.Step(Frame, 0f, 0f);
                Assert.That(sim.Pickup, Is.Null);
            }
        }

        [Test]
        public void ArcadeMatchesSpawnAPowerUpAfterTheFirstDelayAndItFadesIfMissed()
        {
            MatchSim sim = Versus(mode: GameMode.Arcade);
            sim.ServeNow();
            bool spawned = false;
            float t = 0f;
            for (; t < PowerUpTuning.FirstSpawnDelay + 1f && !spawned; t += Frame)
            {
                // Keep the ball out of the way, flying straight up and down at the side.
                sim.Balls[0].Position = new Vec2(6.5f, 0f);
                sim.Balls[0].Velocity = new Vec2(0.001f, 0f);
                sim.Step(Frame, 0f, 0f);
                spawned = sim.Events.Any(e => e.Kind == MatchEventKind.PickupSpawned);
            }
            Assert.That(spawned, Is.True);
            Assert.That(t, Is.EqualTo(PowerUpTuning.FirstSpawnDelay).Within(2 * Frame));
            Pickup pickup = sim.Pickup;
            Assert.That(System.Math.Abs(pickup.Position.X), Is.LessThanOrEqualTo(PowerUpTuning.SpawnHalfWidth));
            Assert.That(System.Math.Abs(pickup.Position.Y), Is.LessThanOrEqualTo(PowerUpTuning.SpawnHalfHeight));

            bool expired = false;
            for (float s = 0f; s < PowerUpTuning.PickupLifetime + 0.5f && !expired; s += Frame)
            {
                sim.Balls[0].Position = new Vec2(6.5f, 0f);
                sim.Step(Frame, 0f, 0f);
                expired = sim.Events.Any(e => e.Kind == MatchEventKind.PickupExpired);
            }
            Assert.That(expired, Is.True);
            Assert.That(sim.Pickup, Is.Null);
        }

        static MatchSim CollectWithBallFrom(Side hitter, PowerUpKind kind)
        {
            MatchSim sim = Versus(mode: GameMode.Arcade);
            sim.ServeNow();
            Ball ball = sim.Balls[0];
            ball.LastHitter = hitter;
            ball.Position = new Vec2(-1f, 1f);
            ball.Velocity = new Vec2(hitter == Side.Left ? 10f : -10f, 2f);
            sim.SpawnPickup(kind, ball.Position + ball.Velocity * 0.05f);
            sim.Step(0.1f, 0f, 0f);
            return sim;
        }

        [Test]
        public void BigPaddleGrowsTheLastHittersPaddleUntilItWearsOff()
        {
            MatchSim sim = CollectWithBallFrom(Side.Left, PowerUpKind.Grow);

            Assert.That(sim.Pickup, Is.Null);
            MatchEvent collected = sim.Events.Single(e => e.Kind == MatchEventKind.PickupCollected);
            Assert.That(collected.Side, Is.EqualTo(Side.Left));
            Assert.That(collected.PowerUp, Is.EqualTo(PowerUpKind.Grow));
            Assert.That(sim.Left.HalfHeight, Is.EqualTo(Tuning.PaddleHalfHeight * PowerUpTuning.GrowScale).Within(1e-4f));
            Assert.That(sim.Right.HalfHeight, Is.EqualTo(Tuning.PaddleHalfHeight));

            for (float t = 0f; t < PowerUpTuning.EffectDuration + 0.5f; t += Frame)
            {
                sim.Balls[0].Position = new Vec2(0f, -3.5f);
                sim.Balls[0].Velocity = new Vec2(0.001f, 0f);
                sim.Step(Frame, 0f, 0f);
                if (sim.Events.Any(e => e.Kind == MatchEventKind.EffectEnded))
                {
                    break;
                }
            }
            Assert.That(sim.Left.HalfHeight, Is.EqualTo(Tuning.PaddleHalfHeight).Within(1e-4f));
            Assert.That(sim.Effects, Is.Empty);
        }

        [Test]
        public void ShrinkRayShrinksTheOpponentsPaddle()
        {
            MatchSim sim = CollectWithBallFrom(Side.Right, PowerUpKind.Shrink);
            Assert.That(sim.Left.HalfHeight, Is.EqualTo(Tuning.PaddleHalfHeight * PowerUpTuning.ShrinkScale).Within(1e-4f));
            Assert.That(sim.Right.HalfHeight, Is.EqualTo(Tuning.PaddleHalfHeight));
            Assert.That(sim.Effects.Single().Target, Is.EqualTo(Side.Left));
        }

        [Test]
        public void MultiBallSplitsTheBallIntoThreeAndTheNextGoalEndsTheRally()
        {
            MatchSim sim = CollectWithBallFrom(Side.Left, PowerUpKind.MultiBall);

            Assert.That(sim.Balls.Count, Is.EqualTo(3));
            float speed = sim.Balls[0].Speed;
            foreach (Ball ball in sim.Balls)
            {
                Assert.That(ball.Speed, Is.EqualTo(speed).Within(1e-3f));
                Assert.That(ball.Velocity.X, Is.GreaterThan(0f), "all three keep heading the same way");
                float angle = System.MathF.Atan2(System.Math.Abs(ball.Velocity.Y), System.Math.Abs(ball.Velocity.X));
                Assert.That(angle, Is.LessThanOrEqualTo(Tuning.MaxBounceAngle + 1e-4f));
            }
            Assert.That(sim.Balls.Select(b => b.Velocity.Y).Distinct().Count(), Is.EqualTo(3));

            sim.Right.Place(-3.5f);
            sim.Balls[1].Position = new Vec2(8f, 3f);
            sim.Balls[1].Velocity = new Vec2(10f, 0f);
            sim.Step(0.1f, 0f, 0f);
            Assert.That(sim.Score.Of(Side.Left), Is.EqualTo(1));
            Assert.That(sim.Balls.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheSamePowerUpTwiceRefreshesInsteadOfStacking()
        {
            MatchSim sim = CollectWithBallFrom(Side.Left, PowerUpKind.Grow);
            Ball ball = sim.Balls[0];
            ball.Position = new Vec2(-1f, 1f);
            ball.Velocity = new Vec2(10f, 0f);
            ball.LastHitter = Side.Left;
            sim.SpawnPickup(PowerUpKind.Grow, new Vec2(-0.5f, 1f));
            sim.Step(0.1f, 0f, 0f);

            Assert.That(sim.Effects.Count, Is.EqualTo(1));
            Assert.That(sim.Effects[0].Remaining, Is.GreaterThan(PowerUpTuning.EffectDuration - 0.2f));
            Assert.That(sim.Left.HalfHeight, Is.EqualTo(Tuning.PaddleHalfHeight * PowerUpTuning.GrowScale).Within(1e-4f));
        }

        [Test]
        public void WinningClearsEveryPowerUp()
        {
            MatchSim sim = CollectWithBallFrom(Side.Left, PowerUpKind.Grow);
            sim.SpawnPickup(PowerUpKind.Shrink, new Vec2(0f, -2f));
            for (int i = 0; i < 5 && sim.Phase != RallyPhase.MatchOver; i++)
            {
                if (sim.Phase != RallyPhase.Playing)
                {
                    Run(sim, MatchSim.PointPause + Frame);
                }
                ConcedeOnce(sim, Side.Right);
            }
            Assert.That(sim.Phase, Is.EqualTo(RallyPhase.MatchOver));
            Assert.That(sim.Pickup, Is.Null);
            Assert.That(sim.Effects, Is.Empty);
            Assert.That(sim.Left.HalfHeight, Is.EqualTo(Tuning.PaddleHalfHeight));
        }
    }
}
