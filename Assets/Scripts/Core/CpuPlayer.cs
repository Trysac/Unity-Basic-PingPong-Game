using System;
using System.Collections.Generic;

namespace RetroRally.Core
{
    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    /// <summary>How a CPU difficulty plays: slower reactions, a slower paddle and bigger misjudgements make it beatable.</summary>
    public readonly struct CpuProfile
    {
        /// <summary>Seconds between decisions; between them the CPU keeps its last target.</summary>
        public readonly float ReactionTime;
        /// <summary>Share of the full paddle speed the CPU may use.</summary>
        public readonly float SpeedShare;
        /// <summary>Largest misjudgement of where the ball will arrive, in units, at the top ball speed.</summary>
        public readonly float AimError;
        /// <summary>Works out bounces off the walls; otherwise it chases the ball's current height.</summary>
        public readonly bool ReadsBounces;
        /// <summary>Tries to meet the ball off-centre to send it back at a steep angle.</summary>
        public readonly bool PlaysAngles;

        public CpuProfile(float reactionTime, float speedShare, float aimError, bool readsBounces, bool playsAngles)
        {
            ReactionTime = reactionTime;
            SpeedShare = speedShare;
            AimError = aimError;
            ReadsBounces = readsBounces;
            PlaysAngles = playsAngles;
        }

        public static CpuProfile For(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy:
                    return new CpuProfile(0.3f, 0.5f, 1.6f, readsBounces: true, playsAngles: false);
                case Difficulty.Normal:
                    return new CpuProfile(0.18f, 0.7f, 1.25f, readsBounces: true, playsAngles: false);
                default:
                    return new CpuProfile(0.08f, 0.95f, 0.75f, readsBounces: true, playsAngles: true);
            }
        }
    }

    /// <summary>
    /// A computer opponent. Every reaction interval it picks the ball that will reach its goal
    /// first, guesses where it will arrive (with an error that grows with ball speed) and steers
    /// the paddle there at a capped speed; with nothing coming it drifts back to the centre.
    /// </summary>
    public sealed class CpuPlayer
    {
        const float SteerRange = 0.35f;
        const float DeadZone = 0.04f;

        readonly Rng rng;
        float timer;
        Ball shot;
        int shotHits = -1;
        float shotError;
        float shotAim;

        public CpuPlayer(Side side, Difficulty difficulty, Rng rng)
        {
            Side = side;
            Difficulty = difficulty;
            Profile = CpuProfile.For(difficulty);
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public Side Side { get; }
        public Difficulty Difficulty { get; }
        public CpuProfile Profile { get; }

        /// <summary>The height the CPU is currently steering its paddle to.</summary>
        public float Target { get; private set; }

        /// <summary>Forgets the current plan (after a point) so the next decision comes at once.</summary>
        public void Reset()
        {
            timer = 0f;
            Target = 0f;
            shot = null;
            shotHits = -1;
        }

        /// <summary>The input (-1..1, already scaled by the profile's speed share) for this frame.</summary>
        public float Think(Paddle self, IReadOnlyList<Ball> balls, float dt)
        {
            timer -= dt;
            if (timer <= 0f)
            {
                timer += Profile.ReactionTime;
                if (timer <= 0f)
                {
                    timer = Profile.ReactionTime;
                }
                Target = Decide(self, balls);
            }
            float gap = Target - self.Y;
            if (Math.Abs(gap) < DeadZone)
            {
                return 0f;
            }
            return Math.Clamp(gap / SteerRange, -1f, 1f) * Profile.SpeedShare;
        }

        float Decide(Paddle self, IReadOnlyList<Ball> balls)
        {
            Ball threat = null;
            float soonest = float.MaxValue;
            float arrival = 0f;
            float contactX = self.Face + self.Side.Facing() * Tuning.BallRadius;
            foreach (Ball ball in balls)
            {
                if (ball.Velocity.X * self.Side.Facing() >= 0f)
                {
                    continue; // going the other way
                }
                if (BallPhysics.TryPredict(ball, contactX, out float y, out float time) && time < soonest)
                {
                    soonest = time;
                    threat = ball;
                    arrival = Profile.ReadsBounces ? y : Math.Clamp(ball.Position.Y, -Tuning.CourtHalfHeight, Tuning.CourtHalfHeight);
                }
            }
            if (threat == null)
            {
                return 0f;
            }
            if (threat != shot || threat.Hits != shotHits)
            {
                // A new shot is coming: misjudge it once (faster balls are harder to read) and,
                // on Hard, pick the half of the paddle that will angle the return.
                shot = threat;
                shotHits = threat.Hits;
                float pace = Math.Clamp(threat.Speed / Tuning.MaxBallSpeed, 0.3f, 1f);
                shotError = rng.Range(-1f, 1f) * Profile.AimError * pace;
                shotAim = Profile.PlaysAngles ? (rng.Value() < 0.5f ? -1f : 1f) * self.HalfHeight * 0.4f : 0f;
            }
            return arrival + shotError - shotAim;
        }
    }
}
