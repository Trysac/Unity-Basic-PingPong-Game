using RetroRally.Core;
using UnityEngine;

namespace RetroRally.Game
{
    public enum GameScreen
    {
        Title,
        Settings,
        Records,
        Playing,
        Paused,
        GameOver,
    }

    /// <summary>
    /// Runs the game: the menus, a CPU-versus-CPU demo behind the title, and matches. It feeds
    /// input to the engine-free <see cref="MatchSim"/>, turns its events into sound, particles,
    /// shake and hit-stop, and keeps the settings and records.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        const string OptionsPrefix = "retrorally.options.";
        const float ResultsDelay = 1.6f;
        const float MaxFrame = 1f / 20f;

        [SerializeField] GameArt art;
        [SerializeField] Hud hud;
        [SerializeField] CameraRig cameraRig;

        CourtView court;
        AudioDirector sound;
        readonly GameInput input = new GameInput();
        IRecordStore store;
        RecordBook records;
        MatchSettings settings;
        CpuPlayer demoLeft;
        int volumeStep = 8;
        bool shakeOn = true;
        int seed;
        float hitStop;
        float inputGuard;
        float overTime;
        bool recorded;
        RecordUpdate lastRecord;
        RallyPhase lastPhase;

        public GameScreen Screen { get; private set; }
        public MatchSim Sim { get; private set; }
        public RecordBook Records => records;
        public MatchSettings Settings => settings;
        public Hud Hud => hud;

        /// <summary>True while the match behind the menus is the CPU-versus-CPU demo.</summary>
        public bool Demo { get; private set; }

        /// <summary>Tests and the screenshot hook drive the paddles instead of the keyboard.</summary>
        public bool ScriptedInput { get; set; }
        public float ScriptedLeft { get; set; }
        public float ScriptedRight { get; set; }

        /// <summary>Freezes the match for hit-stop; tests turn it off to keep timings exact.</summary>
        public bool HitStopEnabled { get; set; } = true;

        void Awake()
        {
            seed = System.Environment.TickCount;
            store = new PlayerPrefsStore();
            LoadOptions();
            sound = gameObject.AddComponent<AudioDirector>();
            sound.Init(volumeStep / 10f, store.GetInt(OptionsPrefix + "muted", 0) == 1);
            court = new GameObject("Court").AddComponent<CourtView>();
            court.Build(art);
            hud.ItemClicked += OnItemClicked;
        }

        void Start()
        {
            ToTitle();
            UpdateSoundLabel();
        }

        void LoadOptions()
        {
            records = new RecordBook(store);
            settings = MatchSettings.Load(store);
            volumeStep = Mathf.Clamp(store.GetInt(OptionsPrefix + "volume", 8), 0, 10);
            shakeOn = store.GetInt(OptionsPrefix + "shake", 1) == 1;
            if (cameraRig != null)
            {
                cameraRig.ShakeScale = shakeOn ? 1f : 0f;
            }
        }

        /// <summary>Keeps records and settings in memory only (tests and screenshots).</summary>
        public void UseMemoryStore()
        {
            store = new MemoryRecordStore();
            LoadOptions();
        }

        /// <summary>Makes the next matches repeatable.</summary>
        public void Seed(int value) => seed = value;

        int NextSeed() => seed++;

        // ---- Flow ----

        public void ToTitle()
        {
            Screen = GameScreen.Title;
            StartDemo();
            hud.ShowPlayHud(false);
            hud.HideBanner();
            hud.ClearFloaters();
            sound.PlayMusic(art.TitleMusic);
            sound.SetDucked(false);
            ShowTitleMenu(0);
        }

        void StartDemo()
        {
            Demo = true;
            sound.Silent = true;
            var demo = new MatchSettings
            {
                Opponent = Opponent.Cpu,
                Difficulty = Difficulty.Hard,
                Mode = GameMode.Arcade,
                PointsToWin = 11,
            };
            int demoSeed = NextSeed();
            Sim = new MatchSim(demo, demoSeed);
            demoLeft = new CpuPlayer(Side.Left, Difficulty.Normal, new Rng(demoSeed + 1));
            hitStop = 0f;
            court.ClearTrails();
        }

        /// <summary>Starts a match against the CPU or a second player with the current settings.</summary>
        public void StartMatch(Opponent opponent)
        {
            settings.Opponent = opponent;
            Demo = false;
            sound.Silent = false;
            Sim = new MatchSim(settings, NextSeed());
            Screen = GameScreen.Playing;
            hitStop = 0f;
            overTime = 0f;
            recorded = false;
            inputGuard = 0.3f;
            lastPhase = Sim.Phase;
            hud.HideMenu();
            hud.HideBanner();
            hud.ClearFloaters();
            hud.ShowPlayHud(true);
            string right = opponent == Opponent.Cpu ? $"CPU  {Name(settings.Difficulty)}" : "PLAYER 2";
            string goal = $"FIRST TO {settings.PointsToWin}" + (settings.Mode == GameMode.Arcade ? "  -  ARCADE" : string.Empty);
            hud.SetPlayers("PLAYER 1", right, goal);
            hud.SetScore(0, 0);
            court.ClearTrails();
            cameraRig.Calm();
            sound.PlayMusic(art.MatchMusic);
            sound.SetDucked(false);
            hud.Banner("READY", null, 700);
        }

        public void Restart() => StartMatch(settings.Opponent);

        public void TogglePause()
        {
            if (Screen == GameScreen.Playing)
            {
                Screen = GameScreen.Paused;
                sound.SetDucked(true);
                sound.PlayUi(art.Pause);
                ShowPauseMenu(0);
            }
            else if (Screen == GameScreen.Paused)
            {
                Screen = GameScreen.Playing;
                sound.SetDucked(false);
                sound.PlayUi(art.Pause);
                hud.HideMenu();
                inputGuard = 0.2f;
            }
        }

        void ShowResults()
        {
            Screen = GameScreen.GameOver;
            sound.SetDucked(true);
            ShowResultsMenu(0);
        }

        // ---- Frame ----

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxFrame);
            inputGuard = Mathf.Max(inputGuard - dt, 0f);
            if (!ScriptedInput && input.Mute)
            {
                sound.ToggleMute();
                store.SetInt(OptionsPrefix + "muted", sound.Muted ? 1 : 0);
                store.Save();
                UpdateSoundLabel();
            }

            switch (Screen)
            {
                case GameScreen.Title:
                case GameScreen.Settings:
                case GameScreen.Records:
                    if (!ScriptedInput)
                    {
                        MenuInput();
                    }
                    StepDemo(dt);
                    break;
                case GameScreen.Playing:
                    if (!ScriptedInput && input.Pause)
                    {
                        TogglePause();
                        break;
                    }
                    StepMatch(dt);
                    break;
                case GameScreen.Paused:
                case GameScreen.GameOver:
                    if (!ScriptedInput)
                    {
                        MenuInput();
                    }
                    break;
            }

            if (Sim != null)
            {
                court.Sync(Sim, Screen == GameScreen.Paused ? 0f : dt);
                if (!Demo)
                {
                    UpdateHud();
                }
            }
        }

        void StepDemo(float dt)
        {
            if (Sim.Phase == RallyPhase.MatchOver)
            {
                StartDemo();
            }
            if (Consume(ref dt))
            {
                return;
            }
            float left = Sim.Phase == RallyPhase.Playing ? demoLeft.Think(Sim.Left, Sim.Balls, dt) : -Sim.Left.Y;
            Sim.Step(dt, left, 0f, leftServe: Sim.PhaseTime > 0.8f);
            React();
        }

        void StepMatch(float dt)
        {
            if (Sim.Phase == RallyPhase.MatchOver)
            {
                overTime += dt;
                if (overTime >= ResultsDelay)
                {
                    ShowResults();
                }
                return;
            }
            if (Consume(ref dt))
            {
                return;
            }
            bool single = Sim.Settings.Opponent == Opponent.Cpu;
            float left, right;
            bool serveLeft = false, serveRight = false;
            if (ScriptedInput)
            {
                left = ScriptedLeft;
                right = ScriptedRight;
            }
            else
            {
                left = input.Paddle(Side.Left, single);
                right = single ? 0f : input.Paddle(Side.Right, false);
                if (inputGuard <= 0f)
                {
                    serveLeft = input.Serve(Side.Left, single);
                    serveRight = !single && input.Serve(Side.Right, false);
                }
            }
            Sim.Step(dt, left, right, serveLeft, serveRight);
            React();
        }

        /// <summary>Spends hit-stop time; true when the match should not move this frame.</summary>
        bool Consume(ref float dt)
        {
            if (hitStop <= 0f)
            {
                return false;
            }
            hitStop -= dt;
            if (hitStop > 0f)
            {
                return true;
            }
            dt = -hitStop;
            hitStop = 0f;
            return dt <= 0f;
        }

        void Freeze(float seconds)
        {
            if (HitStopEnabled)
            {
                hitStop = Mathf.Max(hitStop, seconds);
            }
        }

        // ---- Game feel ----

        void React()
        {
            foreach (MatchEvent e in Sim.Events)
            {
                Vector2 at = new Vector2(e.Position.X, e.Position.Y);
                switch (e.Kind)
                {
                    case MatchEventKind.Serve:
                        sound.Play(art.Serve, 0.7f);
                        Effects.Burst(at, new Vector2(e.Side.Facing(), 0f), art.Glow, Palette.Of(e.Side), Color.white, 8, 4f, 0.18f, 0.3f);
                        break;
                    case MatchEventKind.Wall:
                        sound.Play(art.Wall, 0.55f, 0.9f + 0.3f * e.Strength);
                        Effects.Burst(at, new Vector2(0f, -Mathf.Sign(e.Position.Y)), art.Glow, Palette.Ball, Palette.Wall, 6, 3f, 0.12f, 0.25f, 120f);
                        cameraRig.Shake(0.05f);
                        break;
                    case MatchEventKind.PaddleHit:
                        OnPaddleHit(e, at);
                        break;
                    case MatchEventKind.Goal:
                        OnGoal(e, at);
                        break;
                    case MatchEventKind.MatchWon:
                        OnMatchWon(e);
                        break;
                    case MatchEventKind.PickupSpawned:
                        sound.Play(art.PickupSpawn, 0.6f);
                        Effects.Burst(at, Vector2.zero, art.Glow, Palette.Of(e.PowerUp), Color.white, 14, 3f, 0.18f, 0.5f);
                        break;
                    case MatchEventKind.PickupCollected:
                        OnPickup(e, at);
                        break;
                    case MatchEventKind.PickupExpired:
                        court.PickupPop();
                        break;
                }
            }

            if (!Demo && Sim.Phase == RallyPhase.Serving && lastPhase == RallyPhase.PointScored)
            {
                Side? matchPoint = Sim.Score.IsMatchPoint(Side.Left) ? Side.Left
                    : Sim.Score.IsMatchPoint(Side.Right) ? Side.Right : (Side?)null;
                if (matchPoint.HasValue)
                {
                    hud.Banner("MATCH POINT", matchPoint == Side.Left ? "left" : "right", 1200);
                    sound.Play(art.MatchPoint, 0.8f);
                }
            }
            lastPhase = Sim.Phase;
        }

        void OnPaddleHit(MatchEvent e, Vector2 at)
        {
            float s = e.Strength;
            sound.Play(art.Paddle, 0.8f, 0.85f + 0.55f * s);
            Effects.Burst(at, new Vector2(e.Side.Facing(), e.Offset), art.Glow, Palette.Of(e.Side), Color.white,
                10 + (int)(16 * s), 3.5f + 5f * s, 0.16f + 0.08f * s, 0.35f);
            court.PaddleHit(e.Side, e.Offset, s);
            cameraRig.Shake(0.06f + 0.18f * s);
            cameraRig.Kick(new Vector2(e.Side.Facing(), e.Offset), 0.05f + 0.1f * s);
            Freeze(0.02f + 0.05f * s);
            if (!Demo && Sim.RallyHits >= 10 && Sim.RallyHits % 5 == 0)
            {
                hud.Float(at + new Vector2(e.Side.Facing() * 0.8f, 0.6f), $"RALLY {Sim.RallyHits}", Palette.Of(e.Side));
            }
        }

        void OnGoal(MatchEvent e, Vector2 at)
        {
            Side conceded = e.Side.Other();
            sound.Play(art.Goal, 0.9f);
            court.GoalFlash(conceded);
            Vector2 back = new Vector2(conceded.Facing(), 0f);
            Effects.Burst(new Vector2(Mathf.Clamp(at.x, -Tuning.CourtHalfWidth, Tuning.CourtHalfWidth), at.y), back,
                art.Glow, Palette.Of(e.Side), Color.white, 46, 9f, 0.3f, 0.7f, 140f);
            cameraRig.Shake(0.55f);
            cameraRig.Kick(-back, 0.25f);
            Freeze(0.12f);
            if (!Demo && !Sim.Score.IsOver)
            {
                hud.Banner($"{PlayerName(e.Side)} SCORES", e.Side == Side.Left ? "left" : "right", 950);
            }
        }

        void OnMatchWon(MatchEvent e)
        {
            if (Demo)
            {
                return;
            }
            bool playerLost = Sim.IsCpu(e.Side);
            sound.Play(playerLost ? art.Lose : art.Win);
            sound.PlayMusic(null);
            hud.Banner($"{PlayerName(e.Side)} WINS", e.Side == Side.Left ? "left" : "right", 1500);
            Effects.Burst(new Vector2(e.Side == Side.Left ? -4f : 4f, 0f), Vector2.zero, art.Glow, Palette.Of(e.Side), Palette.Ball, 80, 10f, 0.32f, 1.1f);
            RecordResult();
        }

        void RecordResult()
        {
            if (recorded)
            {
                return;
            }
            recorded = true;
            lastRecord = Sim.Settings.Opponent == Opponent.Cpu
                ? records.RecordCpuMatch(Sim.Settings.Difficulty, Sim.Score.Winner == Side.Left, Sim.LongestRally)
                : records.RecordVersusMatch(Sim.LongestRally);
        }

        void OnPickup(MatchEvent e, Vector2 at)
        {
            AudioClip clip = e.PowerUp == PowerUpKind.Grow ? art.Grow : e.PowerUp == PowerUpKind.Shrink ? art.Shrink : art.MultiBall;
            sound.Play(clip, 0.85f);
            court.PickupPop();
            Effects.Burst(at, Vector2.zero, art.Glow, Palette.Of(e.PowerUp), Color.white, 30, 6f, 0.22f, 0.6f);
            cameraRig.Shake(0.15f);
            if (!Demo)
            {
                hud.Float(new Vector2(at.x, Mathf.Min(at.y + 0.7f, 3f)), PowerUpTuning.Title(e.PowerUp) + "!", Palette.Of(e.PowerUp));
            }
        }

        string PlayerName(Side side) =>
            side == Side.Left ? "PLAYER 1" : Sim != null && Sim.Cpu != null ? "CPU" : "PLAYER 2";

        void UpdateHud()
        {
            hud.SetScore(Sim.Score.Of(Side.Left), Sim.Score.Of(Side.Right));
            hud.SetEffects(Side.Left, Sim.Effects);
            hud.SetEffects(Side.Right, Sim.Effects);
            string prompt = null;
            if (Screen == GameScreen.Playing && Sim.Phase == RallyPhase.Serving && !Sim.IsCpu(Sim.Server))
            {
                bool single = Sim.Settings.Opponent == Opponent.Cpu;
                string button = input.UsingGamepad ? "A" : Sim.Server == Side.Left ? "SPACE" : "ENTER";
                string who = single ? string.Empty : Sim.Server == Side.Left ? "PLAYER 1: " : "PLAYER 2: ";
                prompt = $"{who}PRESS {button} TO SERVE  -  MOVE TO ANGLE IT";
            }
            hud.SetPrompt(prompt);
        }

        // ---- Menus ----

        void ShowTitleMenu(int selected)
        {
            var menu = new MenuModel
            {
                Logo = true,
                Subtitle = $"CPU {Name(settings.Difficulty)}  -  FIRST TO {settings.PointsToWin}  -  {Name(settings.Mode)}",
                Selected = selected,
                Hint = MenuHint(),
            };
            menu.Add("play1", "1 PLAYER  VS  CPU");
            menu.Add("play2", "2 PLAYERS");
            menu.Add("settings", "SETTINGS");
            menu.Add("records", "RECORDS");
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                menu.Add("quit", "QUIT");
            }
            hud.ShowMenu(menu);
        }

        void ShowSettingsMenu(int selected)
        {
            Screen = GameScreen.Settings;
            var menu = new MenuModel
            {
                Title = "SETTINGS",
                Subtitle = settings.Mode == GameMode.Arcade
                    ? "ARCADE: BIG PADDLE, SHRINK RAY AND MULTI-BALL POWER-UPS"
                    : "CLASSIC: JUST THE BALL AND TWO PADDLES",
                Selected = selected,
                Hint = MenuHint(withChange: true),
            };
            menu.Add("cpu", "CPU LEVEL", Name(settings.Difficulty));
            menu.Add("points", "MATCH", $"FIRST TO {settings.PointsToWin}");
            menu.Add("mode", "MODE", Name(settings.Mode));
            menu.Add("volume", "VOLUME", volumeStep.ToString());
            menu.Add("shake", "SCREEN SHAKE", shakeOn ? "ON" : "OFF");
            menu.Add("back", "BACK");
            hud.ShowMenu(menu);
        }

        void ShowRecordsMenu()
        {
            Screen = GameScreen.Records;
            var menu = new MenuModel { Title = "RECORDS", Subtitle = "AGAINST THE CPU, ON THIS DEVICE", Hint = MenuHint() };
            foreach (Difficulty level in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard })
            {
                menu.Body.Add(($"{Name(level)} CPU", $"{records.Wins(level)} WON  {records.Losses(level)} LOST", false));
            }
            menu.Body.Add(("WIN STREAK", records.Streak.ToString(), false));
            menu.Body.Add(("BEST STREAK", records.BestStreak.ToString(), false));
            menu.Body.Add(("LONGEST RALLY (ANY MODE)", Hits(records.LongestRally), false));
            menu.Add("back", "BACK");
            hud.ShowMenu(menu);
        }

        void ShowPauseMenu(int selected)
        {
            var menu = new MenuModel { Title = "PAUSED", Selected = selected, LightDim = true, Hint = MenuHint() };
            menu.Add("resume", "RESUME");
            menu.Add("restart", "RESTART MATCH");
            menu.Add("menu", "MAIN MENU");
            hud.ShowMenu(menu);
        }

        void ShowResultsMenu(int selected)
        {
            Side winner = Sim.Score.Winner ?? Side.Left;
            var menu = new MenuModel
            {
                Title = $"{PlayerName(winner)} WINS",
                TitleStyle = winner == Side.Left ? "left" : "right",
                Subtitle = $"{Sim.Score.Of(Side.Left)}  -  {Sim.Score.Of(Side.Right)}",
                Selected = selected,
                LightDim = true,
                Hint = MenuHint(),
            };
            menu.Body.Add(("LONGEST RALLY", Hits(Sim.LongestRally) + (lastRecord.NewLongestRally ? "  NEW RECORD" : string.Empty), lastRecord.NewLongestRally));
            if (Sim.Settings.Opponent == Opponent.Cpu)
            {
                Difficulty level = Sim.Settings.Difficulty;
                menu.Body.Add(("WIN STREAK", records.Streak + (lastRecord.NewBestStreak ? "  NEW BEST" : string.Empty), lastRecord.NewBestStreak));
                menu.Body.Add(($"RECORD VS {Name(level)} CPU", $"{records.Wins(level)} WON  {records.Losses(level)} LOST", false));
            }
            menu.Add("rematch", "REMATCH");
            menu.Add("menu", "MAIN MENU");
            hud.ShowMenu(menu);
        }

        string MenuHint(bool withChange = false)
        {
            if (input.UsingGamepad)
            {
                return withChange ? "D-PAD TO CHOOSE AND CHANGE  -  A TO SELECT  -  B TO GO BACK" : "D-PAD TO CHOOSE  -  A TO SELECT  -  B TO GO BACK";
            }
            return withChange
                ? "UP/DOWN TO CHOOSE  -  LEFT/RIGHT TO CHANGE  -  ENTER TO SELECT  -  ESC TO GO BACK"
                : "UP/DOWN TO CHOOSE  -  ENTER TO SELECT  -  ESC TO GO BACK  -  M TO MUTE";
        }

        void MenuInput()
        {
            MenuModel menu = hud.Menu;
            if (menu == null)
            {
                return;
            }
            int vertical = input.VerticalStep();
            if (vertical != 0 && menu.Items.Count > 0)
            {
                menu.Selected = (menu.Selected + vertical + menu.Items.Count) % menu.Items.Count;
                sound.PlayUi(art.Move, 0.6f);
                hud.RefreshMenu();
            }
            int horizontal = input.HorizontalStep();
            if (horizontal != 0 && menu.Items.Count > 0 && menu.Items[menu.Selected].Value != null)
            {
                Change(menu.Items[menu.Selected].Id, horizontal);
            }
            if (input.Confirm && menu.Items.Count > 0)
            {
                Activate(menu.Items[menu.Selected].Id);
            }
            else if (input.Back || (Screen == GameScreen.Paused && input.Pause))
            {
                GoBack();
            }
        }

        void OnItemClicked(int index)
        {
            MenuModel menu = hud.Menu;
            if (menu == null || index < 0 || index >= menu.Items.Count)
            {
                return;
            }
            menu.Selected = index;
            Activate(menu.Items[index].Id);
        }

        void GoBack()
        {
            switch (Screen)
            {
                case GameScreen.Settings:
                case GameScreen.Records:
                    Activate("back");
                    break;
                case GameScreen.Paused:
                    TogglePause();
                    break;
                case GameScreen.GameOver:
                    sound.PlayUi(art.Move, 0.6f);
                    ToTitle();
                    break;
            }
        }

        /// <summary>Runs a menu row by its id, as Enter or a click would.</summary>
        public void Activate(string id)
        {
            switch (id)
            {
                case "play1":
                    sound.PlayUi(art.Select);
                    StartMatch(Opponent.Cpu);
                    return;
                case "play2":
                    sound.PlayUi(art.Select);
                    StartMatch(Opponent.Human);
                    return;
                case "settings":
                    sound.PlayUi(art.Select);
                    ShowSettingsMenu(0);
                    return;
                case "records":
                    sound.PlayUi(art.Select);
                    ShowRecordsMenu();
                    return;
                case "quit":
                    Application.Quit();
                    return;
                case "back":
                    sound.PlayUi(art.Move, 0.6f);
                    bool fromRecords = Screen == GameScreen.Records;
                    Screen = GameScreen.Title;
                    ShowTitleMenu(fromRecords ? 3 : 2);
                    return;
                case "resume":
                    TogglePause();
                    return;
                case "restart":
                case "rematch":
                    sound.PlayUi(art.Select);
                    Restart();
                    return;
                case "menu":
                    sound.PlayUi(art.Select);
                    ToTitle();
                    return;
                default:
                    Change(id, 1);
                    return;
            }
        }

        /// <summary>Steps a setting row left or right and saves the result.</summary>
        public void Change(string id, int step)
        {
            switch (id)
            {
                case "cpu":
                    settings.CycleDifficulty(step);
                    break;
                case "points":
                    settings.CyclePoints(step);
                    break;
                case "mode":
                    settings.CycleMode(step);
                    break;
                case "volume":
                    volumeStep = Mathf.Clamp(volumeStep + step, 0, 10);
                    sound.SetVolume(volumeStep / 10f);
                    store.SetInt(OptionsPrefix + "volume", volumeStep);
                    break;
                case "shake":
                    shakeOn = !shakeOn;
                    cameraRig.ShakeScale = shakeOn ? 1f : 0f;
                    store.SetInt(OptionsPrefix + "shake", shakeOn ? 1 : 0);
                    if (shakeOn)
                    {
                        cameraRig.Shake(0.4f);
                    }
                    break;
                default:
                    return;
            }
            settings.Save(store);
            sound.PlayUi(art.Move, 0.6f);
            ShowSettingsMenu(hud.Menu != null ? hud.Menu.Selected : 0);
        }

        void UpdateSoundLabel() => hud.SetSound(sound.Muted ? "SOUND OFF  (M)" : "SOUND ON  (M)");

        static string Hits(int hits) => hits == 1 ? "1 HIT" : $"{hits} HITS";

        static string Name(Difficulty difficulty) => difficulty.ToString().ToUpperInvariant();

        static string Name(GameMode mode) => mode.ToString().ToUpperInvariant();
    }
}
