using System;
using System.Collections.Generic;

namespace RetroRally.Core
{
    public enum RallyPhase
    {
        /// <summary>The ball waits on the server's paddle.</summary>
        Serving,
        Playing,
        /// <summary>A short beat after a goal before the next serve.</summary>
        PointScored,
        MatchOver,
    }

    public enum MatchEventKind
    {
        Serve,
        Wall,
        PaddleHit,
        Goal,
        MatchWon,
        PickupSpawned,
        PickupCollected,
        PickupExpired,
        EffectEnded,
    }

    /// <summary>Something the view should react to (a sound, particles, a banner).</summary>
    public readonly struct MatchEvent
    {
        public readonly MatchEventKind Kind;
        /// <summary>The paddle that hit, served or collected; the side that scored for goals and wins.</summary>
        public readonly Side Side;
        public readonly Vec2 Position;
        /// <summary>For paddle hits the ball speed as a share of the top speed; the hit offset is in <see cref="Offset"/>.</summary>
        public readonly float Strength;
        public readonly float Offset;
        public readonly PowerUpKind PowerUp;

        public MatchEvent(MatchEventKind kind, Side side, Vec2 position, float strength = 0f, float offset = 0f,
            PowerUpKind powerUp = PowerUpKind.Grow)
        {
            Kind = kind;
            Side = side;
            Position = position;
            Strength = strength;
            Offset = offset;
            PowerUp = powerUp;
        }
    }

    /// <summary>
    /// One match from the first serve to the winning point: paddles, balls, serves, goals, the
    /// CPU, and in Arcade mode the power-ups. The view feeds it inputs and draws its state; all of
    /// it runs without the engine, so it is tested in EditMode.
    /// </summary>
    public sealed class MatchSim
    {
        /// <summary>The pause after a goal before the next serve.</summary>
        public const float PointPause = 1.1f;
        /// <summary>A human server who does not serve is served for after this long.</summary>
        public const float AutoServeTime = 3f;
        /// <summary>How long the CPU holds the ball before serving.</summary>
        public const float CpuServeDelay = 0.75f;

        readonly Rng rng;
        readonly List<Ball> balls = new List<Ball>();
        readonly List<PaddleEffect> effects = new List<PaddleEffect>();
        readonly List<MatchEvent> events = new List<MatchEvent>();
        readonly List<Vec2> starts = new List<Vec2>();
        float spawnTimer;

        public MatchSim(MatchSettings settings, int seed)
        {
            Settings = settings?.Copy() ?? throw new ArgumentNullException(nameof(settings));
            rng = new Rng(seed);
            Left = new Paddle(Side.Left);
            Right = new Paddle(Side.Right);
            Score = new MatchScore(Settings.PointsToWin);
            if (Settings.Opponent == Opponent.Cpu)
            {
                Cpu = new CpuPlayer(Side.Right, Settings.Difficulty, rng);
            }
            Server = Side.Left;
            balls.Add(new Ball());
            spawnTimer = PowerUpTuning.FirstSpawnDelay;
            EnterServing();
        }

        public MatchSettings Settings { get; }
        public Paddle Left { get; }
        public Paddle Right { get; }
        public MatchScore Score { get; }
        public CpuPlayer Cpu { get; }
        public RallyPhase Phase { get; private set; }
        public float PhaseTime { get; private set; }
        public Side Server { get; private set; }

        /// <summary>The side that scored the last point (meaningful after the first goal).</summary>
        public Side LastScorer { get; private set; }

        public IReadOnlyList<Ball> Balls => balls;
        public IReadOnlyList<PaddleEffect> Effects => effects;
        public Pickup Pickup { get; private set; }

        /// <summary>Paddle returns in the current rally, and the most in any rally of this match.</summary>
        public int RallyHits { get; private set; }
        public int LongestRally { get; private set; }

        /// <summary>What happened in the last <see cref="Step"/>; cleared at the start of each step.</summary>
        public IReadOnlyList<MatchEvent> Events => events;

        public Paddle PaddleOf(Side side) => side == Side.Left ? Left : Right;

        public bool IsCpu(Side side) => Cpu != null && side == Side.Right;

        /// <summary>
        /// Advances the match by <paramref name="dt"/> seconds. Inputs are -1..1 per paddle; a serve
        /// flag serves when that side holds the ball. The CPU ignores the right-hand input.
        /// </summary>
        public void Step(float dt, float leftInput, float rightInput, bool leftServe = false, bool rightServe = false)
        {
            events.Clear();
            if (dt <= 0f || Phase == RallyPhase.MatchOver)
            {
                return;
            }
            PhaseTime += dt;
            if (Cpu != null)
            {
                rightInput = Phase == RallyPhase.Playing ? Cpu.Think(Right, balls, dt) : CpuIdle(dt);
            }
            Left.Move(leftInput, dt);
            Right.Move(rightInput, dt);

            switch (Phase)
            {
                case RallyPhase.Serving:
                    StepServing(Server == Side.Left ? leftServe : rightServe);
                    break;
                case RallyPhase.Playing:
                    StepPlaying(dt);
                    break;
                case RallyPhase.PointScored:
                    if (PhaseTime >= PointPause)
                    {
                        EnterServing();
                    }
                    break;
            }
        }

        /// <summary>The CPU drifts to the centre while nobody is in play.</summary>
        float CpuIdle(float dt) => Math.Clamp(-Right.Y / 0.35f, -1f, 1f) * Cpu.Profile.SpeedShare;

        void StepServing(bool serveRequested)
        {
            Paddle server = PaddleOf(Server);
            Ball ball = balls[0];
            ball.Position = BallPhysics.HeldPosition(ball, server);
            bool serve = IsCpu(Server)
                ? PhaseTime >= CpuServeDelay
                : serveRequested || PhaseTime >= AutoServeTime;
            if (serve)
            {
                ServeNow();
            }
        }

        /// <summary>Serves at once (also used by tests and the screenshot hook).</summary>
        public void ServeNow()
        {
            if (Phase != RallyPhase.Serving)
            {
                return;
            }
            Paddle server = PaddleOf(Server);
            // Moving while serving angles the serve; the CPU picks a random angle.
            float aim = IsCpu(Server)
                ? rng.Range(-0.8f, 0.8f)
                : Math.Clamp(server.Velocity / Tuning.PaddleSpeed, -1f, 1f) * 0.8f;
            BallPhysics.Serve(balls[0], server, aim);
            events.Add(new MatchEvent(MatchEventKind.Serve, Server, balls[0].Position));
            SetPhase(RallyPhase.Playing);
        }

        void StepPlaying(float dt)
        {
            starts.Clear();
            foreach (Ball ball in balls)
            {
                starts.Add(ball.Position);
            }
            for (int i = 0; i < balls.Count; i++)
            {
                Ball ball = balls[i];
                StepResult result = BallPhysics.Step(ball, Left, Right, dt);
                if (result.Wall)
                {
                    events.Add(new MatchEvent(MatchEventKind.Wall, ball.LastHitter, ball.Position, ball.Speed / Tuning.MaxBallSpeed));
                }
                if (result.Paddle.HasValue)
                {
                    RallyHits++;
                    LongestRally = Math.Max(LongestRally, RallyHits);
                    events.Add(new MatchEvent(MatchEventKind.PaddleHit, result.Paddle.Value, ball.Position,
                        ball.Speed / Tuning.MaxBallSpeed, result.HitOffset));
                }
                if (result.Conceded.HasValue)
                {
                    Goal(result.Conceded.Value, ball.Position);
                    return;
                }
            }
            if (Settings.Mode == GameMode.Arcade)
            {
                StepPowerUps(dt);
            }
        }

        void Goal(Side conceded, Vec2 where)
        {
            Side scorer = conceded.Other();
            LastScorer = scorer;
            bool won = Score.Award(scorer);
            events.Add(new MatchEvent(MatchEventKind.Goal, scorer, where));
            // The rally is over: extra balls vanish and the side that conceded serves next.
            balls.RemoveRange(1, balls.Count - 1);
            Server = conceded;
            RallyHits = 0;
            Cpu?.Reset();
            if (won)
            {
                ClearPowerUps();
                events.Add(new MatchEvent(MatchEventKind.MatchWon, scorer, where));
                SetPhase(RallyPhase.MatchOver);
            }
            else
            {
                SetPhase(RallyPhase.PointScored);
            }
        }

        void EnterServing()
        {
            balls.RemoveRange(1, balls.Count - 1);
            Ball ball = balls[0];
            ball.Velocity = Vec2.Zero;
            ball.Hits = 0;
            ball.LastHitter = Server;
            ball.Position = BallPhysics.HeldPosition(ball, PaddleOf(Server));
            SetPhase(RallyPhase.Serving);
        }

        void SetPhase(RallyPhase phase)
        {
            Phase = phase;
            PhaseTime = 0f;
        }

        // ---- Power-ups (Arcade mode) ----

        void StepPowerUps(float dt)
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                PaddleEffect effect = effects[i];
                effect.Remaining -= dt;
                if (effect.Remaining <= 0f)
                {
                    effects.RemoveAt(i);
                    ApplySizes();
                    events.Add(new MatchEvent(MatchEventKind.EffectEnded, effect.Target, Vec2.Zero, powerUp: effect.Kind));
                }
            }

            if (Pickup == null)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0f)
                {
                    SpawnPickup((PowerUpKind)rng.Range(0, 3),
                        new Vec2(rng.Range(-PowerUpTuning.SpawnHalfWidth, PowerUpTuning.SpawnHalfWidth),
                            rng.Range(-PowerUpTuning.SpawnHalfHeight, PowerUpTuning.SpawnHalfHeight)));
                }
                return;
            }

            Pickup.Age += dt;
            if (Pickup.Age >= PowerUpTuning.PickupLifetime)
            {
                events.Add(new MatchEvent(MatchEventKind.PickupExpired, Side.Left, Pickup.Position, powerUp: Pickup.Kind));
                Pickup = null;
                ScheduleSpawn();
                return;
            }
            for (int i = 0; i < balls.Count; i++)
            {
                // Test the whole path the ball flew this step, so a fast ball cannot skip over it.
                Ball ball = balls[i];
                Vec2 from = i < starts.Count ? starts[i] : ball.Position;
                float reach = PowerUpTuning.PickupRadius + ball.Radius;
                if (DistanceToSegment(Pickup.Position, from, ball.Position) <= reach)
                {
                    Collect(ball);
                    return;
                }
            }
        }

        /// <summary>Places a pickup on the court now (also used by tests and the screenshot hook).</summary>
        public void SpawnPickup(PowerUpKind kind, Vec2 position)
        {
            Pickup = new Pickup { Kind = kind, Position = position };
            events.Add(new MatchEvent(MatchEventKind.PickupSpawned, Side.Left, position, powerUp: kind));
        }

        void Collect(Ball ball)
        {
            PowerUpKind kind = Pickup.Kind;
            Side collector = ball.LastHitter;
            Vec2 where = Pickup.Position;
            Pickup = null;
            ScheduleSpawn();
            switch (kind)
            {
                case PowerUpKind.Grow:
                    AddEffect(kind, collector);
                    break;
                case PowerUpKind.Shrink:
                    AddEffect(kind, collector.Other());
                    break;
                case PowerUpKind.MultiBall:
                    Split(ball);
                    break;
            }
            events.Add(new MatchEvent(MatchEventKind.PickupCollected, collector, where, powerUp: kind));
        }

        void AddEffect(PowerUpKind kind, Side target)
        {
            // Taking the same effect again on the same paddle refreshes it instead of stacking.
            foreach (PaddleEffect effect in effects)
            {
                if (effect.Kind == kind && effect.Target == target)
                {
                    effect.Remaining = PowerUpTuning.EffectDuration;
                    return;
                }
            }
            effects.Add(new PaddleEffect { Kind = kind, Target = target, Remaining = PowerUpTuning.EffectDuration });
            ApplySizes();
        }

        void Split(Ball source)
        {
            float speed = source.Speed;
            float facing = source.Velocity.X >= 0f ? 1f : -1f;
            // The angle above the horizontal; the copies fan out towards the flatter side so all
            // three stay inside the paddle's steepest return angle.
            float angle = MathF.Atan2(source.Velocity.Y, MathF.Abs(source.Velocity.X));
            float turn = angle >= 0f ? -PowerUpTuning.MultiBallSpread : PowerUpTuning.MultiBallSpread;
            for (int i = 1; i <= 2 && balls.Count < PowerUpTuning.MaxBalls; i++)
            {
                float a = Math.Clamp(angle + turn * i, -Tuning.MaxBounceAngle, Tuning.MaxBounceAngle);
                balls.Add(new Ball
                {
                    Position = source.Position,
                    Velocity = new Vec2(MathF.Cos(a) * facing, MathF.Sin(a)) * speed,
                    Hits = source.Hits,
                    LastHitter = source.LastHitter,
                });
            }
        }

        void ApplySizes()
        {
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                float scale = 1f;
                foreach (PaddleEffect effect in effects)
                {
                    if (effect.Target == side)
                    {
                        scale *= effect.Scale;
                    }
                }
                Paddle paddle = PaddleOf(side);
                paddle.HalfHeight = Tuning.PaddleHalfHeight * scale;
                paddle.Place(paddle.Y);
            }
        }

        static float DistanceToSegment(Vec2 point, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            float lengthSquared = ab.X * ab.X + ab.Y * ab.Y;
            float t = lengthSquared > 1e-8f
                ? Math.Clamp(((point.X - a.X) * ab.X + (point.Y - a.Y) * ab.Y) / lengthSquared, 0f, 1f)
                : 0f;
            return (point - (a + ab * t)).Length;
        }

        void ScheduleSpawn() =>
            spawnTimer = rng.Range(PowerUpTuning.MinSpawnInterval, PowerUpTuning.MaxSpawnInterval);

        void ClearPowerUps()
        {
            Pickup = null;
            effects.Clear();
            ApplySizes();
        }
    }
}
