using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace RetroRally.Core.Tests
{
    public class CpuPlayerTests
    {
        const float Frame = 1f / 60f;

        /// <summary>
        /// Plays <paramref name="rallies"/> rallies between a sharp scripted player on the left, who
        /// always aims for a paddle tip to send steep shots, and the CPU on the right. Returns how
        /// many rallies the CPU lost.
        /// </summary>
        static int RalliesLostByCpu(Difficulty difficulty, int rallies, int seed, out int cpuReturns)
        {
            var left = new Paddle(Side.Left);
            var right = new Paddle(Side.Right);
            var cpu = new CpuPlayer(Side.Right, difficulty, new Rng(seed));
            var ball = new Ball();
            var balls = new List<Ball> { ball };
            int lost = 0;
            cpuReturns = 0;
            for (int rally = 0; rally < rallies; rally++)
            {
                left.Place(0f);
                right.Place(0f);
                cpu.Reset();
                BallPhysics.Serve(ball, left, rally % 3 - 1);
                float tip = (rally % 2 == 0 ? 1f : -1f) * (left.HalfHeight * 0.85f);
                for (float t = 0f; t < 30f; t += Frame)
                {
                    float target = 0f;
                    if (ball.Velocity.X < 0f && BallPhysics.TryPredict(ball, left.Face + ball.Radius, out float y, out _))
                    {
                        target = y - tip;
                    }
                    left.Move(Math.Clamp((target - left.Y) / 0.2f, -1f, 1f), Frame);
                    right.Move(cpu.Think(right, balls, Frame), Frame);
                    StepResult result = BallPhysics.Step(ball, left, right, Frame);
                    if (result.Paddle == Side.Right)
                    {
                        cpuReturns++;
                    }
                    if (result.Conceded.HasValue)
                    {
                        if (result.Conceded == Side.Right)
                        {
                            lost++;
                        }
                        break;
                    }
                }
            }
            return lost;
        }

        [Test]
        public void TheCpuNeverMovesFasterThanItsDifficultyAllows()
        {
            foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
            {
                var cpu = new CpuPlayer(Side.Right, difficulty, new Rng(3));
                var paddle = new Paddle(Side.Right);
                var ball = new Ball { Position = new Vec2(0f, 0f), Velocity = new Vec2(12f, 9f) };
                for (int i = 0; i < 120; i++)
                {
                    float input = cpu.Think(paddle, new[] { ball }, Frame);
                    Assert.That(Math.Abs(input), Is.LessThanOrEqualTo(cpu.Profile.SpeedShare + 1e-5f), difficulty.ToString());
                }
            }
        }

        [Test]
        public void TheCpuWaitsForItsReactionTimeBeforeChangingItsMind()
        {
            var cpu = new CpuPlayer(Side.Right, Difficulty.Easy, new Rng(5));
            var paddle = new Paddle(Side.Right);
            var ball = new Ball { Position = new Vec2(0f, 2f), Velocity = new Vec2(8f, 0f) };
            cpu.Think(paddle, new[] { ball }, Frame);
            float first = cpu.Target;

            ball.Position = new Vec2(0f, -2f);
            cpu.Think(paddle, new[] { ball }, Frame);
            Assert.That(cpu.Target, Is.EqualTo(first), "it has not reacted yet");

            for (float t = 0f; t < cpu.Profile.ReactionTime; t += Frame)
            {
                cpu.Think(paddle, new[] { ball }, Frame);
            }
            Assert.That(cpu.Target, Is.LessThan(0f), "after its reaction time it follows the ball down");
        }

        [Test]
        public void WithNothingComingTheCpuGoesBackToTheCentre()
        {
            var cpu = new CpuPlayer(Side.Right, Difficulty.Hard, new Rng(1));
            var paddle = new Paddle(Side.Right);
            paddle.Place(3f);
            var ball = new Ball { Position = new Vec2(0f, 3f), Velocity = new Vec2(-8f, 0f) };
            float input = cpu.Think(paddle, new[] { ball }, Frame);
            Assert.That(cpu.Target, Is.EqualTo(0f));
            Assert.That(input, Is.LessThan(0f));
        }

        [Test]
        public void TheCpuGoesForTheBallThatArrivesFirst()
        {
            var cpu = new CpuPlayer(Side.Right, Difficulty.Normal, new Rng(9));
            var paddle = new Paddle(Side.Right);
            var far = new Ball { Position = new Vec2(-6f, 3f), Velocity = new Vec2(8f, 0f) };
            var near = new Ball { Position = new Vec2(4f, -3f), Velocity = new Vec2(8f, 0f) };
            cpu.Think(paddle, new[] { far, near }, Frame);
            Assert.That(cpu.Target, Is.LessThan(-1.5f));
        }

        [Test]
        public void TheSameSeedPlaysTheSameMatch()
        {
            int a = RalliesLostByCpu(Difficulty.Normal, 10, 21, out int returnsA);
            int b = RalliesLostByCpu(Difficulty.Normal, 10, 21, out int returnsB);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(returnsA, Is.EqualTo(returnsB));
        }

        [Test]
        public void HarderCpusLoseFewerRalliesButEveryLevelIsBeatable()
        {
            const int Rallies = 40;
            int easy = RalliesLostByCpu(Difficulty.Easy, Rallies, 11, out int easyReturns);
            int normal = RalliesLostByCpu(Difficulty.Normal, Rallies, 11, out int normalReturns);
            int hard = RalliesLostByCpu(Difficulty.Hard, Rallies, 11, out int hardReturns);
            TestContext.WriteLine($"lost {easy}/{normal}/{hard}, returns {easyReturns}/{normalReturns}/{hardReturns}");

            Assert.That(easy, Is.GreaterThan(normal));
            Assert.That(normal, Is.GreaterThan(hard));
            Assert.That(hard, Is.GreaterThan(0), "Hard is not a wall");
            Assert.That(easyReturns, Is.GreaterThan(0), "Easy still returns some shots");
            Assert.That(hardReturns, Is.GreaterThan(normalReturns));
        }
    }
}
