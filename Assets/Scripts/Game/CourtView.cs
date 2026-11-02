using System.Collections.Generic;
using RetroRally.Core;
using UnityEngine;

namespace RetroRally.Game
{
    /// <summary>
    /// Draws a <see cref="MatchSim"/> with code-made sprites: the court, glowing paddles that
    /// squash and flash on a hit, balls with trails, and the power-up pickup.
    /// </summary>
    public sealed class CourtView : MonoBehaviour
    {
        const float WallThickness = 0.16f;
        const float PaddleWidth = Tuning.PaddleHalfWidth * 2f;
        const int MaxBalls = PowerUpTuning.MaxBalls;

        sealed class PaddleView
        {
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer Glow;
            public float ShownHalfHeight = Tuning.PaddleHalfHeight;
            public float Squash;
            public float Flash;
        }

        sealed class BallView
        {
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer Glow;
            public TrailRenderer Trail;
            public bool WasVisible;
        }

        GameArt art;
        readonly PaddleView[] paddles = new PaddleView[2];
        readonly List<BallView> balls = new List<BallView>();
        readonly SpriteRenderer[] goalLines = new SpriteRenderer[2];
        readonly float[] goalFlash = new float[2];
        Transform pickup;
        SpriteRenderer pickupRing;
        SpriteRenderer pickupGlow;
        readonly List<SpriteRenderer> pickupParts = new List<SpriteRenderer>();
        PowerUpKind shownKind = (PowerUpKind)(-1);
        float pickupPop;
        float time;

        public void Build(GameArt gameArt)
        {
            art = gameArt;
            BuildCourt();
            paddles[0] = BuildPaddle(Side.Left);
            paddles[1] = BuildPaddle(Side.Right);
            for (int i = 0; i < MaxBalls; i++)
            {
                balls.Add(BuildBall(i));
            }
            BuildPickup();
        }

        // ---- Building ----

        SpriteRenderer Sprite(string name, Sprite sprite, Color color, Vector2 position, Vector2 size, int order,
            Transform parent = null, Material material = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
            return renderer;
        }

        void BuildCourt()
        {
            float width = Tuning.CourtHalfWidth * 2f;
            // A faint grid behind everything.
            for (int x = -9; x <= 9; x++)
            {
                Sprite("Grid", Shapes.Square, Palette.Grid, new Vector2(x, 0f), new Vector2(0.025f, Tuning.CourtHalfHeight * 2f), -10);
            }
            for (int y = -4; y <= 4; y++)
            {
                Sprite("Grid", Shapes.Square, Palette.Grid, new Vector2(0f, y), new Vector2(width + 1f, 0.025f), -10);
            }
            // Walls, with a soft glow along them.
            foreach (float sign in new[] { -1f, 1f })
            {
                float y = sign * (Tuning.CourtHalfHeight + WallThickness * 0.5f);
                Sprite("Wall glow", Shapes.SoftDot, Palette.Wall.WithAlpha(0.35f), new Vector2(0f, y), new Vector2(width + 1.6f, 0.9f), -5, material: art.Glow);
                Sprite("Wall", Shapes.Square, Palette.Wall, new Vector2(0f, y), new Vector2(width + 0.6f, WallThickness), 0);
            }
            // The centre line.
            for (float y = -Tuning.CourtHalfHeight + 0.3f; y < Tuning.CourtHalfHeight; y += 0.55f)
            {
                Sprite("Centre", Shapes.Square, Palette.Line, new Vector2(0f, y), new Vector2(0.1f, 0.3f), -4);
            }
            Sprite("Centre circle", Shapes.Ring, Palette.Line, Vector2.zero, new Vector2(2.6f, 2.6f), -4);
            // Goal lines in each player's colour.
            for (int i = 0; i < 2; i++)
            {
                var side = (Side)i;
                float x = (side == Side.Left ? -1f : 1f) * Tuning.CourtHalfWidth;
                goalLines[i] = Sprite("Goal line", Shapes.Square, Palette.Of(side).WithAlpha(0.25f), new Vector2(x, 0f),
                    new Vector2(0.05f, Tuning.CourtHalfHeight * 2f), -3);
            }
        }

        PaddleView BuildPaddle(Side side)
        {
            var root = new GameObject(side == Side.Left ? "Paddle 1" : "Paddle 2").transform;
            root.SetParent(transform, false);
            Color color = Palette.Of(side);
            var view = new PaddleView
            {
                Root = root,
                Glow = Sprite("Glow", Shapes.SoftDot, color.WithAlpha(0.55f), Vector2.zero, Vector2.one, 4, root, art.Glow),
                Body = Sprite("Body", Shapes.Square, color, Vector2.zero, Vector2.one, 6, root),
            };
            return view;
        }

        BallView BuildBall(int index)
        {
            var root = new GameObject($"Ball {index + 1}").transform;
            root.SetParent(transform, false);
            float size = Tuning.BallRadius * 2f;
            var view = new BallView
            {
                Root = root,
                Glow = Sprite("Glow", Shapes.SoftDot, Palette.Ball.WithAlpha(0.6f), Vector2.zero, Vector2.one * 1.3f, 7, root, art.Glow),
                Body = Sprite("Body", Shapes.Circle, Palette.Ball, Vector2.zero, Vector2.one * size, 9, root),
            };
            view.Trail = view.Body.gameObject.AddComponent<TrailRenderer>();
            view.Trail.sharedMaterial = art.Trail;
            view.Trail.time = 0.16f;
            view.Trail.minVertexDistance = 0.04f;
            view.Trail.widthMultiplier = 1f;
            view.Trail.widthCurve = AnimationCurve.Linear(0f, 0.26f, 1f, 0f);
            view.Trail.sortingOrder = 8;
            view.Trail.numCapVertices = 2;
            root.gameObject.SetActive(false);
            return view;
        }

        void BuildPickup()
        {
            pickup = new GameObject("Pickup").transform;
            pickup.SetParent(transform, false);
            pickupGlow = Sprite("Glow", Shapes.SoftDot, Color.white, Vector2.zero, Vector2.one * 2.2f, 1, pickup, art.Glow);
            pickupRing = Sprite("Ring", Shapes.Ring, Color.white, Vector2.zero, Vector2.one * (PowerUpTuning.PickupRadius * 2.2f), 2, pickup);
            pickup.gameObject.SetActive(false);
        }

        /// <summary>Rebuilds the icon inside the pickup ring: + for grow, - for shrink, three dots for multi-ball.</summary>
        void ShowIcon(PowerUpKind kind)
        {
            foreach (SpriteRenderer part in pickupParts)
            {
                Destroy(part.gameObject);
            }
            pickupParts.Clear();
            Color color = Palette.Of(kind);
            switch (kind)
            {
                case PowerUpKind.Grow:
                    pickupParts.Add(Sprite("Icon", Shapes.Square, color, Vector2.zero, new Vector2(0.44f, 0.12f), 3, pickup));
                    pickupParts.Add(Sprite("Icon", Shapes.Square, color, Vector2.zero, new Vector2(0.12f, 0.44f), 3, pickup));
                    break;
                case PowerUpKind.Shrink:
                    pickupParts.Add(Sprite("Icon", Shapes.Square, color, Vector2.zero, new Vector2(0.44f, 0.12f), 3, pickup));
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 at = Quaternion.Euler(0f, 0f, 90f + i * 120f) * new Vector2(0.18f, 0f);
                        pickupParts.Add(Sprite("Icon", Shapes.Circle, color, at, Vector2.one * 0.17f, 3, pickup));
                    }
                    break;
            }
            pickupRing.color = color;
            pickupGlow.color = color.WithAlpha(0.45f);
            shownKind = kind;
        }

        // ---- Feedback ----

        public void PaddleHit(Side side, float offset, float strength)
        {
            PaddleView view = paddles[(int)side];
            view.Squash = 0.35f + 0.4f * strength;
            view.Flash = 1f;
        }

        public void GoalFlash(Side conceded) => goalFlash[(int)conceded] = 1f;

        public void PickupPop() => pickupPop = 1f;

        /// <summary>Hides every ball trail at once (a new rally starts from the paddle).</summary>
        public void ClearTrails()
        {
            foreach (BallView view in balls)
            {
                view.Trail.Clear();
            }
        }

        // ---- Each frame ----

        public void Sync(MatchSim sim, float dt)
        {
            time += dt;
            for (int i = 0; i < 2; i++)
            {
                SyncPaddle(paddles[i], sim.PaddleOf((Side)i), dt);
                goalFlash[i] = Mathf.Max(goalFlash[i] - dt * 2f, 0f);
                Color line = Palette.Of((Side)i);
                goalLines[i].color = Color.Lerp(line.WithAlpha(0.25f), Color.white, goalFlash[i]);
                goalLines[i].transform.localScale = new Vector3(0.05f + goalFlash[i] * 0.25f, Tuning.CourtHalfHeight * 2f, 1f);
            }

            bool showBalls = sim.Phase != RallyPhase.PointScored && sim.Phase != RallyPhase.MatchOver;
            for (int i = 0; i < balls.Count; i++)
            {
                BallView view = balls[i];
                bool visible = showBalls && i < sim.Balls.Count;
                if (visible != view.WasVisible)
                {
                    view.Root.gameObject.SetActive(visible);
                    view.WasVisible = visible;
                    if (visible)
                    {
                        view.Trail.Clear();
                    }
                }
                if (!visible)
                {
                    continue;
                }
                Ball ball = sim.Balls[i];
                view.Root.localPosition = new Vector3(ball.Position.X, ball.Position.Y, 0f);
                float pace = Mathf.Clamp01(ball.Speed / Tuning.MaxBallSpeed);
                Color hitter = Palette.Of(ball.LastHitter);
                view.Trail.emitting = sim.Phase == RallyPhase.Playing;
                view.Trail.startColor = hitter.WithAlpha(0.9f);
                view.Trail.endColor = hitter.WithAlpha(0f);
                view.Trail.time = 0.1f + 0.12f * pace;
                view.Glow.color = Color.Lerp(Palette.Ball, hitter, 0.5f).WithAlpha(0.35f + 0.45f * pace);
                view.Glow.transform.localScale = Vector3.one * (1.1f + 0.7f * pace);
                if (sim.Phase == RallyPhase.Serving)
                {
                    // The held ball breathes until it is served.
                    float pulse = 1f + 0.12f * Mathf.Sin(time * 8f);
                    view.Body.transform.localScale = Vector3.one * (Tuning.BallRadius * 2f * pulse);
                }
                else
                {
                    view.Body.transform.localScale = Vector3.one * (Tuning.BallRadius * 2f);
                }
            }

            Pickup item = sim.Pickup;
            bool showPickup = item != null || pickupPop > 0f;
            if (pickup.gameObject.activeSelf != showPickup)
            {
                pickup.gameObject.SetActive(showPickup);
            }
            if (item != null)
            {
                if (item.Kind != shownKind)
                {
                    ShowIcon(item.Kind);
                }
                pickup.localPosition = new Vector3(item.Position.X, item.Position.Y, 0f);
                float appear = Mathf.Clamp01(item.Age * 4f);
                float left = PowerUpTuning.PickupLifetime - item.Age;
                bool blink = left < 2f && Mathf.Repeat(time * 8f, 1f) < 0.35f;
                float scale = appear * (1f + 0.08f * Mathf.Sin(time * 6f));
                pickup.localScale = Vector3.one * (blink ? scale * 0.85f : scale);
                pickupRing.transform.localRotation = Quaternion.Euler(0f, 0f, time * 90f);
            }
            else if (pickupPop > 0f)
            {
                pickupPop = Mathf.Max(pickupPop - dt * 4f, 0f);
                pickup.localScale = Vector3.one * (1f + (1f - pickupPop) * 1.2f);
                Color ring = pickupRing.color;
                pickupRing.color = ring.WithAlpha(pickupPop);
                pickupGlow.color = pickupGlow.color.WithAlpha(0.45f * pickupPop);
                if (pickupPop <= 0f)
                {
                    shownKind = (PowerUpKind)(-1);
                }
            }
        }

        void SyncPaddle(PaddleView view, Paddle paddle, float dt)
        {
            view.ShownHalfHeight = Mathf.Lerp(view.ShownHalfHeight, paddle.HalfHeight, 1f - Mathf.Exp(-dt * 12f));
            view.Squash = Mathf.Max(view.Squash - dt * 3.5f, 0f);
            view.Flash = Mathf.Max(view.Flash - dt * 6f, 0f);
            float squash = Mathf.Sin(view.Squash * Mathf.PI) * 0.5f * view.Squash;
            float width = PaddleWidth * (1f + squash * 1.4f);
            float height = view.ShownHalfHeight * 2f * (1f - squash * 0.25f);
            view.Root.localPosition = new Vector3(paddle.X, paddle.Y, 0f);
            view.Body.transform.localScale = new Vector3(width, height, 1f);
            view.Glow.transform.localScale = new Vector3(width + 1.2f, height + 1.4f, 1f);
            Color color = Palette.Of(paddle.Side);
            view.Body.color = Color.Lerp(color, Color.white, view.Flash);
            view.Glow.color = color.WithAlpha(0.45f + 0.4f * view.Flash);
        }
    }
}
