using System.Collections;
using NUnit.Framework;
using RetroRally.Core;
using RetroRally.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace RetroRally.Tests
{
    /// <summary>
    /// The game's flow in the real scene: title, start, serve, score, win, pause and restart,
    /// with what the HUD shows at each step.
    /// </summary>
    public class MatchFlowTests
    {
        GameDirector director;
        VisualElement ui;

        IEnumerator Load()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            director = Object.FindAnyObjectByType<GameDirector>();
            Assert.That(director, Is.Not.Null);
            director.UseMemoryStore();
            director.Seed(1);
            director.HitStopEnabled = false;
            director.ScriptedInput = true;
            ui = director.Hud.GetComponent<UIDocument>().rootVisualElement;
        }

        IEnumerator Start(Opponent opponent, int points = 5)
        {
            director.Settings.PointsToWin = points;
            director.Settings.Mode = GameMode.Classic;
            director.StartMatch(opponent);
            yield return null;
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(director.Demo, Is.False);
        }

        /// <summary>Serves if needed and puts the ball behind <paramref name="conceding"/>'s paddle, heading into its goal.</summary>
        IEnumerator Concede(Side conceding)
        {
            MatchSim sim = director.Sim;
            float waited = 0f;
            while (sim.Phase == RallyPhase.PointScored && waited < 3f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            sim.ServeNow();
            float facing = conceding == Side.Left ? -1f : 1f;
            sim.Balls[0].Position = new Vec2(facing * 8.2f, 0f);
            sim.Balls[0].Velocity = new Vec2(facing * 12f, 0f);
            waited = 0f;
            while (sim.Phase == RallyPhase.Playing && waited < 2f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        string Text(string name) => ui.Q<Label>(name).text;

        static bool Visible(VisualElement element) => !element.ClassListContains("hidden");

        [UnityTest]
        public IEnumerator TheGameOpensOnTheTitleWithTheDemoPlayingBehindIt()
        {
            yield return Load();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Title));
            Assert.That(director.Demo, Is.True);
            Assert.That(Visible(ui.Q("menu")), Is.True);
            Assert.That(Visible(ui.Q("logo")), Is.True);
            Assert.That(Visible(ui.Q("play-hud")), Is.False);
            Assert.That(director.Hud.Menu.Items[0].Id, Is.EqualTo("play1"));
            Assert.That(director.Hud.Menu.Items[1].Id, Is.EqualTo("play2"));

            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(director.Sim.Phase, Is.EqualTo(RallyPhase.Playing), "the CPUs serve by themselves");
        }

        [UnityTest]
        public IEnumerator StartingAMatchShowsTheScoresAndWaitsForPlayerOneToServe()
        {
            yield return Load();
            director.Activate("play1");
            yield return null;

            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(director.Sim.Settings.Opponent, Is.EqualTo(Opponent.Cpu));
            Assert.That(Visible(ui.Q("menu")), Is.False);
            Assert.That(Visible(ui.Q("play-hud")), Is.True);
            Assert.That(Text("score-left"), Is.EqualTo("0"));
            Assert.That(Text("score-right"), Is.EqualTo("0"));
            Assert.That(Text("name-left"), Is.EqualTo("PLAYER 1"));
            Assert.That(Text("name-right"), Does.StartWith("CPU"));
            Assert.That(director.Sim.Phase, Is.EqualTo(RallyPhase.Serving));
            Assert.That(Text("prompt"), Does.Contain("TO SERVE"));

            director.Sim.ServeNow();
            yield return null;
            yield return null;
            Assert.That(director.Sim.Phase, Is.EqualTo(RallyPhase.Playing));
            Assert.That(Visible(ui.Q("prompt")), Is.False);
        }

        [UnityTest]
        public IEnumerator AGoalUpdatesTheScoreboardAndTheConcederServesNext()
        {
            yield return Load();
            yield return Start(Opponent.Human);
            yield return Concede(Side.Right);

            Assert.That(director.Sim.Score.Of(Side.Left), Is.EqualTo(1));
            yield return null;
            Assert.That(Text("score-left"), Is.EqualTo("1"));
            Assert.That(Text("score-right"), Is.EqualTo("0"));
            Assert.That(director.Hud.BannerText, Is.EqualTo("player 1 scores"));

            yield return new WaitForSecondsRealtime(MatchSim.PointPause + 0.2f);
            Assert.That(director.Sim.Phase, Is.EqualTo(RallyPhase.Serving));
            Assert.That(director.Sim.Server, Is.EqualTo(Side.Right));
            Assert.That(Text("prompt"), Does.StartWith("PLAYER 2"));
        }

        [UnityTest]
        public IEnumerator WinningAgainstTheCpuShowsTheResultsAndSavesTheRecord()
        {
            yield return Load();
            yield return Start(Opponent.Cpu, points: 3);
            for (int i = 0; i < 3; i++)
            {
                yield return Concede(Side.Right);
            }
            Assert.That(director.Sim.Phase, Is.EqualTo(RallyPhase.MatchOver));
            Assert.That(director.Sim.Score.Winner, Is.EqualTo(Side.Left));
            Assert.That(director.Records.Wins(director.Settings.Difficulty), Is.EqualTo(1));
            Assert.That(director.Records.Streak, Is.EqualTo(1));

            float waited = 0f;
            while (director.Screen != GameScreen.GameOver && waited < 4f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(director.Screen, Is.EqualTo(GameScreen.GameOver));
            Assert.That(Text("menu-title"), Is.EqualTo("player 1 wins"));
            Assert.That(Text("menu-subtitle"), Is.EqualTo("3  -  0"));
            Assert.That(director.Hud.Menu.Items[0].Id, Is.EqualTo("rematch"));
        }

        [UnityTest]
        public IEnumerator PauseFreezesTheBallAndResumeCarriesOn()
        {
            yield return Load();
            yield return Start(Opponent.Human);
            director.Sim.ServeNow();
            yield return new WaitForSecondsRealtime(0.2f);

            director.TogglePause();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Paused));
            Assert.That(Text("menu-title"), Is.EqualTo("paused"));
            Vec2 before = director.Sim.Balls[0].Position;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(director.Sim.Balls[0].Position, Is.EqualTo(before));

            director.Activate("resume");
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(Visible(ui.Q("menu")), Is.False);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(director.Sim.Balls[0].Position, Is.Not.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator RestartFromThePauseMenuStartsAFreshMatchWithTheSameOpponent()
        {
            yield return Load();
            yield return Start(Opponent.Cpu);
            yield return Concede(Side.Right);
            MatchSim old = director.Sim;
            director.TogglePause();
            director.Activate("restart");
            yield return null;

            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(director.Sim, Is.Not.SameAs(old));
            Assert.That(director.Sim.Settings.Opponent, Is.EqualTo(Opponent.Cpu));
            Assert.That(director.Sim.Score.Of(Side.Left), Is.EqualTo(0));
            Assert.That(Text("score-left"), Is.EqualTo("0"));
        }

        [UnityTest]
        public IEnumerator MainMenuFromThePauseGoesBackToTheTitle()
        {
            yield return Load();
            yield return Start(Opponent.Cpu);
            director.TogglePause();
            director.Activate("menu");
            yield return null;

            Assert.That(director.Screen, Is.EqualTo(GameScreen.Title));
            Assert.That(director.Demo, Is.True);
            Assert.That(Visible(ui.Q("play-hud")), Is.False);
            Assert.That(Visible(ui.Q("logo")), Is.True);
        }

        [UnityTest]
        public IEnumerator SettingsChangesAreShownAndUsedByTheNextMatch()
        {
            yield return Load();
            director.Activate("settings");
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Settings));
            director.Change("points", 1);
            director.Change("mode", 1);
            int points = director.Settings.PointsToWin;
            Assert.That(director.Hud.Menu.Items[1].Value, Is.EqualTo($"FIRST TO {points}"));
            Assert.That(director.Hud.Menu.Items[2].Value, Is.EqualTo("ARCADE"));

            director.Activate("back");
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Title));
            director.Activate("play2");
            yield return null;
            Assert.That(director.Sim.Settings.PointsToWin, Is.EqualTo(points));
            Assert.That(director.Sim.Settings.Mode, Is.EqualTo(GameMode.Arcade));
            Assert.That(director.Sim.Cpu, Is.Null);
            Assert.That(Text("target"), Is.EqualTo($"FIRST TO {points}  -  ARCADE"));
        }
    }
}
