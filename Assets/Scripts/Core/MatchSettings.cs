using System;

namespace RetroRally.Core
{
    /// <summary>Who plays the right paddle.</summary>
    public enum Opponent
    {
        Cpu = 0,
        Human = 1,
    }

    /// <summary>Classic is plain rallies; Arcade adds power-ups to the court.</summary>
    public enum GameMode
    {
        Classic = 0,
        Arcade = 1,
    }

    /// <summary>The choices made in the menus before a match.</summary>
    public sealed class MatchSettings
    {
        /// <summary>The match lengths offered in the settings menu.</summary>
        public static readonly int[] PointChoices = { 3, 5, 7, 11 };

        const string Prefix = "retrorally.settings.";

        public Opponent Opponent = Opponent.Cpu;
        public Difficulty Difficulty = Difficulty.Normal;
        public GameMode Mode = GameMode.Classic;
        public int PointsToWin = 5;

        public MatchSettings Copy() => (MatchSettings)MemberwiseClone();

        /// <summary>Steps <see cref="PointsToWin"/> through <see cref="PointChoices"/>, wrapping around.</summary>
        public void CyclePoints(int step)
        {
            int index = Array.IndexOf(PointChoices, PointsToWin);
            if (index < 0)
            {
                index = 1;
            }
            int count = PointChoices.Length;
            PointsToWin = PointChoices[((index + step) % count + count) % count];
        }

        public void CycleDifficulty(int step) => Difficulty = (Difficulty)Wrap((int)Difficulty + step, 3);

        public void CycleMode(int step) => Mode = (GameMode)Wrap((int)Mode + step, 2);

        /// <summary>Remembers the menu choices (the opponent is picked on the title screen each time).</summary>
        public void Save(IRecordStore store)
        {
            store.SetInt(Prefix + "difficulty", (int)Difficulty);
            store.SetInt(Prefix + "mode", (int)Mode);
            store.SetInt(Prefix + "points", PointsToWin);
            store.Save();
        }

        /// <summary>Reads saved choices, ignoring anything out of range.</summary>
        public static MatchSettings Load(IRecordStore store)
        {
            var settings = new MatchSettings();
            int difficulty = store.GetInt(Prefix + "difficulty", (int)settings.Difficulty);
            int mode = store.GetInt(Prefix + "mode", (int)settings.Mode);
            int points = store.GetInt(Prefix + "points", settings.PointsToWin);
            if (difficulty >= 0 && difficulty <= 2)
            {
                settings.Difficulty = (Difficulty)difficulty;
            }
            if (mode >= 0 && mode <= 1)
            {
                settings.Mode = (GameMode)mode;
            }
            if (Array.IndexOf(PointChoices, points) >= 0)
            {
                settings.PointsToWin = points;
            }
            return settings;
        }

        static int Wrap(int value, int count) => (value % count + count) % count;
    }
}
