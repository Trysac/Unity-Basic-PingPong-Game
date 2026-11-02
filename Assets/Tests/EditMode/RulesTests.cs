using System;
using NUnit.Framework;

namespace RetroRally.Core.Tests
{
    public class PaddleTests
    {
        [Test]
        public void PaddlesStartCentredOnTheirOwnGoalLine()
        {
            var left = new Paddle(Side.Left);
            var right = new Paddle(Side.Right);
            Assert.That(left.X, Is.EqualTo(-Tuning.PaddleX));
            Assert.That(right.X, Is.EqualTo(Tuning.PaddleX));
            Assert.That(left.Y, Is.EqualTo(0f));
            Assert.That(left.Face, Is.GreaterThan(left.X), "the left paddle faces +X");
            Assert.That(right.Face, Is.LessThan(right.X), "the right paddle faces -X");
        }

        [Test]
        public void MovingCoversSpeedTimesTimeAndReportsTheVelocity()
        {
            var paddle = new Paddle(Side.Left);
            paddle.Move(1f, 0.1f);
            Assert.That(paddle.Y, Is.EqualTo(Tuning.PaddleSpeed * 0.1f).Within(1e-4f));
            Assert.That(paddle.Velocity, Is.EqualTo(Tuning.PaddleSpeed).Within(1e-3f));

            paddle.Move(-5f, 0.1f); // input is clamped to -1
            Assert.That(paddle.Y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void PaddlesNeverLeaveTheCourt()
        {
            var paddle = new Paddle(Side.Right);
            paddle.Move(1f, 10f);
            Assert.That(paddle.Y + paddle.HalfHeight, Is.EqualTo(Tuning.CourtHalfHeight).Within(1e-4f));
            paddle.Place(-100f);
            Assert.That(paddle.Y - paddle.HalfHeight, Is.EqualTo(-Tuning.CourtHalfHeight).Within(1e-4f));
        }
    }

    public class MatchScoreTests
    {
        [Test]
        public void TheFirstSideToReachTheTargetWins()
        {
            var score = new MatchScore(3);
            Assert.That(score.Award(Side.Left), Is.False);
            Assert.That(score.Award(Side.Right), Is.False);
            Assert.That(score.Award(Side.Left), Is.False);
            Assert.That(score.IsMatchPoint(Side.Left), Is.True);
            Assert.That(score.IsMatchPoint(Side.Right), Is.False);
            Assert.That(score.IsOver, Is.False);

            Assert.That(score.Award(Side.Left), Is.True);
            Assert.That(score.IsOver, Is.True);
            Assert.That(score.Winner, Is.EqualTo(Side.Left));
            Assert.That(score.Of(Side.Left), Is.EqualTo(3));
            Assert.That(score.Of(Side.Right), Is.EqualTo(1));
            Assert.That(score.IsMatchPoint(Side.Left), Is.False, "no match point once it is over");
        }

        [Test]
        public void NoPointsAfterTheMatchIsOver()
        {
            var score = new MatchScore(1);
            score.Award(Side.Right);
            Assert.Throws<InvalidOperationException>(() => score.Award(Side.Left));
            Assert.That(score.Of(Side.Left), Is.EqualTo(0));
        }

        [Test]
        public void AMatchNeedsAtLeastOnePoint()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchScore(0));
        }
    }

    public class RngTests
    {
        [Test]
        public void TheSameSeedGivesTheSameSequence()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 100; i++)
            {
                Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
            }
        }

        [Test]
        public void RangesStayInsideTheirBounds()
        {
            var rng = new Rng(7);
            for (int i = 0; i < 1000; i++)
            {
                float f = rng.Range(-2f, 3f);
                int n = rng.Range(2, 5);
                Assert.That(f, Is.GreaterThanOrEqualTo(-2f).And.LessThan(3f));
                Assert.That(n, Is.InRange(2, 4));
            }
        }
    }
}
