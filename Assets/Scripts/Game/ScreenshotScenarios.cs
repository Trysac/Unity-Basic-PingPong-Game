using System;
using System.Collections;
using System.IO;
using RetroRally.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RetroRally.Game
{
    /// <summary>
    /// Opt-in capture of README screenshots: <c>RetroRally.exe -screenshot &lt;folder&gt; -scenario title,rally,arcade,pause,gameover,settings</c>.
    /// Without the -screenshot argument nothing is created, so normal runs and WebGL builds
    /// (which have no command line) are unaffected. The same hook runs inside the editor
    /// (ProjectBuilder.CaptureScreenshots), which renders the camera and the HUD off screen and
    /// blends them, for machines that cannot run the player.
    /// </summary>
    public sealed class ScreenshotScenarios : MonoBehaviour
    {
        const string DefaultScenarios = "title,rally,arcade,pause,gameover,settings";
        const int Width = 1280;
        const int Height = 720;

        string folder;
        string[] scenarios;
        RenderTexture sceneTarget;
        RenderTexture uiTarget;
        GameDirector director;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartWhenRequested()
        {
            string target = Argument("-screenshot");
            if (string.IsNullOrEmpty(target))
            {
                return;
            }
            var host = new GameObject(nameof(ScreenshotScenarios)).AddComponent<ScreenshotScenarios>();
            host.folder = target;
            host.scenarios = (Argument("-scenario") ?? DefaultScenarios).Split(',');
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            director = FindAnyObjectByType<GameDirector>();
            director.UseMemoryStore();
            if (Application.isEditor)
            {
                RenderOffscreen();
            }
            yield return new WaitForSecondsRealtime(0.5f);
            foreach (string raw in scenarios)
            {
                string scenario = raw.Trim();
                director.Seed(StableSeed(scenario));
                yield return Stage(scenario);
                Texture2D shot;
                if (Application.isEditor)
                {
                    yield return null;
                    shot = CaptureOffscreen();
                }
                else
                {
                    yield return new WaitForEndOfFrame();
                    shot = ScreenCapture.CaptureScreenshotAsTexture();
                }
                File.WriteAllBytes(Path.Combine(folder, scenario + ".png"), shot.EncodeToPNG());
                Destroy(shot);
                Debug.Log($"Saved screenshot {scenario}.png");
                director.ScriptedInput = false;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        IEnumerator Stage(string scenario)
        {
            director.Settings.Mode = GameMode.Classic;
            director.Settings.PointsToWin = 5;
            director.Settings.Difficulty = Difficulty.Normal;
            switch (scenario)
            {
                case "title":
                    director.ToTitle();
                    yield return new WaitForSecondsRealtime(4.2f);
                    break;
                case "settings":
                    director.Settings.Mode = GameMode.Arcade;
                    director.ToTitle();
                    director.Activate("settings");
                    director.Hud.Menu.Selected = 2;
                    director.Hud.RefreshMenu();
                    yield return new WaitForSecondsRealtime(1.5f);
                    break;
                case "rally":
                    yield return Play(Opponent.Cpu);
                    Award(Side.Left, 3);
                    Award(Side.Right, 2);
                    yield return Rally(2.9f);
                    break;
                case "arcade":
                    director.Settings.Mode = GameMode.Arcade;
                    director.Settings.PointsToWin = 7;
                    yield return Play(Opponent.Human);
                    Award(Side.Left, 2);
                    Award(Side.Right, 4);
                    yield return Rally(0.4f);
                    // Player 1 served, so the ball is theirs: a big paddle for them, then multi-ball.
                    Ball ball = director.Sim.Balls[0];
                    director.Sim.SpawnPickup(PowerUpKind.Grow, ball.Position + ball.Velocity * 0.1f);
                    yield return Rally(0.6f);
                    ball = director.Sim.Balls[0];
                    director.Sim.SpawnPickup(PowerUpKind.MultiBall, ball.Position + ball.Velocity * 0.1f);
                    yield return Rally(0.4f);
                    director.Sim.SpawnPickup(PowerUpKind.Shrink, new Vec2(-1.8f, -2.2f));
                    yield return Rally(0.2f);
                    break;
                case "pause":
                    yield return Play(Opponent.Cpu);
                    Award(Side.Left, 1);
                    Award(Side.Right, 1);
                    yield return Rally(1.4f);
                    director.TogglePause();
                    yield return new WaitForSecondsRealtime(0.6f);
                    break;
                case "gameover":
                    director.Settings.PointsToWin = 5;
                    yield return Play(Opponent.Cpu);
                    Award(Side.Left, 4);
                    Award(Side.Right, 3);
                    yield return Rally(4f);
                    // Send the ball past the CPU for the winning point.
                    MatchSim sim = director.Sim;
                    sim.Right.Place(3.4f);
                    sim.Balls[0].Position = new Vec2(5f, -3f);
                    sim.Balls[0].Velocity = new Vec2(14f, -2f);
                    float waited = 0f;
                    while (director.Screen != GameScreen.GameOver && waited < 6f)
                    {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    yield return new WaitForSecondsRealtime(0.6f);
                    break;
                default:
                    Debug.LogWarning($"Unknown screenshot scenario '{scenario}'.");
                    break;
            }
        }

        IEnumerator Play(Opponent opponent)
        {
            director.StartMatch(opponent);
            director.ScriptedInput = true;
            yield return new WaitForSecondsRealtime(0.8f);
            director.Sim.ServeNow();
        }

        void Award(Side side, int points)
        {
            for (int i = 0; i < points; i++)
            {
                director.Sim.Score.Award(side);
            }
        }

        /// <summary>Plays for a while with a simple autopilot on both human paddles.</summary>
        IEnumerator Rally(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                MatchSim sim = director.Sim;
                director.ScriptedLeft = Steer(sim, sim.Left);
                director.ScriptedRight = Steer(sim, sim.Right);
                yield return null;
            }
            director.ScriptedLeft = 0f;
            director.ScriptedRight = 0f;
        }

        static float Steer(MatchSim sim, Paddle paddle)
        {
            float target = 0f;
            float soonest = float.MaxValue;
            foreach (Ball ball in sim.Balls)
            {
                if (ball.Velocity.X * paddle.Side.Facing() < 0f &&
                    BallPhysics.TryPredict(ball, paddle.Face, out float y, out float time) && time < soonest)
                {
                    soonest = time;
                    target = y - 0.35f * paddle.HalfHeight;
                }
            }
            return Mathf.Clamp((target - paddle.Y) / 0.3f, -1f, 1f);
        }

        /// <summary>Points the camera and the HUD panel at render textures of a fixed size.</summary>
        void RenderOffscreen()
        {
            sceneTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            uiTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            Camera.main.targetTexture = sceneTarget;
            var document = director.Hud.GetComponent<UIDocument>();
            PanelSettings panel = Instantiate(document.panelSettings);
            panel.targetTexture = uiTarget;
            panel.clearColor = true;
            panel.colorClearValue = Color.clear;
            document.panelSettings = panel;
        }

        /// <summary>Blends the HUD over the court, like the screen would show them.</summary>
        Texture2D CaptureOffscreen()
        {
            Color[] scene = Read(sceneTarget);
            Color[] ui = Read(uiTarget);
            for (int i = 0; i < scene.Length; i++)
            {
                Color over = ui[i];
                Color result = Color.Lerp(scene[i], new Color(over.r, over.g, over.b), over.a);
                result.a = 1f;
                scene[i] = result;
            }
            var shot = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            shot.SetPixels(scene);
            shot.Apply();
            return shot;
        }

        static Color[] Read(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            Color[] pixels = texture.GetPixels();
            Destroy(texture);
            return pixels;
        }

        static int StableSeed(string text)
        {
            int seed = 17;
            foreach (char c in text)
            {
                seed = seed * 31 + c;
            }
            return seed & 0xffff;
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
